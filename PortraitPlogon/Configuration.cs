using Dalamud.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using PortraitPlogon.OldConfigs;

namespace PortraitPlogon;

[Serializable]
public class Configuration : IPluginConfiguration {
    public int Version { get; set; } = 1;

    // Portraits[ContentID][job]
    public Dictionary<ulong, Dictionary<string, string>> Portraits = [];

    // the below exist just to make saving less cumbersome
    public void Save() {
        PortraitPlogon.PluginInterface.SavePluginConfig(this);
    }

    public static Configuration Load(string configPath) {
        Migrate(configPath);
        return JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(configPath)) ?? new Configuration();
    }
    
    // should prob be made cleaner
    private static void Migrate(string configFilePath) {
        var text = File.ReadAllText(configFilePath);
        var config = JsonConvert.DeserializeObject(text, typeof(ConfigurationMinimal)) as ConfigurationMinimal;
        if (config == null)
            return;
        if (config.Version < 1) {
            var v0 = JsonConvert.DeserializeObject(text, typeof(ConfigurationV0)) as ConfigurationV0;
            if (v0 == null)
                return;
            // old way hard incompatible with new one
            // just delete them
            v0.Portraits.Clear();
            v0.Version = 1;
            v0.Save();
        }
    }
}

public class ConfigurationMinimal : IPluginConfiguration {
    public int Version { get; set; }
}