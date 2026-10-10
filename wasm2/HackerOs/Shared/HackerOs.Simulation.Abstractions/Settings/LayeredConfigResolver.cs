using System.Globalization;

namespace HackerOs.Simulation.Abstractions.Settings;

/// <summary>Identifies which layer supplied the effective value of a setting (ADR 0044 section 3).</summary>
public enum SettingLayer
{
    /// <summary>The default declared by the app or OS schema.</summary>
    Default = 0,

    /// <summary>The system layer, <c>/etc/&lt;appId&gt;/&lt;document&gt;.config</c>.</summary>
    System = 1,

    /// <summary>The user layer, <c>~/.config/&lt;appId&gt;/&lt;document&gt;.config</c>.</summary>
    User = 2
}

/// <summary>
/// One problem found while reading a settings file. Readers never reject a whole file: the
/// offending line is ignored and the warning is written to the app's log (ADR 0044 section 5).
/// </summary>
/// <param name="Path">Absolute virtual path of the file that holds the problem.</param>
/// <param name="LineNumber">One-based line number of the problem.</param>
/// <param name="QualifiedKey">Qualified key concerned, or an empty string for a structural problem.</param>
/// <param name="Message">Human-readable description, without the path/line prefix.</param>
public sealed record LayeredConfigWarning(string Path, int LineNumber, string QualifiedKey, string Message)
{
    /// <summary>
    /// Formats the warning as one log line, e.g.
    /// <c>settings: /etc/x/settings.config:12: invalid value for 'tab-size' (expected int64), using default</c>.
    /// </summary>
    public string ToLogLine() => $"settings: {Path}:{LineNumber}: {Message}";
}

/// <summary>
/// The merged view of one settings document after layering defaults, <c>/etc</c> and <c>~/.config</c>.
/// </summary>
/// <param name="Values">Qualified key to effective serialized value, for every declared field.</param>
/// <param name="Sources">Qualified key to the layer that supplied <paramref name="Values"/>.</param>
/// <param name="LockedKeys">Qualified keys the system layer locks with its <c>[locked]</c> section.</param>
/// <param name="Warnings">Every problem found in either file, in file then line order.</param>
public sealed record LayeredConfigSnapshot(
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyDictionary<string, SettingLayer> Sources,
    IReadOnlySet<string> LockedKeys,
    IReadOnlyList<LayeredConfigWarning> Warnings);

/// <summary>
/// Pure merge engine shared by the SDK (<c>HackSettings</c>) and the platform
/// (<c>IOsSettingsService</c>), implementing ADR 0044 sections 3 to 6.
/// </summary>
/// <remarks>
/// <para>Effective value of a key = the first found in this order:</para>
/// <list type="number">
/// <item>a key locked by the system layer: the system value, or the default when the system layer
/// has no valid value for it; the user layer is ignored for that key;</item>
/// <item>the user layer;</item>
/// <item>the system layer;</item>
/// <item>the declared default.</item>
/// </list>
/// <para>
/// Malformed lines, undeclared keys and ill-typed values are ignored line by line, each producing
/// one <see cref="LayeredConfigWarning"/>; a duplicate key is not an error, the last one wins.
/// </para>
/// </remarks>
public static class LayeredConfigResolver
{
    /// <summary>Name of the reserved system-layer section that lists locked keys.</summary>
    public const string LockedSectionName = "locked";

    /// <summary>Reserved root key that records the schema version; never a data field.</summary>
    public const string SchemaVersionKey = "schemaVersion";

    /// <summary>Merges the two layers of one document over its schema defaults.</summary>
    /// <param name="schema">Declared fields, types and defaults.</param>
    /// <param name="systemContent">System file text, or <see langword="null"/> when the file is absent.</param>
    /// <param name="userContent">User file text, or <see langword="null"/> when the file is absent.</param>
    /// <param name="systemPath">System file path, used in warnings.</param>
    /// <param name="userPath">User file path, used in warnings.</param>
    /// <returns>The effective values, their sources, the locked keys and every warning.</returns>
    public static LayeredConfigSnapshot Resolve(
        SettingsSchema schema,
        string? systemContent,
        string? userContent,
        string systemPath,
        string userPath)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(systemPath);
        ArgumentNullException.ThrowIfNull(userPath);

        List<LayeredConfigWarning> warnings = [];
        HashSet<string> locked = new(StringComparer.Ordinal);
        Dictionary<string, (string Value, int Line)> systemValues = ReadLayer(
            schema, systemContent, systemPath, isSystem: true, locked, warnings, out List<PendingInvalid> systemInvalid);
        Dictionary<string, (string Value, int Line)> userValues = ReadLayer(
            schema, userContent, userPath, isSystem: false, locked, warnings, out List<PendingInvalid> userInvalid);

        Dictionary<string, string> values = new(StringComparer.Ordinal);
        Dictionary<string, SettingLayer> sources = new(StringComparer.Ordinal);
        foreach (SettingFieldDeclaration field in schema.Fields.Values)
        {
            string key = field.QualifiedKey;
            if (!locked.Contains(key) && userValues.TryGetValue(key, out (string Value, int Line) user))
            {
                values[key] = user.Value;
                sources[key] = SettingLayer.User;
            }
            else if (systemValues.TryGetValue(key, out (string Value, int Line) system))
            {
                values[key] = system.Value;
                sources[key] = SettingLayer.System;
            }
            else
            {
                values[key] = field.DefaultValue;
                sources[key] = SettingLayer.Default;
            }
        }

        // Invalid-value warnings name the value actually used instead, which is only known now.
        foreach (PendingInvalid invalid in systemInvalid.Concat(userInvalid))
        {
            string fallback = sources[invalid.Field.QualifiedKey] switch
            {
                SettingLayer.System => "using system value",
                SettingLayer.User => "using user value",
                _ => "using default"
            };
            warnings.Add(new LayeredConfigWarning(
                invalid.Path,
                invalid.Line,
                invalid.Field.QualifiedKey,
                $"invalid value for '{invalid.Field.QualifiedKey}' (expected {SettingValueCodec.DescribeExpected(invalid.Field)}), {fallback}"));
        }

        warnings.Sort(static (left, right) =>
        {
            int byPath = string.CompareOrdinal(left.Path, right.Path);
            return byPath != 0 ? byPath : left.LineNumber.CompareTo(right.LineNumber);
        });

        return new LayeredConfigSnapshot(values, sources, locked, warnings);
    }

    private static Dictionary<string, (string Value, int Line)> ReadLayer(
        SettingsSchema schema,
        string? content,
        string path,
        bool isSystem,
        HashSet<string> locked,
        List<LayeredConfigWarning> warnings,
        out List<PendingInvalid> invalid)
    {
        Dictionary<string, (string Value, int Line)> values = new(StringComparer.Ordinal);
        invalid = [];
        if (content is null)
        {
            return values;
        }

        bool userLockedSectionReported = false;
        foreach (ConfigLine line in ConfigDocumentLines.Parse(content))
        {
            switch (line.Kind)
            {
                case ConfigLineKind.Malformed:
                    warnings.Add(new LayeredConfigWarning(path, line.LineNumber, string.Empty, "malformed line ignored"));
                    continue;
                case ConfigLineKind.Section when line.Group == LockedSectionName && !isSystem && !userLockedSectionReported:
                    userLockedSectionReported = true;
                    warnings.Add(new LayeredConfigWarning(
                        path, line.LineNumber, string.Empty, "[locked] is only honored in the system layer, section ignored"));
                    continue;
                case ConfigLineKind.KeyValue:
                    break;
                default:
                    continue;
            }

            if (line.InMalformedSection)
            {
                // Already reported once through the malformed header itself.
                continue;
            }

            if (line.Group == LockedSectionName)
            {
                if (isSystem)
                {
                    ReadLockLine(schema, line, path, locked, warnings);
                }

                continue;
            }

            string qualifiedKey = line.QualifiedKey!;
            if (line.Group is null && line.Key == SchemaVersionKey)
            {
                continue;
            }

            if (!schema.Fields.TryGetValue(qualifiedKey, out SettingFieldDeclaration? field))
            {
                warnings.Add(new LayeredConfigWarning(
                    path, line.LineNumber, qualifiedKey, $"unknown key '{qualifiedKey}' ignored"));
                continue;
            }

            if (!SettingValueCodec.TryNormalize(field, line.Value!, out string normalized))
            {
                // A later valid duplicate may still win; an invalid line never erases a valid one.
                invalid.Add(new PendingInvalid(field, path, line.LineNumber));
                continue;
            }

            values[qualifiedKey] = (normalized, line.LineNumber);
        }

        return values;
    }

    private static void ReadLockLine(
        SettingsSchema schema,
        ConfigLine line,
        string path,
        HashSet<string> locked,
        List<LayeredConfigWarning> warnings)
    {
        // Inside [locked] the "key" is itself a qualified key such as "Editor.tab-size"; the parser
        // splits on '=' only, so dots in the key survive intact.
        string target = line.Key!;
        if (!schema.Fields.ContainsKey(target))
        {
            warnings.Add(new LayeredConfigWarning(
                path, line.LineNumber, target, $"cannot lock unknown key '{target}', ignored"));
            return;
        }

        switch (line.Value!.Trim())
        {
            case "true":
                locked.Add(target);
                break;
            case "false":
                locked.Remove(target);
                break;
            default:
                warnings.Add(new LayeredConfigWarning(
                    path, line.LineNumber, target, $"invalid lock value for '{target}' (expected boolean), ignored"));
                break;
        }
    }

    private sealed record PendingInvalid(SettingFieldDeclaration Field, string Path, int Line);
}

/// <summary>
/// Validates and normalizes serialized setting values against their declared type.
/// </summary>
public static class SettingValueCodec
{
    /// <summary>Checks a serialized value and returns its canonical form.</summary>
    /// <param name="field">Declared field.</param>
    /// <param name="value">Serialized value as read from a file.</param>
    /// <param name="normalized">Canonical serialized value (e.g. an integer without leading <c>+</c>).</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is valid for <paramref name="field"/>.</returns>
    public static bool TryNormalize(SettingFieldDeclaration field, string value, out string normalized)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(value);
        normalized = value;
        switch (field.ValueType)
        {
            case SettingValueType.Integer:
                if (!long.TryParse(value.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long number))
                {
                    return false;
                }

                normalized = number.ToString(CultureInfo.InvariantCulture);
                return true;
            case SettingValueType.Boolean:
                normalized = value.Trim();
                return normalized is "true" or "false";
            case SettingValueType.Enum:
                normalized = value.Trim();
                return field.AllowedValues is not null && field.AllowedValues.Contains(normalized, StringComparer.Ordinal);
            case SettingValueType.String:
            default:
                return true;
        }
    }

    /// <summary>Describes the expected type for warnings, e.g. <c>int64</c> or <c>one of: dark, light</c>.</summary>
    /// <param name="field">Declared field.</param>
    public static string DescribeExpected(SettingFieldDeclaration field)
    {
        ArgumentNullException.ThrowIfNull(field);
        return field.ValueType switch
        {
            SettingValueType.Integer => "int64",
            SettingValueType.Boolean => "boolean",
            SettingValueType.Enum => $"one of: {string.Join(", ", field.AllowedValues ?? [])}",
            _ => "string"
        };
    }
}
