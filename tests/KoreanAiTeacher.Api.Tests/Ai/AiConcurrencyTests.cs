using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KoreanAiTeacher.Api.Tests.Infrastructure;
using KoreanAiTeacher.Application.Ai;
using KoreanAiTeacher.Contracts;
using KoreanAiTeacher.Domain.Ai;
using KoreanAiTeacher.Domain.Essays;
using KoreanAiTeacher.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace KoreanAiTeacher.Api.Tests.Ai;

public class AiConcurrencyTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private const string Prompt = "{\"instructionKo\":\"다음을 보고 글을 쓰십시오.\",\"text\":\"여가 시간이 증가하고 있다. 다음 자료를 보고 설명하십시오.\",\"modelAnswers\":[\"여가 시간이 크게 늘었다.\"]}";

    private sealed class BlockingProvider : IAiProvider
    {
        public AiProviderKind Kind => AiProviderKind.OpenAi;
        public bool IsConfigured => true;
        public string Model => "test-model";
        public int Calls;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<AiJsonResponse> CompleteJsonAsync(AiJsonRequest request, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            Entered.TrySetResult();
            await Release.Task.WaitAsync(ct);
            return new AiJsonResponse(Prompt, Model, 100, 50);
        }
    }

    [Fact]
    public async Task Parallel_creates_with_the_same_id_pay_once_and_return_the_same_question()
    {
        var ai = new BlockingProvider();
        await using var factory = new ApiFactory { AiProviders = [ai] };
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var request = new CreateEssayRequest(Guid.NewGuid(), EssayTask.Q53, EssaySource.Ai);
        var first = client.PostAsJsonAsync("/api/essays", request, Json);
        await ai.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second = client.PostAsJsonAsync("/api/essays", request, Json);
        ai.Release.TrySetResult();
        var results = await Task.WhenAll(first, second);
        Assert.All(results, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        Assert.Equal(1, ai.Calls);
        var a = (await results[0].Content.ReadFromJsonAsync<EssayDto>(Json))!;
        var b = (await results[1].Content.ReadFromJsonAsync<EssayDto>(Json))!;
        Assert.Equal((a.Id, a.Text), (b.Id, b.Text));
    }

    [Fact]
    public async Task In_flight_calls_reserve_the_last_quota_slot_before_a_second_call_can_start()
    {
        var ai = new BlockingProvider();
        await using var factory = new ApiFactory { AiProviders = [ai] };
        factory.Settings["Ai:MaxCallsPerUserPerDay"] = "1";
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var first = client.PostAsJsonAsync("/api/essays", new CreateEssayRequest(Guid.NewGuid(), EssayTask.Q53, EssaySource.Ai), Json);
        await ai.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            var second = await client.PostAsJsonAsync("/api/essays", new CreateEssayRequest(Guid.NewGuid(), EssayTask.Q53, EssaySource.Ai), Json);
            Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
            Assert.Equal(1, ai.Calls);
        }
        finally { ai.Release.TrySetResult(); }
        Assert.Equal(HttpStatusCode.Created, (await first).StatusCode);
    }

    [Fact]
    public async Task Ping_is_rate_limited_and_counts_towards_the_daily_quota()
    {
        var ai = new FakeAiProvider(AiProviderKind.OpenAi);
        await using var factory = new ApiFactory { AiProviders = [ai] };
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/ai/ping/OpenAi", null)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsync("/api/ai/ping/OpenAi", null)).StatusCode);
        Assert.Equal(1, (await client.GetFromJsonAsync<AiStatusDto>("/api/ai/status", Json))!.CallsToday);
        Assert.Single(ai.Requests);
    }

    private static void ConfigurePrice(ApiFactory factory)
    {
        factory.Settings["Ai:Budget:Prices:0:Provider"] = "OpenAi";
        factory.Settings["Ai:Budget:Prices:0:Model"] = "fake-gpt";
        factory.Settings["Ai:Budget:Prices:0:Unit"] = "tokens";
        factory.Settings["Ai:Budget:Prices:0:InputPerMillionUsd"] = "1";
        factory.Settings["Ai:Budget:Prices:0:OutputPerMillionUsd"] = "2";
        factory.Settings["Ai:Budget:Prices:0:AsOf"] = "2026-09-01";
    }

    [Fact]
    public async Task A_known_monthly_budget_stops_billable_calls_before_contacting_the_provider()
    {
        var ai = new FakeAiProvider(AiProviderKind.OpenAi);
        await using var factory = new ApiFactory { AiProviders = [ai] };
        ConfigurePrice(factory);
        factory.Settings["Ai:Budget:MonthlyLimitUsd"] = "1";
        factory.Settings["Ai:Budget:HostingMonthlyUsd"] = "1";
        var a = await factory.SignedInClientAsync(ApiFactory.UserA);
        var b = await factory.SignedInClientAsync(ApiFactory.UserB);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await a.PostAsync("/api/ai/ping/OpenAi", null)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await b.PostAsync("/api/ai/ping/OpenAi", null)).StatusCode);
        Assert.Empty(ai.Requests);
        Assert.Equal(1m, (await a.GetFromJsonAsync<AiBudgetDto>("/api/ai/budget", Json))!.EstimatedUsd);
    }

    [Fact]
    public async Task Observed_usage_replaces_the_reservation_and_missing_rates_are_visible()
    {
        var ai = new FakeAiProvider(AiProviderKind.OpenAi);
        await using var factory = new ApiFactory { AiProviders = [ai] };
        ConfigurePrice(factory);
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        (await client.PostAsync("/api/ai/ping/OpenAi", null)).EnsureSuccessStatusCode();
        var budget = (await client.GetFromJsonAsync<AiBudgetDto>("/api/ai/budget", Json))!;
        Assert.Equal(0.00028m, budget.EstimatedUsd); // fake provider: 120 input and 80 output tokens
        Assert.Equal(0, budget.UnknownPriceCalls);
        Assert.Equal(0, budget.PendingCalls);
    }

    [Fact]
    public async Task Canceling_a_caller_finishes_the_reservation_without_leaving_pending_usage()
    {
        var ai = new BlockingProvider();
        await using var factory = new ApiFactory { AiProviders = [ai] };
        await factory.SignedInClientAsync(ApiFactory.UserA);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gateway = scope.ServiceProvider.GetRequiredService<AiGateway>();
        using var canceled = new CancellationTokenSource();
        var call = gateway.CompleteJsonAsync((await db.Users.SingleAsync()).Id,
            new AiJsonRequest("test", "JSON only", "hello", "test", "{}", 50), canceled.Token);
        await ai.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        var usage = await db.AiUsages.SingleAsync();
        Assert.False(usage.Succeeded);
        Assert.NotEqual("pending", usage.Error);
    }
}
