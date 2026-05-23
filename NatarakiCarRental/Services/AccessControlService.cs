using NatarakiCarRental.Models;
using NatarakiCarRental.Repositories;

namespace NatarakiCarRental.Services;

public sealed class AccessControlService
{
    private static User? _currentUser;
    private static List<string> _currentPermissions = [];

    private readonly PermissionRepository _permissionRepository;

    public AccessControlService() : this(new PermissionRepository())
    {
    }

    public AccessControlService(PermissionRepository permissionRepository)
    {
        _permissionRepository = permissionRepository;
    }

    public static User? CurrentUser => _currentUser;
    public static bool IsLoggedIn => _currentUser != null;
    public static bool IsOwner => _currentUser?.IsOwner ?? false;

    public async Task InitializeAsync(User user)
    {
        _currentUser = user;
        var permissions = await _permissionRepository.GetKeysByRoleIdAsync(user.RoleId);
        _currentPermissions = permissions.ToList();
    }

    public static void Logout()
    {
        _currentUser = null;
        _currentPermissions.Clear();
    }

    public static bool HasPermission(string permissionKey)
    {
        if (IsOwner) return true;
        return _currentPermissions.Contains(permissionKey, StringComparer.OrdinalIgnoreCase);
    }

    public static bool HasAnyPermission(params string[] permissionKeys)
    {
        if (IsOwner) return true;
        return permissionKeys.Any(key => _currentPermissions.Contains(key, StringComparer.OrdinalIgnoreCase));
    }

    public static void EnforcePermission(string permissionKey)
    {
        if (!HasPermission(permissionKey))
        {
            throw new UnauthorizedAccessException($"Access Denied: Missing permission '{permissionKey}'.");
        }
    }
}
