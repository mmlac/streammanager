using Avalonia.Controls;
using StreamManager.App.ViewModels;

namespace StreamManager.App.Views;

public partial class ReauthModalWindow : Window
{
    public ReauthModalWindow()
    {
        InitializeComponent();
    }

    public ReauthModalWindow(ReauthModalViewModel vm) : this()
    {
        DataContext = vm;
        vm.Closed += (_, _) => Close();
    }
}
