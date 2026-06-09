namespace NatarakiCarRental.Models;

public sealed class Customer
{
    public int CustomerId { get; set; }
    public string CustomerType { get; set; } = "Rental";
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? CompanyName { get; set; }
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Region { get; set; }
    public string? Province { get; set; }
    public string? City { get; set; }
    public string? Barangay { get; set; }
    public string? StreetAddress { get; set; }
    public bool IsBlacklisted { get; set; }
    public string? BlacklistReason { get; set; }
    public bool IsWalkIn { get; set; }
    public bool IsArchived { get; set; }
    public string? Notes { get; set; }
    public string? MaintenancePreferences { get; set; }
    public string? DriverLicensePath { get; set; }
    public string? ProofOfBillingPath { get; set; }
    public string? ValidIdFilePath { get; set; }
    public string? SelfieWithValidIdFilePath { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
}

