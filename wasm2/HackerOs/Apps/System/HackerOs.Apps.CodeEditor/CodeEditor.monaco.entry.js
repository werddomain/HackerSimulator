// Import only the languages CodeEditorSyntaxMode actually offers instead of
// pulling in monaco-editor's full editor.main.js (which registers 80+
// languages and roughly doubles the bundle size for no benefit here). Each
// import's transitive CSS (codicons, widgets, ...) is bundled automatically
// by esbuild into the sibling monaco.bundle.css output.
import * as monaco from "monaco-editor/esm/vs/editor/edcore.main.js";
import "monaco-editor/esm/vs/language/css/monaco.contribution.js";
import "monaco-editor/esm/vs/language/html/monaco.contribution.js";
import "monaco-editor/esm/vs/language/json/monaco.contribution.js";
import "monaco-editor/esm/vs/language/typescript/monaco.contribution.js";
import "monaco-editor/esm/vs/basic-languages/markdown/markdown.contribution.js";
import "monaco-editor/esm/vs/basic-languages/csharp/csharp.contribution.js";

// Loaded once per module URL (the browser caches dynamic import()s), so this
// stylesheet injection and worker wiring only ever run a single time.
const stylesheetHref = new URL("./monaco.bundle.css", import.meta.url).href;
if (!document.querySelector(`link[href="${stylesheetHref}"]`)) {
  const link = document.createElement("link");
  link.rel = "stylesheet";
  link.href = stylesheetHref;
  document.head.appendChild(link);
}

// Monaco cancels in-flight language-service work (worker RPC requests, validation, ...) when a
// model or editor disposes mid-request. Its own errors.js explicitly excludes this exact error
// shape from onUnexpectedError (see isCancellationError there: name === "Canceled" && message
// === "Canceled"), but some internal call sites reject a bare promise with it instead of routing
// through that filter, so it can still reach the page as an unhandled rejection. Suppress only
// that precise, well-known shape; anything else still propagates normally.
window.addEventListener("unhandledrejection", (event) => {
  const reason = event.reason;
  if (reason instanceof Error && reason.name === "Canceled" && reason.message === "Canceled") {
    event.preventDefault();
  }
});

const WORKER_FILE_BY_LABEL = {
  json: "json.worker.js",
  css: "css.worker.js",
  scss: "css.worker.js",
  less: "css.worker.js",
  html: "html.worker.js",
  handlebars: "html.worker.js",
  razor: "html.worker.js",
  typescript: "ts.worker.js",
  javascript: "ts.worker.js"
};

self.MonacoEnvironment = {
  getWorkerUrl(_moduleId, label) {
    const workerFile = WORKER_FILE_BY_LABEL[label] ?? "editor.worker.js";
    return new URL(workerFile, import.meta.url).href;
  }
};

const LANGUAGE_BY_MODE = {
  cSharp: "csharp",
  javaScript: "javascript",
  typeScript: "typescript",
  html: "html",
  css: "css",
  json: "json",
  markdown: "markdown",
  plainText: "plaintext"
};

function languageFor(mode) {
  return LANGUAGE_BY_MODE[mode] ?? "plaintext";
}

export function createCodeEditor(host, dotNet, content, mode) {
  let suppressChange = false;
  let disposed = false;

  const model = monaco.editor.createModel(content ?? "", languageFor(mode));
  const editor = monaco.editor.create(host, {
    model,
    theme: "vs-dark",
    automaticLayout: true,
    minimap: { enabled: true },
    fontSize: 14,
    tabSize: 2,
    wordWrap: "on",
    scrollBeyondLastLine: false
  });

  const changeSubscription = editor.onDidChangeModelContent(() => {
    if (!suppressChange && !disposed) {
      void dotNet.invokeMethodAsync("OnEditorChanged", model.getValue());
    }
  });

  return {
    setDocument(nextContent) {
      if (disposed) return;
      const value = nextContent ?? "";
      if (model.getValue() === value) return;
      suppressChange = true;
      try {
        model.pushEditOperations([], [{ range: model.getFullModelRange(), text: value }], () => null);
      } finally {
        suppressChange = false;
      }
    },
    setLanguage(nextMode) {
      if (!disposed) monaco.editor.setModelLanguage(model, languageFor(nextMode));
    },
    focus() {
      if (!disposed) editor.focus();
    },
    destroy() {
      if (disposed) return;
      disposed = true;
      changeSubscription.dispose();
      editor.dispose();
      model.dispose();
    }
  };
}
