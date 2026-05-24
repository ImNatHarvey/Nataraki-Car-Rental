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
        if (string.IsNullOrWhiteSpace(password)) return false;

        // Fetch owner account (assuming only one is marked IsOwner=1 for demo/simplicity)
        var users = await _userService.SearchUsersAsync();
        var owner = users.FirstOrDefault(u => u.IsOwner);
        if (owner == null) return false;

        User? ownerDetails = await _userService.GetUserByIdAsync(owner.UserId);
        if (ownerDetails == null) return false;

        return BCrypt.Net.BCrypt.Verify(password, ownerDetails.PasswordHash);
    }
}
