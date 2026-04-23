namespace StreamManager.App.Presets;

// UI-facing prompts the preset orchestration needs. Behind an interface so
// the VM layer stays testable without Avalonia — tests use a fake that
// records calls and replays scripted answers.
public interface IPresetDialogs
{
    // §6.3 step 2: confirm before a Load clobbers unsaved edits.
    Task<bool> ConfirmLoadOverDirtyFormAsync(string presetName);

    // Save-as collision (§5 test coverage "duplicate name produces
    // 'Replace existing?' confirm"). Surfaced after the name dialog
    // returns a name that matches an existing preset.
    Task<bool> ConfirmReplacePresetAsync(string presetName);

    // Delete confirm. Preset deletion is irreversible in v1 (no undo).
    Task<bool> ConfirmDeletePresetAsync(string presetName);

    // Name-entry dialog for Save-as. Returns the entered name or null if
    // the user cancelled. Duplicate / empty / too-long validation lives
    // inside the dialog VM; this method returns only a validated name.
    Task<string?> PromptSaveAsNameAsync(IReadOnlyList<string> existingNames);

    // Fatal `presets.json` read errors (malformed / newer-than-supported
    // schema). Shown once on startup; preset features are then disabled.
    Task ShowPresetLoadErrorAsync(string message);
}
