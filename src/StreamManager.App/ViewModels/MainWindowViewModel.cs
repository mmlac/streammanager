using CommunityToolkit.Mvvm.ComponentModel;

namespace StreamManager.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _placeholder = "StreamManager — slice 1 shell";
}
