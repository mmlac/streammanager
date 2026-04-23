using Avalonia.Controls;
using StreamManager.App.ViewModels;

namespace StreamManager.App.Views;

public partial class SavePresetDialogWindow : Window
{
    public SavePresetDialogWindow()
    {
        InitializeComponent();
    }

    public SavePresetDialogWindow(SavePresetDialogViewModel vm) : this()
    {
        DataContext = vm;
        vm.Closed += (_, _) => Close();
    }
}
