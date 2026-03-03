using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using EmbeddedNetworkLab.Core;
using EmbeddedNetworkLab.Core.Models;

namespace EmbeddedNetworkLab.Infrastructure.Services
{
    public class AppConfigService : IAppConfigService
    {
        public string AppDirectory { get; }
        public string ConfigPath { get; }
        public string DefaultCommandsPath { get; }

        private AppConfig _config = new AppConfig();

        private class AppConfig
        {
            public string? LastCommandsFile { get; set; }
        }

        public string? LastCommandsFile
        {
            get => _config.LastCommandsFile;
            set
            {
                _config.LastCommandsFile = value;
                SaveConfig();
            }
        }

        public AppConfigService()
        {
            AppDirectory = AppContext.BaseDirectory;
            ConfigPath = Path.Combine(AppDirectory, "config.json");
            DefaultCommandsPath = Path.Combine(AppDirectory, "serial_commands.json");

            EnsureAndGetCommandsFile();
        }

        public string EnsureAndGetCommandsFile()
        {
            try
            {
                Directory.CreateDirectory(AppDirectory);

                if (!File.Exists(DefaultCommandsPath))
                {
                    var defaults = new List<CommandEntry>();
                    for (int i = 0; i < 10; i++) defaults.Add(new CommandEntry());
                    File.WriteAllText(DefaultCommandsPath, JsonSerializer.Serialize(defaults, new JsonSerializerOptions { WriteIndented = true }));
                }

                if (File.Exists(ConfigPath))
                {
                    var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath));
                    if (cfg != null) _config = cfg;
                }

                var toLoad = !string.IsNullOrWhiteSpace(_config.LastCommandsFile) && File.Exists(_config.LastCommandsFile)
                    ? _config.LastCommandsFile
                    : DefaultCommandsPath;

                _config.LastCommandsFile = toLoad;
                SaveConfig();
                return toLoad;
            }
            catch
            {
                return DefaultCommandsPath;
            }
        }

        public List<CommandEntry> LoadCommandsFromPath(string path)
        {
            try
            {
                var json = File.ReadAllText(path);
                var list = JsonSerializer.Deserialize<List<CommandEntry>>(json);
                LastCommandsFile = path;
                return list ?? new List<CommandEntry>();
            }
            catch
            {
                return new List<CommandEntry>();
            }
        }

        public void SaveCommandsToPath(List<CommandEntry> list, string path)
        {
            try
            {
                File.WriteAllText(path, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
                LastCommandsFile = path;
            }
            catch
            {
                // ignore
            }
        }

        private void SaveConfig()
        {
            try
            {
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                // ignore
            }
        }
    }
}
