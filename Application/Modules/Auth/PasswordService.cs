using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace SPRMS.Services.Domain;

/// <summary>
/// Password hashing service using Argon2id algorithm.
/// </summary>
public interface IPasswordService
{
    /// <summary>Hashes a password with salt using Argon2id.</summary>
    Task<(string hash, string salt)> HashPasswordAsync(string password);

    /// <summary>Verifies a password against its hash.</summary>
    Task<bool> VerifyPasswordAsync(string password, string hash, string salt);

    /// <summary>Validates password complexity (must be >= 8 chars, contain uppercase, lowercase, digit, special char).</summary>
    bool ValidatePasswordComplexity(string password);
}

/// <summary>
/// Argon2id password hashing implementation.
/// Configuration: Memory=64MB, Iterations=3, Parallelism=4 (OWASP recommendations, 2023)
/// </summary>
public sealed class PasswordService : IPasswordService
{
    private const int SaltLength = 32;              // 256 bits
    private const int Iterations = 3;               // Time cost (verified with benchmarks: ~100ms per hash)
    private const int MemoryCost = 65536;           // 64MB memory
    private const int Parallelism = 4;              // Parallelism factor
    private const int DerivedKeyLength = 32;        // 256-bit output

    /// <summary>
    /// Hashes a password using Argon2id with random salt.
    /// Returns tuple of (hash, salt) both as base64 strings for database storage.
    /// </summary>
    public async Task<(string hash, string salt)> HashPasswordAsync(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be empty", nameof(password));

        // Generate random salt
        using (var rng = RandomNumberGenerator.Create())
        {
            byte[] saltBytes = new byte[SaltLength];
            rng.GetBytes(saltBytes);
            string saltBase64 = Convert.ToBase64String(saltBytes);

            // Hash password with Argon2id
            var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = saltBytes,
                DegreeOfParallelism = Parallelism,
                MemorySize = MemoryCost,
                Iterations = Iterations
            };

            byte[] hashBytes = await argon2.GetBytesAsync(DerivedKeyLength);
            string hashBase64 = Convert.ToBase64String(hashBytes);

            return (hashBase64, saltBase64);
        }
    }

    /// <summary>
    /// Verifies a password against its stored hash and salt.
    /// Uses constant-time comparison to prevent timing attacks.
    /// </summary>
    public async Task<bool> VerifyPasswordAsync(string password, string hash, string salt)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(salt))
            return false;

        try
        {
            byte[] saltBytes = Convert.FromBase64String(salt);
            byte[] storedHash = Convert.FromBase64String(hash);

            var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = saltBytes,
                DegreeOfParallelism = Parallelism,
                MemorySize = MemoryCost,
                Iterations = Iterations
            };

            byte[] computedHash = await argon2.GetBytesAsync(DerivedKeyLength);

            // Constant-time comparison (prevents timing attacks)
            return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
        }
        catch
        {
            // Any parsing errors → reject (prevents oracle attacks)
            return false;
        }
    }

    /// <summary>
    /// Validates password strength:
    /// - Minimum 8 characters
    /// - At least 1 uppercase letter (A-Z)
    /// - At least 1 lowercase letter (a-z)
    /// - At least 1 digit (0-9)
    /// - At least 1 special character (!@#$%^&*-_=+)
    /// </summary>
    public bool ValidatePasswordComplexity(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            return false;

        bool hasUppercase = password.Any(char.IsUpper);
        bool hasLowercase = password.Any(char.IsLower);
        bool hasDigit = password.Any(char.IsDigit);
        bool hasSpecialChar = password.Any(c => "!@#$%^&*-_=+".Contains(c));

        return hasUppercase && hasLowercase && hasDigit && hasSpecialChar;
    }
}
