# HackerOS Accessibility Evidence and WCAG 2.2 AA Checklist

## Overview

HackerOS targets **WCAG 2.2 Level AA**. This document records the intended contract separately from evidence that has actually been collected. It is not a blanket conformance claim.

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

1. **Automated representative check:** `IndexedDbBrowserContractTests.Representative_platform_surfaces_have_no_axe_violations` uses `Deque.AxeCore.Playwright` 4.12.0 and currently verifies that the idle desktop, a window, a dialog, the full-screen Terminal renderer, the local CodeMirror surface, and the Hack Paint canvas surface contain no serious or critical axe findings. Moderate findings are still reported by axe and are not treated as resolved.
2. **Required expanded automation:** The launcher, taskbar, complete Terminal window, File Explorer, Text Editor, Settings, complete Code Editor window, and the complete Hack Paint app still need dedicated scans. Keyboard order/traps/restoration, Escape behavior, 200% zoom, mobile layout, long text, reduced motion, and RTL also require executable coverage.
3. **Human evidence checklist:** These items remain unchecked until a person performs the steps and records the browser/OS/assistive-technology versions and artifacts.
   - [ ] Launch Terminal from the Start menu using only `Tab`, arrow keys, and `Enter`.
   - [ ] Move and resize a window with the documented keyboard commands and confirm a visible focus indicator throughout.
   - [ ] Open a modal dialog, verify focus cannot escape it, close it with `Escape`, and confirm focus returns to the invoking control.
   - [ ] Navigate the File Explorer tree using `Up`, `Down`, `Right`, and `Left` and verify state changes are announced.
   - [ ] Complete a screen-reader pass over the desktop, launcher, taskbar, window chrome, dialogs, and each first-party app.

Store Playwright traces, screenshots, console output, and network logs under `artifacts/playwright/`. Store human test notes and screen-reader recordings under `artifacts/accessibility/manual/<date>/`. Do not check a human-evidence item from automated output alone.
