using System.Text.Json;
using HackerOs.App.Abstractions;
using HackerOs.Platform.Core.Discovery;

namespace HackerOs.Tools.ManifestValidator;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            Console.WriteLine("HackerOS App Manifest Validator CLI v1.0");
            Console.WriteLine("Usage: hackeros-validate <path-to-app.manifest.json>");
            return 0;
        }

        string manifestPath = args[0];
        if (!File.Exists(manifestPath))
        {
            Console.Error.WriteLine($"[ERROR] File not found: '{manifestPath}'");
            return 1;
        }

        try
        {
            string json = File.ReadAllText(manifestPath);
            AppManifest? manifest = JsonSerializer.Deserialize<AppManifest>(json, AppManifestJsonSerializerOptions.Default);

            if (manifest is null)
            {
                Console.Error.WriteLine($"[ERROR] Deserialization produced null manifest from '{manifestPath}'");
                return 1;
            }

            IReadOnlyList<AppCatalogError> errors = AppManifestValidation.Validate(manifest);
            if (errors.Count == 0)
            {
                Console.WriteLine($"[OK] Manifest '{manifest.Id}' v{manifest.Version} is VALID.");
                return 0;
            }

            Console.Error.WriteLine($"[INVALID] Manifest '{manifestPath}' has {errors.Count} validation error(s):");
            foreach (AppCatalogError error in errors)
            {
                Console.Error.WriteLine($"  - [{error.Code}] {error.Message}");
            }
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] Exception validating manifest: {ex.Message}");
            return 1;
        }
    }
}
