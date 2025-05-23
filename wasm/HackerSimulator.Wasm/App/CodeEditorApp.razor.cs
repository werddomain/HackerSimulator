using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using HackerSimulator.Wasm.Core;
using BlazorMonaco;

namespace HackerSimulator.Wasm.Apps
{
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
        private MonacoEditor? _editor;
        private readonly StandaloneEditorConstructionOptions _editorOptions = new()
        {
            AutomaticLayout = true
        };

        protected override void OnInitialized()
        {
            base.OnInitialized();
            Title = "Code Editor";
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


        private void Close(EditorTab tab)
        {
            _tabs.Remove(tab);
            if (_activeTab == tab)
                _activeTab = _tabs.LastOrDefault();
        }
    }
}
