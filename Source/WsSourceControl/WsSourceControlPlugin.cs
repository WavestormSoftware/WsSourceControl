using System;
using FlaxEngine;

namespace WsSourceControl
{
    public class WsSourceControlPlugin : GamePlugin
    {
        public WsSourceControlPlugin()
        {
            _description = new PluginDescription
            {
                Name = "WsSourceControl",
                Category = "Source Control",
                Author = "Wavestorm Software",
                AuthorUrl = "https://github.com/WavestormSoftware",
                RepositoryUrl = "https://github.com/WavestormSoftware/FlaxGameProject",
                Description = "Professional Git-based source control integration for Flax Engine by Wavestorm Software.",
                Version = new Version(1, 1),
                IsAlpha = false,
                IsBeta = false,
            };
        }
    }
}
