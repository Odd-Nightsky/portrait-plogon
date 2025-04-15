using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace PortraitPlogon;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class PenumbraIPC {
    private const int PenumbraApiMajor = 5;
    private const int PenumbraApiMinor = 0;

    public static bool CheckAvailability() {
        try {
            if (PortraitPlogon.PluginInterface.InstalledPlugins.All(x => x.Name != "Penumbra"))
                return false;
            if (!PortraitPlogon.PluginInterface.InstalledPlugins.First(x => x.Name == "Penumbra").IsLoaded)
                return false;
                
            var result = new Penumbra.Api.IpcSubscribers.ApiVersion(PortraitPlogon.PluginInterface).Invoke();

            return result.Breaking == PenumbraApiMajor && result.Features >= PenumbraApiMinor;
        } catch (Exception) {
            return false;
        }
    }

    public static void ConvertPngToTexAsIs(string inputFile, string outputFile) {
        new Penumbra.Api.IpcSubscribers.ConvertTextureFile(PortraitPlogon.PluginInterface)
        .Invoke(inputFile, outputFile, Penumbra.Api.Enums.TextureType.AsIsTex);
    }

    public static void AddTemporaryModAll(string name, Dictionary<string, string> modDict) {
        // string tag, Dictionary<string, string> [game path, system path], string manipstring, int priority
        new Penumbra.Api.IpcSubscribers.AddTemporaryModAll(PortraitPlogon.PluginInterface).Invoke(name, modDict, "", 0);
    }

    public static void RemoveTemporaryModAll(string name) {
        // string tag, Dictionary<string, string> [game path, system path], string manipstring, int priority
        new Penumbra.Api.IpcSubscribers.RemoveTemporaryModAll(PortraitPlogon.PluginInterface).Invoke(name, 0);
    }

    public static void Dispose() {}
}
