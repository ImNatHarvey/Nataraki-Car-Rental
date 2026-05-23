using NatarakiCarRental.Models;
using NatarakiCarRental.Repositories;

namespace NatarakiCarRental.Services;

public sealed class AuthService
{
    private readonly UserRepository _userRepository;
    private readonly AccessControlService _accessControlService;

    public AuthService()
        : this(new UserRepository(), new AccessControlService())
    {
    }

    public AuthService(UserRepository userRepository, AccessControlService accessControlService)
    {
        _userRepository = userRepository;
        _accessControlService = accessControlService;
    }

    public async Task<User?> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        User? user = await _userRepository.GetActiveUserByUsernameAsync(username.Trim());

        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return null;
        }

        await _userRepository.UpdateLastLoginAsync(user.UserId);
        await _accessControlService.InitializeAsync(user);

        return user;
    }
}
