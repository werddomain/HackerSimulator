import { EditorState, Compartment } from "@codemirror/state";
import {
  EditorView,
  drawSelection,
  dropCursor,
  highlightActiveLine,
  highlightActiveLineGutter,
  highlightSpecialChars,
  keymap,
  lineNumbers,
  rectangularSelection
} from "@codemirror/view";
import { defaultKeymap, history, historyKeymap, indentWithTab } from "@codemirror/commands";
import {
  bracketMatching,
  defaultHighlightStyle,
  foldGutter,
  indentOnInput,
  StreamLanguage,
  syntaxHighlighting
} from "@codemirror/language";
import { searchKeymap } from "@codemirror/search";
import { css } from "@codemirror/lang-css";
import { html } from "@codemirror/lang-html";
import { javascript } from "@codemirror/lang-javascript";
import { json } from "@codemirror/lang-json";
import { markdown } from "@codemirror/lang-markdown";
import { csharp } from "@codemirror/legacy-modes/mode/clike";

const languageCompartment = new Compartment();

function languageFor(mode) {
  switch (mode) {
    case "cSharp": return StreamLanguage.define(csharp);
    case "javaScript": return javascript();
    case "typeScript": return javascript({ typescript: true });
    case "html": return html();
    case "css": return css();
    case "json": return json();
    case "markdown": return markdown();
    default: return [];
  }
}

export function createCodeEditor(host, dotNet, content, mode) {
  let suppressChange = false;
  let disposed = false;
  const state = EditorState.create({
    doc: content ?? "",
    extensions: [
      lineNumbers(),
      highlightActiveLineGutter(),
      highlightSpecialChars(),
      history(),
      foldGutter(),
      drawSelection(),
      dropCursor(),
      EditorState.allowMultipleSelections.of(true),
      indentOnInput(),
      syntaxHighlighting(defaultHighlightStyle, { fallback: true }),
      bracketMatching(),
      rectangularSelection(),
      highlightActiveLine(),
      keymap.of([indentWithTab, ...defaultKeymap, ...searchKeymap, ...historyKeymap]),
      languageCompartment.of(languageFor(mode)),
      EditorView.lineWrapping,
      EditorView.updateListener.of(update => {
        if (update.docChanged && !suppressChange && !disposed) {
          void dotNet.invokeMethodAsync("OnEditorChanged", update.state.doc.toString());
        }
      })
    ]
  });

  const view = new EditorView({ state, parent: host });
  return {
    setDocument(nextContent) {
      if (disposed) return;
      const value = nextContent ?? "";
      if (view.state.doc.toString() === value) return;
      suppressChange = true;
      try {
        view.dispatch({ changes: { from: 0, to: view.state.doc.length, insert: value } });
      } finally {
        suppressChange = false;
      }
    },
    setLanguage(nextMode) {
      if (!disposed) {
        view.dispatch({ effects: languageCompartment.reconfigure(languageFor(nextMode)) });
      }
    },
    focus() {
      if (!disposed) view.focus();
    },
    destroy() {
      if (disposed) return;
      disposed = true;
      view.destroy();
    }
  };
}
