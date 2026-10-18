using System.Text;
using System.Text.Json;

namespace HackerOs.Platform.Core.Keyring;

/// <summary>One decrypted entry of a keyring.</summary>
/// <param name="AppId">Owning app.</param>
/// <param name="Id">Entry id, unique per app.</param>
/// <param name="Label">Optional label.</param>
/// <param name="Secret">The secret.</param>
/// <param name="UpdatedAtUtc">Last write.</param>
internal sealed record KeyringEntryRecord(string AppId, string Id, string? Label, string Secret, DateTimeOffset UpdatedAtUtc);

/// <summary>The sealed envelope of <c>login.keyring</c>: KDF parameters, nonce and ciphertext.</summary>
internal sealed record KeyringEnvelope(int Iterations, byte[] Salt, byte[] Nonce, byte[] SealedData);

/// <summary>
/// Text format of <c>~/.local/share/keyrings/login.keyring</c> (ADR 0100 section 2): a small JSON
/// envelope (format, version, KDF parameters, base64 salt, nonce and ciphertext), written by hand
/// with <see cref="Utf8JsonWriter"/> so it needs no reflection. Only the envelope is readable; the
/// entries are inside the AES-GCM ciphertext.
/// </summary>
internal static class KeyringFileFormat
{
    public const string FormatName = "hackeros-keyring";

    public const int Version = 1;

    public const string KdfName = "pbkdf2-sha256";

    public const string CipherName = "aes-256-gcm";

    public static string WriteEnvelope(KeyringEnvelope envelope)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("format", FormatName);
            writer.WriteNumber("version", Version);
            writer.WriteString("kdf", KdfName);
            writer.WriteNumber("iterations", envelope.Iterations);
            writer.WriteString("salt", Convert.ToBase64String(envelope.Salt));
            writer.WriteString("cipher", CipherName);
            writer.WriteString("nonce", Convert.ToBase64String(envelope.Nonce));
            writer.WriteString("ciphertext", Convert.ToBase64String(envelope.SealedData));
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    /// <summary>Parses an envelope, or returns <see langword="null"/> for an unknown or damaged file.</summary>
    public static KeyringEnvelope? ReadEnvelope(string text)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            JsonElement root = document.RootElement;
            if (root.GetProperty("format").GetString() != FormatName
                || root.GetProperty("version").GetInt32() != Version
                || root.GetProperty("kdf").GetString() != KdfName
                || root.GetProperty("cipher").GetString() != CipherName)
            {
                return null;
            }

            return new KeyringEnvelope(
                root.GetProperty("iterations").GetInt32(),
                Convert.FromBase64String(root.GetProperty("salt").GetString()!),
                Convert.FromBase64String(root.GetProperty("nonce").GetString()!),
                Convert.FromBase64String(root.GetProperty("ciphertext").GetString()!));
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or FormatException or InvalidOperationException)
        {
            return null;
        }
    }

    public static byte[] WriteEntries(IEnumerable<KeyringEntryRecord> entries)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("entries");
            foreach (KeyringEntryRecord entry in entries)
            {
                writer.WriteStartObject();
                writer.WriteString("app", entry.AppId);
                writer.WriteString("id", entry.Id);
                if (entry.Label is not null)
                {
                    writer.WriteString("label", entry.Label);
                }

                writer.WriteString("secret", entry.Secret);
                writer.WriteString("updated", entry.UpdatedAtUtc);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    /// <summary>Parses decrypted entries, or returns <see langword="null"/> when they are damaged.</summary>
    public static List<KeyringEntryRecord>? ReadEntries(byte[] plaintext)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(plaintext);
            List<KeyringEntryRecord> entries = [];
            foreach (JsonElement entry in document.RootElement.GetProperty("entries").EnumerateArray())
            {
                entries.Add(new KeyringEntryRecord(
                    entry.GetProperty("app").GetString()!,
                    entry.GetProperty("id").GetString()!,
                    entry.TryGetProperty("label", out JsonElement label) ? label.GetString() : null,
                    entry.GetProperty("secret").GetString()!,
                    entry.GetProperty("updated").GetDateTimeOffset()));
            }

            return entries;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or FormatException or InvalidOperationException)
        {
            return null;
        }
    }
}
