using StreamManager.App.Presets;

namespace StreamManager.Core.Tests.Presets;

// Scriptable stand-in for Avalonia-backed IPresetDialogs. Tests push
// scripted answers onto `Answers` and `SaveAsNames`; every invocation
// is recorded in `Calls` so tests can assert the orchestrator prompted
// the user at the right moment.
public sealed class FakePresetDialogs : IPresetDialogs
{
    public Queue<bool> ConfirmLoadAnswers { get; } = new();
    public Queue<bool> ConfirmReplaceAnswers { get; } = new();
    public Queue<bool> ConfirmDeleteAnswers { get; } = new();
    public Queue<string?> SaveAsNames { get; } = new();

    public List<string> Calls { get; } = new();
    public List<string> LoadErrorsShown { get; } = new();

    public Task<bool> ConfirmLoadOverDirtyFormAsync(string presetName)
    {
        Calls.Add($"ConfirmLoadOverDirtyForm:{presetName}");
        return Task.FromResult(ConfirmLoadAnswers.Count > 0 ? ConfirmLoadAnswers.Dequeue() : false);
    }

    public Task<bool> ConfirmReplacePresetAsync(string presetName)
    {
        Calls.Add($"ConfirmReplace:{presetName}");
        return Task.FromResult(ConfirmReplaceAnswers.Count > 0 ? ConfirmReplaceAnswers.Dequeue() : false);
    }

    public Task<bool> ConfirmDeletePresetAsync(string presetName)
    {
        Calls.Add($"ConfirmDelete:{presetName}");
        return Task.FromResult(ConfirmDeleteAnswers.Count > 0 ? ConfirmDeleteAnswers.Dequeue() : false);
    }

    public Task<string?> PromptSaveAsNameAsync(IReadOnlyList<string> existingNames)
    {
        Calls.Add("PromptSaveAsName");
        return Task.FromResult(SaveAsNames.Count > 0 ? SaveAsNames.Dequeue() : null);
    }

    public Task ShowPresetLoadErrorAsync(string message)
    {
        LoadErrorsShown.Add(message);
        Calls.Add($"ShowPresetLoadError:{message}");
        return Task.CompletedTask;
    }
}
