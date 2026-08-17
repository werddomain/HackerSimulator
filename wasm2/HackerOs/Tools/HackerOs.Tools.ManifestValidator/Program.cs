using System.Text.Json;
using HackerOs.App.Abstractions;

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
            AppManifest? manifest = JsonSerializer.Deserialize(
                json, ManifestValidatorJsonContext.Default.AppManifest);

            if (manifest is null)
            {
                Console.Error.WriteLine($"[ERROR] Deserialization produced null manifest from '{manifestPath}'");
                return 1;
            }

            // Source-generated deserialization assigns null for omitted interface-typed
            // collection properties. Normalize those optional fields before validation;
            // explicit nulls are treated the same as an omitted optional collection.
            manifest = manifest with
            {
                Localizations = manifest.Localizations ?? [],
                Capabilities = manifest.Capabilities ?? [],
                Intents = manifest.Intents ?? [],
                Dependencies = manifest.Dependencies ?? [],
                Assets = manifest.Assets ?? [],
                FileHandlers = manifest.FileHandlers ?? []
            };

            ManifestValidationResult validation = AppManifestValidator.Validate(manifest);
            if (validation.IsValid)
            {
                Console.WriteLine($"[OK] Manifest '{manifest.Id}' v{manifest.Version} is VALID.");
                return 0;
            }

            Console.Error.WriteLine($"[INVALID] Manifest '{manifestPath}' has {validation.Errors.Count} validation error(s):");
            foreach (ManifestValidationError error in validation.Errors)
            {
                Console.Error.WriteLine($"  - [{error.Code}] {error.Path}: {error.Message}");
            }
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] Exception validating manifest: {ex}");
            return 1;
        }
    }
}
