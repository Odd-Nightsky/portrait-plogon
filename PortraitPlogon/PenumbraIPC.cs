using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Ipc;
using Penumbra.Api;

namespace PortraitPlogon;


public class PenumbraIPC {
    private const int PenumbraApiMajor = 5;
    private const int PenumbraApiMinor = 0;

    public static bool CheckAvailablity() {
        try {
            if (PortraitPlogon.PluginInterface.InstalledPlugins.Any(x => x.Name == "Penumbra")) {
                if (!PortraitPlogon.PluginInterface.InstalledPlugins.First(x => x.Name == "Penumbra").IsLoaded)
                    return false;
                
                var result = new Penumbra.Api.IpcSubscribers.ApiVersion(PortraitPlogon.PluginInterface).Invoke();

                if (result.Breaking != PenumbraApiMajor || result.Features < PenumbraApiMinor) {
                    return false;
                }
                return true;
            }
            return false;
        } catch (Exception) {
            return false;
        }
    }

    public static void ConvertPngToTexAsIs(string inputFile, string outputFile) {
        new Penumbra.Api.IpcSubscribers.ConvertTextureFile(PortraitPlogon.PluginInterface)
        .Invoke(inputFile, outputFile, Penumbra.Api.Enums.TextureType.AsIsTex);
    }

    public static void AddTemporaryModAll(string name, Dictionary<string, string> mod_dict) {
        // string tag, Dictionary<string, string> [game path, system path], string manipstring, int priority
        new Penumbra.Api.IpcSubscribers.AddTemporaryModAll(PortraitPlogon.PluginInterface).Invoke(name, mod_dict, "", 0);
    }

    public static void RemoveTemporaryModAll(string name) {
        // string tag, Dictionary<string, string> [game path, system path], string manipstring, int priority
        new Penumbra.Api.IpcSubscribers.RemoveTemporaryModAll(PortraitPlogon.PluginInterface).Invoke(name, 0);
    }

    public static void Dispose() {}
}
