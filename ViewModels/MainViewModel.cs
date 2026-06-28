using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
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

    private DispatcherTimer? _clipboardTimer;
    private DispatcherTimer? _schedulerTimer;
    private string _lastClipboardText = "";
    private bool _isProcessingQueue;
    private readonly object _queueLock = new();
    private readonly System.Collections.Generic.List<HistoryItem> _allHistoryItems = new();

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    public MainViewModel()
    {
        _ytDlpService = new YtDlpService();
        _historyService = new HistoryService();
        
        HistoryItems = new ObservableCollection<HistoryItem>();
        DownloadQueue = new ObservableCollection<QueueItem>();
        
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
        MaxConcurrentDownloads = _settings.MaxConcurrentDownloads;
        EnableClipboardMonitor = _settings.EnableClipboardMonitor;
        MinimizeToTray = _settings.MinimizeToTray;
        SchedulerEnabled = _settings.SchedulerEnabled;
        SchedulerTime = _settings.SchedulerTime;
        SelectedSchedulerAction = _settings.SchedulerAction;
        SelectedTheme = _settings.Theme;
        SelectedLanguage = _settings.Language;
        SelectedSpeedLimit = _settings.SpeedLimit ?? "Unlimited";

        // Load History
        var history = await _historyService.LoadHistoryAsync();
        foreach (var item in history.OrderByDescending(h => h.DownloadDate))
        {
            _allHistoryItems.Add(item);
        }
        FilterHistory();

        UpdateDependencyStatus();

        // Apply theme and language settings on startup
        ApplyTheme(SelectedTheme);
        ApplyLanguage(SelectedLanguage);

        // Start background timers
        StartClipboardMonitor();
        StartScheduler();
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
    [NotifyPropertyChangedFor(nameof(HasThumbnail))]
    private string thumbnailUrl = "";

    public bool HasNoThumbnail => string.IsNullOrEmpty(ThumbnailUrl);
    public bool HasThumbnail => !string.IsNullOrEmpty(ThumbnailUrl);

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

    [ObservableProperty]
    private int maxConcurrentDownloads = 2;

    [ObservableProperty]
    private bool enableClipboardMonitor = false;

    [ObservableProperty]
    private bool minimizeToTray = false;

    [ObservableProperty]
    private bool schedulerEnabled = false;

    [ObservableProperty]
    private string schedulerTime = "02:00";

    [ObservableProperty]
    private string selectedSchedulerAction = "None"; // None, Shutdown, Sleep

    [ObservableProperty]
    private string selectedTheme = "Dark"; // Dark, Light

    [ObservableProperty]
    private string selectedLanguage = "en"; // en, vi

    [ObservableProperty]
    private string selectedSpeedLimit = "Unlimited";

    [ObservableProperty]
    private string historySearchQuery = "";

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
    public ObservableCollection<QueueItem> DownloadQueue { get; }

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
                SelectedSpeedLimit,
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

            // Gather size
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

            _allHistoryItems.Insert(0, historyItem);
            FilterHistory();
            await _historyService.SaveHistoryAsync(_allHistoryItems);
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

            _allHistoryItems.Insert(0, historyItem);
            FilterHistory();
            await _historyService.SaveHistoryAsync(_allHistoryItems);
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
        _allHistoryItems.Clear();
        FilterHistory();
        await _historyService.SaveHistoryAsync(_allHistoryItems);
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
        
        // Re-apply theme and language when saved
        ApplyTheme(SelectedTheme);
        ApplyLanguage(SelectedLanguage);
        
        StatusText = "Settings saved successfully.";
    }

    private void SaveAppSettings()
    {
        _settings.DefaultDownloadPath = DownloadPath;
        _settings.DefaultQuality = SelectedQuality;
        _settings.DefaultBrowser = SelectedBrowser;
        _settings.UseAria2 = UseAria2;
        _settings.Threads = Threads;
        _settings.MaxConcurrentDownloads = MaxConcurrentDownloads;
        _settings.EnableClipboardMonitor = EnableClipboardMonitor;
        _settings.MinimizeToTray = MinimizeToTray;
        _settings.SchedulerEnabled = SchedulerEnabled;
        _settings.SchedulerTime = SchedulerTime;
        _settings.SchedulerAction = SelectedSchedulerAction;
        _settings.Theme = SelectedTheme;
        _settings.Language = SelectedLanguage;
        _settings.SpeedLimit = SelectedSpeedLimit;

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

    // --- SPRINT 2: QUEUE CONTROLLERS ---

    [RelayCommand]
    private void AddToQueue()
    {
        if (string.IsNullOrWhiteSpace(VideoUrl))
        {
            StatusText = "Please enter a valid video URL.";
            return;
        }

        var url = VideoUrl;
        VideoUrl = ""; // Reset URL field

        var queueItem = new QueueItem
        {
            Id = Guid.NewGuid(),
            Title = "Resolving Link Metadata...",
            Url = url,
            Status = "Pending",
            SavePath = DownloadPath,
            Quality = SelectedQuality,
            IsAudioOnly = IsAudioOnly,
            WriteSubtitles = WriteSubtitles,
            UseAria2 = UseAria2,
            Browser = SelectedBrowser
        };

        DownloadQueue.Add(queueItem);
        StatusText = "Added to download queue.";

        // Run background analysis
        _ = Task.Run(async () =>
        {
            try
            {
                var info = await _ytDlpService.GetVideoInfoAsync(url, queueItem.Browser);
                if (info != null)
                {
                    queueItem.Title = info.Title;
                }
                else
                {
                    queueItem.Title = "Video File";
                }
            }
            catch (Exception ex)
            {
                queueItem.Title = "Unresolved Video URL";
                queueItem.AppendLogLine($"[WARNING] Metadata analysis failed. Details: {ex.Message}");
            }
            finally
            {
                _ = ProcessQueueAsync();
            }
        });
    }

    [RelayCommand]
    private void StartQueue()
    {
        foreach (var item in DownloadQueue)
        {
            if (item.Status == "Paused" || item.Status == "Failed")
            {
                item.Status = "Pending";
            }
        }
        _ = ProcessQueueAsync();
        StatusText = "Queue processing started.";
    }

    [RelayCommand]
    private void PauseQueue()
    {
        foreach (var item in DownloadQueue)
        {
            if (item.Status == "Downloading" || item.Status == "Pending")
            {
                if (item.Status == "Downloading")
                {
                    item.Cts?.Cancel();
                }
                item.Status = "Paused";
            }
        }
        StatusText = "Queue processing paused.";
    }

    [RelayCommand]
    private void PauseItem(QueueItem? item)
    {
        if (item == null) return;
        if (item.Status == "Downloading")
        {
            item.Cts?.Cancel();
        }
        item.Status = "Paused";
    }

    [RelayCommand]
    private void ResumeItem(QueueItem? item)
    {
        if (item == null) return;
        item.Status = "Pending";
        _ = ProcessQueueAsync();
    }

    [RelayCommand]
    private void RemoveItem(QueueItem? item)
    {
        if (item == null) return;
        if (item.Status == "Downloading")
        {
            item.Cts?.Cancel();
        }
        DownloadQueue.Remove(item);
    }

    [RelayCommand]
    private void RetryItem(QueueItem? item)
    {
        if (item == null) return;
        item.Progress = 0;
        item.Speed = "";
        item.ETA = "";
        item.Status = "Pending";
        item.LogContent.Clear();
        item.AppendLogLine("[INFO] Retrying download...");
        _ = ProcessQueueAsync();
    }

    [RelayCommand]
    private void ShowLogWindow(QueueItem? item)
    {
        if (item == null) return;
        
        App.Current.Dispatcher.Invoke(() =>
        {
            var logWin = new LogWindow(item)
            {
                Owner = App.Current.MainWindow
            };
            logWin.Show();
        });
    }

    private async Task ProcessQueueAsync()
    {
        lock (_queueLock)
        {
            if (_isProcessingQueue) return;
            _isProcessingQueue = true;
        }

        try
        {
            while (true)
            {
                int activeCount = DownloadQueue.Count(q => q.Status == "Downloading");
                if (activeCount >= MaxConcurrentDownloads)
                {
                    break;
                }

                var nextItem = DownloadQueue.FirstOrDefault(q => q.Status == "Pending");
                if (nextItem == null)
                {
                    break;
                }

                nextItem.Status = "Downloading";
                _ = DownloadQueueItemAsync(nextItem);
            }
        }
        finally
        {
            lock (_queueLock)
            {
                _isProcessingQueue = false;
            }
        }
    }

    private async Task DownloadQueueItemAsync(QueueItem item)
    {
        item.Cts = new CancellationTokenSource();
        var token = item.Cts.Token;

        try
        {
            item.AppendLogLine("[INFO] Resolving tool binaries...");
            await _ytDlpService.EnsureYtDlpInstalledAsync(msg => item.AppendLogLine($"[INFO] {msg}"));
            if (item.UseAria2)
            {
                await _ytDlpService.EnsureAria2InstalledAsync(msg => item.AppendLogLine($"[INFO] {msg}"));
            }
            if (!item.IsAudioOnly)
            {
                await _ytDlpService.EnsureFFmpegInstalledAsync(msg => item.AppendLogLine($"[INFO] {msg}"));
            }

            item.AppendLogLine("[INFO] Tool check complete. Launching yt-dlp...");
            await _ytDlpService.DownloadVideoAsync(
                item,
                Threads,
                SelectedSpeedLimit,
                null,
                token);

            item.Progress = 100;
            item.Status = "Completed";
            item.Speed = "";
            item.ETA = "";
            item.AppendLogLine("[INFO] Download completed successfully!");

            string sizeStr = "Unknown";
            try
            {
                var sanitizedTitle = SanitizeFileName(item.Title);
                var directory = new DirectoryInfo(item.SavePath);
                var match = directory.GetFiles().FirstOrDefault(f => f.Name.Contains(sanitizedTitle, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    double sizeMb = (double)match.Length / (1024 * 1024);
                    sizeStr = $"{sizeMb:F1} MB";
                }
            }
            catch { }

            var historyItem = new HistoryItem
            {
                Title = item.Title,
                Url = item.Url,
                SavePath = item.SavePath,
                Website = string.IsNullOrEmpty(item.Browser) ? "Web Stream" : item.Browser,
                DownloadDate = DateTime.Now,
                Status = "Success",
                FileSize = sizeStr
            };

            lock (_allHistoryItems)
            {
                _allHistoryItems.Insert(0, historyItem);
            }
            FilterHistory();
            await _historyService.SaveHistoryAsync(_allHistoryItems);

            // Fire Custom Toast notification (Sprint 3)
            TriggerNotification(item.Title, true);
        }
        catch (OperationCanceledException)
        {
            item.Status = "Paused";
            item.Speed = "";
            item.ETA = "";
            item.AppendLogLine("[INFO] Process suspended by user.");
        }
        catch (Exception ex)
        {
            item.Status = "Failed";
            item.ErrorMessage = ex.Message;
            item.Speed = "";
            item.ETA = "";
            item.AppendLogLine($"[ERROR] Download terminated. Detail: {ex.Message}");

            var historyItem = new HistoryItem
            {
                Title = item.Title,
                Url = item.Url,
                SavePath = item.SavePath,
                Website = "Web Stream",
                DownloadDate = DateTime.Now,
                Status = "Failed",
                FileSize = "N/A"
            };

            lock (_allHistoryItems)
            {
                _allHistoryItems.Insert(0, historyItem);
            }
            FilterHistory();
            await _historyService.SaveHistoryAsync(_allHistoryItems);

            // Fire Custom Toast notification (Sprint 3)
            TriggerNotification(item.Title, false);
        }
        finally
        {
            item.Cts?.Dispose();
            item.Cts = null;

            _ = ProcessQueueAsync();

            // Run post-download scheduler actions (Sprint 4)
            CheckSchedulerPowerActions();
        }
    }

    // --- SPRINT 3: AUTOMATION AND TRAY HELPERS ---

    private void StartClipboardMonitor()
    {
        _clipboardTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clipboardTimer.Tick += (s, e) =>
        {
            if (!EnableClipboardMonitor) return;
            try
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(text) && text != _lastClipboardText)
                    {
                        text = text.Trim();
                        // Regex matches common video sites
                        if ((text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                             text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) &&
                            (text.Contains("youtube.com") || text.Contains("youtu.be") || 
                             text.Contains("tiktok.com") || text.Contains("facebook.com") || 
                             text.Contains("instagram.com") || text.Contains("vimeo.com")))
                        {
                            _lastClipboardText = text;
                            VideoUrl = text;
                            StatusText = "Video link automatically copied from clipboard.";
                            _ = AnalyzeAsync(); // Trigger analysis on auto-detect
                        }
                    }
                }
            }
            catch { }
        };
        _clipboardTimer.Start();
    }

    private void TriggerNotification(string title, bool isSuccess)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            try
            {
                var notifyWin = new NotificationWindow(title, isSuccess)
                {
                    Topmost = true,
                    ShowInTaskbar = false
                };
                notifyWin.Show();
            }
            catch { }
        });
    }

    // --- SPRINT 4: SCHEDULER & LOCALIZATION HELPERS ---

    private void StartScheduler()
    {
        _schedulerTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(15)
        };
        _schedulerTimer.Tick += (s, e) =>
        {
            if (!SchedulerEnabled) return;
            try
            {
                var currentTime = DateTime.Now.ToString("HH:mm");
                if (currentTime == SchedulerTime)
                {
                    // Kick off queue
                    StatusText = "Scheduled download window hit. Processing queue...";
                    StartQueue();
                }
            }
            catch { }
        };
        _schedulerTimer.Start();
    }

    private void CheckSchedulerPowerActions()
    {
        if (!SchedulerEnabled || SelectedSchedulerAction == "None") return;

        // Check if queue is fully empty of active downloads
        int active = DownloadQueue.Count(q => q.Status == "Downloading" || q.Status == "Pending");
        if (active == 0)
        {
            StatusText = $"Downloads finished. Executing power action: {SelectedSchedulerAction} in 30 seconds.";
            
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            timer.Tick += (s, ev) =>
            {
                timer.Stop();
                // Ensure no new download started in the meantime
                if (DownloadQueue.Count(q => q.Status == "Downloading" || q.Status == "Pending") == 0)
                {
                    if (SelectedSchedulerAction == "Shutdown")
                    {
                        System.Diagnostics.Process.Start("shutdown.exe", "/s /t 0");
                    }
                    else if (SelectedSchedulerAction == "Sleep")
                    {
                        SetSuspendState(false, true, true); // Win32 sleep call
                    }
                }
            };
            timer.Start();
        }
    }

    private void ApplyTheme(string theme)
    {
        try
        {
            var app = Application.Current;
            var dictionaries = app.Resources.MergedDictionaries;
            
            // Clear existing dark/light resource dictionaries if any
            var existingThemeDicts = dictionaries.Where(d => d.Source != null && 
                (d.Source.OriginalString.Contains("DarkTheme.xaml") || 
                 d.Source.OriginalString.Contains("LightTheme.xaml"))).ToList();
            
            foreach (var dict in existingThemeDicts)
            {
                dictionaries.Remove(dict);
            }

            var themeUri = new Uri($"Themes/{theme}Theme.xaml", UriKind.Relative);
            dictionaries.Add(new ResourceDictionary { Source = themeUri });
        }
        catch { }
    }

    private void ApplyLanguage(string langCode)
    {
        try
        {
            var app = Application.Current;
            var dictionaries = app.Resources.MergedDictionaries;

            var existingLangDicts = dictionaries.Where(d => d.Source != null && 
                (d.Source.OriginalString.Contains("Lang.en.xaml") || 
                 d.Source.OriginalString.Contains("Lang.vi.xaml"))).ToList();

            foreach (var dict in existingLangDicts)
            {
                dictionaries.Remove(dict);
            }

            var langUri = new Uri($"Resources/Lang.{langCode}.xaml", UriKind.Relative);
            dictionaries.Add(new ResourceDictionary { Source = langUri });
        }
        catch { }
    }

    partial void OnHistorySearchQueryChanged(string value)
    {
        FilterHistory();
    }

    private void FilterHistory()
    {
        if (HistoryItems == null) return;
        
        App.Current?.Dispatcher?.Invoke(() =>
        {
            HistoryItems.Clear();
            var query = string.IsNullOrWhiteSpace(HistorySearchQuery) ? "" : HistorySearchQuery.ToLower().Trim();
            lock (_allHistoryItems)
            {
                foreach (var item in _allHistoryItems)
                {
                    if (string.IsNullOrEmpty(query) || 
                        (item.Title != null && item.Title.ToLower().Contains(query)) ||
                        (item.Url != null && item.Url.ToLower().Contains(query)) ||
                        (item.Website != null && item.Website.ToLower().Contains(query)))
                    {
                        HistoryItems.Add(item);
                    }
                }
            }
        });
    }

    [ObservableProperty]
    private string updateStatusText = "App is up to date";

    [ObservableProperty]
    private bool isUpdateAvailable;

    private string _latestDownloadUrl = "";

    [RelayCommand]
    private async Task CheckForUpdates()
    {
        try
        {
            UpdateStatusText = "Checking for updates...";
            using var client = new System.Net.Http.HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "UniversalVideoDownloader-Updater");
            
            var response = await client.GetAsync("https://api.github.com/repos/iamk1102/UniversalVideoDownloader/releases/latest");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var matchTag = Regex.Match(json, @"""tag_name""\s*:\s*""([^""]+)""");
                if (matchTag.Success)
                {
                    var latestVersion = matchTag.Groups[1].Value.Replace("v", "");
                    var currentVersion = "1.0.0";
                    
                    if (IsNewerVersion(latestVersion, currentVersion))
                    {
                        var matchUrl = Regex.Match(json, @"""browser_download_url""\s*:\s*""([^""]+\.exe)""");
                        if (matchUrl.Success)
                        {
                            _latestDownloadUrl = matchUrl.Groups[1].Value;
                        }
                        
                        IsUpdateAvailable = true;
                        UpdateStatusText = $"Version {latestVersion} available!";
                        
                        var result = MessageBox.Show(
                            $"A new version (v{latestVersion}) of Universal Video Downloader is available!\nDo you want to download and install it now?",
                            "Update Available",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);
                            
                        if (result == MessageBoxResult.Yes)
                        {
                            await DownloadAndApplyUpdateAsync();
                        }
                    }
                    else
                    {
                        IsUpdateAvailable = false;
                        UpdateStatusText = "You are running the latest version.";
                        MessageBox.Show("You are running the latest version (v1.0.0).", "Check for Updates", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            else
            {
                UpdateStatusText = "Check failed. Remote server unavailable.";
                MessageBox.Show("Could not connect to GitHub API to check for updates.", "Update Check Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            UpdateStatusText = "Check failed. No internet connection.";
            MessageBox.Show($"Could not check for updates. Error: {ex.Message}", "Update Check Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool IsNewerVersion(string latest, string current)
    {
        try
        {
            var lVer = new Version(latest);
            var cVer = new Version(current);
            return lVer > cVer;
        }
        catch
        {
            return false;
        }
    }

    private async Task DownloadAndApplyUpdateAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_latestDownloadUrl))
            {
                MessageBox.Show("Could not resolve update download link.", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            UpdateStatusText = "Downloading update...";
            var tempExePath = Path.Combine(Path.GetTempPath(), "UniversalVideoDownloader_Update.exe");
            
            using (var client = new System.Net.Http.HttpClient())
            {
                var bytes = await client.GetByteArrayAsync(_latestDownloadUrl);
                await File.WriteAllBytesAsync(tempExePath, bytes);
            }

            var currentExe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(currentExe)) return;
            
            var batchPath = Path.Combine(Path.GetTempPath(), "uvd_update.bat");
            var batchContent = $"""
@echo off
timeout /t 2 /nobreak > nul
copy /y "{tempExePath}" "{currentExe}"
start "" "{currentExe}"
del "%~f0"
""";
            await File.WriteAllTextAsync(batchPath, batchContent);

            MessageBox.Show("Update downloaded successfully! The application will restart to apply the update.", "Update Installed", MessageBoxButton.OK, MessageBoxImage.Information);
            
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batchPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            System.Diagnostics.Process.Start(startInfo);
            
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Update failed: {ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}