using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace PortraitPlogon;

[Serializable]
public class Configuration : IPluginConfiguration {
    public int Version { get; set; } = 0;

    // Portraits[own_hash][job]
    public Dictionary<string, Dictionary<string, string>> Portraits = [];

    // the below exist just to make saving less cumbersome
    public void Save() {
        PortraitPlogon.PluginInterface.SavePluginConfig(this);
    }
}

