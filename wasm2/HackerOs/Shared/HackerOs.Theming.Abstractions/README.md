# HackerOs.Theming.Abstractions

Framework-neutral contracts for the built-in HackerOS theme system. The package exposes the
stable desktop/mobile theme IDs, the bounded accent IDs, `ThemeCatalog`, and the persisted
`ThemePreferences` value. It has no Blazor, browser, host, or server dependency.

```csharp
ThemeDefinition theme = ThemeCatalog.Get(WellKnownThemeIds.Windows7);
IReadOnlyList<ThemeDefinition> mobileThemes = ThemeCatalog.GetThemes(ThemePlatform.Mobile);
```

Theme IDs are exact, ordinal, and data-only. Consumers should validate persisted or external
values with `ThemeCatalog.TryGet` and `ThemeCatalog.IsKnownAccent` before rendering them.
