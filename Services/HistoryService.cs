using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using UniversalVideoDownloader.Models;

namespace UniversalVideoDownloader.Services;

public class HistoryService
{
    private readonly string _folderPath;
    private readonly string _historyFilePath;
    private readonly string _settingsFilePath;

    public HistoryService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _folderPath = Path.Combine(appData, "UniversalVideoDownloader");
        _historyFilePath = Path.Combine(_folderPath, "history.json");
        _settingsFilePath = Path.Combine(_folderPath, "settings.json");
    }

    private void EnsureFolderExists()
    {
        if (!Directory.Exists(_folderPath))
        {
            Directory.CreateDirectory(_folderPath);
        }
    }

    public async Task<List<HistoryItem>> LoadHistoryAsync()
    {
        try
        {
            if (!File.Exists(_historyFilePath))
            {
                return new List<HistoryItem>();
            }

            var json = await File.ReadAllTextAsync(_historyFilePath);
            return JsonSerializer.Deserialize<List<HistoryItem>>(json) ?? new List<HistoryItem>();
        }
        catch
        {
            return new List<HistoryItem>();
        }
    }

    public async Task SaveHistoryAsync(List<HistoryItem> history)
    {
        try
        {
            EnsureFolderExists();
            var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_historyFilePath, json);
        }
        catch { }
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return CreateDefaultSettings();
            }

            var json = await File.ReadAllTextAsync(_settingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? CreateDefaultSettings();
        }
        catch
        {
            return CreateDefaultSettings();
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        try
        {
            EnsureFolderExists();
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsFilePath, json);
        }
        catch { }
    }

    private AppSettings CreateDefaultSettings()
    {
        return new AppSettings
        {
            DefaultDownloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            DefaultQuality = "Best",
            DefaultBrowser = "None",
            UseAria2 = true,
            Threads = 8
        };
    }
}
