using NatarakiCarRental.Models;
using NatarakiCarRental.Repositories;

namespace NatarakiCarRental.Services;

public sealed class SecurityVerificationService
{
    private readonly UserRepository _userRepository;

    public SecurityVerificationService() : this(new UserRepository())
    {
    }

    public SecurityVerificationService(UserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<bool> VerifyOwnerPasswordAsync(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        IReadOnlyList<User> owners = await _userRepository.GetActiveOwnersAsync();
        if (owners.Count == 0)
        {
            return false;
        }

        foreach (User owner in owners)
        {
            if (!IsValidBCryptHash(owner.PasswordHash))
            {
                continue;
            }

            try
            {
                if (BCrypt.Net.BCrypt.Verify(password, owner.PasswordHash))
                {
                    return true;
                }
            }
            catch
            {
                // Ignore invalid/corrupt hashes and continue checking other active owner rows.
            }
        }

        return false;
    }

    private static bool IsValidBCryptHash(string? passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)) return false;
        return passwordHash.StartsWith("$2a$", StringComparison.Ordinal)
               || passwordHash.StartsWith("$2b$", StringComparison.Ordinal)
               || passwordHash.StartsWith("$2y$", StringComparison.Ordinal);
    }
}
