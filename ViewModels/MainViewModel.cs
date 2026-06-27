using CommunityToolkit.Mvvm.ComponentModel;

namespace UniversalVideoDownloader.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string videoUrl = "";

    [ObservableProperty]
    private string title = "-";

    [ObservableProperty]
    private string website = "-";

    [ObservableProperty]
    private string duration = "-";

    [ObservableProperty]
    private string resolution = "-";
}