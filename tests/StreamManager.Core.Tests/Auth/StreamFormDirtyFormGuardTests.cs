using System.Threading;
using System.Threading.Tasks;
using StreamManager.App.Auth;
using StreamManager.App.ViewModels;
using Xunit;

namespace StreamManager.Core.Tests.Auth;

public class StreamFormDirtyFormGuardTests
{
    [Fact]
    public async Task CleanForm_ReturnsTrueWithoutPrompting()
    {
        var form = new StreamFormViewModel();
        var prompt = new StubPrompt(result: false);
        var guard = new StreamFormDirtyFormGuard(form, prompt);

        var ok = await guard.ConfirmOverwriteAsync(CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(0, prompt.Calls);
    }

    [Fact]
    public async Task DirtyForm_DelegatesToPrompt()
    {
        var form = new StreamFormViewModel();
        form.SetLiveBaseline(form.CaptureSnapshot());
        form.Title = "dirty";
        Assert.True(form.IsDirtyVsLive);

        var prompt = new StubPrompt(result: true);
        var guard = new StreamFormDirtyFormGuard(form, prompt);

        var ok = await guard.ConfirmOverwriteAsync(CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(1, prompt.Calls);
    }

    [Fact]
    public async Task DirtyForm_PromptDeclined_ReturnsFalse()
    {
        var form = new StreamFormViewModel();
        form.SetLiveBaseline(form.CaptureSnapshot());
        form.Title = "dirty";

        var prompt = new StubPrompt(result: false);
        var guard = new StreamFormDirtyFormGuard(form, prompt);

        var ok = await guard.ConfirmOverwriteAsync(CancellationToken.None);

        Assert.False(ok);
        Assert.Equal(1, prompt.Calls);
    }

    private sealed class StubPrompt(bool result) : IConfirmOverwritePrompt
    {
        public int Calls;
        public Task<bool> ShowAsync(CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }
}
