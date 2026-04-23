using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StreamManager.App.ViewModels;

public sealed partial class SavePresetDialogViewModel : ObservableValidator
{
    public const int NameMaxLength = 80;

    private readonly HashSet<string> _existingNamesCaseInsensitive;

    public SavePresetDialogViewModel()
        : this(Array.Empty<string>())
    {
    }

    public SavePresetDialogViewModel(IReadOnlyList<string> existingNames)
    {
        ArgumentNullException.ThrowIfNull(existingNames);
        _existingNamesCaseInsensitive = new HashSet<string>(
            existingNames,
            StringComparer.OrdinalIgnoreCase);
        ValidateAllProperties();
    }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(NameMaxLength, ErrorMessage = "Name must be ≤ 80 characters.")]
    private string _name = "";

    public string? Result { get; private set; }

    public bool WasCancelled { get; private set; }

    public event EventHandler? Closed;

    // A duplicate name is NOT a validation error — the orchestrator
    // surfaces the "Replace existing?" confirm (design §5 test coverage)
    // so the dialog just reports whether the entered name matches.
    public bool NameMatchesExisting =>
        !string.IsNullOrWhiteSpace(Name)
        && _existingNamesCaseInsensitive.Contains(Name.Trim());

    public bool CanConfirm => !HasErrors && !string.IsNullOrWhiteSpace(Name);

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        Result = Name.Trim();
        WasCancelled = false;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel()
    {
        Result = null;
        WasCancelled = true;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(CanConfirm));
        OnPropertyChanged(nameof(NameMatchesExisting));
        ConfirmCommand.NotifyCanExecuteChanged();
    }
}
