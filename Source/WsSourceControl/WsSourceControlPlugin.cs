#if FLAX_EDITOR
using System;
using FlaxEditor;
using FlaxEditor.GUI;
using FlaxEditor.GUI.ContextMenu;
using FlaxEditor.GUI.Docking;
using FlaxEngine;
using WsSourceControl;
using WsSourceControl.Git;

namespace WsSourceControlEditor
{
    public class WsSourceControlPlugin : EditorPlugin
    {
        public const string SettingsName = "WsSourceControl";

        private ToolStripButton _toolstripButton;
        private ContextMenuButton _menuButton;
        private WsSourceControlWindow _window;

        public WsSourceControlPlugin()
        {
            _description = new PluginDescription
            {
                Name = "WsSourceControl",
                Category = "Source Control",
                Author = "Wavestorm Software",
                AuthorUrl = "https://github.com/WavestormSoftware",
                RepositoryUrl = "https://github.com/WavestormSoftware/WsSourceControl",
                HomepageUrl = "https://github.com/WavestormSoftware/WsSourceControl",
                Description = "Git source control panel for Flax Engine.",
                Version = new Version(0, 2, 0),
                IsAlpha = false,
                IsBeta = true,
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

            Editor.Options.AddCustomSettings(SettingsName, () => new WsSourceControlSettings());
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

            Editor.Options.RemoveCustomSettings(SettingsName);

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
