// ============================================================================
// Universal Video Downloader
// Copyright (c) 2026 Kaka91. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// https://github.com/iamk1102/UniversalVideoDownloader
// ============================================================================

using System;
using System.Text;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UniversalVideoDownloader.Models;

public class QueueItem : ObservableObject
{
    private Guid _id;
    private string _title = "";
    private string _url = "";
    private string _status = "Pending"; // Pending, Downloading, Paused, Completed, Failed
    private double _progress;
    private string _speed = "";
    private string _eta = "";
    private string _savePath = "";
    private string _quality = "Best";
    private bool _isAudioOnly;
    private bool _writeSubtitles;
    private bool _useAria2;
    private string _browser = "None";
    private string _errorMessage = "";

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Url
    {
        get => _url;
        set => SetProperty(ref _url, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    public string Speed
    {
        get => _speed;
        set => SetProperty(ref _speed, value);
    }

    public string ETA
    {
        get => _eta;
        set => SetProperty(ref _eta, value);
    }

    public string SavePath
    {
        get => _savePath;
        set => SetProperty(ref _savePath, value);
    }

    public string Quality
    {
        get => _quality;
        set => SetProperty(ref _quality, value);
    }

    public bool IsAudioOnly
    {
        get => _isAudioOnly;
        set => SetProperty(ref _isAudioOnly, value);
    }

    public bool WriteSubtitles
    {
        get => _writeSubtitles;
        set => SetProperty(ref _writeSubtitles, value);
    }

    public bool UseAria2
    {
        get => _useAria2;
        set => SetProperty(ref _useAria2, value);
    }

    public string Browser
    {
        get => _browser;
        set => SetProperty(ref _browser, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    // Accumulates standard output logs for this specific download process
    public StringBuilder LogContent { get; } = new StringBuilder();

    public string LogContentText => LogContent.ToString();

    public void AppendLogLine(string line)
    {
        lock (LogContent)
        {
            LogContent.AppendLine(line);
        }
        OnPropertyChanged(nameof(LogContentText));
    }

    // Cancellation token source for cancelling this individual item
    public CancellationTokenSource? Cts { get; set; }
}
