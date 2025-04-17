using System.Collections.Generic;
using Dalamud.Configuration;

namespace PortraitPlogon.OldConfigs;

public class ConfigurationV0 : IPluginConfiguration {
    public int Version { get; set; } = 1;

    // Portraits[ContentID][job]
    public Dictionary<string, Dictionary<string, string>> Portraits = [];

    // the below exist just to make saving less cumbersome
    public void Save() {
        PortraitPlogon.PluginInterface.SavePluginConfig(this);
    }
}