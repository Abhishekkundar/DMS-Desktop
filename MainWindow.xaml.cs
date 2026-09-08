using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DMS.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        LoadSampleRows();
    }

    private void LoadSampleRows()
    {
        var rows = new List<DocumentRow>
        {
            new(1, "PRJ-001", "Finance", "2026", "September", "Invoice", "ABC Traders Invoice"),
            new(2, "PRJ-002", "Works", "2026", "August", "Contract", "Road Work Agreement"),
            new(3, "PRJ-003", "Administration", "2026", "July", "Letter", "Office Order"),
            new(4, "PRJ-004", "Education", "2026", "September", "Report", "Monthly Report")
        };

        var grid = FindVisualChild<DataGrid>(SearchPanel);
        if (grid != null)
            grid.ItemsSource = rows;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
                return result;

            var nested = FindVisualChild<T>(child);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private void ShowOnly(UIElement panel, string title)
    {
        HomePanel.Visibility = Visibility.Collapsed;
        SearchPanel.Visibility = Visibility.Collapsed;
        ReplyPanel.Visibility = Visibility.Collapsed;
        CreateUserPanel.Visibility = Visibility.Collapsed;

        panel.Visibility = Visibility.Visible;
        SectionTitle.Text = title;
    }

    private void CreateUser_Click(object sender, RoutedEventArgs e)
        => ShowOnly(CreateUserPanel, "Create Account");

    private void Search_Click(object sender, RoutedEventArgs e)
        => ShowOnly(SearchPanel, "Search");

    private void Reply_Click(object sender, RoutedEventArgs e)
        => ShowOnly(ReplyPanel, "Reply to Document Request");

    private void View_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Document preview will open here.", "Document Preview",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Controls.PrintDialog();
        if (dialog.ShowDialog() == true)
        {
            var visual = new TextBlock
            {
                Text = "DOCUMENT MANAGEMENT SYSTEM\n\nReply / Document",
                FontSize = 18,
                Margin = new Thickness(60)
            };
            dialog.PrintVisual(visual, "DMS Document");
        }
    }

    private void ReplyClear_Click(object sender, RoutedEventArgs e)
    {
        ReplyToBox.Clear();
        ReplyTextBox.Clear();
    }

    private void ReplySubmit_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ReplyToBox.Text))
        {
            MessageBox.Show("Please enter the requesting person's name.",
                "DMS", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(ReplyTextBox.Text))
        {
            MessageBox.Show("Please enter the reply.",
                "DMS", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show("Reply prepared successfully.",
            "DMS", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Do you want to logout?", "DMS",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            var login = new LoginWindow();
            Application.Current.MainWindow = login;
            login.Show();
            Close();
        }
    }
}

public record DocumentRow(
    int SlNo,
    string ProjectCode,
    string Unit,
    string Year,
    string Month,
    string VoucherType,
    string Description);
