using System;

namespace UniversalVideoDownloader.Models;

public class HistoryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string SavePath { get; set; } = "";
    public string Website { get; set; } = "";
    public DateTime DownloadDate { get; set; } = DateTime.Now;
    public string Status { get; set; } = "Success";
    public string FileSize { get; set; } = "Unknown";
}
