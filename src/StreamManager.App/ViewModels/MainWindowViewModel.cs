using CommunityToolkit.Mvvm.ComponentModel;

namespace StreamManager.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel()
        : this(new StreamFormViewModel())
    {
    }

    public MainWindowViewModel(StreamFormViewModel streamForm)
    {
        StreamForm = streamForm;
    }

    public StreamFormViewModel StreamForm { get; }
}
