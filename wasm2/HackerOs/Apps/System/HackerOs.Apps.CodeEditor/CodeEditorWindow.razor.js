import { createCodeEditor } from "./codemirror.bundle.js";

// The collocated boundary keeps CodeMirror private to this component and exposes
// only document, language, focus, and deterministic disposal operations to C#.
export function createEditor(host, dotNet, content, mode) {
  const editor = createCodeEditor(host, dotNet, content, mode);
  // CodeMirror's contenteditable surface gets role="textbox" from its own bundle but no
  // accessible name (axe aria-input-field-name) — the wrapping host div previously carried
  // an aria-label instead, which axe flags as aria-prohibited-attr since a plain div has no
  // role for aria-label to attach to. Label the actual textbox element directly.
  const content_ = host.querySelector(".cm-content");
  if (content_) {
    content_.setAttribute("aria-label", "Source code editor");
  }
  return editor;
}
