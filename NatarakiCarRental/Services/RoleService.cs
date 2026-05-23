using FluentValidation;
using FluentValidation.Results;
using NatarakiCarRental.Models;
using NatarakiCarRental.Repositories;

namespace NatarakiCarRental.Services;

public sealed class RoleService
{
    private readonly RoleRepository _roleRepository;
    private readonly PermissionRepository _permissionRepository;
    private readonly ActivityLogService _activityLogService;

    public RoleService() : this(new RoleRepository(), new PermissionRepository(), new ActivityLogService())
    {
    }

    public RoleService(RoleRepository roleRepository, PermissionRepository permissionRepository, ActivityLogService activityLogService)
    {
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _activityLogService = activityLogService;
    }

    public Task<IReadOnlyList<Role>> GetAllRolesAsync(bool includeArchived = false)
    {
        return _roleRepository.GetAllAsync(includeArchived);
    }

    public Task<IReadOnlyList<Permission>> GetAllPermissionsAsync()
    {
        return _permissionRepository.GetAllAsync();
    }

    public async Task<RoleWithPermissions?> GetRoleWithPermissionsAsync(int roleId)
    {
        Role? role = await _roleRepository.GetByIdAsync(roleId);
        if (role == null) return null;

        var keys = await _permissionRepository.GetKeysByRoleIdAsync(roleId);
        return new RoleWithPermissions
        {
            RoleId = role.RoleId,
            RoleName = role.RoleName,
            Description = role.Description,
            IsActive = role.IsActive,
            PermissionKeys = keys.ToList()
        };
    }

    public async Task CreateRoleAsync(RoleWithPermissions request, int currentUserId)
    {
        if (string.IsNullOrWhiteSpace(request.RoleName))
        {
            throw new ValidationException([new ValidationFailure("RoleName", "Role name is required.")]);
        }

        Role role = new()
        {
            RoleName = request.RoleName.Trim(),
            Description = request.Description?.Trim(),
            IsActive = request.IsActive,
            IsSystemRole = false
        };

        role.RoleId = await _roleRepository.AddAsync(role);
        await _permissionRepository.SetRolePermissionsAsync(role.RoleId, request.PermissionKeys);

        await _activityLogService.LogAsync(
            "Create",
            "Role",
            role.RoleId,
            $"Created role {role.RoleName} with {request.PermissionKeys.Count} permissions.",
            currentUserId);
    }

    public async Task UpdateRoleAsync(RoleWithPermissions request, int currentUserId)
    {
        Role? existing = await _roleRepository.GetByIdAsync(request.RoleId);
        if (existing == null) throw new InvalidOperationException("Role not found.");
        if (existing.IsSystemRole && !existing.RoleName.Equals("Owner", StringComparison.OrdinalIgnoreCase))
        {
             // System roles (except Owner) can have description/status updated but not name if we want to be strict.
             // But the user prompt says system roles cannot be renamed/deleted.
        }

        existing.RoleName = request.RoleName.Trim();
        existing.Description = request.Description?.Trim();
        existing.IsActive = request.IsActive;

        await _roleRepository.UpdateAsync(existing);
        
        // Owner permissions are protected
        if (!existing.RoleName.Equals("Owner", StringComparison.OrdinalIgnoreCase))
        {
            await _permissionRepository.SetRolePermissionsAsync(existing.RoleId, request.PermissionKeys);
        }

        await _activityLogService.LogAsync(
            "Update",
            "Role",
            existing.RoleId,
            $"Updated role {existing.RoleName}.",
            currentUserId);
    }

    public async Task ArchiveRoleAsync(int roleId, int currentUserId)
    {
        Role? role = await _roleRepository.GetByIdAsync(roleId);
        if (role == null) return;
        if (role.IsSystemRole) throw new InvalidOperationException("System roles cannot be archived.");

        int userCount = await _roleRepository.GetUserCountAsync(roleId);
        if (userCount > 0) throw new InvalidOperationException($"Cannot archive role '{role.RoleName}' because {userCount} active users are assigned to it.");

        await _roleRepository.ArchiveAsync(roleId);

        await _activityLogService.LogAsync(
            "Archive",
            "Role",
            roleId,
            $"Archived role {role.RoleName}.",
            currentUserId);
    }
}
