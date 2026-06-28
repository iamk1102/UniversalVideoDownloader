using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using UniversalVideoDownloader.ViewModels;

namespace UniversalVideoDownloader;

public partial class MainWindow : Window
{
    private NotifyIcon? _notifyIcon;
    private bool _isExiting;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();

        InitializeTrayIcon();
        Closing += MainWindow_Closing;
        StateChanged += MainWindow_StateChanged;
    }

    private void InitializeTrayIcon()
    {
        try
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "Universal Video Downloader"
            };

            _notifyIcon.DoubleClick += (s, e) => RestoreWindow();

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Open Manager", null, (s, e) => RestoreWindow());
            
            var pauseItem = new ToolStripMenuItem("Pause All Downloads", null, (s, e) =>
            {
                if (DataContext is MainViewModel vm)
                {
                    var cmd = vm.PauseQueueCommand;
                    if (cmd.CanExecute(null)) cmd.Execute(null);
                }
            });
            contextMenu.Items.Add(pauseItem);

            var resumeItem = new ToolStripMenuItem("Resume All Downloads", null, (s, e) =>
            {
                if (DataContext is MainViewModel vm)
                {
                    var cmd = vm.StartQueueCommand;
                    if (cmd.CanExecute(null)) cmd.Execute(null);
                }
            });
            contextMenu.Items.Add(resumeItem);

            contextMenu.Items.Add(new ToolStripSeparator());
            
            var exitItem = new ToolStripMenuItem("Exit", null, (s, e) =>
            {
                _isExiting = true;
                Close();
            });
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
        }
        catch { }
    }

    private void RestoreWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.MinimizeToTray && !_isExiting)
        {
            e.Cancel = true;
            Hide();
            _notifyIcon?.ShowBalloonTip(3000, "Universal Video Downloader", 
                "Minimized to system tray. Active downloads will continue in the background.", 
                ToolTipIcon.Info);
        }
        else
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
        }
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && DataContext is MainViewModel vm && vm.MinimizeToTray)
        {
            Hide();
        }
    }

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.Text) || e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        string? url = null;
        if (e.Data.GetDataPresent(System.Windows.DataFormats.Text))
        {
            url = e.Data.GetData(System.Windows.DataFormats.Text) as string;
        }
        else if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = e.Data.GetData(System.Windows.DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0)
            {
                url = files[0];
            }
        }

        if (!string.IsNullOrEmpty(url) && DataContext is MainViewModel vm)
        {
            vm.VideoUrl = url;
            vm.StatusText = "Link dropped. Resolving...";
            if (vm.AnalyzeCommand.CanExecute(null))
            {
                vm.AnalyzeCommand.Execute(null);
            }
        }
    }
}