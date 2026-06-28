// ============================================================================
// Universal Video Downloader
// Copyright (c) 2026 Kaka91. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// https://github.com/iamk1102/UniversalVideoDownloader
// ============================================================================

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
