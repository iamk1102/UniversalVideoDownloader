using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace UniversalVideoDownloader;

public partial class NotificationWindow : Window
{
    public bool IsSuccess { get; }
    public string TitleText { get; }
    public string MessageText { get; }

    public NotificationWindow(string videoTitle, bool isSuccess)
    {
        InitializeComponent();
        
        IsSuccess = isSuccess;
        TitleText = isSuccess ? "Download Complete" : "Download Failed";
        MessageText = videoTitle;
        
        DataContext = this;
        
        Loaded += NotificationWindow_Loaded;
    }

    private void NotificationWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Position window in the bottom-right corner of screen
        var workingArea = SystemParameters.WorkArea;
        Left = workingArea.Right - Width - 16;
        
        // Start from below screen bounds
        Top = workingArea.Bottom;
        
        // Slide up animation
        var slideUp = new DoubleAnimation
        {
            From = workingArea.Bottom,
            To = workingArea.Bottom - Height - 16,
            Duration = TimeSpan.FromMilliseconds(400),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        
        BeginAnimation(TopProperty, slideUp);

        // Auto close timer
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        timer.Tick += (s, ev) =>
        {
            timer.Stop();
            SlideDownAndClose();
        };
        timer.Start();
    }

    private void SlideDownAndClose()
    {
        var workingArea = SystemParameters.WorkArea;
        var slideDown = new DoubleAnimation
        {
            From = Top,
            To = workingArea.Bottom,
            Duration = TimeSpan.FromMilliseconds(400),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        
        slideDown.Completed += (s, e) => Close();
        BeginAnimation(TopProperty, slideDown);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        SlideDownAndClose();
    }
}
// Note: In C#, if we need Windows Taskbar and animations, using DoubleAnimation on Top property is standard.
