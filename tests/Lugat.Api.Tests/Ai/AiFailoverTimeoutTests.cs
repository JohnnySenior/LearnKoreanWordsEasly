using System.Diagnostics;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Application;
using Lugat.Application.Ai;
using Lugat.Domain.Ai;
using Lugat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Ai;

public class AiFailoverTimeoutTests
{
    private sealed class Provider(AiProviderKind kind, Func<CancellationToken, Task<AiJsonResponse>> call) : IAiProvider
    {
        public AiProviderKind Kind => kind;
        public bool IsConfigured => true;
        public string Model => "test-model";
        public int Calls;
        public Task<AiJsonResponse> CompleteJsonAsync(AiJsonRequest request, CancellationToken ct)
        {
            Calls++;
            return call(ct);
        }
    }
    private static AiJsonRequest Request => new("catalog-draft", "instructions", "words", "test_answer",
        """{"type":"object","properties":{"ok":{"type":"boolean"}}}""", 100, Timeout: TimeSpan.FromMilliseconds(3500));
    private static async Task<Guid> OwnerAsync(ApiFactory f)
    {
        await f.SignedInClientAsync(ApiFactory.UserA);
        await using var scope = f.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().UserLogins.Where(l => l.ProviderUserId == ApiFactory.UserA.ToString()).Select(l => l.UserId).SingleAsync();
    }

    [Fact]
    public async Task Slow_OpenAi_can_finish_after_Claude_rejects_immediately_and_both_calls_are_accounted()
    {
        var claude = new Provider(AiProviderKind.Anthropic, _ => throw new AiProviderException("Claude: hisobda API mablag‘i yetarli emas", false));
        var gpt = new Provider(AiProviderKind.OpenAi, async ct => { await Task.Delay(2300, ct); return new("{\"ok\":true}", "test-model", 10, 20); });
        await using var f = new ApiFactory { AiProviders = [claude, gpt] };
        f.Settings["Ai:Timeout"] = "00:00:02";
        f.Settings["Ai:TotalTimeout"] = "00:00:04";
        var owner = await OwnerAsync(f);
        await using var scope = f.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<AiGateway>().CompleteJsonAsync(owner, Request, CancellationToken.None);
        Assert.Equal(AiProviderKind.OpenAi, result.Provider);
        Assert.Equal((1, 1), (claude.Calls, gpt.Calls));
        var usages = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AiUsages.OrderBy(u => u.Provider).ToListAsync();
        Assert.Equal(new[] { (AiProviderKind.Anthropic, false), (AiProviderKind.OpenAi, true) }, usages.Select(u => (u.Provider, u.Succeeded)));
        Assert.Equal((10, 20), (usages[1].InputTokens, usages[1].OutputTokens));
    }

    [Fact]
    public async Task A_long_request_still_stops_at_the_overall_deadline()
    {
        var cancelled = false;
        var gpt = new Provider(AiProviderKind.OpenAi, async ct =>
        {
            try { await Task.Delay(TimeSpan.FromSeconds(30), ct); }
            catch (OperationCanceledException) { cancelled = true; throw; }
            return new("{}", "test-model", 1, 1);
        });
        await using var f = new ApiFactory { AiProviders = [gpt] };
        f.Settings["Ai:TotalTimeout"] = "00:00:00.700";
        var owner = await OwnerAsync(f);
        await using var scope = f.Services.CreateAsyncScope();
        var elapsed = Stopwatch.StartNew();
        await Assert.ThrowsAsync<ServiceUnavailableException>(() => scope.ServiceProvider.GetRequiredService<AiGateway>().CompleteJsonAsync(owner, Request, CancellationToken.None));
        Assert.True(cancelled);
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(10));
        Assert.False((await scope.ServiceProvider.GetRequiredService<AppDbContext>().AiUsages.SingleAsync()).Succeeded);
    }

    [Fact]
    public async Task A_caller_cancellation_does_not_start_a_fallback()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var claude = new Provider(AiProviderKind.Anthropic, async ct => { entered.TrySetResult(); await Task.Delay(TimeSpan.FromSeconds(30), ct); return new("{}", "test-model", 1, 1); });
        var gpt = new Provider(AiProviderKind.OpenAi, _ => Task.FromResult(new AiJsonResponse("{}", "test-model", 1, 1)));
        await using var f = new ApiFactory { AiProviders = [claude, gpt] };
        var owner = await OwnerAsync(f);
        await using var scope = f.Services.CreateAsyncScope();
        using var stop = new CancellationTokenSource();
        var call = scope.ServiceProvider.GetRequiredService<AiGateway>().CompleteJsonAsync(owner, Request, stop.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        Assert.Equal(0, gpt.Calls);
    }

    [Fact]
    public async Task A_credit_failure_is_not_hidden_by_a_later_fallback_timeout()
    {
        var claude = new Provider(AiProviderKind.Anthropic, _ => throw new AiProviderException("Claude: hisobda API mablag‘i yetarli emas", false));
        var gpt = new Provider(AiProviderKind.OpenAi, _ => throw new TaskCanceledException());
        await using var f = new ApiFactory { AiProviders = [claude, gpt] };
        var owner = await OwnerAsync(f);
        await using var scope = f.Services.CreateAsyncScope();
        var error = await Assert.ThrowsAsync<ServiceUnavailableException>(() => scope.ServiceProvider.GetRequiredService<AiGateway>().CompleteJsonAsync(owner, Request, CancellationToken.None));
        Assert.Contains("Claude: hisobda API mablag‘i", error.Message);
        Assert.Contains("ChatGPT", error.Message);
        Assert.Contains("javob kechikdi", error.Message);
    }
}
