namespace DMS.Desktop.Data;

public sealed class UserRecord
{
    public string UserId { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string EmployeeId { get; set; } = string.Empty;

    public string Role { get; set; } = "User";

    public bool CanCreateUser { get; set; }

    public bool CanDeleteUser { get; set; }

    public bool CanSearch { get; set; } = true;

    public bool CanIndex { get; set; }

    public bool CanUpload { get; set; }

    public bool CanExcel { get; set; }

    public bool CanEdit { get; set; }

    public bool CanDeleteDocument { get; set; }

    public bool CanReply { get; set; }

    public bool CanPrint { get; set; }

    public bool IsActive { get; set; } = true;
}