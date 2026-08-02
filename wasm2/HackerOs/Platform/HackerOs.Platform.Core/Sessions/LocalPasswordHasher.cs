using System.Security.Cryptography;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Core.Sessions;

/// <summary>
/// Creates and verifies <see cref="LocalPasswordCredential"/> records using PBKDF2-HMAC-SHA256,
/// per ADR 0013. Comparison is constant-time to avoid timing side channels.
/// </summary>
public static class LocalPasswordHasher
{
    /// <summary>Identifier for the key-derivation function and parameters produced by this version.</summary>
    public const string KdfIdentifier = "pbkdf2-sha256-v1";

    private const int SaltLengthBytes = 16;
    private const int VerifierLengthBytes = 32;
    private const int DefaultIterations = 210_000;

    /// <summary>Creates a new salted credential for a plaintext password.</summary>
    /// <param name="password">Plaintext password; never stored.</param>
    /// <param name="iterations">PBKDF2 work factor; higher is slower but more resistant to brute force.</param>
    /// <exception cref="ArgumentException"><paramref name="password"/> is empty.</exception>
    public static LocalPasswordCredential Create(string password, int iterations = DefaultIterations)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltLengthBytes);
        byte[] verifier = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, iterations, HashAlgorithmName.SHA256, VerifierLengthBytes);

        return new LocalPasswordCredential(KdfIdentifier, salt, iterations, verifier);
    }

    /// <summary>Verifies a plaintext password against a stored credential in constant time.</summary>
    /// <param name="password">Plaintext password to verify.</param>
    /// <param name="credential">Stored credential to verify against.</param>
    /// <returns><see langword="true"/> if the password matches; <see langword="false"/> for a mismatch or an unrecognized KDF version.</returns>
    public static bool Verify(string password, LocalPasswordCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);

        if (credential.KdfIdentifier != KdfIdentifier)
        {
            // Fail closed: never attempt to verify against a KDF version we no longer trust.
            return false;
        }

        byte[] computed = Rfc2898DeriveBytes.Pbkdf2(
            password, credential.Salt, credential.Iterations, HashAlgorithmName.SHA256, credential.Verifier.Length);

        return CryptographicOperations.FixedTimeEquals(computed, credential.Verifier);
    }
}
