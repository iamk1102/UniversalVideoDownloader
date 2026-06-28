// ============================================================================
// Universal Video Downloader
// Copyright (c) 2026 Kaka91. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// https://github.com/iamk1102/UniversalVideoDownloader
// ============================================================================

using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UniversalVideoDownloader.Helpers;

public static class ProcessHelper
{
    public static async Task<ProcessResult> RunProcessAsync(
        string fileName,
        string arguments,
        Action<string>? onOutputReceived = null,
        Action<string>? onErrorReceived = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ProcessResult();

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        var outputCloseTcs = new TaskCompletionSource<bool>();
        var errorCloseTcs = new TaskCompletionSource<bool>();

        process.OutputDataReceived += (s, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
                onOutputReceived?.Invoke(e.Data);
            }
            else
            {
                outputCloseTcs.TrySetResult(true);
            }
        };

        process.ErrorDataReceived += (s, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
                onErrorReceived?.Invoke(e.Data);
            }
            else
            {
                errorCloseTcs.TrySetResult(true);
            }
        };

        if (!process.Start())
        {
            result.ExitCode = -1;
            result.Error = "Failed to start process.";
            return result;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            // Wait for process to exit
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            
            // Wait for streams to finish flushing or timeout after 2 seconds
            await Task.WhenAll(
                Task.WhenAny(outputCloseTcs.Task, Task.Delay(2000, cancellationToken)),
                Task.WhenAny(errorCloseTcs.Task, Task.Delay(2000, cancellationToken))
            ).ConfigureAwait(false);

            result.ExitCode = process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            result.IsCanceled = true;
            result.ExitCode = -1;
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(true); // Kill process tree
                }
            }
            catch { }
        }
        catch (Exception ex)
        {
            result.ExitCode = -1;
            result.Error = ex.Message;
        }

        result.Output = outputBuilder.ToString();
        result.Error = errorBuilder.ToString();
        return result;
    }
}

public class ProcessResult
{
    public int ExitCode { get; set; }
    public string Output { get; set; } = "";
    public string Error { get; set; } = "";
    public bool IsCanceled { get; set; }
}