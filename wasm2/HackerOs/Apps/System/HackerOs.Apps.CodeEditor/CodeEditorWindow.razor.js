import { createCodeEditor } from "./codemirror.bundle.js";

// The collocated boundary keeps CodeMirror private to this component and exposes
// only document, language, focus, and deterministic disposal operations to C#.
export function createEditor(host, dotNet, content, mode) {
  return createCodeEditor(host, dotNet, content, mode);
}
