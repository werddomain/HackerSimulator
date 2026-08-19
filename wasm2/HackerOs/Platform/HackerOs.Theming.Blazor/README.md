# HackerOs.Theming.Blazor

Native Blazor theme composition for HackerOS and for the reusable window/taskbar packages. The
Razor class library owns the offline static asset `_content/HackerOs.Theming.Blazor/themes.css`,
the root `ThemeScope`, and the token-driven `ThemePreview` used by appearance selectors.

```razor
@using HackerOs.Theming.Abstractions
@using HackerOs.Theming.Blazor

<ThemeScope ThemeId="@WellKnownThemeIds.HackerOs"
            Platform="ThemePlatform.Desktop"
            AccentId="@WellKnownAccentIds.Green"
            AnimationsEnabled="true">
    @* Shell, WindowHost, and Taskbar content *@
</ThemeScope>
```

Render one `ThemeScope` around a shell root. All attribute values are validated against
`ThemeCatalog`; arbitrary CSS, remote fonts, images, scripts, and theme URLs are not accepted.
