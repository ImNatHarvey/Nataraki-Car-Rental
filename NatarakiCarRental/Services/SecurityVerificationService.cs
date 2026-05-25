using NatarakiCarRental.Forms.ManageSystem;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Services;

public sealed class SecurityVerificationService
{
    private readonly UserService _userService;

    public SecurityVerificationService() : this(new UserService())
    {
    }

    public SecurityVerificationService(UserService userService)
    {
        _userService = userService;
    }

    public async Task<bool> RequireOwnerVerificationIfNeededAsync(int currentUserId, string actionName)
    {
        // 1. If current user is Owner, bypass verification
        User? currentUser = await _userService.GetUserByIdAsync(currentUserId);
        if (currentUser?.IsOwner == true)
        {
            return true;
        }

        // 2. Otherwise, require owner password confirmation
        using OwnerPasswordConfirmationForm form = new(actionName);
        if (form.ShowDialog() == DialogResult.OK)
        {
            MessageBoxHelper.ShowSuccess("Owner verification successful.");
            return true;
        }

        return false;
    }

    public async Task<bool> VerifyOwnerPasswordAsync(string password)
    {
        if (string.IsNullOrEmpty(password)) return false;

        // Owner verification must read the active Owner row fresh each time.
        // Login and verification both validate BCrypt hashes from dbo.Users.PasswordHash.
        User? ownerDetails = await _userService.GetActiveOwnerAsync();
        if (ownerDetails == null || string.IsNullOrWhiteSpace(ownerDetails.PasswordHash)) return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, ownerDetails.PasswordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
