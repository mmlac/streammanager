using StreamManager.App.ViewModels;
using StreamManager.Core.Auth;

namespace StreamManager.App.Auth;

// Real IDirtyFormGuard: inspects StreamFormViewModel.IsDirtyVsLive and
// invokes the confirm prompt only when an overwrite would actually clobber
// edits (design.md §6.2). Used both by IStreamFetchCoordinator (pre-fetch)
// and by IReauthOrchestrator (post-reauth retry, §6.7 step 3).
public sealed class StreamFormDirtyFormGuard : IDirtyFormGuard
{
    private readonly StreamFormViewModel _form;
    private readonly IConfirmOverwritePrompt _prompt;

    public StreamFormDirtyFormGuard(StreamFormViewModel form, IConfirmOverwritePrompt prompt)
    {
        _form = form;
        _prompt = prompt;
    }

    public Task<bool> ConfirmOverwriteAsync(CancellationToken ct)
    {
        if (!_form.IsDirtyVsLive)
        {
            return Task.FromResult(true);
        }
        return _prompt.ShowAsync(ct);
    }
}
