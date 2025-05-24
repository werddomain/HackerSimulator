using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace HackerSimulator.Wasm.Core
{
    public class EncryptionService
    {
        public byte[] DeriveKey(string secret)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes(secret));
        }

        public string Encrypt(string plaintext, byte[] key)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.GenerateIV();
            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plaintext);
            var cipher = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            var result = new byte[aes.IV.Length + cipher.Length];
            aes.IV.CopyTo(result, 0);
            cipher.CopyTo(result, aes.IV.Length);
            return Convert.ToBase64String(result);
        }

        public string Decrypt(string encrypted, byte[] key)
        {
            var bytes = Convert.FromBase64String(encrypted);
            using var aes = Aes.Create();
            aes.Key = key;
            var iv = new byte[aes.BlockSize / 8];
            Array.Copy(bytes, iv, iv.Length);
            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor();
            var cipher = new byte[bytes.Length - iv.Length];
            Array.Copy(bytes, iv.Length, cipher, 0, cipher.Length);
            var plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            return Encoding.UTF8.GetString(plain);
        }
    }
}
