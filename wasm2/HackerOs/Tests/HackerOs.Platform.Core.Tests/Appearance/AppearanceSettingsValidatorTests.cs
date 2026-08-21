using HackerOs.Platform.Core.Appearance;

namespace HackerOs.Platform.Core.Tests.Appearance;

public sealed class AppearanceSettingsValidatorTests
{
    private readonly AppearanceSettingsValidator _validator = new();

    [Fact]
    public void Validate_DefaultDocumentContent_IsValid()
    {
        Assert.Empty(_validator.Validate(AppearanceSettingsDocuments.EmptyDocumentContent));
    }

    [Fact]
    public void Validate_LegacyVersionOneDocument_RemainsValidForMigration()
    {
        Assert.Empty(_validator.Validate(
            """{"schemaVersion":1,"accent":"cyan","animationsEnabled":false}"""));
    }

    [Fact]
    public void Validate_VersionTwoThemeFromWrongPlatform_ReportsAnError()
    {
        List<string> errors = [.. _validator.Validate(
            """{"schemaVersion":2,"desktopThemeId":"android","mobileThemeId":"ios","accent":"green","animationsEnabled":true}""")];

        Assert.Contains("appearance.desktop-theme-invalid", errors);
    }

    [Fact]
    public void Validate_UnknownAccent_ReportsAnError()
    {
        List<string> errors = [.. _validator.Validate(
            """{"schemaVersion":1,"accent":"chartreuse","animationsEnabled":true}""")];

        Assert.Contains("appearance.accent-invalid", errors);
    }

    [Fact]
    public void Validate_MissingAnimationsFlag_ReportsAnError()
    {
        List<string> errors = [.. _validator.Validate("""{"schemaVersion":1,"accent":"green"}""")];

        Assert.Contains("appearance.animations-enabled-invalid", errors);
    }

    [Fact]
    public void Validate_MalformedJson_ReportsAnError()
    {
        Assert.Contains("settings.json-invalid", _validator.Validate("{not json"));
    }
}
