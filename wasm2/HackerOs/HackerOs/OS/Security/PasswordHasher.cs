using System;
using System.Security.Cryptography;

namespace HackerOs.OS.Security
{
    /// <summary>
    /// Provides password hashing and verification services using BCrypt
    /// </summary>
    public static class PasswordHasher
    {
        // BCrypt work factor - higher is more secure but slower
        private const int BcryptWorkFactor = 12;
        
        /// <summary>
        /// Hashes a password using BCrypt
        /// </summary>
        /// <param name="password">The plain text password to hash</param>
        /// <returns>The BCrypt hash with embedded salt</returns>
        public static string HashPassword(string password)
        {
            // BCrypt.Net-Next automatically generates a salt and includes it in the hash
            return BCrypt.Net.BCrypt.HashPassword(password, BcryptWorkFactor);
        }
        
        /// <summary>
        /// Verifies a password against a BCrypt hash
        /// </summary>
        /// <param name="password">The plain text password to verify</param>
        /// <param name="hashedPassword">The BCrypt hash to compare against</param>
        /// <returns>True if the password matches, false otherwise</returns>
        public static bool VerifyPassword(string password, string hashedPassword)
        {
            if (string.IsNullOrEmpty(hashedPassword))
                return false;
                
            // Check if this is a BCrypt hash (starts with $2a$, $2b$, or $2y$)
            if (hashedPassword.StartsWith("$2"))
            {
                return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
            }
            
            // Legacy hash format handling (for migration)
            return false;
        }
        
        /// <summary>
        /// Checks if a hash needs to be upgraded to a newer format or work factor
        /// </summary>
        /// <param name="hashedPassword">The hash to check</param>
        /// <returns>True if upgrade is needed, false otherwise</returns>
        public static bool NeedsUpgrade(string hashedPassword)
        {
            // Not a BCrypt hash, needs upgrade
            if (!hashedPassword.StartsWith("$2"))
                return true;
                
            try
            {
                // Extract work factor from BCrypt hash
                string workFactorStr = hashedPassword.Split('$')[2];
                int workFactor = int.Parse(workFactorStr);
                
                // If work factor is lower than current, needs upgrade
                return workFactor < BcryptWorkFactor;
            }
            catch
            {
                // If we can't parse it properly, better upgrade it
                return true;
            }
        }
        
        /// <summary>
        /// Upgrades a password hash to the latest format, if verification succeeds
        /// </summary>
        /// <param name="password">The plain text password</param>
        /// <param name="currentHash">The current hash</param>
        /// <returns>A new hash if upgrade needed, or null if verification failed</returns>
        public static string? UpgradePasswordHashIfNeeded(string password, string currentHash)
        {
            // Verify the password against the current hash
            if (!VerifyPassword(password, currentHash))
                return null; // Verification failed
                
            // Check if hash needs upgrade
            if (NeedsUpgrade(currentHash))
            {
                // Generate a new hash with current standards
                return HashPassword(password);
            }
            
            // No upgrade needed
            return null;
        }
        
        /// <summary>
        /// Migrates a password from legacy PBKDF2 format to BCrypt
        /// </summary>
        /// <param name="password">The plain text password</param>
        /// <param name="legacyHash">The legacy hash</param>
        /// <param name="salt">The salt used for the legacy hash</param>
        /// <returns>A new BCrypt hash if verification succeeds, or null if it fails</returns>
        public static string? MigrateFromLegacyHash(string password, string legacyHash, string salt)
        {
            try
            {
                // Recreate the legacy hash for verification
                byte[] saltBytes = Convert.FromBase64String(salt);
                using (var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 10000, HashAlgorithmName.SHA256))
                {
                    byte[] hashBytes = pbkdf2.GetBytes(32);
                    string computedHash = Convert.ToBase64String(hashBytes);
                    
                    // Check if the hashes match
                    if (computedHash == legacyHash)
                    {
                        // Verified, create a new BCrypt hash
                        return HashPassword(password);
                    }
                }
            }
            catch
            {
                // If anything goes wrong, return null
            }
            
            return null;
        }
    }
}
