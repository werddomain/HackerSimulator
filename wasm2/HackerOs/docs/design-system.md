# HackerOS Design System & Visual Tokens Specification

## Overview

The HackerOS Design System enforces a **Modern Gothic / Hacker Console** aesthetic across the shell, platform controls, windows, dialogs, and first-party applications.
It leverages MudBlazor platform wrappers combined strictly with component-scoped CSS files (`.razor.css`) and global CSS Custom Properties (`app.css`).

---

## 1. Core Color Palette & Design Tokens

All colors, surfaces, borders, and status indicators are controlled via CSS custom properties declared in `wwwroot/css/app.css`:

```css
:root {
  /* Surfaces */
  --hackeros-bg-deep: #090d12;
  --hackeros-surface-bg: #0d1117;
  --hackeros-header-bg: #161b22;
  --hackeros-card-bg: #21262d;
  --hackeros-popover-bg: #1c2128;

  /* Typography & Text Colors */
  --hackeros-text-primary: #c9d1d9;
  --hackeros-text-secondary: #8b949e;
  --hackeros-text-disabled: #484f58;
  --hackeros-text-accent: #42d392;
  --hackeros-text-link: #58a6ff;

  /* Borders & Dividers */
  --hackeros-border: #30363d;
  --hackeros-border-subtle: #21262d;
  --hackeros-border-focus: #58a6ff;

  /* Accent & Glow Effects */
  --hackeros-accent: #42d392;
  --hackeros-accent-glow: rgba(66, 211, 146, 0.25);
  --hackeros-cyan: #38bdf8;
  --hackeros-purple: #c084fc;

  /* Status Colors */
  --hackeros-status-success: #3fb950;
  --hackeros-status-warning: #d29922;
  --hackeros-status-danger: #f85149;
  --hackeros-status-info: #58a6ff;

  /* Motion & Transitions */
  --hackeros-transition-fast: 120ms ease;
  --hackeros-transition-normal: 200ms ease;

  /* Z-Index Hierarchy */
  --hackeros-z-desktop: 1;
  --hackeros-z-window: 100;
  --hackeros-z-taskbar: 1000;
  --hackeros-z-modal: 5000;
  --hackeros-z-notification: 9000;
}
```

---

## 2. Component Styling Rules (AGENTS.md Compliance)

1. **Scoped CSS Files Mandatory:**
   - Every Blazor component (`MyComponent.razor`) **MUST** place its component-specific styles in a matching scoped CSS file (`MyComponent.razor.css`).
   - Inline styles (`style="..."`) and embedded `<style>` tags in `.razor` files are **PROHIBITED**.

2. **MudBlazor Wrapper Integration:**
   - Use MudBlazor controls (`MudMenu`, `MudTabs`, `MudDataGrid`, `MudButton`) wrapped in platform components to maintain visual consistency.
   - MudBlazor themes are configured to map directly to HackerOS custom tokens.

---

## 3. Theme Boundary Security (DECISION: D-013)

Themes in HackerOS customize visual appearance by altering CSS Custom Properties and JSON color values in `/etc/hackeros/theme.json`.
- **PROHIBITED:** Themes are data-only and static CSS assets. Themes CANNOT inject arbitrary JavaScript scripts, dynamic code, or unverified executable bundles into the browser runtime context.
