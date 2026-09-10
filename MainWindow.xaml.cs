using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace DMS.Desktop;

public partial class MainWindow : Window
{
    private readonly string _dataFolder;
    private readonly string _documentsFolder;
    private readonly string _usersFile;
    private readonly string _documentsFile;
    private readonly string _repliesFile;

    private List<UserRecord> _users = new();
    private List<DocumentRecord> _documents = new();
    private List<ReplyRecord> _replies = new();

    private string? _selectedIndexFile;
    private string? _selectedUploadFile;
    private string? _selectedReplyAttachment;
    private DocumentRecord? _selectedDocument;

    public MainWindow()
    {
        InitializeComponent();

        _dataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DMS.Desktop");
        _documentsFolder = Path.Combine(_dataFolder, "Documents");
        _usersFile = Path.Combine(_dataFolder, "users.json");
        _documentsFile = Path.Combine(_dataFolder, "documents.json");
        _repliesFile = Path.Combine(_dataFolder, "replies.json");

        Directory.CreateDirectory(_dataFolder);
        Directory.CreateDirectory(_documentsFolder);

        LoadData();
        EnsureAdminUser();
        RefreshAll();
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private void LoadData()
    {
        _users = LoadJson<List<UserRecord>>(_usersFile) ?? new();
        _documents = LoadJson<List<DocumentRecord>>(_documentsFile) ?? new();
        _replies = LoadJson<List<ReplyRecord>>(_repliesFile) ?? new();
    }

    private static T? LoadJson<T>(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions)
                : default;
        }
        catch
        {
            return default;
        }
    }

    private static void SaveJson<T>(string path, T data)
        => File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOptions));

    private void SaveData()
    {
        SaveJson(_usersFile, _users);
        SaveJson(_documentsFile, _documents);
        SaveJson(_repliesFile, _replies);
    }

    private void EnsureAdminUser()
    {
        if (_users.Any(x => x.UserId.Equals("admin", StringComparison.OrdinalIgnoreCase)))
            return;

        _users.Add(new UserRecord(
            "Administrator", "ADMIN", "admin", "password",
            new List<string> { "Create User", "Delete User", "Search", "Index", "Upload", "Excel", "Edit", "Delete" }));
        SaveJson(_usersFile, _users);
    }

    private void RefreshAll()
    {
        DocumentsGrid.ItemsSource = null;
        DocumentsGrid.ItemsSource = _documents.ToList();

        DeleteUserCombo.ItemsSource = null;
        DeleteUserCombo.ItemsSource = _users
            .Where(x => !x.UserId.Equals("admin", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.UserId)
            .ToList();

        StatusText.Text = $"DMS  |  Administrator: admin  |  {_documents.Count} document(s) | Ready";
    }

    private void ShowOnly(UIElement panel, string title)
    {
        HomePanel.Visibility = Visibility.Collapsed;
        SearchPanel.Visibility = Visibility.Collapsed;
        CreateUserPanel.Visibility = Visibility.Collapsed;
        DeleteUserPanel.Visibility = Visibility.Collapsed;
        IndexPanel.Visibility = Visibility.Collapsed;
        UploadPanel.Visibility = Visibility.Collapsed;
        ExcelPanel.Visibility = Visibility.Collapsed;
        EditPanel.Visibility = Visibility.Collapsed;
        ReplyPanel.Visibility = Visibility.Collapsed;

        panel.Visibility = Visibility.Visible;
        SectionTitle.Text = title;
    }

    private void CreateUser_Click(object sender, RoutedEventArgs e)
        => ShowOnly(CreateUserPanel, "Create Account");

    private void DeleteUser_Click(object sender, RoutedEventArgs e)
    {
        RefreshAll();
        ShowOnly(DeleteUserPanel, "Delete User");
    }

    private void Search_Click(object sender, RoutedEventArgs e)
    {
        ShowOnly(SearchPanel, "Search");
        ApplySearch();
    }

    private void Index_Click(object sender, RoutedEventArgs e)
        => ShowOnly(IndexPanel, "Document Index");

    private void Upload_Click(object sender, RoutedEventArgs e)
        => ShowOnly(UploadPanel, "Upload Document");

    private void Excel_Click(object sender, RoutedEventArgs e)
        => ShowOnly(ExcelPanel, "Excel Report");

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedDocument is null)
        {
            MessageBox.Show("Open Search and select a document first.", "DMS",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        EditDocumentNoBox.Text = _selectedDocument.DocumentNo;
        EditProjectCodeBox.Text = _selectedDocument.ProjectCode;
        EditUnitBox.Text = _selectedDocument.Unit;
        EditYearBox.Text = _selectedDocument.Year;
        EditMonthBox.Text = _selectedDocument.Month;
        EditVoucherBox.Text = _selectedDocument.VoucherType;
        EditDescriptionBox.Text = _selectedDocument.Description;

        ShowOnly(EditPanel, "Edit Document");
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedDocument is null)
        {
            MessageBox.Show("Select a document from Search first.", "DMS",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"Delete document {_selectedDocument.DocumentNo}?\n\nThis Phase 1 version removes the record and stored file.",
            "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        if (!string.IsNullOrWhiteSpace(_selectedDocument.FilePath) && File.Exists(_selectedDocument.FilePath))
            File.Delete(_selectedDocument.FilePath);

        _documents.RemoveAll(x => x.DocumentNo == _selectedDocument.DocumentNo);
        _selectedDocument = null;
        SaveData();
        RefreshAll();
        ShowOnly(SearchPanel, "Search");
        ApplySearch();
    }

    private void SearchExecute_Click(object sender, RoutedEventArgs e) => ApplySearch();

    private void ApplySearch()
    {
        var project = SearchProjectCodeBox.Text.Trim();
        var unit = SelectedText(SearchUnitCombo);
        var year = SelectedText(SearchYearCombo);
        var month = SelectedText(SearchMonthCombo);
        var voucher = SelectedText(SearchVoucherCombo);
        var description = SearchDescriptionBox.Text.Trim();

        var results = _documents.Where(d =>
            Contains(d.ProjectCode, project) &&
            Matches(unit, d.Unit) &&
            Matches(year, d.Year) &&
            Matches(month, d.Month) &&
            Matches(voucher, d.VoucherType) &&
            (string.IsNullOrWhiteSpace(description) ||
             Contains(d.Description, description) ||
             Contains(d.DocumentNo, description)))
            .ToList();

        DocumentsGrid.ItemsSource = null;
        DocumentsGrid.ItemsSource = results;
        StatusText.Text = $"DMS  |  Search completed | {results.Count} result(s)";
    }

    private static bool Contains(string source, string search)
        => string.IsNullOrWhiteSpace(search) ||
           source.Contains(search, StringComparison.OrdinalIgnoreCase);

    private static bool Matches(string selected, string value)
        => string.IsNullOrWhiteSpace(selected) || selected == "--All--" || selected == value;

    private static string SelectedText(ComboBox box)
        => (box.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

    private void SearchClear_Click(object sender, RoutedEventArgs e)
    {
        SearchProjectCodeBox.Clear();
        SearchDescriptionBox.Clear();
        SearchUnitCombo.SelectedIndex = 0;
        SearchYearCombo.SelectedIndex = 0;
        SearchMonthCombo.SelectedIndex = 0;
        SearchVoucherCombo.SelectedIndex = 0;
        ApplySearch();
    }

    private void DocumentsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedDocument = DocumentsGrid.SelectedItem as DocumentRecord;
        if (_selectedDocument is not null)
            StatusText.Text = $"DMS  |  Selected: {_selectedDocument.DocumentNo}";
    }

    private void CreateUserSave_Click(object sender, RoutedEventArgs e)
    {
        var name = UserNameBox.Text.Trim();
        var employeeId = EmployeeIdBox.Text.Trim();
        var userId = UserIdBox.Text.Trim();
        var password = UserPasswordBox.Password;

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(employeeId) ||
            string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(password))
        {
            MessageBox.Show("Name, Employee ID, User ID and Password are required.",
                "DMS", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_users.Any(x => x.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("This User ID already exists.", "DMS",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var permissions = new List<(CheckBox box, string name)>
        {
            (PermissionCreate, "Create User"),
            (PermissionDeleteUser, "Delete User"),
            (PermissionSearch, "Search"),
            (PermissionIndex, "Index"),
            (PermissionUpload, "Upload"),
            (PermissionExcel, "Excel"),
            (PermissionEdit, "Edit"),
            (PermissionDelete, "Delete")
        };

        _users.Add(new UserRecord(
            name, employeeId, userId, password,
            permissions.Where(x => x.box.IsChecked == true).Select(x => x.name).ToList()));

        SaveData();
        RefreshAll();
        CreateUserClear_Click(sender, e);

        MessageBox.Show($"User '{userId}' created successfully.", "DMS",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CreateUserClear_Click(object sender, RoutedEventArgs e)
    {
        UserNameBox.Clear();
        EmployeeIdBox.Clear();
        UserIdBox.Clear();
        UserPasswordBox.Clear();

        PermissionCreate.IsChecked = false;
        PermissionDeleteUser.IsChecked = false;
        PermissionSearch.IsChecked = true;
        PermissionIndex.IsChecked = false;
        PermissionUpload.IsChecked = false;
        PermissionExcel.IsChecked = false;
        PermissionEdit.IsChecked = false;
        PermissionDelete.IsChecked = false;
    }

    private void DeleteUserConfirm_Click(object sender, RoutedEventArgs e)
    {
        var userId = DeleteUserCombo.SelectedItem?.ToString();

        if (string.IsNullOrWhiteSpace(userId))
        {
            MessageBox.Show("Select a user to delete.", "DMS",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show($"Delete user '{userId}'?", "Confirm Delete",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        _users.RemoveAll(x => x.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase));
        SaveJson(_usersFile, _users);
        RefreshAll();

        MessageBox.Show("User deleted successfully.", "DMS",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void DeleteUserClear_Click(object sender, RoutedEventArgs e)
        => DeleteUserCombo.SelectedIndex = -1;

    private void IndexChooseFile_Click(object sender, RoutedEventArgs e)
    {
        var path = ChooseDocumentFile();
        if (path is null) return;

        _selectedIndexFile = path;
        IndexFileBox.Text = path;
    }

    private void IndexSave_Click(object sender, RoutedEventArgs e)
    {
        var documentNo = IndexDocumentNoBox.Text.Trim();
        var projectCode = IndexProjectCodeBox.Text.Trim();
        var unit = SelectedText(IndexUnitCombo);
        var year = SelectedText(IndexYearCombo);
        var month = SelectedText(IndexMonthCombo);
        var voucher = SelectedText(IndexVoucherCombo);
        var description = IndexDescriptionBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(documentNo) || string.IsNullOrWhiteSpace(projectCode) ||
            string.IsNullOrWhiteSpace(unit) || string.IsNullOrWhiteSpace(year) ||
            string.IsNullOrWhiteSpace(month) || string.IsNullOrWhiteSpace(voucher) ||
            string.IsNullOrWhiteSpace(description))
        {
            MessageBox.Show("Complete all document metadata fields.", "DMS",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_documents.Any(x => x.DocumentNo.Equals(documentNo, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("Document No. already exists.", "DMS",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var nextNo = _documents.Count == 0 ? 1 : _documents.Max(x => x.SlNo) + 1;
        var record = new DocumentRecord(
            nextNo, documentNo, projectCode, unit, year, month, voucher, description, "", DateTime.Now);

        if (!string.IsNullOrWhiteSpace(_selectedIndexFile))
            record = record with { FilePath = CopyDocument(_selectedIndexFile, documentNo) };

        _documents.Add(record);
        SaveJson(_documentsFile, _documents);
        RefreshAll();
        IndexClear_Click(sender, e);

        MessageBox.Show($"Document {documentNo} indexed successfully.", "DMS",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void IndexClear_Click(object sender, RoutedEventArgs e)
    {
        IndexDocumentNoBox.Clear();
        IndexProjectCodeBox.Clear();
        IndexDescriptionBox.Clear();
        IndexUnitCombo.SelectedIndex = -1;
        IndexYearCombo.SelectedIndex = -1;
        IndexMonthCombo.SelectedIndex = -1;
        IndexVoucherCombo.SelectedIndex = -1;
        IndexFileBox.Clear();
        _selectedIndexFile = null;
    }

    private void UploadChooseFile_Click(object sender, RoutedEventArgs e)
    {
        var path = ChooseDocumentFile();
        if (path is null) return;

        _selectedUploadFile = path;
        UploadFileBox.Text = path;
    }

    private void UploadSave_Click(object sender, RoutedEventArgs e)
    {
        var documentNo = UploadDocumentNoBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(documentNo))
        {
            MessageBox.Show("Enter the Document No.", "DMS",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var document = _documents.FirstOrDefault(x =>
            x.DocumentNo.Equals(documentNo, StringComparison.OrdinalIgnoreCase));

        if (document is null)
        {
            MessageBox.Show("Document No. is not indexed. Index the document first.",
                "DMS", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedUploadFile))
        {
            MessageBox.Show("Choose a file to upload.", "DMS",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!string.IsNullOrWhiteSpace(document.FilePath) && File.Exists(document.FilePath))
            File.Delete(document.FilePath);

        var newPath = CopyDocument(_selectedUploadFile, documentNo);
        var index = _documents.FindIndex(x => x.DocumentNo == document.DocumentNo);
        _documents[index] = document with { FilePath = newPath };
        SaveJson(_documentsFile, _documents);

        UploadClear_Click(sender, e);
        RefreshAll();

        MessageBox.Show("Document uploaded successfully.", "DMS",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void UploadClear_Click(object sender, RoutedEventArgs e)
    {
        UploadDocumentNoBox.Clear();
        UploadFileBox.Clear();
        _selectedUploadFile = null;
    }

    private string CopyDocument(string source, string documentNo)
    {
        var extension = Path.GetExtension(source);
        var destination = Path.Combine(_documentsFolder, documentNo + extension);
        File.Copy(source, destination, true);
        return destination;
    }

    private static string? ChooseDocumentFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Document",
            Filter = "Documents|*.pdf;*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.doc;*.docx;*.xls;*.xlsx|All Files|*.*",
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private void View_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is DocumentRecord document)
            OpenDocument(document);
    }

    private void OpenDocument(DocumentRecord document)
    {
        if (string.IsNullOrWhiteSpace(document.FilePath) || !File.Exists(document.FilePath))
        {
            MessageBox.Show(
                $"No file is attached to {document.DocumentNo}.\n\nDescription: {document.Description}",
                "Document View", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(document.FilePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to open the document.\n{ex.Message}", "DMS",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Reply_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not DocumentRecord document)
            return;

        _selectedDocument = document;
        ReplyDocumentNoBox.Text = document.DocumentNo;
        ReplyRequestNoBox.Text = "REQ-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        ReplyRequestDateBox.Text = DateTime.Now.ToString("dd/MM/yyyy");
        ReplySubjectBox.Text = "Reply regarding " + document.Description;
        ShowOnly(ReplyPanel, "Reply to Document Request");
    }

    private void ReplyChooseFile_Click(object sender, RoutedEventArgs e)
    {
        var path = ChooseDocumentFile();
        if (path is null) return;

        _selectedReplyAttachment = path;
        ReplyAttachmentBox.Text = path;
    }

    private void ReplyClear_Click(object sender, RoutedEventArgs e)
    {
        ReplyToBox.Clear();
        ReplyAddressBox.Clear();
        ReplySubjectBox.Clear();
        ReplyReferenceBox.Clear();
        ReplyTextBox.Clear();
        ReplyAttachmentBox.Clear();
        _selectedReplyAttachment = null;
    }

    private void ReplySubmit_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ReplyToBox.Text) ||
            string.IsNullOrWhiteSpace(ReplyTextBox.Text))
        {
            MessageBox.Show("To and Reply are required.", "DMS",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var replyId = "REP-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff");
        var attachmentPath = "";

        if (!string.IsNullOrWhiteSpace(_selectedReplyAttachment))
        {
            attachmentPath = CopyReplyAttachment(_selectedReplyAttachment, replyId);
        }

        _replies.Add(new ReplyRecord(
            replyId,
            ReplyRequestNoBox.Text.Trim(),
            ReplyRequestDateBox.Text.Trim(),
            ReplyDocumentNoBox.Text.Trim(),
            ReplyToBox.Text.Trim(),
            ReplyAddressBox.Text.Trim(),
            ReplySubjectBox.Text.Trim(),
            ReplyReferenceBox.Text.Trim(),
            ReplyTextBox.Text.Trim(),
            attachmentPath,
            DateTime.Now));

        SaveJson(_repliesFile, _replies);
        MessageBox.Show($"Reply saved successfully.\nReply No.: {replyId}", "DMS",
            MessageBoxButton.OK, MessageBoxImage.Information);
        ReplyClear_Click(sender, e);
    }

    private string CopyReplyAttachment(string source, string replyId)
    {
        var repliesFolder = Path.Combine(_dataFolder, "Replies");
        Directory.CreateDirectory(repliesFolder);
        var destination = Path.Combine(repliesFolder, replyId + Path.GetExtension(source));
        File.Copy(source, destination, true);
        return destination;
    }

    private void ReplyPrint_Click(object sender, RoutedEventArgs e)
        => PrintReply();

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is DocumentRecord document)
        {
            PrintText($"DOCUMENT MANAGEMENT SYSTEM\n\nDocument No.: {document.DocumentNo}\n" +
                      $"Project Code: {document.ProjectCode}\nUnit: {document.Unit}\nYear: {document.Year}\n" +
                      $"Month: {document.Month}\nVoucher Type: {document.VoucherType}\n" +
                      $"Description: {document.Description}");
        }
    }

    private void PrintReply()
    {
        var text = $"DOCUMENT MANAGEMENT SYSTEM\n\n" +
                   $"Request No.: {ReplyRequestNoBox.Text}\n" +
                   $"Request Date: {ReplyRequestDateBox.Text}\n" +
                   $"Document No.: {ReplyDocumentNoBox.Text}\n" +
                   $"To: {ReplyToBox.Text}\n" +
                   $"Address: {ReplyAddressBox.Text}\n" +
                   $"Subject: {ReplySubjectBox.Text}\n" +
                   $"Reference: {ReplyReferenceBox.Text}\n\n" +
                   $"Reply:\n{ReplyTextBox.Text}";
        PrintText(text);
    }

    private static void PrintText(string text)
    {
        var dialog = new System.Windows.Controls.PrintDialog();
        if (dialog.ShowDialog() != true) return;

        var visual = new TextBlock
        {
            Text = text,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(50)
        };
        dialog.PrintVisual(visual, "DMS Print");
    }

    private void EditSave_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedDocument is null) return;

        var index = _documents.FindIndex(x => x.DocumentNo == _selectedDocument.DocumentNo);
        if (index < 0) return;

        _documents[index] = _selectedDocument with
        {
            ProjectCode = EditProjectCodeBox.Text.Trim(),
            Unit = EditUnitBox.Text.Trim(),
            Year = EditYearBox.Text.Trim(),
            Month = EditMonthBox.Text.Trim(),
            VoucherType = EditVoucherBox.Text.Trim(),
            Description = EditDescriptionBox.Text.Trim()
        };

        _selectedDocument = _documents[index];
        SaveJson(_documentsFile, _documents);
        RefreshAll();

        MessageBox.Show("Document updated successfully.", "DMS",
            MessageBoxButton.OK, MessageBoxImage.Information);
        ShowOnly(SearchPanel, "Search");
        ApplySearch();
    }

    private void EditClear_Click(object sender, RoutedEventArgs e)
    {
        EditProjectCodeBox.Clear();
        EditUnitBox.Clear();
        EditYearBox.Clear();
        EditMonthBox.Clear();
        EditVoucherBox.Clear();
        EditDescriptionBox.Clear();
    }

    private void ExcelGenerate_Click(object sender, RoutedEventArgs e)
    {
        var fileName = string.IsNullOrWhiteSpace(ExcelFileNameBox.Text)
            ? "DMS_Report"
            : ExcelFileNameBox.Text.Trim();

        var dialog = new SaveFileDialog
        {
            FileName = fileName + ".csv",
            Filter = "CSV file|*.csv",
            DefaultExt = ".csv"
        };

        if (dialog.ShowDialog() != true) return;

        var rows = DocumentsGrid.ItemsSource as IEnumerable<DocumentRecord> ?? _documents;
        var sb = new StringBuilder();
        sb.AppendLine("Sl.No,Document No.,Project Code,Unit,Year,Month,Voucher Type,Description,File");

        var i = 1;
        foreach (var d in rows)
        {
            sb.AppendLine(string.Join(",",
                i++,
                Csv(d.DocumentNo),
                Csv(d.ProjectCode),
                Csv(d.Unit),
                Csv(d.Year),
                Csv(d.Month),
                Csv(d.VoucherType),
                Csv(d.Description),
                Csv(d.FilePath)));
        }

        File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
        MessageBox.Show("Report generated successfully.", "DMS",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static string Csv(string value)
        => "\"" + value.Replace("\"", "\"\"") + "\"";

    private void ExcelClear_Click(object sender, RoutedEventArgs e)
        => ExcelFileNameBox.Text = "DMS_Report";

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || LanguageCombo.SelectedIndex < 0) return;
        if (LanguageCombo.SelectedIndex == 1)
            MessageBox.Show("Kannada UI translation will be expanded in the next localization phase.",
                "DMS", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Do you want to logout?", "DMS",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var login = new LoginWindow();
        Application.Current.MainWindow = login;
        login.Show();
        Close();
    }
}

public record UserRecord(
    string Name,
    string EmployeeId,
    string UserId,
    string Password,
    List<string> Permissions);

public record DocumentRecord(
    int SlNo,
    string DocumentNo,
    string ProjectCode,
    string Unit,
    string Year,
    string Month,
    string VoucherType,
    string Description,
    string FilePath,
    DateTime CreatedAt);

public record ReplyRecord(
    string ReplyNo,
    string RequestNo,
    string RequestDate,
    string DocumentNo,
    string To,
    string Address,
    string Subject,
    string Reference,
    string ReplyText,
    string AttachmentPath,
    DateTime CreatedAt);
