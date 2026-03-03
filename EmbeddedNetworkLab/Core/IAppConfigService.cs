using System.Collections.Generic;
using EmbeddedNetworkLab.Core.Models;

namespace EmbeddedNetworkLab.Core
{
    public interface IAppConfigService
    {
        string AppDirectory { get; }
        string ConfigPath { get; }
        string DefaultCommandsPath { get; }

        /// <summary>
        /// Ensure default files exist and return the path to the commands file to load (last used or default)
        /// </summary>
        string EnsureAndGetCommandsFile();

        List<CommandEntry> LoadCommandsFromPath(string path);
        void SaveCommandsToPath(List<CommandEntry> list, string path);

        string? LastCommandsFile { get; set; }
    }
}
