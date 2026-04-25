using Microsoft.Extensions.Logging.Abstractions;
using StreamManager.Core.Auth;
using Xunit;

namespace StreamManager.Core.Tests.Auth;

public class ReauthOrchestratorTests
{
    [Fact]
    public async Task On401_Prompts_Reauths_AndRetries()
    {
        var auth = new FakeAuthenticator();
        var prompt = new FakePrompt(accepted: true);
        var guard = new FakeGuard(allow: true);
        var orch = NewOrchestrator(auth, prompt, guard);

        var attempts = 0;
        var result = await orch.ExecuteAsync(async ct =>
        {
            attempts++;
            if (attempts == 1) throw new UnauthorizedException("401");
            return "ok";
        }, ReauthOperationOptions.Default, CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.Equal(2, attempts);
        Assert.Equal(1, prompt.Calls);
        Assert.Equal(1, auth.ConnectCalls);
        Assert.Equal(0, guard.Calls); // not a re-fetch, no dirty-form check
    }

    [Fact]
    public async Task ReauthCancel_PropagatesCancellation()
    {
        var auth = new FakeAuthenticator();
        var prompt = new FakePrompt(accepted: false);
        var guard = new FakeGuard(allow: true);
        var orch = NewOrchestrator(auth, prompt, guard);

        await Assert.ThrowsAsync<OperationCanceledException>(() => orch.ExecuteAsync<string>(
            _ => throw new UnauthorizedException("401"),
            ReauthOperationOptions.Default,
            CancellationToken.None));

        Assert.Equal(0, auth.ConnectCalls);
    }

    [Fact]
    public async Task RefetchRetry_InvokesDirtyFormGuardBeforeOverwriting()
    {
        var auth = new FakeAuthenticator();
        var prompt = new FakePrompt(accepted: true);
        var guard = new FakeGuard(allow: true);
        var orch = NewOrchestrator(auth, prompt, guard);

        var operationCalls = 0;
        var result = await orch.ExecuteAsync(async ct =>
        {
            operationCalls++;
            if (operationCalls == 1) throw new UnauthorizedException("401");
            // On retry: ensure guard ran first (auth.ConnectCalls already 1).
            Assert.Equal(1, guard.Calls);
            return "fresh";
        }, new ReauthOperationOptions { DirtyFormGuardOnRetry = true }, CancellationToken.None);

        Assert.Equal("fresh", result);
        Assert.Equal(1, guard.Calls);
        Assert.Equal(1, auth.ConnectCalls);
    }

    [Fact]
    public async Task RefetchRetry_DirtyFormDeclined_AbortsRetry()
    {
        var auth = new FakeAuthenticator();
        var prompt = new FakePrompt(accepted: true);
        var guard = new FakeGuard(allow: false);
        var orch = NewOrchestrator(auth, prompt, guard);

        var operationCalls = 0;
        await Assert.ThrowsAsync<OperationCanceledException>(() => orch.ExecuteAsync(async ct =>
        {
            operationCalls++;
            if (operationCalls == 1) throw new UnauthorizedException("401");
            return "should-not-run";
        }, new ReauthOperationOptions { DirtyFormGuardOnRetry = true }, CancellationToken.None));

        Assert.Equal(1, operationCalls); // retry skipped
        Assert.Equal(1, auth.ConnectCalls); // reauth still happened
        Assert.Equal(1, guard.Calls);
    }

    [Fact]
    public async Task SuccessfulFirstCall_DoesNotPromptReauth()
    {
        var auth = new FakeAuthenticator();
        var prompt = new FakePrompt(accepted: true);
        var guard = new FakeGuard(allow: true);
        var orch = NewOrchestrator(auth, prompt, guard);

        var result = await orch.ExecuteAsync(_ => Task.FromResult(42),
            ReauthOperationOptions.Default, CancellationToken.None);

        Assert.Equal(42, result);
        Assert.Equal(0, prompt.Calls);
        Assert.Equal(0, auth.ConnectCalls);
        Assert.Equal(0, guard.Calls);
    }

    private static ReauthOrchestrator NewOrchestrator(
        FakeAuthenticator auth, FakePrompt prompt, FakeGuard guard)
        => new(auth, prompt, guard, NullLogger<ReauthOrchestrator>.Instance);

    private sealed class FakeAuthenticator : IYouTubeAuthenticator
    {
        public int ConnectCalls;
        public Task<AccountInfo> ConnectInteractiveAsync(CancellationToken ct, Action<string>? onAuthUrlReady = null)
        {
            ConnectCalls++;
            return Task.FromResult(new AccountInfo("test@example.com", null));
        }
        public Task<AccountInfo?> TrySilentReconnectAsync(CancellationToken ct)
            => Task.FromResult<AccountInfo?>(null);
        public Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakePrompt(bool accepted) : IReauthPrompt
    {
        public int Calls;
        public Task<bool> PromptAsync(CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(accepted);
        }
    }

    private sealed class FakeGuard(bool allow) : IDirtyFormGuard
    {
        public int Calls;
        public Task<bool> ConfirmOverwriteAsync(CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(allow);
        }
    }
}
