using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniversalVideoDownloader.Services;
using UniversalVideoDownloader.Models;

namespace UniversalVideoDownloader.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly YtDlpService _ytDlpService;
    private readonly HistoryService _historyService;
    private CancellationTokenSource? _cts;
    private AppSettings _settings = new();

    public MainViewModel()
    {
        _ytDlpService = new YtDlpService();
        _historyService = new HistoryService();
        
        HistoryItems = new ObservableCollection<HistoryItem>();
        
        // Load settings and history on startup asynchronously
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        _settings = await _historyService.LoadSettingsAsync();
        
        // Populate viewmodel properties from loaded settings
        DownloadPath = _settings.DefaultDownloadPath;
        SelectedQuality = _settings.DefaultQuality;
        SelectedBrowser = _settings.DefaultBrowser;
        UseAria2 = _settings.UseAria2;
        Threads = _settings.Threads;

        // Load History
        var history = await _historyService.LoadHistoryAsync();
        foreach (var item in history.OrderByDescending(h => h.DownloadDate))
        {
            HistoryItems.Add(item);
        }

        UpdateDependencyStatus();
    }

    [ObservableProperty]
    private int selectedTabIndex = 0;

    // --- Download Tab Properties ---
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoThumbnail))]
    private string thumbnailUrl = "";

    public bool HasNoThumbnail => string.IsNullOrEmpty(ThumbnailUrl);

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private double progressPercentage = 0;

    [ObservableProperty]
    private string downloadSpeed = "";

    [ObservableProperty]
    private string eta = "";

    [ObservableProperty]
    private bool isAudioOnly = false;

    [ObservableProperty]
    private bool writeSubtitles = false;

    // --- Settings Properties ---
    [ObservableProperty]
    private string downloadPath = "";

    [ObservableProperty]
    private string selectedQuality = "Best";

    [ObservableProperty]
    private string selectedBrowser = "None";

    [ObservableProperty]
    private bool useAria2 = true;

    [ObservableProperty]
    private int threads = 8;

    // --- Dependency Status ---
    [ObservableProperty]
    private string ytDlpStatus = "Checking...";

    [ObservableProperty]
    private string ffmpegStatus = "Checking...";

    [ObservableProperty]
    private string aria2Status = "Checking...";

    [ObservableProperty]
    private bool isYtDlpInstalled;

    [ObservableProperty]
    private bool isFFmpegInstalled;

    [ObservableProperty]
    private bool isAria2Installed;

    // --- Navigation & States ---
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    private bool isBusy;

    [ObservableProperty]
    private bool isDownloading;

    private bool CanExecuteAction => !IsBusy;

    public ObservableCollection<HistoryItem> HistoryItems { get; }

    private void UpdateDependencyStatus()
    {
        IsYtDlpInstalled = _ytDlpService.IsYtDlpInstalled;
        IsFFmpegInstalled = _ytDlpService.IsFFmpegInstalled;
        IsAria2Installed = _ytDlpService.IsAria2Installed;

        YtDlpStatus = IsYtDlpInstalled ? "Installed" : "Not Installed";
        FfmpegStatus = IsFFmpegInstalled ? "Installed" : "Not Installed";
        Aria2Status = IsAria2Installed ? "Installed" : "Not Installed";
    }

    [RelayCommand]
    private void ChangeTab(string tabIndexStr)
    {
        if (int.TryParse(tabIndexStr, out int idx))
        {
            SelectedTabIndex = idx;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteAction))]
    private async Task AnalyzeAsync()
    {
        if (string.IsNullOrWhiteSpace(VideoUrl))
        {
            StatusText = "Please enter a valid video URL.";
            return;
        }

        IsBusy = true;
        StatusText = "Initializing analysis...";
        ProgressPercentage = 0;
        DownloadSpeed = "";
        Eta = "";

        _cts = new CancellationTokenSource();

        try
        {
            await _ytDlpService.EnsureYtDlpInstalledAsync(progressMsg =>
            {
                StatusText = progressMsg;
            });
            UpdateDependencyStatus();

            StatusText = "Analyzing URL metadata...";
            var info = await _ytDlpService.GetVideoInfoAsync(VideoUrl, SelectedBrowser, _cts.Token);
            if (info != null)
            {
                Title = info.Title;
                Website = info.Website;
                Duration = info.Duration;
                Resolution = info.Resolution;
                ThumbnailUrl = info.Thumbnail;
                StatusText = "Analysis complete.";
            }
            else
            {
                StatusText = "Could not parse video info.";
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Analysis canceled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _cts = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteAction))]
    private async Task DownloadAsync()
    {
        if (string.IsNullOrWhiteSpace(VideoUrl))
        {
            StatusText = "Please enter a valid video URL.";
            return;
        }

        IsBusy = true;
        IsDownloading = true;
        StatusText = "Preparing download parameters...";
        ProgressPercentage = 0;
        DownloadSpeed = "0 KB/s";
        Eta = "--:--";

        _cts = new CancellationTokenSource();

        // Save active video title for history item log
        string activeTitle = Title != "-" ? Title : "Video Download";

        try
        {
            await _ytDlpService.EnsureYtDlpInstalledAsync(progressMsg => StatusText = progressMsg);
            if (UseAria2)
            {
                await _ytDlpService.EnsureAria2InstalledAsync(progressMsg => StatusText = progressMsg);
            }
            if (!IsAudioOnly)
            {
                await _ytDlpService.EnsureFFmpegInstalledAsync(progressMsg => StatusText = progressMsg);
            }
            UpdateDependencyStatus();

            StatusText = "Downloading video...";
            await _ytDlpService.DownloadVideoAsync(
                VideoUrl,
                DownloadPath,
                SelectedQuality,
                IsAudioOnly,
                SelectedBrowser,
                WriteSubtitles,
                UseAria2,
                Threads,
                (percent, speed, etaValue) =>
                {
                    ProgressPercentage = percent;
                    DownloadSpeed = speed;
                    Eta = etaValue;
                    StatusText = $"Downloading... {percent:F1}%";
                },
                _cts.Token);

            ProgressPercentage = 100;
            StatusText = "Download completed successfully!";
            DownloadSpeed = "";
            Eta = "";

            // Gather file size info if possible
            string sizeStr = "Unknown";
            try
            {
                var sanitizedTitle = SanitizeFileName(activeTitle);
                var directory = new DirectoryInfo(DownloadPath);
                var match = directory.GetFiles().FirstOrDefault(f => f.Name.Contains(sanitizedTitle, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    double sizeMb = (double)match.Length / (1024 * 1024);
                    sizeStr = $"{sizeMb:F1} MB";
                }
            }
            catch { }

            // Save to history
            var historyItem = new HistoryItem
            {
                Title = activeTitle,
                Url = VideoUrl,
                SavePath = DownloadPath,
                Website = Website != "-" ? Website : "Web Stream",
                DownloadDate = DateTime.Now,
                Status = "Success",
                FileSize = sizeStr
            };

            HistoryItems.Insert(0, historyItem);
            await _historyService.SaveHistoryAsync(HistoryItems.ToList());
        }
        catch (OperationCanceledException)
        {
            StatusText = "Download canceled.";
            ProgressPercentage = 0;
            DownloadSpeed = "";
            Eta = "";
        }
        catch (Exception ex)
        {
            StatusText = $"Download error: {ex.Message}";
            
            // Save failed download to history
            var historyItem = new HistoryItem
            {
                Title = activeTitle,
                Url = VideoUrl,
                SavePath = DownloadPath,
                Website = Website != "-" ? Website : "Web Stream",
                DownloadDate = DateTime.Now,
                Status = "Failed",
                FileSize = "N/A"
            };

            HistoryItems.Insert(0, historyItem);
            await _historyService.SaveHistoryAsync(HistoryItems.ToList());
        }
        finally
        {
            IsBusy = false;
            IsDownloading = false;
            _cts = null;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Download Folder",
            InitialDirectory = DownloadPath
        };

        if (dialog.ShowDialog() == true)
        {
            DownloadPath = dialog.FolderName;
            SaveAppSettings();
        }
    }

    [RelayCommand]
    private async Task ClearHistory()
    {
        HistoryItems.Clear();
        await _historyService.SaveHistoryAsync(new System.Collections.Generic.List<HistoryItem>());
    }

    [RelayCommand]
    private void OpenFolder(HistoryItem? item)
    {
        if (item == null) return;
        try
        {
            if (Directory.Exists(item.SavePath))
            {
                System.Diagnostics.Process.Start("explorer.exe", item.SavePath);
            }
        }
        catch { }
    }

    [RelayCommand]
    private async Task InstallDependencies()
    {
        IsBusy = true;
        StatusText = "Installing dependencies...";
        
        try
        {
            await _ytDlpService.EnsureYtDlpInstalledAsync(msg => StatusText = msg);
            await _ytDlpService.EnsureFFmpegInstalledAsync(msg => StatusText = msg);
            await _ytDlpService.EnsureAria2InstalledAsync(msg => StatusText = msg);
            
            UpdateDependencyStatus();
            StatusText = "All tools installed and updated successfully!";
        }
        catch (Exception ex)
        {
            StatusText = $"Installation error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        SaveAppSettings();
        StatusText = "Settings saved successfully.";
    }

    private void SaveAppSettings()
    {
        _settings.DefaultDownloadPath = DownloadPath;
        _settings.DefaultQuality = SelectedQuality;
        _settings.DefaultBrowser = SelectedBrowser;
        _settings.UseAria2 = UseAria2;
        _settings.Threads = Threads;

        _ = _historyService.SaveSettingsAsync(_settings);
    }

    private string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return name;
    }
}