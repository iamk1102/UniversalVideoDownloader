namespace UniversalVideoDownloader.Models;

public class AppSettings
{
    public string DefaultDownloadPath { get; set; } = "";
    public string DefaultQuality { get; set; } = "Best";
    public string DefaultBrowser { get; set; } = "None";
    public bool UseAria2 { get; set; } = true;
    public int Threads { get; set; } = 8;
    public int MaxConcurrentDownloads { get; set; } = 2;
    public bool EnableClipboardMonitor { get; set; } = false;
    public bool MinimizeToTray { get; set; } = false;
    public bool SchedulerEnabled { get; set; } = false;
    public string SchedulerTime { get; set; } = "02:00";
    public string SchedulerAction { get; set; } = "None"; // None, Shutdown, Sleep
    public string Theme { get; set; } = "Dark"; // Dark, Light
    public string Language { get; set; } = "en"; // en, vi
}
