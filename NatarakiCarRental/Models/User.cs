namespace NatarakiCarRental.Models;

public sealed class User
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? SecurityQuestion { get; set; }
    public string? SecurityAnswer { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsOwner { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? ArchivedAt { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}
