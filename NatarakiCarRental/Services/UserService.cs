using FluentValidation;
using FluentValidation.Results;
using NatarakiCarRental.Models;
using NatarakiCarRental.Repositories;

namespace NatarakiCarRental.Services;

public sealed class UserService
{
    private readonly UserRepository _userRepository;
    private readonly ActivityLogService _activityLogService;

    public UserService() : this(new UserRepository(), new ActivityLogService())
    {
    }

    public UserService(UserRepository userRepository, ActivityLogService activityLogService)
    {
        _userRepository = userRepository;
        _activityLogService = activityLogService;
    }

    public Task<IReadOnlyList<UserListItem>> SearchUsersAsync(string? searchTerm = null, int? roleId = null, bool? isActive = null, bool includeArchived = false)
    {
        return _userRepository.SearchAsync(searchTerm, roleId, isActive, includeArchived);
    }

    public Task<User?> GetUserByIdAsync(int userId)
    {
        return _userRepository.GetByIdAsync(userId);
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
            RoleId = request.RoleId,
            IsActive = request.IsActive,
            IsOwner = false // Only system can seed owner
        };

        user.UserId = await _userRepository.AddAsync(user);

        await _activityLogService.LogAsync(
            "Create",
            "User",
            user.UserId,
            $"Created user {user.Username} ({user.FullName}).",
            currentUserId);
    }

    public async Task UpdateUserAsync(UpdateUserRequest request, int currentUserId)
    {
        AccessControlService.EnforcePermission("ManageSystem.Users");
        ValidateUpdateRequest(request);

        User? existing = await _userRepository.GetByIdAsync(request.UserId);
        if (existing == null) throw new InvalidOperationException("User not found.");

        string username = request.Username.Trim();
        if (await _userRepository.ExistsByUsernameAsync(username, request.UserId))
        {
            throw new ValidationException([new ValidationFailure("Username", "Username is already taken.")]);
        }

        existing.FirstName = request.FirstName.Trim();
        existing.LastName = request.LastName.Trim();
        existing.Email = request.Email?.Trim();
        existing.PhoneNumber = request.PhoneNumber?.Trim();
        existing.Username = username;

        if (existing.IsOwner)
        {
            existing.IsActive = true;
        }
        else
        {
            existing.RoleId = request.RoleId;
            existing.IsActive = request.IsActive;
        }

        await _userRepository.UpdateAsync(existing);

        if (AccessControlService.CurrentUser?.UserId == existing.UserId)
        {
            // Update current session if the user updated themselves
            AccessControlService.CurrentUser.Username = existing.Username;
            AccessControlService.CurrentUser.FirstName = existing.FirstName;
            AccessControlService.CurrentUser.LastName = existing.LastName;
            AccessControlService.CurrentUser.Email = existing.Email;
            AccessControlService.CurrentUser.PhoneNumber = existing.PhoneNumber;
        }

        await _activityLogService.LogAsync(
            "Update",
            "User",
            existing.UserId,
            $"Updated user {existing.Username} ({existing.FullName}).",
            currentUserId);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, int currentUserId)
    {
        AccessControlService.EnforcePermission("ManageSystem.Users");
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            throw new ValidationException([new ValidationFailure("NewPassword", "Password must be at least 8 characters.")]);
        }

        string hash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _userRepository.UpdatePasswordAsync(request.UserId, hash);

        User? user = await _userRepository.GetByIdAsync(request.UserId);
        await _activityLogService.LogAsync(
            "Update",
            "User",
            request.UserId,
            $"Changed password for user {user?.Username ?? request.UserId.ToString()}.",
            currentUserId);
    }

    public async Task ArchiveUserAsync(int userId, int currentUserId)
    {
        AccessControlService.EnforcePermission("ManageSystem.Users");
        User? user = await _userRepository.GetByIdAsync(userId);
        if (user == null) return;
        if (user.IsOwner) throw new InvalidOperationException("System owner account cannot be archived.");

        await _userRepository.ArchiveAsync(userId);

        await _activityLogService.LogAsync(
            "Archive",
            "User",
            userId,
            $"Archived user {user.Username}.",
            currentUserId);
    }

    public async Task RestoreUserAsync(int userId, int currentUserId)
    {
        AccessControlService.EnforcePermission("ManageSystem.Users");
        await _userRepository.RestoreAsync(userId);
        User? user = await _userRepository.GetByIdAsync(userId);

        await _activityLogService.LogAsync(
            "Restore",
            "User",
            userId,
            $"Restored user {user?.Username ?? userId.ToString()}.",
            currentUserId);
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
}
