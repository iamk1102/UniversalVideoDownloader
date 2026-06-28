using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UniversalVideoDownloader.Helpers;
using UniversalVideoDownloader.Models;

namespace UniversalVideoDownloader.Services;

public class YtDlpService
{
    private readonly string _toolsDir;
    private readonly string _ytDlpPath;

    public YtDlpService()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        _toolsDir = Path.Combine(appDir, "Tools");
        _ytDlpPath = Path.Combine(_toolsDir, "yt-dlp.exe");
    }

    public bool IsYtDlpInstalled => File.Exists(_ytDlpPath);
    public bool IsFFmpegInstalled => File.Exists(Path.Combine(_toolsDir, "ffmpeg.exe")) && File.Exists(Path.Combine(_toolsDir, "ffprobe.exe"));
    public bool IsAria2Installed => File.Exists(Path.Combine(_toolsDir, "aria2c.exe"));

    public async Task EnsureYtDlpInstalledAsync(Action<string>? onProgress = null)
    {
        if (IsYtDlpInstalled) return;

        if (!Directory.Exists(_toolsDir))
        {
            Directory.CreateDirectory(_toolsDir);
        }

        onProgress?.Invoke("Downloading yt-dlp... 0%");

        var downloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
        using (var client = new HttpClient())
        using (var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using (var contentStream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = new FileStream(_ytDlpPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                var buffer = new byte[8192];
                var totalRead = 0L;
                var bytesRead = 0;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        var percentage = (double)totalRead / totalBytes * 100;
                        onProgress?.Invoke($"Downloading yt-dlp... {percentage:F1}%");
                    }
                    else
                    {
                        onProgress?.Invoke($"Downloading yt-dlp... {(double)totalRead / (1024 * 1024):F1} MB");
                    }
                }
            }
        }
        
        onProgress?.Invoke("yt-dlp downloaded successfully!");
    }

    public async Task EnsureFFmpegInstalledAsync(Action<string>? onProgress = null)
    {
        if (IsFFmpegInstalled) return;

        if (!Directory.Exists(_toolsDir))
        {
            Directory.CreateDirectory(_toolsDir);
        }

        // Official yt-dlp recommended static build link
        var downloadUrl = "https://github.com/yt-dlp/ffmpeg-builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
        
        onProgress?.Invoke("Downloading FFmpeg package...");
        await DownloadZipAndExtractFileAsync(downloadUrl, "ffmpeg.exe", "ffmpeg.exe", onProgress);
        await DownloadZipAndExtractFileAsync(downloadUrl, "ffprobe.exe", "ffprobe.exe", onProgress);
        
        onProgress?.Invoke("FFmpeg installed successfully!");
    }

    public async Task EnsureAria2InstalledAsync(Action<string>? onProgress = null)
    {
        if (IsAria2Installed) return;

        if (!Directory.Exists(_toolsDir))
        {
            Directory.CreateDirectory(_toolsDir);
        }

        var downloadUrl = "https://github.com/aria2/aria2/releases/download/release-1.37.0/aria2-1.37.0-win-64bit-build1.zip";
        
        onProgress?.Invoke("Downloading aria2 package...");
        await DownloadZipAndExtractFileAsync(downloadUrl, "aria2c.exe", "aria2c.exe", onProgress);
        
        onProgress?.Invoke("aria2 installed successfully!");
    }

    private async Task DownloadZipAndExtractFileAsync(
        string zipUrl,
        string targetFileName,
        string searchInZip,
        Action<string>? onProgress = null)
    {
        var tempZipPath = Path.Combine(_toolsDir, Path.GetRandomFileName() + ".zip");
        try
        {
            using (var client = new HttpClient())
            using (var response = await client.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    var totalRead = 0L;
                    var bytesRead = 0;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;

                        if (totalBytes > 0)
                        {
                            var percentage = (double)totalRead / totalBytes * 100;
                            onProgress?.Invoke($"Downloading {targetFileName}... {percentage:F1}%");
                        }
                        else
                        {
                            onProgress?.Invoke($"Downloading {targetFileName}... {(double)totalRead / (1024 * 1024):F1} MB");
                        }
                    }
                }
            }

            onProgress?.Invoke($"Extracting {targetFileName}...");

            using (var archive = ZipFile.OpenRead(tempZipPath))
            {
                ZipArchiveEntry? targetEntry = null;
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith(searchInZip, StringComparison.OrdinalIgnoreCase))
                    {
                        targetEntry = entry;
                        break;
                    }
                }

                if (targetEntry != null)
                {
                    var destPath = Path.Combine(_toolsDir, targetFileName);
                    targetEntry.ExtractToFile(destPath, overwrite: true);
                }
                else
                {
                    throw new FileNotFoundException($"Could not find '{searchInZip}' inside the zip archive.");
                }
            }
        }
        finally
        {
            if (File.Exists(tempZipPath))
            {
                try { File.Delete(tempZipPath); } catch { }
            }
        }
    }

    public async Task<VideoInfo?> GetVideoInfoAsync(string url, string browserCookies = "None", CancellationToken cancellationToken = default)
    {
        await EnsureYtDlpInstalledAsync();

        var sb = new StringBuilder();
        sb.Append($"--dump-json --impersonate chrome ");
        if (!string.IsNullOrEmpty(browserCookies) && !browserCookies.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append($"--cookies-from-browser {browserCookies.ToLower()} ");
        }
        sb.Append($"\"{url}\"");

        var result = await ProcessHelper.RunProcessAsync(
            _ytDlpPath,
            sb.ToString(),
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0 || string.IsNullOrEmpty(result.Output))
        {
            throw new Exception($"Failed to retrieve video information. Error: {result.Error}");
        }

        try
        {
            using var doc = JsonDocument.Parse(result.Output);
            var root = doc.RootElement;

            var title = root.TryGetProperty("title", out var pTitle) ? pTitle.GetString() ?? "" : "Unknown Title";
            var website = root.TryGetProperty("extractor", out var pExtractor) ? pExtractor.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(website) && root.TryGetProperty("extractor_key", out var pExtKey))
            {
                website = pExtKey.GetString() ?? "";
            }

            var durationSec = root.TryGetProperty("duration", out var pDuration) ? pDuration.GetDouble() : 0;
            var durationStr = FormatDuration(durationSec);

            var resolution = root.TryGetProperty("resolution", out var pRes) ? pRes.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(resolution))
            {
                var width = root.TryGetProperty("width", out var pWidth) ? pWidth.GetInt32() : 0;
                var height = root.TryGetProperty("height", out var pHeight) ? pHeight.GetInt32() : 0;
                if (width > 0 && height > 0)
                {
                    resolution = $"{width}x{height}";
                }
            }

            var thumbnail = root.TryGetProperty("thumbnail", out var pThumb) ? pThumb.GetString() ?? "" : "";

            return new VideoInfo
            {
                Title = title,
                Website = website,
                Duration = durationStr,
                Resolution = resolution,
                Thumbnail = thumbnail
            };
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to parse video info JSON. Detail: {ex.Message}", ex);
        }
    }

    public async Task DownloadVideoAsync(
        string url,
        string destinationDir,
        string quality = "Best",
        bool audioOnly = false,
        string browserCookies = "None",
        bool writeSubtitles = false,
        bool useAria2 = true,
        int threads = 8,
        string speedLimit = "Unlimited",
        Action<double, string, string>? onProgressReport = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureYtDlpInstalledAsync();

        if (string.IsNullOrEmpty(destinationDir))
        {
            destinationDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }

        if (!Directory.Exists(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
        }

        var sb = new StringBuilder();
        
        // Base config
        sb.Append($"--impersonate chrome ");
        sb.Append($"-N {threads} ");
        sb.Append($"--continue --no-overwrites ");

        // Save output format template
        if (audioOnly)
        {
            sb.Append($"-o \"{Path.Combine(destinationDir, "%(title)s.%(ext)s")}\" ");
        }
        else
        {
            // Merged file template
            sb.Append($"-o \"{Path.Combine(destinationDir, "%(title)s.%(ext)s")}\" ");
        }

        // Format selection
        if (audioOnly)
        {
            sb.Append($"-f \"ba/b\" -x --audio-format mp3 ");
        }
        else
        {
            if (string.IsNullOrEmpty(quality) || quality.Equals("Best", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append($"-f \"bv*+ba/b\" --merge-output-format mp4 ");
            }
            else
            {
                // quality value is numeric e.g. "1080"
                sb.Append($"-f \"bv*[height<={quality}]+ba/b[height<={quality}]\" --merge-output-format mp4 ");
            }
        }

        // Browser cookies
        if (!string.IsNullOrEmpty(browserCookies) && !browserCookies.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append($"--cookies-from-browser {browserCookies.ToLower()} ");
        }

        // Subtitles
        if (writeSubtitles)
        {
            sb.Append($"--write-subs --embed-subs ");
        }

        // Aria2 download acceleration
        if (useAria2 && IsAria2Installed)
        {
            sb.Append($"--downloader aria2c --downloader-args \"aria2c:-x{threads} -s{threads} -k1M\" ");
        }

        // Speed Limit
        if (!string.IsNullOrEmpty(speedLimit) && !speedLimit.Equals("Unlimited", StringComparison.OrdinalIgnoreCase))
        {
            var limitArg = speedLimit.Replace(" KB/s", "K").Replace(" MB/s", "M").Replace(" ", "");
            sb.Append($"--limit-rate {limitArg} ");
        }

        sb.Append($"\"{url}\"");

        var progressRegex = new Regex(@"\[download\]\s+([0-9.]+)%\s+of\s+\S+\s+at\s+(\S+)\s+ETA\s+(\S+)", RegexOptions.Compiled);

        var result = await ProcessHelper.RunProcessAsync(
            _ytDlpPath,
            sb.ToString(),
            onOutputReceived: line =>
            {
                var match = progressRegex.Match(line);
                if (match.Success)
                {
                    if (double.TryParse(match.Groups[1].Value, out var percent))
                    {
                        var speed = match.Groups[2].Value;
                        var eta = match.Groups[3].Value;
                        onProgressReport?.Invoke(percent, speed, eta);
                    }
                }
            },
            cancellationToken: cancellationToken);

        if (result.IsCanceled)
        {
            throw new OperationCanceledException("Download canceled by user.");
        }

        if (result.ExitCode != 0)
        {
            throw new Exception($"Download failed. Error: {result.Error}");
        }
    }

    public async Task DownloadVideoAsync(
        QueueItem item,
        int threads = 8,
        string speedLimit = "Unlimited",
        Action<double, string, string>? onProgressReport = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureYtDlpInstalledAsync();

        var destinationDir = item.SavePath;
        if (string.IsNullOrEmpty(destinationDir))
        {
            destinationDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }

        if (!Directory.Exists(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
        }

        var sb = new StringBuilder();
        
        // Base config
        sb.Append($"--impersonate chrome ");
        sb.Append($"-N {threads} ");
        sb.Append($"--continue --no-overwrites ");

        sb.Append($"-o \"{Path.Combine(destinationDir, "%(title)s.%(ext)s")}\" ");

        // Format selection
        if (item.IsAudioOnly)
        {
            sb.Append($"-f \"ba/b\" -x --audio-format mp3 ");
        }
        else
        {
            if (string.IsNullOrEmpty(item.Quality) || item.Quality.Equals("Best", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append($"-f \"bv*+ba/b\" --merge-output-format mp4 ");
            }
            else
            {
                sb.Append($"-f \"bv*[height<={item.Quality}]+ba/b[height<={item.Quality}]\" --merge-output-format mp4 ");
            }
        }

        // Browser cookies
        if (!string.IsNullOrEmpty(item.Browser) && !item.Browser.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append($"--cookies-from-browser {item.Browser.ToLower()} ");
        }

        // Subtitles
        if (item.WriteSubtitles)
        {
            sb.Append($"--write-subs --embed-subs ");
        }

        // Aria2 download acceleration
        if (item.UseAria2 && IsAria2Installed)
        {
            sb.Append($"--downloader aria2c --downloader-args \"aria2c:-x{threads} -s{threads} -k1M\" ");
        }

        // Speed Limit
        if (!string.IsNullOrEmpty(speedLimit) && !speedLimit.Equals("Unlimited", StringComparison.OrdinalIgnoreCase))
        {
            var limitArg = speedLimit.Replace(" KB/s", "K").Replace(" MB/s", "M").Replace(" ", "");
            sb.Append($"--limit-rate {limitArg} ");
        }

        sb.Append($"\"{item.Url}\"");

        var progressRegex = new Regex(@"\[download\]\s+([0-9.]+)%\s+of\s+\S+\s+at\s+(\S+)\s+ETA\s+(\S+)", RegexOptions.Compiled);

        var result = await ProcessHelper.RunProcessAsync(
            _ytDlpPath,
            sb.ToString(),
            onOutputReceived: line =>
            {
                item.AppendLogLine(line);

                var match = progressRegex.Match(line);
                if (match.Success)
                {
                    if (double.TryParse(match.Groups[1].Value, out var percent))
                    {
                        var speed = match.Groups[2].Value;
                        var eta = match.Groups[3].Value;
                        
                        item.Progress = percent;
                        item.Speed = speed;
                        item.ETA = eta;

                        onProgressReport?.Invoke(percent, speed, eta);
                    }
                }
            },
            onErrorReceived: line =>
            {
                item.AppendLogLine($"[ERROR] {line}");
            },
            cancellationToken: cancellationToken);

        if (result.IsCanceled)
        {
            throw new OperationCanceledException("Download canceled by user.");
        }

        if (result.ExitCode != 0)
        {
            throw new Exception($"Download failed. Error: {result.Error}");
        }
    }

    private string FormatDuration(double seconds)
    {
        if (seconds <= 0) return "Unknown";
        var time = TimeSpan.FromSeconds(seconds);
        if (time.TotalHours >= 1)
        {
            return time.ToString(@"hh\:mm\:ss");
        }
        return time.ToString(@"mm\:ss");
    }
}