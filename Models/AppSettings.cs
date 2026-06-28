namespace UniversalVideoDownloader.Models;

public class AppSettings
{
    public string DefaultDownloadPath { get; set; } = "";
    public string DefaultQuality { get; set; } = "Best";
    public string DefaultBrowser { get; set; } = "None";
    public bool UseAria2 { get; set; } = true;
    public int Threads { get; set; } = 8;
}
