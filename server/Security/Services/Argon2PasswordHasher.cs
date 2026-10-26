using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Security.Services;

// Based on the supplied authorization-frontend-backend exercise.
public sealed class Argon2PasswordHasher
{
    private const int MemoryKiB = 65536, Iterations = 3, Parallelism = 4;
    // Unknown usernames still perform a password verification.
    public string DummyHash { get; } = Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Derive(password, salt);
        return $"$argon2id$v=19$m={MemoryKiB},t={Iterations},p={Parallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string encoded)
    {
        try
        {
            var parts = encoded.Split('$');
            if (parts.Length != 6 || parts[1] != "argon2id" || parts[2] != "v=19" ||
                parts[3] != $"m={MemoryKiB},t={Iterations},p={Parallelism}") return false;
            var salt = Convert.FromBase64String(parts[4]);
            var expected = Convert.FromBase64String(parts[5]);
            return salt.Length == 16 && expected.Length == 32 &&
                CryptographicOperations.FixedTimeEquals(expected, Derive(password, salt));
        }
        catch (FormatException) { return false; }
    }

    private static byte[] Derive(string password, byte[] salt)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt, MemorySize = MemoryKiB, Iterations = Iterations,
            DegreeOfParallelism = Parallelism
        };
        return argon.GetBytes(32);
    }
}
