using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Lugat.Api.Tests.Ai;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Contracts;
using Lugat.Domain.Ai;
using Lugat.Domain.Premium;
using Lugat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Premium;

/// <summary>Stage 5 (spec §7, §13.6): Premium, manual payments, promo codes.</summary>
public class PremiumTests
{
    private const long Admin = ApiFactory.UserB;

    private readonly FakeAiProvider _ai = new(AiProviderKind.Anthropic);

    private ApiFactory NewFactory() => new() { Premium = false, AdminTelegramIds = [Admin], AiProviders = [_ai] };

    private static async Task<Guid> UserIdAsync(ApiFactory factory, long telegramId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().UserLogins
            .Where(l => l.ProviderUserId == telegramId.ToString()).Select(l => l.UserId).SingleAsync();
    }

    private static async Task<PaymentDto> PayAsync(HttpClient admin, Guid userId, SubscriptionPlan plan, int amount, string? promo = null, Guid? id = null)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/payments", new RecordPaymentRequest(id ?? Guid.NewGuid(), userId, plan, amount, promo, null, "chek bor"), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PaymentDto>(ApiFactory.Json))!;
    }

    private static async Task<PremiumStatusDto> StatusAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<PremiumStatusDto>("/api/me/premium", ApiFactory.Json))!;

    /// <summary>The Premium endpoints of spec §13.6: pronunciation, mistakes notebook, sentence ladder (and the other AI features).</summary>
    private static async Task<List<HttpResponseMessage>> PremiumCallsAsync(HttpClient client)
    {
        var wav = new byte[4000];
        "RIFF"u8.CopyTo(wav);
        var audio = new ByteArrayContent(wav);
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        return
        [
            await client.PostAsync("/api/speech/assess", new MultipartFormDataContent { { audio, "audio", "a.wav" }, { new StringContent("학교"), "reference" } }),
            await client.GetAsync("/api/mistakes"),
            await client.GetAsync("/api/ladders"),
            await client.GetAsync("/api/weak-points"),
            await client.GetAsync("/api/traps"),
            await client.GetAsync("/api/practice/target"),
            await client.PostAsJsonAsync("/api/tests", new CreateTestRequest(Guid.NewGuid(), [ApiFactory.Unit(1)], 0, Oral: true), ApiFactory.Json),
        ];
    }

    [Fact]
    public async Task Without_Premium_the_Premium_features_answer_403_premium_required()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await factory.AddWordsAsync(ApiFactory.UserA, 1, ApiFactory.Words("가", 6));

        var responses = await PremiumCallsAsync(client);
        var free = new[]
        {
            await client.GetAsync("/api/today"),
            await client.GetAsync($"/api/units/{ApiFactory.Unit(1)}"),
            await client.GetAsync("/api/writing/queue"),
            await client.GetAsync("/api/vocabulary/usage"),
        };
        var prepare = await client.PostAsync($"/api/vocabulary/units/{ApiFactory.Unit(1)}/usage", null); // AI per learner

        Assert.All(responses.Append(prepare), r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
        foreach (var r in responses.Append(prepare))
        {
            using var problem = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            Assert.Equal("premium-required", problem.RootElement.GetProperty("type").GetString());
            Assert.Equal("Bu funksiya Premium’da.", problem.RootElement.GetProperty("detail").GetString());
        }

        Assert.All(free, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode)); // spec §7.1: learning is free
        Assert.Empty(_ai.Requests); // no AI cost for a learner without Premium
    }

    [Fact]
    public async Task A_recorded_payment_opens_Premium_at_once_and_it_closes_when_the_time_is_up()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var admin = await factory.SignedInClientAsync(Admin);
        var userId = await UserIdAsync(factory, ApiFactory.UserA);
        var before = await StatusAsync(client);

        var payment = await PayAsync(admin, userId, SubscriptionPlan.Monthly, 5000);
        var after = await StatusAsync(client);
        var mistakes = await client.GetAsync("/api/mistakes");

        factory.Time.Advance(TimeSpan.FromDays(31));
        client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var expired = await StatusAsync(client);
        var closed = await client.GetAsync("/api/mistakes");

        Assert.False(before.IsPremium);
        Assert.Matches("^L-[2-9A-HJKMNP-Z]{4}$", before.PaymentCode);
        Assert.Equal(before.PaymentCode, after.PaymentCode); // made once
        Assert.True(after.IsPremium);
        Assert.Equal(ApiFactory.Start.AddMonths(1), after.EndsAt);
        Assert.Equal((5000, 5000, PaymentStatus.Confirmed), (payment.AmountKrw, payment.ExpectedKrw, payment.Status));
        Assert.Equal(HttpStatusCode.OK, mistakes.StatusCode);
        Assert.False(expired.IsPremium);
        Assert.Equal(HttpStatusCode.Forbidden, closed.StatusCode);
    }

    [Fact]
    public async Task Two_payments_add_up_and_recording_one_twice_counts_once()
    {
        await using var factory = NewFactory();
        await factory.SignedInClientAsync(ApiFactory.UserA);
        var admin = await factory.SignedInClientAsync(Admin);
        var userId = await UserIdAsync(factory, ApiFactory.UserA);
        var id = Guid.NewGuid();

        var first = await PayAsync(admin, userId, SubscriptionPlan.Monthly, 5000, id: id);
        var again = await PayAsync(admin, userId, SubscriptionPlan.Monthly, 5000, id: id);
        factory.Time.Advance(TimeSpan.FromDays(10));
        admin = await factory.SignedInClientAsync(Admin);
        var second = await PayAsync(admin, userId, SubscriptionPlan.Quarterly, 12000);
        var list = (await admin.GetFromJsonAsync<List<PaymentDto>>($"/api/admin/payments?userId={userId}", ApiFactory.Json))!;

        Assert.Equal(first.Id, again.Id);
        Assert.Equal(2, list.Count);
        Assert.Equal(ApiFactory.Start.AddMonths(1), second.StartsAt); // the quarter starts when the month ends
        Assert.Equal(ApiFactory.Start.AddMonths(4), second.EndsAt);
        Assert.Equal("Test", list[0].RecordedBy);
    }

    [Fact]
    public async Task The_payments_list_costs_the_same_queries_for_two_payments_as_for_six()
    {
        // Audit 2026-10-26: every row read its subscription, names and promo code with three queries of its own.
        await using var factory = NewFactory();
        var admin = await factory.SignedInClientAsync(Admin);
        var teacher = await UserIdAsync(factory, Admin);
        await admin.PostAsJsonAsync("/api/admin/promo-codes", new CreatePromoCodeRequest("ustoz10", 10, null, teacher, null), ApiFactory.Json);
        using var sql = new SqlCounter(factory.DatabaseName);

        async Task<(int Queries, List<PaymentDto> List)> PayAndListAsync(int from, int to)
        {
            for (var n = from; to > n; n++)
            {
                await factory.SignedInClientAsync(7000 + n, $"To‘lovchi {n}");
                var userId = await UserIdAsync(factory, 7000 + n);
                await PayAsync(admin, userId, SubscriptionPlan.Quarterly, n % 2 == 0 ? 10800 : 12000, n % 2 == 0 ? "ustoz10" : null);
            }

            sql.Reset();
            var list = (await admin.GetFromJsonAsync<List<PaymentDto>>("/api/admin/payments", ApiFactory.Json))!;
            return (sql.Count, list);
        }

        var two = await PayAndListAsync(0, 2);
        var six = await PayAndListAsync(2, 6);

        Assert.Equal(two.Queries, six.Queries);
        Assert.Equal(6, six.List.Count);
        Assert.All(six.List, p => Assert.Equal("Test", p.RecordedBy));
        Assert.All(six.List, p => Assert.StartsWith("To‘lovchi ", p.UserName));
        Assert.Equal(3, six.List.Count(p => p.PromoCode == "USTOZ10"));
        Assert.All(six.List, p => Assert.Equal(p.PaidAt.AddMonths(3), p.EndsAt));
    }

    [Fact]
    public async Task A_promo_code_gives_its_discount_and_the_teacher_their_commission()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var admin = await factory.SignedInClientAsync(Admin);
        var teacher = await UserIdAsync(factory, Admin);
        var userId = await UserIdAsync(factory, ApiFactory.UserA);

        var created = await admin.PostAsJsonAsync("/api/admin/promo-codes", new CreatePromoCodeRequest("ustoz10", 10, null, teacher, null), ApiFactory.Json);
        var duplicate = await admin.PostAsJsonAsync("/api/admin/promo-codes", new CreatePromoCodeRequest("USTOZ10", 5, null, null, null), ApiFactory.Json);
        var byLearner = await client.PostAsJsonAsync("/api/admin/promo-codes", new CreatePromoCodeRequest("MENIKI", 90, null, null, null), ApiFactory.Json);
        var check = (await (await client.PostAsJsonAsync("/api/premium/promo/check", new PromoCheckRequest("Ustoz10", SubscriptionPlan.Quarterly), ApiFactory.Json))
            .Content.ReadFromJsonAsync<PromoCheckDto>(ApiFactory.Json))!;
        var unknown = (await (await client.PostAsJsonAsync("/api/premium/promo/check", new PromoCheckRequest("YOQ", SubscriptionPlan.Monthly), ApiFactory.Json))
            .Content.ReadFromJsonAsync<PromoCheckDto>(ApiFactory.Json))!;
        var payment = await PayAsync(admin, userId, SubscriptionPlan.Quarterly, 10800, "ustoz10");
        var promos = (await admin.GetFromJsonAsync<List<PromoCodeDto>>("/api/admin/promo-codes", ApiFactory.Json))!;

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode); // codes are unique regardless of case
        Assert.Equal(HttpStatusCode.Forbidden, byLearner.StatusCode);
        Assert.Equal((true, "USTOZ10", 10, 12000, 10800), (check.Valid, check.Code, check.DiscountPercent, check.PriceKrw, check.FinalPriceKrw));
        Assert.False(unknown.Valid);
        Assert.NotNull(unknown.Message);
        Assert.Equal((10800, 10, 3240, "USTOZ10"), (payment.ExpectedKrw, payment.DiscountPercent, payment.CommissionKrw, payment.PromoCode)); // 30% of 10,800
        Assert.Equal(1, promos.Single().Payments);
    }

    [Fact]
    public async Task A_refund_ends_that_period_today_and_moves_the_next_one_up()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var admin = await factory.SignedInClientAsync(Admin);
        var userId = await UserIdAsync(factory, ApiFactory.UserA);
        var month = await PayAsync(admin, userId, SubscriptionPlan.Monthly, 5000);
        var quarter = await PayAsync(admin, userId, SubscriptionPlan.Quarterly, 12000);
        factory.Time.Advance(TimeSpan.FromDays(5));
        admin = await factory.SignedInClientAsync(Admin);
        client = await factory.SignedInClientAsync(ApiFactory.UserA);

        var refunded = await (await admin.PostAsJsonAsync($"/api/admin/payments/{month.Id}/refund", new RefundPaymentRequest("bekor qildi"), ApiFactory.Json))
            .Content.ReadFromJsonAsync<PaymentDto>(ApiFactory.Json);
        var twice = await admin.PostAsJsonAsync($"/api/admin/payments/{month.Id}/refund", new RefundPaymentRequest(null), ApiFactory.Json);
        var status = await StatusAsync(client);
        var list = (await admin.GetFromJsonAsync<List<PaymentDto>>($"/api/admin/payments?userId={userId}", ApiFactory.Json))!;

        var now = ApiFactory.Start.AddDays(5);
        Assert.Equal((PaymentStatus.Refunded, now), (refunded!.Status, refunded.EndsAt));
        Assert.Equal(HttpStatusCode.OK, twice.StatusCode);
        var moved = list.Single(p => p.Id == quarter.Id);
        var removed = month.EndsAt - now; // the unused rest of the month
        Assert.Equal((now, quarter.EndsAt - removed), (moved.StartsAt, moved.EndsAt)); // the whole quarter, starting today
        Assert.Equal(quarter.EndsAt - quarter.StartsAt, moved.EndsAt - moved.StartsAt);
        Assert.True(status.IsPremium);
        Assert.Equal(moved.EndsAt, status.EndsAt);
    }

    [Fact]
    public async Task The_admin_finds_the_payer_by_code_and_the_plans_say_when_bank_details_are_missing()
    {
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserA, "Aziz");
        var admin = await factory.SignedInClientAsync(Admin);
        var code = (await StatusAsync(client)).PaymentCode;

        var byCode = (await admin.GetFromJsonAsync<List<AdminUserDto>>($"/api/admin/users?q={code.Replace("-", "").ToLowerInvariant()}", ApiFactory.Json))!;
        var byName = (await admin.GetFromJsonAsync<List<AdminUserDto>>("/api/admin/users?q=azi", ApiFactory.Json))!;
        var byLearner = await client.GetAsync("/api/admin/users?q=azi");
        var plans = (await client.GetFromJsonAsync<PremiumPlansDto>("/api/premium/plans", ApiFactory.Json))!;

        Assert.Equal("Aziz", Assert.Single(byCode).DisplayName);
        Assert.Equal(code, Assert.Single(byName).PaymentCode);
        Assert.Equal(HttpStatusCode.Forbidden, byLearner.StatusCode);
        Assert.Equal([(SubscriptionPlan.Monthly, 5000, 1), (SubscriptionPlan.Quarterly, 12000, 3)], plans.Plans.Select(p => (p.Plan, p.PriceKrw, p.Months)));
        Assert.Null(plans.Bank); // spec §15 Q4: the page says "Tez orada"
    }

    [Fact]
    public async Task A_new_learner_gets_the_trial_when_it_is_on()
    {
        await using var factory = NewFactory();
        factory.Settings["Premium:TrialDays"] = "7";
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);

        var status = await StatusAsync(client);

        Assert.Equal((true, SubscriptionPlan.Trial, ApiFactory.Start.AddDays(7)), (status.IsPremium, status.Plan, status.EndsAt));
    }
}
