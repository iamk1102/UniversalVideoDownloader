using System;
using System.Windows;
using System.Windows.Controls;
using UniversalVideoDownloader.Models;

namespace UniversalVideoDownloader;

public partial class LogWindow : Window
{
    public LogWindow(QueueItem item)
    {
        InitializeComponent();
        DataContext = item;
    }

    private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.ScrollToEnd();
        }
    }

    private void CopyLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is QueueItem item)
            {
                Clipboard.SetText(item.LogContentText);
                MessageBox.Show("Logs copied to clipboard!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to copy logs. Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
