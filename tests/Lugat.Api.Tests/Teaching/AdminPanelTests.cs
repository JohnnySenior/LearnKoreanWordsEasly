using System.Net;
using System.Net.Http.Json;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Contracts;
using Lugat.Domain.Ai;
using Lugat.Domain.Premium;
using Lugat.Domain.Users;
using Lugat.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Teaching;

/// <summary>Stage 7 (spec §10, §13.8): the admin panel besides content and payments.</summary>
public class AdminPanelTests
{
    private const long Admin = 3001;
    private const long Editor = 3002;
    private const long Learner = 3003;
    private const long Teacher = 3004;

    private static ApiFactory NewFactory() => new() { Premium = false, AdminTelegramIds = [Admin] };

    private static async Task<T> OkAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json))!;
    }

    private static Task<Guid> IdAsync(ApiFactory factory, long telegramId) => TeachingTests.UserIdAsync(factory, telegramId);

    [Fact]
    public async Task An_editor_reaches_content_but_not_payments_users_or_figures()
    {
        await using var factory = NewFactory();
        var editor = await factory.SignedInClientAsync(Editor, "Muharrir");
        await TeachingTests.GrantAsync(factory, Editor, UserRoles.Editor);
        var learnerId = (await factory.SignedInClientAsync(Learner, "O‘quvchi")) is not null ? await IdAsync(factory, Learner) : default;

        Assert.Equal(HttpStatusCode.OK, (await editor.GetAsync("/api/admin/catalog/levels")).StatusCode);
        foreach (var url in new[]
        {
            "/api/admin/payments", "/api/admin/users?q=O‘q", $"/api/admin/users/{learnerId}", "/api/admin/promo-codes",
            "/api/admin/commissions", "/api/admin/usage", "/api/admin/metrics",
        })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await editor.GetAsync(url)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await editor.PostAsJsonAsync("/api/admin/payments",
            new RecordPaymentRequest(Guid.NewGuid(), learnerId, SubscriptionPlan.Monthly, 5000, null, null, null), ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.PutAsJsonAsync($"/api/admin/users/{learnerId}/roles", new SetRolesRequest(["Admin"]), ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.PostAsJsonAsync($"/api/admin/users/{learnerId}/premium", new AdjustPremiumRequest(30, "sovg‘a"), ApiFactory.Json)).StatusCode);
    }

    [Fact]
    public async Task Roles_are_given_and_taken_with_an_audit_entry_and_count_at_once()
    {
        await using var factory = NewFactory();
        var admin = await factory.SignedInClientAsync(Admin, "Ega");
        var learner = await factory.SignedInClientAsync(Learner, "Aziz");
        var learnerId = await IdAsync(factory, Learner);
        var adminId = await IdAsync(factory, Admin);
        Assert.Equal(HttpStatusCode.Forbidden, (await learner.GetAsync("/api/teacher/groups")).StatusCode);

        var detail = await OkAsync<AdminUserDetailDto>(await admin.PutAsJsonAsync($"/api/admin/users/{learnerId}/roles", new SetRolesRequest(["Teacher"]), ApiFactory.Json));
        Assert.Equal(["Learner", "Teacher"], detail.Roles);
        var entry = Assert.Single(detail.Audit);
        Assert.Equal(("roles", "Rollar: o‘quvchi → o‘quvchi, o‘qituvchi"), (entry.Action, entry.Detail));
        Assert.Equal(HttpStatusCode.OK, (await learner.GetAsync("/api/teacher/groups")).StatusCode);

        await OkAsync<AdminUserDetailDto>(await admin.PutAsJsonAsync($"/api/admin/users/{learnerId}/roles", new SetRolesRequest([]), ApiFactory.Json));
        Assert.Equal(HttpStatusCode.Forbidden, (await learner.GetAsync("/api/teacher/groups")).StatusCode);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PutAsJsonAsync($"/api/admin/users/{adminId}/roles", new SetRolesRequest(["Teacher"]), ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PutAsJsonAsync($"/api/admin/users/{learnerId}/roles", new SetRolesRequest(["Boss"]), ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/admin/users/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Premium_is_changed_by_hand_only_with_a_reason_and_days_come_off_the_end()
    {
        await using var factory = NewFactory();
        var admin = await factory.SignedInClientAsync(Admin, "Ega");
        var learner = await factory.SignedInClientAsync(Learner, "Aziz");
        var id = await IdAsync(factory, Learner);
        var now = factory.Time.GetUtcNow();
        Task<HttpResponseMessage> AdjustAsync(int days, string reason) =>
            admin.PostAsJsonAsync($"/api/admin/users/{id}/premium", new AdjustPremiumRequest(days, reason), ApiFactory.Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await AdjustAsync(30, " ")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await AdjustAsync(-5, "xato to‘lov")).StatusCode); // nothing to take
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await AdjustAsync(0, "nol kun")).StatusCode);

        await OkAsync<PaymentDto>(await admin.PostAsJsonAsync("/api/admin/payments", new RecordPaymentRequest(Guid.NewGuid(), id, SubscriptionPlan.Monthly, 5000, null, null, null), ApiFactory.Json));
        var gift = await OkAsync<AdminUserDetailDto>(await AdjustAsync(30, "o‘qituvchi uchun sinov"));
        Assert.Equal(now.AddMonths(1).AddDays(30), gift.PremiumEndsAt);
        Assert.Equal(SubscriptionSource.Admin, gift.Subscriptions[^1].Source);

        var cut = await OkAsync<AdminUserDetailDto>(await AdjustAsync(-40, "qisman qaytarildi"));
        Assert.Equal(now.AddMonths(1).AddDays(-10), cut.PremiumEndsAt); // the gift's 30 days, then 10 of the paid month

        var gone = await OkAsync<AdminUserDetailDto>(await AdjustAsync(-365, "hammasi qaytarildi"));
        Assert.False(gone.IsPremium);
        Assert.False((await learner.GetFromJsonAsync<PremiumStatusDto>("/api/me/premium", ApiFactory.Json))!.IsPremium);
        Assert.Equal(["-365 kun: hammasi qaytarildi", "-40 kun: qisman qaytarildi", "+30 kun: o‘qituvchi uchun sinov"], gone.Audit.Select(a => a.Detail));
        Assert.All(gone.Audit, a => Assert.Equal("Ega", a.Admin));
    }

    [Fact]
    public async Task A_teachers_month_is_marked_paid_once_and_a_promo_code_can_be_turned_off()
    {
        await using var factory = NewFactory();
        var admin = await factory.SignedInClientAsync(Admin, "Ega");
        var teacher = await factory.SignedInClientAsync(Teacher, "Ustoz");
        await TeachingTests.GrantAsync(factory, Teacher, UserRoles.Teacher);
        await factory.SignedInClientAsync(Learner, "Aziz");
        var teacherId = await IdAsync(factory, Teacher);
        var promo = await OkAsync<PromoCodeDto>(await admin.PostAsJsonAsync("/api/admin/promo-codes", new CreatePromoCodeRequest("USTOZ", 10, 30, teacherId, null), ApiFactory.Json));
        await OkAsync<PaymentDto>(await admin.PostAsJsonAsync("/api/admin/payments",
            new RecordPaymentRequest(Guid.NewGuid(), await IdAsync(factory, Learner), SubscriptionPlan.Monthly, 4500, "USTOZ", null, null), ApiFactory.Json));

        var due = Assert.Single(await OkAsync<List<TeacherCommissionDto>>(await admin.GetAsync("/api/admin/commissions")));
        Assert.Equal(("Ustoz", 1, 1350, false), (due.Teacher, due.Payments, due.Commission, due.Paid));
        var request = new MarkCommissionPaidRequest(teacherId, due.Month);
        var paid = await OkAsync<TeacherCommissionDto>(await admin.PostAsJsonAsync("/api/admin/commissions/paid", request, ApiFactory.Json));
        var again = await OkAsync<TeacherCommissionDto>(await admin.PostAsJsonAsync("/api/admin/commissions/paid", request, ApiFactory.Json));
        Assert.True(paid.Paid);
        Assert.Equal(paid.PaidAt, again.PaidAt);
        Assert.True(Assert.Single((await OkAsync<CommissionReportDto>(await teacher.GetAsync("/api/teacher/commission"))).Months).Paid);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/api/admin/commissions/paid", new MarkCommissionPaidRequest(teacherId, due.Month.AddMonths(-1)), ApiFactory.Json)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/admin/promo-codes/{promo.Id}/active", new SetPromoActiveRequest(false), ApiFactory.Json)).StatusCode);
        var check = await OkAsync<PromoCheckDto>(await teacher.PostAsJsonAsync("/api/premium/promo/check", new PromoCheckRequest("ustoz", SubscriptionPlan.Monthly), ApiFactory.Json));
        Assert.False(check.Valid);
    }

    [Fact]
    public async Task Won_and_som_commissions_of_one_month_are_counted_and_paid_apart()
    {
        // L-48: one learner paid in won, another in so‘m, both with the teacher's code, in the same month.
        await using var factory = NewFactory();
        var admin = await factory.SignedInClientAsync(Admin, "Ega");
        var teacher = await factory.SignedInClientAsync(Teacher, "Ustoz");
        await TeachingTests.GrantAsync(factory, Teacher, UserRoles.Teacher);
        await factory.SignedInClientAsync(Learner, "Aziz");
        var teacherId = await IdAsync(factory, Teacher);
        await OkAsync<PromoCodeDto>(await admin.PostAsJsonAsync("/api/admin/promo-codes", new CreatePromoCodeRequest("USTOZ", 10, 30, teacherId, null), ApiFactory.Json));
        var learner = await IdAsync(factory, Learner);
        await OkAsync<PaymentDto>(await admin.PostAsJsonAsync("/api/admin/payments",
            new RecordPaymentRequest(Guid.NewGuid(), learner, SubscriptionPlan.Monthly, 4500, "USTOZ", null, null), ApiFactory.Json));
        await OkAsync<PaymentDto>(await admin.PostAsJsonAsync("/api/admin/payments",
            new RecordPaymentRequest(Guid.NewGuid(), learner, SubscriptionPlan.Monthly, 26100, "USTOZ", null, null, Currency.Uzs), ApiFactory.Json));

        var rows = await OkAsync<List<TeacherCommissionDto>>(await admin.GetAsync("/api/admin/commissions"));
        var month = rows[0].Month;
        await OkAsync<TeacherCommissionDto>(await admin.PostAsJsonAsync("/api/admin/commissions/paid", new MarkCommissionPaidRequest(teacherId, month, Currency.Uzs), ApiFactory.Json));
        var after = await OkAsync<List<TeacherCommissionDto>>(await admin.GetAsync("/api/admin/commissions"));
        var report = await OkAsync<CommissionReportDto>(await teacher.GetAsync("/api/teacher/commission"));
        var revenue = (await OkAsync<MetricsDto>(await admin.GetAsync("/api/admin/metrics"))).Revenue;

        Assert.Equal([(Currency.Krw, 4500, 1350), (Currency.Uzs, 26100, 7830)], rows.Select(r => (r.Currency, r.Amount, r.Commission)));
        Assert.Equal([(Currency.Krw, false), (Currency.Uzs, true)], after.Select(r => (r.Currency, r.Paid)));
        Assert.Equal([(Currency.Krw, 4500, 1350), (Currency.Uzs, 26100, 7830)], report.Totals.Select(t => (t.Currency, t.Amount, t.Commission)));
        Assert.Equal([(Currency.Krw, false), (Currency.Uzs, true)], report.Months.Select(m => (m.Currency, m.Paid)));
        Assert.Equal([(Currency.Krw, 4500), (Currency.Uzs, 26100)], revenue.Select(r => (r.Currency, r.Amount)));
    }

    [Fact]
    public async Task The_figures_count_sign_ups_active_days_retention_premium_revenue_and_spending()
    {
        await using var factory = NewFactory();
        var admin = await factory.SignedInClientAsync(Admin, "Ega");
        await factory.SignedInClientAsync(Learner, "Aziz");
        await factory.SignedInClientAsync(Teacher, "Bekor");
        await OkAsync<PaymentDto>(await admin.PostAsJsonAsync("/api/admin/payments",
            new RecordPaymentRequest(Guid.NewGuid(), await IdAsync(factory, Learner), SubscriptionPlan.Quarterly, 12000, null, null, null), ApiFactory.Json));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AiUsages.AddRange(
                new AiUsage { Id = Guid.NewGuid(), OwnerId = await IdAsync(factory, Learner), Feature = "sentence-check", Model = "m", InputTokens = 100, OutputTokens = 50, EstimatedUsd = 0.01m, Succeeded = true, OccurredAt = factory.Time.GetUtcNow() },
                new AiUsage { Id = Guid.NewGuid(), OwnerId = await IdAsync(factory, Learner), Feature = "tts", Unit = "characters", Model = "v", InputTokens = 20, Succeeded = false, Error = "timeout", OccurredAt = factory.Time.GetUtcNow() });
            await db.SaveChangesAsync();
        }

        // Next day only the learner comes back (any signed-in request marks the day).
        factory.Time.Advance(TimeSpan.FromDays(1));
        admin = await factory.SignedInClientAsync(Admin, "Ega");
        await factory.SignedInClientAsync(Learner, "Aziz");

        var m = await OkAsync<MetricsDto>(await admin.GetAsync("/api/admin/metrics"));
        Assert.Equal((3, 3, 3), (m.Users, m.NewLast7Days, m.NewLast30Days));
        Assert.Equal([2, 3], m.DailyActive.Take(2).Select(d => d.Users)); // today, yesterday
        Assert.Equal((3, 2, (int?)67), (m.D1.Cohort, m.D1.Returned, m.D1.Percent));
        Assert.Equal((0, (int?)null), (m.D7.Cohort, m.D7.Percent));
        Assert.Equal(1, m.PremiumNow);
        Assert.Equal(12000, Assert.Single(m.Revenue).Amount);

        var usage = await OkAsync<UsageReportDto>(await admin.GetAsync("/api/admin/usage?days=7"));
        Assert.Equal((2, 1, 0.01m), (usage.ByDay.Sum(d => d.Calls), usage.ByDay.Sum(d => d.Failed), usage.ByDay.Sum(d => d.Usd)));
        Assert.Equal(["sentence-check", "tts"], usage.ByFeature.Select(f => f.Feature));
        Assert.Equal((150L, 1), (usage.ByFeature[0].Amount, usage.ByFeature[1].UnknownPrice));
    }

    [Fact]
    public async Task Deleted_accounts_leave_the_teachers_view_and_a_deleted_teachers_groups_close()
    {
        await using var factory = NewFactory();
        var teacher = await factory.SignedInClientAsync(Teacher, "Ustoz");
        await TeachingTests.GrantAsync(factory, Teacher, UserRoles.Teacher);
        var learner = await factory.SignedInClientAsync(Learner, "Aziz");
        var other = await factory.SignedInClientAsync(Editor, "Boshqa");
        var group = await OkAsync<TeacherGroupDto>(await teacher.PostAsJsonAsync("/api/teacher/groups", new CreateGroupRequest(Guid.NewGuid(), "Guruh"), ApiFactory.Json));
        await OkAsync<MyGroupDto>(await learner.PostAsync($"/api/groups/join/{group.InviteCode}", null));
        await OkAsync<MyGroupDto>(await other.PostAsync($"/api/groups/join/{group.InviteCode}", null));

        Assert.Equal(HttpStatusCode.NoContent, (await learner.DeleteAsync("/api/me")).StatusCode);
        var detail = await OkAsync<GroupDetailDto>(await teacher.GetAsync($"/api/teacher/groups/{group.Id}"));
        Assert.Equal(["Boshqa"], detail.Students.Select(s => s.Name));

        Assert.Equal(HttpStatusCode.NoContent, (await teacher.DeleteAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/groups/invite/{group.InviteCode}")).StatusCode);
        Assert.Empty(await other.GetFromJsonAsync<List<MyGroupDto>>("/api/me/groups", ApiFactory.Json) ?? []);
    }
}
