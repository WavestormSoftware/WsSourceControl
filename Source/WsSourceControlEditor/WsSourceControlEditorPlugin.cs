#if FLAX_EDITOR
using System;
using FlaxEditor;
using FlaxEditor.GUI;
using FlaxEditor.GUI.ContextMenu;
using FlaxEditor.GUI.Docking;
using FlaxEngine;

namespace WsSourceControlEditor
{
    public class WsSourceControlEditorPlugin : EditorPlugin
    {
        private ToolStripButton _toolstripButton;
        private ContextMenuButton _menuButton;
        private WsSourceControlWindow _window;

        public WsSourceControlEditorPlugin()
        {
            _description = new PluginDescription
            {
                Name = "WsSourceControl",
                Category = "Source Control",
                Author = "Wavestorm Software",
                AuthorUrl = "https://github.com/WavestormSoftware",
                Description = "Professional Git-based source control GUI for Flax Engine.",
                Version = new Version(2, 0),
                IsAlpha = false,
                IsBeta = false,
            };
        }

        public override void InitializeEditor()
        {
            base.InitializeEditor();

            _toolstripButton = Editor.UI.ToolStrip.AddButton("Source Control");
            _toolstripButton.Clicked += OnOpenSourceControl;

            _menuButton = Editor.UI.MenuWindow.ContextMenu.AddButton("Source Control");
            _menuButton.ShortKeys = "F8";
            _menuButton.Clicked += OnOpenSourceControl;
        }

        public override void DeinitializeEditor()
        {
            if (_window != null)
            {
                _window.Close();
                _window = null;
            }

            if (_toolstripButton != null)
            {
                _toolstripButton.Dispose();
                _toolstripButton = null;
            }

            if (_menuButton != null)
            {
                _menuButton.Dispose();
                _menuButton = null;
            }

            base.DeinitializeEditor();
        }

        private void OnOpenSourceControl()
        {
            if (_window == null || _window.IsDisposing)
            {
                _window = new WsSourceControlWindow();
                _window.Show();
            }
            else
            {
                _window.Focus();
            }
        }
    }
}
#endif
