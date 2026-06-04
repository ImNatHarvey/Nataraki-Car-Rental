using FluentValidation;
using FluentValidation.Results;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Repositories;

namespace NatarakiCarRental.Services;

public sealed class UserService
{
    private readonly UserRepository _userRepository;
    private readonly ActivityLogService _activityLogService;
    private readonly NotificationService _notificationService = new();

    public UserService() : this(new UserRepository(), new ActivityLogService(), new NotificationService())
    {
    }

    public UserService(UserRepository userRepository, ActivityLogService activityLogService, NotificationService notificationService)
    {
        _userRepository = userRepository;
        _activityLogService = activityLogService;
        _notificationService = notificationService;
    }

    public Task<IReadOnlyList<UserListItem>> SearchUsersAsync(string? searchTerm = null, int? roleId = null, bool? isActive = null, bool includeArchived = false)
    {
        return _userRepository.SearchAsync(searchTerm, roleId, isActive, includeArchived);
    }

    public Task<User?> GetUserByIdAsync(int userId)
    {
        return _userRepository.GetByIdAsync(userId);
    }

    public async Task<bool> CheckUserExistsAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;
        User? user = await _userRepository.GetActiveUserByUsernameAsync(username.Trim());
        return user != null;
    }

    public async Task ResetPasswordAsync(string username, string lastName, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("Username is required.");
        if (string.IsNullOrWhiteSpace(lastName)) throw new ArgumentException("Last name is required for verification.");
        
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            throw new ValidationException([new ValidationFailure("NewPassword", "Password must be at least 8 characters.")]);
        }

        User? user = await _userRepository.GetActiveUserByUsernameAsync(username.Trim());
        if (user == null) throw new InvalidOperationException("User not found.");

        if (!string.Equals(user.LastName.Trim(), lastName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Verification Failed: Last Name does not match records.");
        }

        string hash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _userRepository.UpdatePasswordAsync(user.UserId, hash);

        await _activityLogService.LogAsync(
            action: "Updated",
            module: "User",
            entityId: user.UserId,
            description: $"Password reset via last name verification for user {user.Username}.",
            userId: user.UserId,
            entityName: user.FullName);
    }

    public Task<User?> GetActiveOwnerAsync()
    {
        return _userRepository.GetActiveOwnerAsync();
    }

    public async Task CreateUserAsync(CreateUserRequest request, int currentUserId)
    {
        AccessControlService.EnforcePermission("ManageSystem.Users");
        ValidateCreateRequest(request);

        if (await _userRepository.ExistsByUsernameAsync(request.Username))
        {
            throw new ValidationException([new ValidationFailure("Username", "Username is already taken.")]);
        }

        User user = new()
        {
            Username = request.Username.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email?.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            ProfileImagePath = request.ProfileImagePath,
            RoleId = request.RoleId,
            IsActive = request.IsActive,
            IsOwner = false // Only system can seed owner
        };

        user.UserId = await _userRepository.AddAsync(user);

        await _activityLogService.LogAsync(
            action: "Created",
            module: "User",
            entityId: user.UserId,
            description: $"Created user {user.Username} ({user.FullName}).",
            userId: currentUserId,
            entityName: user.FullName);

        await _notificationService.NotifyAsync(
            "User Added",
            $"New user account created: {user.Username} ({user.FullName})",
            type: "Success",
            entityId: user.UserId,
            module: "User");
    }

    public async Task UpdateUserAsync(UpdateUserRequest request, int currentUserId)
    {
        AccessControlService.EnforcePermission("ManageSystem.Users");
        ValidateUpdateRequest(request);

        User? existing = await _userRepository.GetByIdAsync(request.UserId);
        if (existing == null) throw new InvalidOperationException("User not found.");

        if (existing.IsOwner && AccessControlService.CurrentUser?.IsOwner != true)
        {
            throw new InvalidOperationException("Only the system owner can update the owner account.");
        }

        string username = request.Username.Trim();
        if (await _userRepository.ExistsByUsernameAsync(username, request.UserId))
        {
            throw new ValidationException([new ValidationFailure("Username", "Username is already taken.")]);
        }

        User oldUser = new User
        {
            UserId = existing.UserId,
            RoleId = existing.RoleId,
            Username = existing.Username,
            FirstName = existing.FirstName,
            LastName = existing.LastName,
            Email = existing.Email,
            PhoneNumber = existing.PhoneNumber,
            ProfileImagePath = existing.ProfileImagePath,
            IsActive = existing.IsActive,
            IsOwner = existing.IsOwner,
            IsArchived = existing.IsArchived
        };

        existing.FirstName = request.FirstName.Trim();
        existing.LastName = request.LastName.Trim();
        existing.Email = request.Email?.Trim();
        existing.PhoneNumber = request.PhoneNumber?.Trim();
        existing.Username = username;
        existing.ProfileImagePath = request.ProfileImagePath ?? existing.ProfileImagePath;

        if (existing.IsOwner)
        {
            existing.IsActive = true;
        }
        else
        {
            existing.RoleId = request.RoleId;
            existing.IsActive = request.IsActive;
        }

        var (oldValue, newValue) = DiffHelper.GetJsonDiff(oldUser, existing);
        if (oldValue == null) return; // Only log and update if ACTUAL changes occurred

        await _userRepository.UpdateAsync(existing);

        if (AccessControlService.CurrentUser?.UserId == existing.UserId)
        {
            // Update current session if the user updated themselves
            AccessControlService.CurrentUser.Username = existing.Username;
            AccessControlService.CurrentUser.FirstName = existing.FirstName;
            AccessControlService.CurrentUser.LastName = existing.LastName;
            AccessControlService.CurrentUser.Email = existing.Email;
            AccessControlService.CurrentUser.PhoneNumber = existing.PhoneNumber;
            AccessControlService.CurrentUser.ProfileImagePath = existing.ProfileImagePath;
        }

        await _activityLogService.LogAsync(
            action: "Updated",
            module: "User",
            entityId: existing.UserId,
            description: $"Updated user {existing.Username} ({existing.FullName}).",
            userId: currentUserId,
            entityName: existing.FullName,
            oldValue: oldValue,
            newValue: newValue);
    }

    public async Task<User> UpdateSelfProfileAsync(UpdateSelfProfileRequest request, int currentUserId)
    {
        if (request.UserId != currentUserId)
        {
            throw new UnauthorizedAccessException("You can only update your own profile.");
        }

        ValidateSelfProfileRequest(request);

        User? existing = await _userRepository.GetByIdAsync(request.UserId);
        if (existing == null) throw new InvalidOperationException("User not found.");

        string username = request.Username.Trim();
        if (await _userRepository.ExistsByUsernameAsync(username, request.UserId))
        {
            throw new ValidationException([new ValidationFailure("Username", "Username is already taken.")]);
        }

        User oldUser = new User
        {
            UserId = existing.UserId,
            RoleId = existing.RoleId,
            Username = existing.Username,
            FirstName = existing.FirstName,
            LastName = existing.LastName,
            Email = existing.Email,
            PhoneNumber = existing.PhoneNumber,
            ProfileImagePath = existing.ProfileImagePath,
            IsActive = existing.IsActive,
            IsOwner = existing.IsOwner,
            IsArchived = existing.IsArchived
        };

        existing.FirstName = request.FirstName.Trim();
        existing.LastName = request.LastName.Trim();
        existing.Username = username;
        existing.ProfileImagePath = string.IsNullOrWhiteSpace(request.ProfileImagePath) ? null : request.ProfileImagePath;

        var (oldValue, newValue) = DiffHelper.GetJsonDiff(oldUser, existing);
        if (oldValue == null) return existing; // Only log and update if ACTUAL changes occurred

        await _userRepository.UpdateSelfProfileAsync(existing);

        if (AccessControlService.CurrentUser?.UserId == existing.UserId)
        {
            AccessControlService.CurrentUser.Username = existing.Username;
            AccessControlService.CurrentUser.FirstName = existing.FirstName;
            AccessControlService.CurrentUser.LastName = existing.LastName;
            AccessControlService.CurrentUser.ProfileImagePath = existing.ProfileImagePath;
        }

        await _activityLogService.LogAsync(
            action: "Updated",
            module: "User",
            entityId: existing.UserId,
            description: $"Updated own profile for user {existing.Username}.",
            userId: currentUserId,
            entityName: existing.FullName,
            oldValue: oldValue,
            newValue: newValue);

        return existing;
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, int currentUserId)
    {
        AccessControlService.EnforcePermission("ManageSystem.Users");
        
        User? user = await _userRepository.GetByIdAsync(request.UserId);
        if (user != null && user.IsOwner && AccessControlService.CurrentUser?.IsOwner != true)
        {
            throw new InvalidOperationException("Only the system owner can change the owner's password.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            throw new ValidationException([new ValidationFailure("NewPassword", "Password must be at least 8 characters.")]);
        }

        string hash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _userRepository.UpdatePasswordAsync(request.UserId, hash);

        await _activityLogService.LogAsync(
            action: "Updated",
            module: "User",
            entityId: request.UserId,
            description: $"Changed password for user {user?.Username ?? request.UserId.ToString()}.",
            userId: currentUserId,
            entityName: user?.FullName ?? request.UserId.ToString());
    }

    public async Task ChangeOwnPasswordAsync(ChangeOwnPasswordRequest request, int currentUserId)
    {
        if (request.UserId != currentUserId)
        {
            throw new UnauthorizedAccessException("You can only change your own password.");
        }

        List<ValidationFailure> failures = [];
        if (string.IsNullOrWhiteSpace(request.CurrentPassword)) failures.Add(new("CurrentPassword", "Current password is required."));
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8) failures.Add(new("NewPassword", "New password must be at least 8 characters."));
        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal)) failures.Add(new("ConfirmPassword", "New password confirmation must match."));
        if (failures.Count > 0) throw new ValidationException(failures);

        User? userToUpdate = await _userRepository.GetByIdAsync(request.UserId);
        if (userToUpdate is null) throw new InvalidOperationException("User not found.");
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, userToUpdate.PasswordHash))
        {
            throw new InvalidOperationException("Current password is incorrect.");
        }

        string hash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _userRepository.UpdatePasswordAsync(request.UserId, hash);

        await _activityLogService.LogAsync(
            action: "Updated",
            module: "User",
            entityId: request.UserId,
            description: $"Changed own password for user {userToUpdate.Username}.",
            userId: currentUserId,
            entityName: userToUpdate.FullName);
    }

    public async Task ArchiveUserAsync(int userId, int currentUserId)
    {
        AccessControlService.EnforcePermission("ManageSystem.Users");
        if (userId == currentUserId)
        {
            throw new InvalidOperationException("You cannot archive your own account.");
        }
        
        User? user = await _userRepository.GetByIdAsync(userId);
        if (user == null) return;
        if (user.IsOwner) throw new InvalidOperationException("System owner account cannot be archived.");

        await _userRepository.ArchiveAsync(userId);

        await _activityLogService.LogAsync(
            action: "Archived",
            module: "User",
            entityId: userId,
            description: $"Archived user {user.Username}.",
            userId: currentUserId,
            entityName: user.FullName);
    }

    public async Task RestoreUserAsync(int userId, int currentUserId)
    {
        AccessControlService.EnforcePermission("ManageSystem.Users");
        await _userRepository.RestoreAsync(userId);
        User? user = await _userRepository.GetByIdAsync(userId);

        await _activityLogService.LogAsync(
            action: "Restored",
            module: "User",
            entityId: userId,
            description: $"Restored user {user?.Username ?? userId.ToString()}.",
            userId: currentUserId,
            entityName: user?.FullName ?? userId.ToString());
    }

    private static void ValidateCreateRequest(CreateUserRequest request)
    {
        List<ValidationFailure> failures = [];
        if (string.IsNullOrWhiteSpace(request.Username)) failures.Add(new("Username", "Username is required."));
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8) failures.Add(new("Password", "Password must be at least 8 characters."));
        if (string.IsNullOrWhiteSpace(request.FirstName)) failures.Add(new("FirstName", "First Name is required."));
        if (string.IsNullOrWhiteSpace(request.LastName)) failures.Add(new("LastName", "Last Name is required."));
        if (request.RoleId <= 0) failures.Add(new("RoleId", "Role is required."));
        if (failures.Count > 0) throw new ValidationException(failures);
    }

    private static void ValidateUpdateRequest(UpdateUserRequest request)
    {
        List<ValidationFailure> failures = [];
        if (string.IsNullOrWhiteSpace(request.Username)) failures.Add(new("Username", "Username is required."));
        if (string.IsNullOrWhiteSpace(request.FirstName)) failures.Add(new("FirstName", "First Name is required."));
        if (string.IsNullOrWhiteSpace(request.LastName)) failures.Add(new("LastName", "Last Name is required."));
        if (request.RoleId <= 0) failures.Add(new("RoleId", "Role is required."));
        if (failures.Count > 0) throw new ValidationException(failures);
    }

    private static void ValidateSelfProfileRequest(UpdateSelfProfileRequest request)
    {
        List<ValidationFailure> failures = [];
        if (string.IsNullOrWhiteSpace(request.Username)) failures.Add(new("Username", "Username is required."));
        if (string.IsNullOrWhiteSpace(request.FirstName)) failures.Add(new("FirstName", "First Name is required."));
        if (string.IsNullOrWhiteSpace(request.LastName)) failures.Add(new("LastName", "Last Name is required."));
        if (failures.Count > 0) throw new ValidationException(failures);
    }
}
