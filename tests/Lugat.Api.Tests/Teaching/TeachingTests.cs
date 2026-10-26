using System.Net;
using System.Net.Http.Json;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Contracts;
using Lugat.Domain.Learning;
using Lugat.Domain.Premium;
using Lugat.Domain.Testing;
using Lugat.Domain.Users;
using Lugat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Teaching;

/// <summary>Stage 7 (spec §9, §13.8): groups, the teacher panel, the commission report.</summary>
public class TeachingTests
{
    private const long Teacher = 2001;
    private const long OtherTeacher = 2002;
    private const long Learner = 2003;
    private const long Stranger = 2004;
    private const long Admin = 2005;

    private static ApiFactory NewFactory() => new() { Premium = false, AdminTelegramIds = [Admin] };

    internal static async Task<Guid> UserIdAsync(ApiFactory factory, long telegramId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().UserLogins
            .Where(l => l.ProviderUserId == telegramId.ToString()).Select(l => l.UserId).SingleAsync();
    }

    /// <summary>Gives a role directly in the database, as the admin panel would.</summary>
    internal static async Task GrantAsync(ApiFactory factory, long telegramId, UserRoles role)
    {
        var id = await UserIdAsync(factory, telegramId);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == id);
        user.Roles |= role;
        await db.SaveChangesAsync();
    }

    private static async Task<T> OkAsync<T>(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json))!;
    }

    private static async Task<HttpClient> TeacherAsync(ApiFactory factory, long telegramId, string name)
    {
        var client = await factory.SignedInClientAsync(telegramId, name);
        await GrantAsync(factory, telegramId, UserRoles.Teacher);
        return client;
    }

    private static async Task<TeacherGroupDto> CreateGroupAsync(HttpClient teacher, string name = "Seul 1-guruh") =>
        await OkAsync<TeacherGroupDto>(await teacher.PostAsJsonAsync("/api/teacher/groups", new CreateGroupRequest(Guid.NewGuid(), name), ApiFactory.Json));

    [Fact]
    public async Task A_teacher_sees_only_learners_who_joined_their_group_and_not_after_they_leave()
    {
        await using var factory = NewFactory();
        var teacher = await TeacherAsync(factory, Teacher, "Ustoz Aziz");
        var other = await TeacherAsync(factory, OtherTeacher, "Boshqa ustoz");
        var learner = await factory.SignedInClientAsync(Learner, "Dilnoza");
        var stranger = await factory.SignedInClientAsync(Stranger, "Begona");
        var learnerId = await UserIdAsync(factory, Learner);
        var strangerId = await UserIdAsync(factory, Stranger);
        var group = await CreateGroupAsync(teacher);

        var invite = await OkAsync<GroupInviteDto>(await learner.GetAsync($"/api/groups/invite/{group.InviteCode.ToLowerInvariant()}"));
        Assert.Equal(("Seul 1-guruh", "Ustoz Aziz", false), (invite.GroupName, invite.TeacherName, invite.AlreadyMember));
        var before = await OkAsync<GroupDetailDto>(await teacher.GetAsync($"/api/teacher/groups/{group.Id}"));
        Assert.Empty(before.Students); // opening the link is not consent

        var joined = await OkAsync<MyGroupDto>(await learner.PostAsync($"/api/groups/join/{group.InviteCode}", null));
        await OkAsync<MyGroupDto>(await learner.PostAsync($"/api/groups/join/{group.InviteCode}", null)); // again: no change
        Assert.Equal(group.Id, joined.Id);

        var detail = await OkAsync<GroupDetailDto>(await teacher.GetAsync($"/api/teacher/groups/{group.Id}"));
        Assert.Equal([learnerId], detail.Students.Select(s => s.UserId));
        Assert.Equal(HttpStatusCode.OK, (await teacher.GetAsync($"/api/teacher/students/{learnerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync($"/api/teacher/students/{strangerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/teacher/groups/{group.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/teacher/students/{learnerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync("/api/teacher/groups")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync($"/api/teacher/students/{learnerId}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await learner.DeleteAsync($"/api/groups/{group.Id}/membership")).StatusCode);
        Assert.Empty(await learner.GetFromJsonAsync<List<MyGroupDto>>("/api/me/groups", ApiFactory.Json) ?? []);
        Assert.Empty((await OkAsync<GroupDetailDto>(await teacher.GetAsync($"/api/teacher/groups/{group.Id}"))).Students);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync($"/api/teacher/students/{learnerId}")).StatusCode);
        Assert.Equal(0, (await OkAsync<List<TeacherGroupDto>>(await teacher.GetAsync("/api/teacher/groups"))).Single().Members);
    }

    [Fact]
    public async Task A_whole_class_on_one_network_can_join_at_once()
    {
        await using var factory = NewFactory();
        var teacher = await TeacherAsync(factory, Teacher, "Ustoz");
        var group = await CreateGroupAsync(teacher);

        for (var i = 0; i < 15; i++)
        {
            var learner = await factory.SignedInClientAsync(5000 + i, $"O‘quvchi {i}");
            Assert.Equal(HttpStatusCode.OK, (await learner.GetAsync($"/api/groups/invite/{group.InviteCode}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await learner.PostAsync($"/api/groups/join/{group.InviteCode}", null)).StatusCode);
        }

        Assert.Equal(15, (await OkAsync<List<TeacherGroupDto>>(await teacher.GetAsync("/api/teacher/groups"))).Single().Members);
    }

    [Fact]
    public async Task A_new_invite_link_or_a_closed_group_stops_the_old_link()
    {
        await using var factory = NewFactory();
        var teacher = await TeacherAsync(factory, Teacher, "Ustoz");
        var learner = await factory.SignedInClientAsync(Learner, "Dilnoza");
        var group = await CreateGroupAsync(teacher);

        var renewed = await OkAsync<TeacherGroupDto>(await teacher.PostAsync($"/api/teacher/groups/{group.Id}/invite", null));
        Assert.NotEqual(group.InviteCode, renewed.InviteCode);
        Assert.Equal(HttpStatusCode.NotFound, (await learner.PostAsync($"/api/groups/join/{group.InviteCode}", null)).StatusCode);
        await OkAsync<MyGroupDto>(await learner.PostAsync($"/api/groups/join/{renewed.InviteCode}", null));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await teacher.PostAsync($"/api/groups/join/{renewed.InviteCode}", null)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await teacher.DeleteAsync($"/api/teacher/groups/{group.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await learner.GetAsync($"/api/groups/invite/{renewed.InviteCode}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync($"/api/teacher/students/{await UserIdAsync(factory, Learner)}")).StatusCode);
        Assert.Empty(await learner.GetFromJsonAsync<List<MyGroupDto>>("/api/me/groups", ApiFactory.Json) ?? []);
    }

    [Fact]
    public async Task The_group_table_shows_units_tests_activity_and_the_hardest_words()
    {
        await using var factory = NewFactory();
        var teacher = await TeacherAsync(factory, Teacher, "Ustoz");
        var learner = await factory.SignedInClientAsync(Learner, "Dilnoza");
        var group = await CreateGroupAsync(teacher);
        await OkAsync<MyGroupDto>(await learner.PostAsync($"/api/groups/join/{group.InviteCode}", null));
        await factory.AddWordsAsync(Learner, 1, ApiFactory.Words("가", 3));

        // Two days of study: today and tomorrow (Korea time), each with "Eslay olmadim" on the first card.
        for (var day = 0; day < 2; day++)
        {
            var session = (await OkAsync<StartSessionResponse>(await learner.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, true), ApiFactory.Json))).Session!;
            await OkAsync<AnswerResultDto>(await learner.PostAsJsonAsync($"/api/sessions/{session.Id}/answers",
                new AnswerRequest(Guid.NewGuid(), session.Position, ReviewAnswer.Forgot, false, 2000), ApiFactory.Json));
            factory.Time.Advance(TimeSpan.FromDays(1));
            learner = await factory.SignedInClientAsync(Learner, "Dilnoza");
        }

        teacher = await factory.SignedInClientAsync(Teacher, "Ustoz");
        var detail = await OkAsync<GroupDetailDto>(await teacher.GetAsync($"/api/teacher/groups/{group.Id}"));
        var row = Assert.Single(detail.Students);
        Assert.Equal(("Dilnoza", "A-1", 0, (int?)null), (row.Name, row.CurrentUnit, row.PassedUnits, row.LastTestPercent));
        Assert.Equal(3, row.ActiveDays7);
        var hardest = Assert.Single(detail.HardestWords);
        Assert.Equal(("가가", 2, 1), (hardest.Korean, hardest.Mistakes, hardest.Learners));

        var student = await OkAsync<StudentDetailDto>(await teacher.GetAsync($"/api/teacher/students/{row.UserId}"));
        Assert.Equal("A-1", Assert.Single(student.Units).Unit);
        Assert.Equal("가가", Assert.Single(student.TopMistakes).Korean);
        var json = await (await teacher.GetAsync($"/api/teacher/students/{row.UserId}")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("Telegram", json); // no logins
    }

    [Fact]
    public async Task The_group_table_costs_the_same_queries_for_two_learners_as_for_six()
    {
        // Audit 2026-10-26: the mistakes of each learner were read one by one, and every unit test of every learner
        // was loaded with its questions to find the latest score.
        await using var factory = NewFactory();
        var teacher = await TeacherAsync(factory, Teacher, "Ustoz");
        var group = await CreateGroupAsync(teacher);
        using var sql = new SqlCounter(factory.DatabaseName);

        async Task<int> JoinAndCountAsync(int from, int to)
        {
            for (var n = from; to > n; n++)
            {
                var telegramId = 6000 + n;
                var learner = await factory.SignedInClientAsync(telegramId, $"O‘quvchi {n}");
                await OkAsync<MyGroupDto>(await learner.PostAsync($"/api/groups/join/{group.InviteCode}", null));
                await factory.AddWordsAsync(telegramId, 1, ApiFactory.Words("가", 3));
                var session = (await OkAsync<StartSessionResponse>(await learner.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, true), ApiFactory.Json))).Session!;
                await OkAsync<AnswerResultDto>(await learner.PostAsJsonAsync($"/api/sessions/{session.Id}/answers",
                    new AnswerRequest(Guid.NewGuid(), session.Position, ReviewAnswer.Forgot, false, 2000), ApiFactory.Json));
                await AddUnitTestsAsync(factory, await UserIdAsync(factory, telegramId));
            }

            teacher = await factory.SignedInClientAsync(Teacher, "Ustoz");
            await teacher.GetAsync("/api/me"); // warm the sign-in, so only the table is counted
            sql.Reset();
            var detail = await OkAsync<GroupDetailDto>(await teacher.GetAsync($"/api/teacher/groups/{group.Id}"));
            var count = sql.Count;
            Assert.Equal(to, detail.Students.Count);
            Assert.All(detail.Students, s => Assert.Equal(80, s.LastTestPercent)); // the latest test, not the best or first
            var hardest = Assert.Single(detail.HardestWords);
            Assert.Equal((to, to), (hardest.Mistakes, hardest.Learners));
            return count;
        }

        var two = await JoinAndCountAsync(0, 2);
        var six = await JoinAndCountAsync(2, 6);

        Assert.Equal(two, six);
    }

    /// <summary>Two finished unit tests of 5 questions: 2 right, then (later) 4 right.</summary>
    private static async Task AddUnitTestsAsync(ApiFactory factory, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = factory.Time.GetUtcNow();
        foreach (var (right, finished) in new[] { (2, now.AddHours(-2)), (4, now.AddHours(-1)) })
        {
            db.TestRuns.Add(new TestRun
            {
                Id = Guid.NewGuid(),
                OwnerId = userId,
                Title = "A-1-bo‘lim · yakuniy test",
                Status = TestRunStatus.Finished,
                Purpose = TestPurpose.UnitFinal,
                Questions = [.. Enumerable.Range(0, 5).Select(i => new TestQuestion(i, TestQuestionKind.WordMeaning, "?", null, null, ["a", "b"], "a", null, null, null, false))],
                Answers = [.. Enumerable.Range(0, 5).Select(i => new TestAnswer(i, i < right ? "a" : "b", i < right, null, finished))],
                CreatedAt = finished.AddMinutes(-5),
                FinishedAt = finished,
            });
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task The_commission_report_counts_payments_with_the_teachers_code_without_names()
    {
        await using var factory = NewFactory();
        var teacher = await TeacherAsync(factory, Teacher, "Ustoz");
        var teacherId = await UserIdAsync(factory, Teacher);
        var admin = await factory.SignedInClientAsync(Admin, "Ega");
        var learner = await factory.SignedInClientAsync(Learner, "Dilnoza Karimova");
        var learnerId = await UserIdAsync(factory, Learner);
        await OkAsync<PromoCodeDto>(await admin.PostAsJsonAsync("/api/admin/promo-codes", new CreatePromoCodeRequest("aziz10", 10, 30, teacherId, null), ApiFactory.Json));

        // The invite link brings the teacher's code to the learner's Premium page.
        var group = await CreateGroupAsync(teacher);
        await OkAsync<MyGroupDto>(await learner.PostAsync($"/api/groups/join/{group.InviteCode}", null));
        Assert.Equal("AZIZ10", (await learner.GetFromJsonAsync<PremiumStatusDto>("/api/me/premium", ApiFactory.Json))!.PromoCode);

        foreach (var plan in new[] { SubscriptionPlan.Monthly, SubscriptionPlan.Quarterly })
        {
            await OkAsync<PaymentDto>(await admin.PostAsJsonAsync("/api/admin/payments",
                new RecordPaymentRequest(Guid.NewGuid(), learnerId, plan, plan == SubscriptionPlan.Monthly ? 4500 : 10800, "AZIZ10", null, null), ApiFactory.Json));
        }

        var refunded = await OkAsync<PaymentDto>(await admin.PostAsJsonAsync("/api/admin/payments",
            new RecordPaymentRequest(Guid.NewGuid(), learnerId, SubscriptionPlan.Monthly, 4500, "AZIZ10", null, null), ApiFactory.Json));
        await OkAsync<PaymentDto>(await admin.PostAsJsonAsync($"/api/admin/payments/{refunded.Id}/refund", new RefundPaymentRequest(null), ApiFactory.Json));

        var response = await teacher.GetAsync("/api/teacher/commission");
        var report = await OkAsync<CommissionReportDto>(response);
        Assert.Equal((2, 15300, 4590), (report.Payments, report.AmountKrw, report.CommissionKrw)); // 30% of 4,500 + 10,800
        var month = Assert.Single(report.Months);
        Assert.Equal((new DateOnly(2026, 9, 1), 2, false), (month.Month, month.Payments, month.Paid));
        Assert.Equal("AZIZ10", Assert.Single(report.Codes).Code);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Dilnoza", json);
        Assert.DoesNotContain(learnerId.ToString(), json);
    }

    [Fact]
    public async Task A_referral_code_is_kept_only_when_it_is_a_teachers_and_the_first_one()
    {
        await using var factory = NewFactory();
        await TeacherAsync(factory, Teacher, "Ustoz");
        await TeacherAsync(factory, OtherTeacher, "Boshqa");
        var admin = await factory.SignedInClientAsync(Admin, "Ega");
        var learner = await factory.SignedInClientAsync(Learner, "Dilnoza");
        await OkAsync<PromoCodeDto>(await admin.PostAsJsonAsync("/api/admin/promo-codes", new CreatePromoCodeRequest("CHEGIRMA", 10, 0, null, null), ApiFactory.Json));
        await OkAsync<PromoCodeDto>(await admin.PostAsJsonAsync("/api/admin/promo-codes", new CreatePromoCodeRequest("BIRINCHI", 10, 30, await UserIdAsync(factory, Teacher), null), ApiFactory.Json));
        await OkAsync<PromoCodeDto>(await admin.PostAsJsonAsync("/api/admin/promo-codes", new CreatePromoCodeRequest("IKKINCHI", 10, 30, await UserIdAsync(factory, OtherTeacher), null), ApiFactory.Json));

        async Task<string?> ReferAsync(string code) =>
            (await OkAsync<Dictionary<string, string?>>(await learner.PostAsJsonAsync("/api/me/referral", new ReferralRequest(code), ApiFactory.Json)))["promoCode"];

        Assert.Null(await ReferAsync("CHEGIRMA")); // not a teacher's
        Assert.Null(await ReferAsync("YOQ"));
        Assert.Equal("BIRINCHI", await ReferAsync("birinchi"));
        Assert.Equal("BIRINCHI", await ReferAsync("IKKINCHI"));
    }
}
