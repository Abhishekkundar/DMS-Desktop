using System.IO;
using System.Text;
using System.Text.Json;

namespace DMS.Desktop.Data;

public sealed class UserStore
{
    private const string DefaultAdminUserId = "admin";
    private const string DefaultAdminPassword = "password";

    private readonly string _dataFolder;
    private readonly string _usersFile;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public UserStore()
    {
        _dataFolder = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "DMS.Desktop");

        _usersFile = Path.Combine(
            _dataFolder,
            "users.json");

        Directory.CreateDirectory(_dataFolder);

        EnsureUsersFile();
    }

    public List<UserRecord> GetUsers()
    {
        if (!File.Exists(_usersFile))
        {
            return new List<UserRecord>();
        }

        try
        {
            var json = File.ReadAllText(_usersFile);

            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<UserRecord>();
            }

            return JsonSerializer.Deserialize<List<UserRecord>>(
                       json,
                       JsonOptions)
                   ?? new List<UserRecord>();
        }
        catch (JsonException)
        {
            return new List<UserRecord>();
        }
        catch (IOException)
        {
            return new List<UserRecord>();
        }
    }

    public bool UserExists(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        return GetUsers().Any(user =>
            string.Equals(
                user.UserId,
                userId.Trim(),
                StringComparison.OrdinalIgnoreCase));
    }

    public bool AddUser(UserRecord user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var userId = user.UserId.Trim();

        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        if (UserExists(userId))
        {
            return false;
        }

        user.UserId = userId;
        user.FullName = user.FullName.Trim();
        user.EmployeeId = user.EmployeeId.Trim();

        if (string.IsNullOrWhiteSpace(user.Role))
        {
            user.Role = "User";
        }
        else
        {
            user.Role = user.Role.Trim();
        }

        var users = GetUsers();

        users.Add(user);

        SaveUsers(users);

        return true;
    }

    public bool DeleteUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        if (string.Equals(
                userId,
                DefaultAdminUserId,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var users = GetUsers();

        var user = users.FirstOrDefault(existingUser =>
            string.Equals(
                existingUser.UserId,
                userId.Trim(),
                StringComparison.OrdinalIgnoreCase));

        if (user is null)
        {
            return false;
        }

        users.Remove(user);

        SaveUsers(users);

        return true;
    }

    public UserRecord? Authenticate(
        string userId,
        string password)
    {
        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrEmpty(password))
        {
            return null;
        }

        var users = GetUsers();

        return users.FirstOrDefault(user =>
            user.IsActive &&
            string.Equals(
                user.UserId,
                userId.Trim(),
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                user.Password,
                password,
                StringComparison.Ordinal));
    }

    private void EnsureUsersFile()
    {
            List<UserRecord> users;
        if (File.Exists(_usersFile))
    {
        try
        {
            var json = File.ReadAllText(_usersFile);

            users = string.IsNullOrWhiteSpace(json)
                ? new List<UserRecord>()
                : JsonSerializer.Deserialize<List<UserRecord>>(
                    json,
                    JsonOptions) ?? new List<UserRecord>();
        }
        catch (JsonException)
        {
            users = new List<UserRecord>();
        }
        catch (IOException)
        {
            users = new List<UserRecord>();
        }
    }
    else
    {
        users = new List<UserRecord>();
    }
     var admin = users.FirstOrDefault(user =>
        string.Equals(
            user.UserId,
            DefaultAdminUserId,
            StringComparison.OrdinalIgnoreCase));

    if (admin is null)
    {
        admin = new UserRecord
        {
            UserId = DefaultAdminUserId,
            Password = DefaultAdminPassword,
            FullName = "System Administrator",
            EmployeeId = "ADMIN",
            Role = "Administrator",

            CanCreateUser = true,
            CanDeleteUser = true,
            CanSearch = true,
            CanIndex = true,
            CanUpload = true,
            CanExcel = true,
            CanEdit = true,
            CanDeleteDocument = true,
            CanReply = true,
            CanPrint = true,

            IsActive = true
        };

        users.Add(admin);
    }
    else
    {
        // Repair / normalize the existing administrator account.
        admin.Role = "Administrator";

        if (string.IsNullOrWhiteSpace(admin.FullName))
        {
            admin.FullName = "System Administrator";
        }

        if (string.IsNullOrWhiteSpace(admin.EmployeeId))
        {
            admin.EmployeeId = "ADMIN";
        }

        admin.CanCreateUser = true;
        admin.CanDeleteUser = true;
        admin.CanSearch = true;
        admin.CanIndex = true;
        admin.CanUpload = true;
        admin.CanExcel = true;
        admin.CanEdit = true;
        admin.CanDeleteDocument = true;
        admin.CanReply = true;
        admin.CanPrint = true;
        admin.IsActive = true;
    }

    SaveUsers(users);
}

// save user class
private void SaveUsers(List<UserRecord> users)
    {
    var json = JsonSerializer.Serialize(
        users,
        JsonOptions);

    File.WriteAllText(
        _usersFile,
        json,
        Encoding.UTF8);
    }
}
