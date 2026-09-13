using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using DMS.Desktop.Data;

namespace DMS.Desktop;

public partial class MainWindow : Window
{
    private readonly UserRecord _currentUser;

    private readonly string _dataFolder;
    private readonly string _documentsFolder;
    private readonly string _uploadedFolder;
    private readonly string _documentsFile;
    private readonly string _repliesFile;

    private readonly UserStore _userStore;

    private List<DocumentRecord> _documents = new();
    private List<ReplyRecord> _replies = new();

    private string? _selectedIndexFile;
    private string? _selectedUploadFile;
    private string? _selectedReplyAttachment;
    private DocumentRecord? _selectedDocument;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public MainWindow(UserRecord currentUser)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        _currentUser = currentUser;

        InitializeComponent();

        _dataFolder = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "DMS.Desktop");

        _documentsFolder = Path.Combine(
            _dataFolder,
            "Documents");

        _uploadedFolder = Path.Combine(
            _dataFolder,
            "UploadedDocuments");

        _documentsFile = Path.Combine(
            _dataFolder,
            "documents.json");

        _repliesFile = Path.Combine(
            _dataFolder,
            "replies.json");

        _userStore = new UserStore();

        Directory.CreateDirectory(_dataFolder);
        Directory.CreateDirectory(_documentsFolder);
        Directory.CreateDirectory(_uploadedFolder);

        LoadData();
        ApplyPermissions();
        RefreshAll();
    }

    #region Data Management

    private void LoadData()
    {
        _documents =
            LoadJson<List<DocumentRecord>>(_documentsFile)
            ?? new List<DocumentRecord>();

        _replies =
            LoadJson<List<ReplyRecord>>(_repliesFile)
            ?? new List<ReplyRecord>();
    }

    private static T? LoadJson<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        try
        {
            var json = File.ReadAllText(path);

            if (string.IsNullOrWhiteSpace(json))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(
                json,
                JsonOptions);
        }
        catch (JsonException)
        {
            MessageBox.Show(
                $"The data file could not be read:\n\n{Path.GetFileName(path)}",
                "DMS Data Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return default;
        }
        catch (IOException)
        {
            MessageBox.Show(
                $"The data file could not be accessed:\n\n{Path.GetFileName(path)}",
                "DMS Data Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return default;
        }
    }

    private static void SaveJson<T>(string path, T data)
    {
        var json = JsonSerializer.Serialize(
            data,
            JsonOptions);

        File.WriteAllText(
            path,
            json,
            Encoding.UTF8);
    }

    private void SaveData()
    {
        SaveJson(_documentsFile, _documents);
        SaveJson(_repliesFile, _replies);
    }

    private void RefreshAll()
    {
        ApplyPermissions();

        CurrentUserText.Text =$"{_currentUser.FullName} ({_currentUser.UserId})";
        DocumentsGrid.ItemsSource = null;
        DocumentsGrid.ItemsSource = _documents.ToList();

        DeleteUserCombo.ItemsSource = null;
        DeleteUserCombo.ItemsSource = _userStore
            .GetUsers()
            .Where(user =>
                !string.Equals(
                    user.UserId,
                    "admin",
                    StringComparison.OrdinalIgnoreCase))
            .Select(user => user.UserId)
            .OrderBy(userId => userId)
            .ToList();

        StatusText.Text =
    $"DMS  |  User: {_currentUser.FullName} ({_currentUser.UserId}) | " +
    $"{_documents.Count} document(s) | Ready";
    }

    #endregion

    #region Permissions

    private bool RequirePermission(bool allowed, string operation)
    {
        if (allowed)
        {
            return true;
        }

        MessageBox.Show(
            $"You do not have permission to {operation}.",
            "Access Denied",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        return false;
    }

    #endregion

    #region Navigation

    private void ShowOnly(
        UIElement panel,
        string title)
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

    private void CreateUser_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!RequirePermission(_currentUser.CanCreateUser, "create users"))
        {
            return;
        }

        ShowOnly(
            CreateUserPanel,
            "Create Account");
    }

    private void DeleteUser_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!RequirePermission(_currentUser.CanDeleteUser, "delete users"))
        {
            return;
        }

        RefreshAll();

        ShowOnly(
            DeleteUserPanel,
            "Delete User");
    }

    private void Search_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!RequirePermission(_currentUser.CanSearch, "search documents"))
        {
            return;
        }

        ShowOnly(
            SearchPanel,
            "Search");

        ApplySearch();
    }

    private void Index_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!RequirePermission(_currentUser.CanIndex, "index documents"))
        {
            return;
        }

        ShowOnly(
            IndexPanel,
            "Document Index");
    }

    private void Upload_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!RequirePermission(_currentUser.CanUpload, "upload documents"))
        {
            return;
        }

        ShowOnly(
            UploadPanel,
            "Upload Document");
    }

    private void Excel_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!RequirePermission(_currentUser.CanExcel, "generate Excel reports"))
        {
            return;
        }

        ShowOnly(
            ExcelPanel,
            "Excel Report");
    }

    #endregion

    #region User Management

    private void SaveUser_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!RequirePermission(_currentUser.CanCreateUser, "create users"))
        {
            return;
        }

        var name = UserNameBox.Text.Trim();
        var employeeId = EmployeeIdBox.Text.Trim();
        var userId = UserIdBox.Text.Trim();
        var password = UserPasswordBox.Password;

        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(employeeId) ||
            string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrEmpty(password))
        {
            MessageBox.Show(
                "Name, Employee ID, User ID and Password are required.",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        if (userId.Length > 50)
        {
            MessageBox.Show(
                "User ID cannot exceed 50 characters.",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        if (password.Length < 6)
        {
            MessageBox.Show(
                "Password must contain at least 6 characters.",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        if (_userStore.UserExists(userId))
        {
            MessageBox.Show(
                "This User ID already exists.",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        var user = new UserRecord
        {
            UserId = userId,
            Password = password,
            FullName = name,
            EmployeeId = employeeId,
            Role = "User",

            CanCreateUser =
                PermissionCreate.IsChecked == true,

            CanDeleteUser =
                PermissionDeleteUser.IsChecked == true,

            CanSearch =
                PermissionSearch.IsChecked == true,

            CanIndex =
                PermissionIndex.IsChecked == true,

            CanUpload =
                PermissionUpload.IsChecked == true,

            CanExcel =
                PermissionExcel.IsChecked == true,

            CanEdit =
                PermissionEdit.IsChecked == true,

            CanDeleteDocument =
                PermissionDelete.IsChecked == true,

            CanReply =
                PermissionReply.IsChecked == true,

            CanPrint =
                PermissionPrint.IsChecked == true,

            IsActive = true
        };

        if (!_userStore.AddUser(user))
        {
            MessageBox.Show(
                "Unable to create the user.",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return;
        }

        RefreshAll();

        CreateUserClear_Click(
            sender,
            e);

        MessageBox.Show(
            $"User '{userId}' created successfully.",
            "DMS",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void CreateUserClear_Click(
        object sender,
        RoutedEventArgs e)
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
        PermissionReply.IsChecked = false;
        PermissionPrint.IsChecked = false;
    }

    private void DeleteUserConfirm_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!RequirePermission(_currentUser.CanDeleteUser, "delete users"))
        {
            return;
        }
        var userId =
            DeleteUserCombo.SelectedItem?.ToString();

        if (string.IsNullOrWhiteSpace(userId))
        {
            MessageBox.Show(
                "Select a user to delete.",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        if (string.Equals(
                userId,
                "admin",
                StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                "The administrator account cannot be deleted.",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        var confirmation = MessageBox.Show(
            $"Delete user '{userId}'?\n\nThis action cannot be undone.",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        if (!_userStore.DeleteUser(userId))
        {
            MessageBox.Show(
                "Unable to delete the user.",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return;
        }

        RefreshAll();

        DeleteUserCombo.SelectedIndex = -1;

        MessageBox.Show(
            "User deleted successfully.",
            "DMS",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void DeleteUserClear_Click(
        object sender,
        RoutedEventArgs e)
    {
        DeleteUserCombo.SelectedIndex = -1;
    }

    #endregion

    #region Search

    private void SearchExecute_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!RequirePermission(_currentUser.CanSearch, "search documents"))
        {
            return;
        }

        ApplySearch();
    }

    private void ApplySearch()
    {
        var projectCode =
            SearchProjectCodeBox.Text.Trim();

        var unit =
            SelectedText(SearchUnitCombo);

        var year =
            SelectedText(SearchYearCombo);

        var month =
            SelectedText(SearchMonthCombo);

        var voucher =
            SelectedText(SearchVoucherCombo);

        var description =
            SearchDescriptionBox.Text.Trim();

        var results = _documents
            .Where(document =>
                Contains(
                    document.ProjectCode,
                    projectCode) &&

                Matches(
                    unit,
                    document.Unit) &&

                Matches(
                    year,
                    document.Year) &&

                Matches(
                    month,
                    document.Month) &&

                Matches(
                    voucher,
                    document.VoucherType) &&

                (
                    string.IsNullOrWhiteSpace(description) ||
                    Contains(
                        document.Description,
                        description) ||
                    Contains(
                        document.DocumentNo,
                        description)
                ))
            .ToList();

        DocumentsGrid.ItemsSource = null;
        DocumentsGrid.ItemsSource = results;

        StatusText.Text =
            $"DMS  |  Search completed | {results.Count} result(s)";
    }

    private static bool Contains(
        string source,
        string search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return source.Contains(
            search,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool Matches(
        string selected,
        string value)
    {
        return string.IsNullOrWhiteSpace(selected) ||
               selected == "--All--" ||
               string.Equals(
                   selected,
                   value,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string SelectedText(
        ComboBox box)
    {
        return (box.SelectedItem as ComboBoxItem)
                   ?.Content
                   ?.ToString()
               ?? string.Empty;
    }

    private void SearchClear_Click(
        object sender,
        RoutedEventArgs e)
    {
        SearchProjectCodeBox.Clear();
        SearchDescriptionBox.Clear();

        SearchUnitCombo.SelectedIndex = 0;
        SearchYearCombo.SelectedIndex = 0;
        SearchMonthCombo.SelectedIndex = 0;
        SearchVoucherCombo.SelectedIndex = 0;

        ApplySearch();
    }

    private void DocumentsGrid_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        _selectedDocument =
            DocumentsGrid.SelectedItem as DocumentRecord;

        if (_selectedDocument is not null)
        {
            StatusText.Text =
                $"DMS  |  Selected: {_selectedDocument.DocumentNo}";
        }
    }

    #endregion

    // ===== NEW / UPDATED CODE: Upload-first document workflow =====
    // Indexing now selects only files already uploaded to DMS.
    #region Document Index

    private void RefreshUploadedFiles()
    {
        IndexUploadedFileCombo.ItemsSource = null;

        if (!Directory.Exists(_uploadedFolder))
        {
            return;
        }

        var files = Directory
            .GetFiles(_uploadedFolder)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.CreationTime)
            .ToList();

        IndexUploadedFileCombo.ItemsSource = files;
        IndexUploadedFileCombo.DisplayMemberPath = nameof(FileInfo.Name);
    }

    private void IndexUploadedFile_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (IndexUploadedFileCombo.SelectedItem is not FileInfo file)
        {
            _selectedIndexFile = null;
            IndexFileBox.Clear();
            return;
        }

        _selectedIndexFile = file.FullName;
        IndexFileBox.Text = file.Name;
    }

    private void IndexSave_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!RequirePermission(_currentUser.CanIndex, "index documents"))
        {
            return;
        }

        var documentNo =
            IndexDocumentNoBox.Text.Trim();

        var projectCode =
            IndexProjectCodeBox.Text.Trim();

        var unit =
            SelectedText(IndexUnitCombo);

        var year =
            SelectedText(IndexYearCombo);

        var month =
            SelectedText(IndexMonthCombo);

        var voucher =
            SelectedText(IndexVoucherCombo);

        var description =
            IndexDescriptionBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(documentNo) ||
            string.IsNullOrWhiteSpace(projectCode) ||
            string.IsNullOrWhiteSpace(unit) ||
            string.IsNullOrWhiteSpace(year) ||
            string.IsNullOrWhiteSpace(month) ||
            string.IsNullOrWhiteSpace(voucher) ||
            string.IsNullOrWhiteSpace(description))
        {
            MessageBox.Show(
                "Complete all document metadata fields.",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedIndexFile) ||
            !File.Exists(_selectedIndexFile))
        {
            MessageBox.Show(
                "Select an uploaded document to index.",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        if (_documents.Any(document =>
                string.Equals(
                    document.DocumentNo,
                    documentNo,
                    StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(
                "Document No. already exists.",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        var nextSlNo =
            _documents.Count == 0
                ? 1
                : _documents.Max(
                    document => document.SlNo) + 1;

        try
        {
            var storedPath = CopyDocument(
                _selectedIndexFile,
                documentNo);

            var record = new DocumentRecord(
                nextSlNo,
                documentNo,
                projectCode,
                unit,
                year,
                month,
                voucher,
                description,
                storedPath,
                DateTime.Now);

            _documents.Add(record);

            SaveJson(
                _documentsFile,
                _documents);

            File.Delete(_selectedIndexFile);

            IndexClear_Click(
                sender,
                e);

            RefreshUploadedFiles();
            RefreshAll();

            MessageBox.Show(
                $"Document {documentNo} indexed successfully.",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (IOException ex)
        {
            MessageBox.Show(
                $"Unable to index the uploaded document.\n\n{ex.Message}",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void IndexClear_Click(
        object sender,
        RoutedEventArgs e)
    {
        IndexDocumentNoBox.Clear();
        IndexProjectCodeBox.Clear();
        IndexDescriptionBox.Clear();

        IndexUnitCombo.SelectedIndex = -1;
        IndexYearCombo.SelectedIndex = -1;
        IndexMonthCombo.SelectedIndex = -1;
        IndexVoucherCombo.SelectedIndex = -1;
        IndexUploadedFileCombo.SelectedIndex = -1;

        IndexFileBox.Clear();

        _selectedIndexFile = null;
    }

    #endregion


    // ===== NEW / UPDATED CODE: Upload-first document workflow =====
    // Uploading stores the file in UploadedDocuments before indexing.
    #region Document Upload

    private void UploadChooseFile_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!RequirePermission(_currentUser.CanUpload, "upload documents"))
        {
            return;
        }

        var path = ChooseDocumentFile();

        if (path is null)
        {
            return;
        }

        _selectedUploadFile = path;
        UploadFileBox.Text = path;
    }

    private void UploadSave_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!RequirePermission(_currentUser.CanUpload, "upload documents"))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedUploadFile))
        {
            MessageBox.Show(
                "Choose a document to upload.",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        try
        {
            var uploadedPath = CopyToUploadedDocuments(
                _selectedUploadFile);

            var fileName = Path.GetFileName(uploadedPath);

            UploadClear_Click(
                sender,
                e);

            RefreshUploadedFiles();
            RefreshAll();

            MessageBox.Show(
                $"Document uploaded successfully.\n\nFile: {fileName}\n\nThe document is now available in Document Index for metadata entry.",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (IOException ex)
        {
            MessageBox.Show(
                $"Unable to upload the document.\n\n{ex.Message}",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void UploadClear_Click(
        object sender,
        RoutedEventArgs e)
    {
        UploadFileBox.Clear();
        _selectedUploadFile = null;
    }

    private string CopyToUploadedDocuments(
        string source)
    {
        if (!File.Exists(source))
        {
            throw new FileNotFoundException(
                "The selected document could not be found.",
                source);
        }

        Directory.CreateDirectory(_uploadedFolder);

        var originalName =
            Path.GetFileName(source);

        var extension =
            Path.GetExtension(originalName);

        var baseName =
            Path.GetFileNameWithoutExtension(originalName);

        var safeBaseName =
            SanitizeFileName(baseName);

        var destination =
            Path.Combine(
                _uploadedFolder,
                safeBaseName + extension);

        var counter = 1;

        while (File.Exists(destination))
        {
            destination = Path.Combine(
                _uploadedFolder,
                $"{safeBaseName} ({counter}){extension}");

            counter++;
        }

        File.Copy(
            source,
            destination);

        return destination;
    }

    private string CopyDocument(
        string source,
        string documentNo)
    {
        if (!File.Exists(source))
        {
            throw new FileNotFoundException(
                "The selected document could not be found.",
                source);
        }

        var extension =
            Path.GetExtension(source);

        var safeFileName =
            SanitizeFileName(documentNo);

        var destination =
            Path.Combine(
                _documentsFolder,
                safeFileName + extension);

        File.Copy(
            source,
            destination,
            true);

        return destination;
    }

    private static string SanitizeFileName(
        string fileName)
    {
        var invalidCharacters =
            Path.GetInvalidFileNameChars();

        var builder = new StringBuilder(
            fileName.Length);

        foreach (var character in fileName)
        {
            builder.Append(
                invalidCharacters.Contains(character)
                    ? '_'
                    : character);
        }

        return builder.ToString();
    }

    // ===== UPDATED: Used only by Upload. Index no longer opens a local file dialog. =====
    private static string? ChooseDocumentFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Document to Upload",
            Filter =
                "Documents|*.pdf;*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.doc;*.docx;*.xls;*.xlsx|" +
                "All Files|*.*",
            Multiselect = false,
            CheckFileExists = true,
            CheckPathExists = true
        };

        return dialog.ShowDialog() == true
            ? dialog.FileName
            : null;
    }

    #endregion


    #region Document View

    private void View_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!RequirePermission(_currentUser.CanSearch, "view documents"))
        {
            return;
        }
        if (sender is Button button &&
            button.DataContext is DocumentRecord document)
        {
            OpenDocument(document);
        }
    }

    private void OpenDocument(
        DocumentRecord document)
    {
        if (string.IsNullOrWhiteSpace(
                document.FilePath) ||
            !File.Exists(document.FilePath))
        {
            MessageBox.Show(
                $"No file is attached to {document.DocumentNo}.\n\n" +
                $"Description: {document.Description}",
                "Document View",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = document.FilePath,
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to open the document.\n\n{ex.Message}",
                "Document View",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    #endregion

    #region Edit Document

    private void Edit_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!RequirePermission(_currentUser.CanEdit, "edit documents"))
        {
            return;
        }
        if (_selectedDocument is null)
        {
            MessageBox.Show(
                "Open Search and select a document first.",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        EditDocumentNoBox.Text =
            _selectedDocument.DocumentNo;

        EditProjectCodeBox.Text =
            _selectedDocument.ProjectCode;

        EditUnitBox.Text =
            _selectedDocument.Unit;

        EditYearBox.Text =
            _selectedDocument.Year;

        EditMonthBox.Text =
            _selectedDocument.Month;

        EditVoucherBox.Text =
            _selectedDocument.VoucherType;

        EditDescriptionBox.Text =
            _selectedDocument.Description;

        ShowOnly(
            EditPanel,
            "Edit Document");
    }

    private void EditSave_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!RequirePermission(_currentUser.CanEdit, "edit documents"))
        {
            return;
        }
        if (_selectedDocument is null)
        {
            return;
        }

        var index =
            _documents.FindIndex(document =>
                string.Equals(
                    document.DocumentNo,
                    _selectedDocument.DocumentNo,
                    StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            MessageBox.Show(
                "The selected document could not be found.",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        var projectCode =
            EditProjectCodeBox.Text.Trim();

        var unit =
            EditUnitBox.Text.Trim();

        var year =
            EditYearBox.Text.Trim();

        var month =
            EditMonthBox.Text.Trim();

        var voucher =
            EditVoucherBox.Text.Trim();

        var description =
            EditDescriptionBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(projectCode) ||
            string.IsNullOrWhiteSpace(unit) ||
            string.IsNullOrWhiteSpace(year) ||
            string.IsNullOrWhiteSpace(month) ||
            string.IsNullOrWhiteSpace(voucher) ||
            string.IsNullOrWhiteSpace(description))
        {
            MessageBox.Show(
                "Complete all document metadata fields.",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        _documents[index] =
            _documents[index] with
            {
                ProjectCode = projectCode,
                Unit = unit,
                Year = year,
                Month = month,
                VoucherType = voucher,
                Description = description
            };

        _selectedDocument =
            _documents[index];

        SaveJson(
            _documentsFile,
            _documents);

        RefreshAll();

        MessageBox.Show(
            "Document updated successfully.",
            "DMS",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        ShowOnly(
            SearchPanel,
            "Search");

        ApplySearch();
    }

    private void EditClear_Click(
        object sender,
        RoutedEventArgs e)
    {
        EditProjectCodeBox.Clear();
        EditUnitBox.Clear();
        EditYearBox.Clear();
        EditMonthBox.Clear();
        EditVoucherBox.Clear();
        EditDescriptionBox.Clear();
    }

    #endregion

    #region Delete Document

    private void Delete_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!RequirePermission(_currentUser.CanDeleteDocument, "delete documents"))
        {
            return;
        }
        if (_selectedDocument is null)
        {
            MessageBox.Show(
                "Select a document from Search first.",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        var confirmation =
            MessageBox.Show(
                $"Delete document {_selectedDocument.DocumentNo}?\n\n" +
                "The document record and its stored file will be removed.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(
                    _selectedDocument.FilePath) &&
                File.Exists(
                    _selectedDocument.FilePath))
            {
                File.Delete(
                    _selectedDocument.FilePath);
            }

            _documents.RemoveAll(document =>
                string.Equals(
                    document.DocumentNo,
                    _selectedDocument.DocumentNo,
                    StringComparison.OrdinalIgnoreCase));

            _selectedDocument = null;

            SaveJson(
                _documentsFile,
                _documents);

            RefreshAll();

            ShowOnly(
                SearchPanel,
                "Search");

            ApplySearch();

            MessageBox.Show(
                "Document deleted successfully.",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (IOException ex)
        {
            MessageBox.Show(
                $"Unable to delete the document.\n\n{ex.Message}",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    #endregion

    #region Reply

    private void Reply_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!RequirePermission(_currentUser.CanReply, "reply to documents"))
        {
            return;
        }
        if (sender is not Button button ||
            button.DataContext is not DocumentRecord document)
        {
            return;
        }

        _selectedDocument = document;

        var requestDate = DateTime.Now;

        ReplyDocumentNoBox.Text =
            document.DocumentNo;

        ReplyRequestNoBox.Text =
            "REQ-" +
            requestDate.ToString(
                "yyyyMMdd-HHmmss");

        ReplyRequestDateBox.Text =
            requestDate.ToString(
                "dd/MM/yyyy");

        ReplySubjectBox.Text =
            "Reply regarding " +
            document.Description;

        ShowOnly(
            ReplyPanel,
            "Reply to Document Request");
    }

    private void ReplyChooseFile_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!RequirePermission(_currentUser.CanReply, "attach reply files"))
        {
            return;
        }
        var path = ChooseDocumentFile();

        if (path is null)
        {
            return;
        }

        _selectedReplyAttachment = path;
        ReplyAttachmentBox.Text = path;
    }

    private void ReplyClear_Click(
        object sender,
        RoutedEventArgs e)
    {
        ReplyToBox.Clear();
        ReplyAddressBox.Clear();
        ReplySubjectBox.Clear();
        ReplyReferenceBox.Clear();
        ReplyTextBox.Clear();
        ReplyAttachmentBox.Clear();

        _selectedReplyAttachment = null;
    }

    private void ReplySubmit_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!RequirePermission(_currentUser.CanReply, "save replies"))
        {
            return;
        }
        var to =
            ReplyToBox.Text.Trim();

        var replyText =
            ReplyTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(to) ||
            string.IsNullOrWhiteSpace(replyText))
        {
            MessageBox.Show(
                "To and Reply are required.",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        var replyId =
            "REP-" +
            DateTime.Now.ToString(
                "yyyyMMdd-HHmmssfff");

        var attachmentPath =
            string.Empty;

        try
        {
            if (!string.IsNullOrWhiteSpace(
                    _selectedReplyAttachment))
            {
                attachmentPath =
                    CopyReplyAttachment(
                        _selectedReplyAttachment,
                        replyId);
            }

            var reply = new ReplyRecord(
                replyId,
                ReplyRequestNoBox.Text.Trim(),
                ReplyRequestDateBox.Text.Trim(),
                ReplyDocumentNoBox.Text.Trim(),
                to,
                ReplyAddressBox.Text.Trim(),
                ReplySubjectBox.Text.Trim(),
                ReplyReferenceBox.Text.Trim(),
                replyText,
                attachmentPath,
                DateTime.Now);

            _replies.Add(reply);

            SaveJson(
                _repliesFile,
                _replies);

            MessageBox.Show(
                $"Reply saved successfully.\n\nReply No.: {replyId}",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            ReplyClear_Click(
                sender,
                e);
        }
        catch (IOException ex)
        {
            MessageBox.Show(
                $"Unable to save the reply.\n\n{ex.Message}",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private string CopyReplyAttachment(
        string source,
        string replyId)
    {
        if (!File.Exists(source))
        {
            throw new FileNotFoundException(
                "The selected attachment could not be found.",
                source);
        }

        var repliesFolder =
            Path.Combine(
                _dataFolder,
                "Replies");

        Directory.CreateDirectory(
            repliesFolder);

        var extension =
            Path.GetExtension(source);

        var destination =
            Path.Combine(
                repliesFolder,
                replyId + extension);

        File.Copy(
            source,
            destination,
            true);

        return destination;
    }

    private void ReplyPrint_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!RequirePermission(_currentUser.CanPrint, "print replies"))
        {
            return;
        }

        PrintReply();
    }

    #endregion

    #region Printing

    private void Print_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!RequirePermission(_currentUser.CanPrint, "print documents"))
        {
            return;
        }
        if (sender is not Button button ||
            button.DataContext is not DocumentRecord document)
        {
            return;
        }

        var text =
            "DOCUMENT MANAGEMENT SYSTEM\n\n" +
            $"Document No.: {document.DocumentNo}\n" +
            $"Project Code: {document.ProjectCode}\n" +
            $"Unit: {document.Unit}\n" +
            $"Year: {document.Year}\n" +
            $"Month: {document.Month}\n" +
            $"Voucher Type: {document.VoucherType}\n" +
            $"Description: {document.Description}";

        PrintText(text);
    }

    private void PrintReply()
    {
        var text =
            "DOCUMENT MANAGEMENT SYSTEM\n\n" +
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

    private static void PrintText(
        string text)
    {
        var dialog =
            new System.Windows.Controls.PrintDialog();

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var visual =
            new TextBlock
            {
                Text = text,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(50)
            };

        dialog.PrintVisual(
            visual,
            "DMS Print");
    }

    #endregion

    #region Excel Export

    private void ExcelGenerate_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!RequirePermission(_currentUser.CanExcel, "generate Excel reports"))
        {
            return;
        }
        var fileName =
            string.IsNullOrWhiteSpace(
                ExcelFileNameBox.Text)
                ? "DMS_Report"
                : ExcelFileNameBox.Text.Trim();

        var dialog =
            new SaveFileDialog
            {
                FileName = fileName + ".csv",
                Filter = "CSV file|*.csv",
                DefaultExt = ".csv",
                AddExtension = true
            };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var rows =
            DocumentsGrid.ItemsSource
                as IEnumerable<DocumentRecord>
            ?? _documents;

        var builder =
            new StringBuilder();

        builder.AppendLine(
            "Sl.No,Document No.,Project Code,Unit,Year,Month,Voucher Type,Description,File");

        var serialNumber = 1;

        foreach (var document in rows)
        {
            builder.AppendLine(
                string.Join(
                    ",",
                    serialNumber++,
                    Csv(document.DocumentNo),
                    Csv(document.ProjectCode),
                    Csv(document.Unit),
                    Csv(document.Year),
                    Csv(document.Month),
                    Csv(document.VoucherType),
                    Csv(document.Description),
                    Csv(document.FilePath)));
        }

        try
        {
            File.WriteAllText(
                dialog.FileName,
                builder.ToString(),
                new UTF8Encoding(true));

            MessageBox.Show(
                "Report generated successfully.",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (IOException ex)
        {
            MessageBox.Show(
                $"Unable to create the report.\n\n{ex.Message}",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static string Csv(
        string value)
    {
        return "\"" +
               value.Replace(
                   "\"",
                   "\"\"") +
               "\"";
    }

    private void ExcelClear_Click(
        object sender,
        RoutedEventArgs e)
    {
        ExcelFileNameBox.Text =
            "DMS_Report";
    }

    #endregion

    #region Language

// language combo
    private void LanguageCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!IsLoaded ||
            LanguageCombo.SelectedIndex < 0)
        {
            return;
        }

        if (LanguageCombo.SelectedIndex == 1)
        {
            MessageBox.Show(
                "Kannada UI translation will be expanded in the localization phase.",
                "DMS",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    #endregion

    #region Logout
    private void ApplyPermissions()
{
    CreateUserButton.Visibility =
        _currentUser.CanCreateUser
            ? Visibility.Visible
            : Visibility.Collapsed;

    DeleteUserButton.Visibility =
        _currentUser.CanDeleteUser
            ? Visibility.Visible
            : Visibility.Collapsed;

    SearchButton.Visibility =
        _currentUser.CanSearch
            ? Visibility.Visible
            : Visibility.Collapsed;

    IndexButton.Visibility =
        _currentUser.CanIndex
            ? Visibility.Visible
            : Visibility.Collapsed;

    UploadButton.Visibility =
        _currentUser.CanUpload
            ? Visibility.Visible
            : Visibility.Collapsed;

    ExcelButton.Visibility =
        _currentUser.CanExcel
            ? Visibility.Visible
            : Visibility.Collapsed;

    DeleteButton.Visibility =
        _currentUser.CanDeleteDocument
            ? Visibility.Visible
            : Visibility.Collapsed;

    EditButton.Visibility =
        _currentUser.CanEdit
            ? Visibility.Visible
            : Visibility.Collapsed;

    LogoutButton.Visibility = Visibility.Visible;
}

    private void Logout_Click(
        object sender,
        RoutedEventArgs e)
    {
        var confirmation =
            MessageBox.Show(
                "Do you want to logout?",
                "DMS",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        var login =
            new LoginWindow();

        Application.Current.MainWindow =
            login;

        login.Show();

        Close();
    }

    #endregion
}

public sealed record DocumentRecord(
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

public sealed record ReplyRecord(
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