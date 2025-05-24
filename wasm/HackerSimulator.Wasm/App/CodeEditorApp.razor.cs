using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Web;
using HackerSimulator.Wasm.Shared.Terminal;
using Microsoft.AspNetCore.Components;
using HackerSimulator.Wasm.Core;
using BlazorMonaco;
using BlazorMonaco.Editor;

namespace HackerSimulator.Wasm.Apps
{
    [OpenFileType("js", "ts", "cs", "json", "html", "css")]
    [AppIcon("fa:code")]
    public partial class CodeEditorApp : Windows.WindowBase
    {
        [Inject] private FileSystemService FS { get; set; } = default!;

        private class EditorTab
        {
            public string Path { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public string Language { get; set; } = "text";
            public bool IsDirty { get; set; }
        }

        private readonly List<EditorTab> _tabs = new();
        private EditorTab? _activeTab;
        private CodeEditor? _editor;
        private string _workingDirectory = "/home/user";

        private int _terminalCount = 1;
        private int _activeTerminal;
        private readonly List<Shared.Terminal.Terminal?> _terminalRefs = new();

        private StandaloneEditorConstructionOptions _editorOptions(StandaloneCodeEditor editor) { 
            return new()
            {
                AutomaticLayout = true,
                Language = _activeTab?.Language ?? "text",
                Theme = "vs-dark",
                ReadOnly = false,
                Value = _activeTab?.Content ?? string.Empty
            };
        }
        

        protected override void OnInitialized()
        {
            base.OnInitialized();
            Title = "Code Editor";
            _terminalRefs.Add(null); // placeholder for first terminal
        }

        private static string GuessLanguage(string path)
        {
            var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".cs" => "csharp",
                ".html" or ".htm" => "html",
                ".css" => "css",
                _ => "javascript"
            };
        }

        private async Task OpenFile(string path)
        {
            var tab = _tabs.FirstOrDefault(t => t.Path == path);
            if (tab == null)
            {
                var content = await FS.ReadFile(path);
                tab = new EditorTab { Path = path, Content = content, Language = GuessLanguage(path) };
                _tabs.Add(tab);
            }
            _activeTab = tab;
            StateHasChanged();
        }

        private void Activate(EditorTab tab)
        {
            _activeTab = tab;
            StateHasChanged();
        }

        private async Task EditorChanged()
        {
            if (_activeTab != null && _editor != null)
            {
                _activeTab.Content = await _editor.GetValue();
                _activeTab.IsDirty = true;
            }
        }

        private async Task Save()
        {
            if (_activeTab == null) return;
            await FS.WriteFile(_activeTab.Path, _activeTab.Content);
            _activeTab.IsDirty = false;
        }

        private async Task OpenFolder()
        {
            var dialog = new Dialogs.FolderPickerDialog();
            var path = await dialog.ShowDialog(this);
            if (!string.IsNullOrWhiteSpace(path))
            {
                _workingDirectory = path;
            }
        }

        private void AddTerminal()
        {
            _terminalCount++;
            _terminalRefs.Add(null);
        }

        private void ActivateTerminal(int index)
        {
            _activeTerminal = index;
        }

        private record Workspace(string WorkingDirectory, List<string> Files);

        private async Task SaveWorkspace()
        {
            var dialog = new Dialogs.PromptDialog { Message = "Workspace file path?" };
            var path = await dialog.ShowDialog(this);
            if (string.IsNullOrWhiteSpace(path)) return;
            var data = new Workspace(_workingDirectory, _tabs.Select(t => t.Path).ToList());
            var json = JsonSerializer.Serialize(data);
            await FS.WriteFile(path, json);
        }

        private async Task LoadWorkspace()
        {
            var dialog = new Dialogs.FilePickerDialog();
            var path = await dialog.ShowDialog(this);
            if (string.IsNullOrWhiteSpace(path)) return;
            var json = await FS.ReadFile(path);
            var data = JsonSerializer.Deserialize<Workspace>(json);
            if (data == null) return;
            _workingDirectory = data.WorkingDirectory;
            foreach (var file in data.Files)
                await OpenFile(file);
        }

        private async Task HandleKey(KeyboardEventArgs e)
        {
            if (e.CtrlKey && (e.Key == "s" || e.Code == "KeyS"))
            {
                await Save();
            }
        }


        private void Close(EditorTab tab)
        {
            _tabs.Remove(tab);
            if (_activeTab == tab)
                _activeTab = _tabs.LastOrDefault();
        }
    }
}
