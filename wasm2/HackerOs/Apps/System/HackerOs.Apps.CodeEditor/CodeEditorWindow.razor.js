import { createCodeEditor } from "./monaco/monaco.bundle.js";

// The collocated boundary keeps Monaco private to this component and exposes
// only document, language, focus, and deterministic disposal operations to C#.
export function createEditor(host, dotNet, content, mode) {
  const editor = createCodeEditor(host, dotNet, content, mode);
  // Monaco's hidden input surface already carries role="textbox" but a
  // generic accessible name ("Editor content") — relabel it directly instead
  // of the wrapping host div, which axe flags as aria-prohibited-attr since a
  // plain div has no role for aria-label to attach to. Browsers with the
  // EditContext API get a `div.native-edit-context`; others fall back to the
  // classic `textarea.inputarea`.
  const inputArea = host.querySelector(".native-edit-context, textarea.inputarea");
  if (inputArea) {
    inputArea.setAttribute("aria-label", "Source code editor");
  }
  return editor;
}
