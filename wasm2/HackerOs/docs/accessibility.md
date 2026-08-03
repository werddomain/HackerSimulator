# HackerOS Accessibility & WCAG 2.2 AA Compliance Guide

## Overview

HackerOS is designed to meet **WCAG 2.2 Level AA** accessibility standards across the Desktop Shell, window runtime, file dialogs, MudBlazor platform wrappers, and first-party applications.

---

## 1. Key Accessibility Standards

### A. Color Contrast & Legibility
- **Text Contrast:** Text against background surfaces maintains at least a **4.5:1** contrast ratio (e.g. `#c9d1d9` text on `#0d1117` background = 14.2:1).
- **UI Component Boundaries:** Interactive controls, focus rings, and border indicators maintain at least a **3:1** contrast ratio.

### B. Keyboard Navigation & Focus Management
- **Full Keyboard Accessibility:** Every interactive button, menu item, window control, input field, and tree node is reachable and operable via keyboard.
- **Focus Indicators:** All focusable elements show a distinct, high-contrast focus ring:
  ```css
  :focus-visible {
    outline: 2px solid var(--hackeros-border-focus, #58a6ff);
    outline-offset: 2px;
  }
  ```
- **Modal Dialog Trapping:** Modal file dialogs and prompt popups trap keyboard focus within the dialog container until closed or cancelled using `Escape`.

### C. WAI-ARIA & Screen Reader Semantics
- **Window Windows:** Use `role="region"` or `role="dialog"` with `aria-labelledby` pointing to the window title bar.
- **Modal Dialogs:** Use `role="dialog" aria-modal="true"` with explicit `aria-label` or `aria-labelledby`.
- **Taskbar & Launcher:** Use `role="toolbar"`, `role="navigation"`, and `role="menu"` with `aria-expanded` and `aria-haspopup` attributes.

### D. Reduced Motion Support
- Users requesting reduced motion via operating system settings have CSS transitions and animations disabled automatically:
  ```css
  @media (prefers-reduced-motion: reduce) {
    *, ::before, ::after {
      animation-duration: 0.01ms !important;
      transition-duration: 0.01ms !important;
    }
  }
  ```

---

## 2. Automated & Manual Accessibility Testing

1. **Automated E2E Check:** Playwright E2E test suite incorporates `@axe-core/playwright` audits to verify 0 accessibility violations during shell and app launches.
2. **Keyboard Checklist:**
   - [x] Launch Terminal via Start Menu using `Tab` + `Enter`.
   - [x] Move window using Keyboard shortcuts (`Alt+F7` + Arrow keys).
   - [x] Close modal dialog using `Escape`.
   - [x] Navigate File Explorer tree using `Up`/`Down`/`Right`/`Left` arrow keys.
