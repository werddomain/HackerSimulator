// Bundled by build.mjs into wwwroot/xterm/xterm.bundle.js (plus xterm.bundle.css for the
// stylesheet import below). Only TerminalPanel.razor.js imports it: xterm.js stays private to
// the terminal library, which exposes nothing but "write bytes", "focus" and "dispose" to C#.
//
// xterm.js is used exactly as a hardware terminal is (ADR 0061): it renders the bytes the
// shell and its commands write (ECMA-48 control sequences included) and sends back the bytes
// the keyboard produces. Line editing, history, completion and the alternate-screen frames
// are all decided in C# (TerminalPanel, TerminalLineEditor, TerminalInputDecoder).
import { Terminal } from "@xterm/xterm";
import { FitAddon } from "@xterm/addon-fit";
import "@xterm/xterm/css/xterm.css";

// Loaded once per module URL (the browser caches dynamic import()s), so the stylesheet
// injection only ever runs a single time.
const stylesheetHref = new URL("./xterm.bundle.css", import.meta.url).href;
if (!document.querySelector(`link[href="${stylesheetHref}"]`)) {
  const link = document.createElement("link");
  link.rel = "stylesheet";
  link.href = stylesheetHref;
  document.head.appendChild(link);
}

// The 16-colour ANSI palette. Kept close to the legacy terminal's (src/apps/terminal.ts) so
// coloured output looks the same, with HackerOS's accent green. Background, foreground and
// cursor come from the design tokens of the host element (see themeFor).
const ANSI_PALETTE = {
  black: "#1b2126",
  red: "#ff5c57",
  green: "#42d392",
  yellow: "#f3f99d",
  blue: "#57c7ff",
  magenta: "#ff6ac1",
  cyan: "#9aedfe",
  white: "#e6edf1",
  brightBlack: "#6b7b86",
  brightRed: "#ff8a85",
  brightGreen: "#69ff94",
  brightYellow: "#ffffa5",
  brightBlue: "#8cd9ff",
  brightMagenta: "#ff92df",
  brightCyan: "#c4f7ff",
  brightWhite: "#ffffff"
};

function cssValue(style, name, fallback) {
  const value = style.getPropertyValue(name).trim();
  return value.length > 0 ? value : fallback;
}

function themeFor(host) {
  const style = getComputedStyle(host);
  const accent = cssValue(style, "--hos-accent", "#42d392");
  return {
    ...ANSI_PALETTE,
    background: cssValue(style, "--hos-bg-base", "#0d1114"),
    foreground: cssValue(style, "--hos-text-primary", "#f4f7f8"),
    cursor: accent,
    cursorAccent: cssValue(style, "--hos-bg-base", "#0d1114"),
    selectionBackground: "rgba(66, 211, 146, 0.3)",
    // xterm.js draws its own scrollbar: tint its slider with the accent.
    scrollbarSliderBackground: "rgba(66, 211, 146, 0.25)",
    scrollbarSliderHoverBackground: "rgba(66, 211, 146, 0.45)",
    scrollbarSliderActiveBackground: "rgba(66, 211, 146, 0.6)"
  };
}

/**
 * Browser shortcuts the terminal must leave alone so the surrounding UI still gets them:
 * the console's tab shortcuts (TerminalTabs, ADR 0058 section 4) and the clipboard ones.
 * Returning false from xterm's custom key handler lets the event bubble untouched.
 */
function isHostShortcut(event) {
  if (!event.ctrlKey || event.altKey || event.metaKey) {
    return false;
  }

  const key = event.key.toLowerCase();
  if (event.shiftKey && (key === "t" || key === "w")) {
    return true;
  }

  return !event.shiftKey && (event.key === "PageUp" || event.key === "PageDown");
}

/**
 * Creates an xterm.js terminal inside `host` and wires it to the TerminalPanel .NET object:
 * keyboard bytes go to OnTerminalData, the character-cell size to UpdateTerminalViewport.
 */
export function createTerminal(host, dotNet, options) {
  const style = getComputedStyle(host);
  const terminal = new Terminal({
    cursorBlink: true,
    cursorStyle: "block",
    fontFamily: style.fontFamily || "monospace",
    fontSize: Number.parseFloat(style.fontSize) || 14,
    lineHeight: 1.2,
    scrollback: options?.scrollback ?? 5000,
    allowProposedApi: false,
    theme: themeFor(host)
  });
  const fit = new FitAddon();
  terminal.loadAddon(fit);
  terminal.open(host);

  const textarea = terminal.textarea;
  if (textarea) {
    textarea.setAttribute("aria-label", options?.ariaLabel ?? "Terminal command input");
    if (options?.useVirtualKeyboard) {
      // A touch focus must not open the platform keyboard: HackerOS's own keyboard types.
      textarea.setAttribute("inputmode", "none");
    }
  }

  let disposed = false;
  let lastColumns = 0;
  let lastRows = 0;

  const resize = () => {
    // A hidden panel (an inactive terminal tab) has no size: keep its last viewport.
    if (disposed || host.clientWidth === 0 || host.clientHeight === 0) {
      return;
    }

    try {
      fit.fit();
    } catch {
      return;
    }

    if (terminal.cols !== lastColumns || terminal.rows !== lastRows) {
      lastColumns = terminal.cols;
      lastRows = terminal.rows;
      // Fire-and-forget: a resize can arrive after the .NET side disposed this panel.
      dotNet.invokeMethodAsync("UpdateTerminalViewport", terminal.cols, terminal.rows).catch(() => {});
    }
  };

  const observer = new ResizeObserver(resize);
  observer.observe(host);
  resize();

  terminal.attachCustomKeyEventHandler((event) => {
    if (event.type !== "keydown") {
      return true;
    }

    if (isHostShortcut(event)) {
      return false;
    }

    const key = event.key.toLowerCase();
    // Ctrl+C copies when text is selected (and sends ^C otherwise); Ctrl+Shift+C always copies.
    if (event.ctrlKey && !event.altKey && key === "c" && (event.shiftKey || terminal.hasSelection())) {
      const selection = terminal.getSelection();
      if (selection && navigator.clipboard) {
        navigator.clipboard.writeText(selection).catch(() => {});
      }

      terminal.clearSelection();
      event.preventDefault();
      return false;
    }

    // Ctrl+V / Ctrl+Shift+V: let the browser fire its paste event, which xterm turns into data.
    if (event.ctrlKey && !event.altKey && key === "v") {
      return false;
    }

    return true;
  });

  const dataSubscription = terminal.onData((data) => {
    if (!disposed) {
      dotNet.invokeMethodAsync("OnTerminalData", data).catch(() => {});
    }
  });

  return {
    write(data) {
      if (!disposed && data) {
        terminal.write(data);
      }
    },
    focus() {
      if (!disposed) {
        terminal.focus();
      }
    },
    dimensions() {
      return [terminal.cols, terminal.rows];
    },
    dispose() {
      if (disposed) {
        return;
      }

      disposed = true;
      observer.disconnect();
      dataSubscription.dispose();
      terminal.dispose();
    }
  };
}
