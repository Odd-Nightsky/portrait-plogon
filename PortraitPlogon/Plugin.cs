using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Collections.Generic;
using System.Text.Json;
using PortraitPlogon.Windows;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using Dalamud.Game;
using System.Linq;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Diagnostics;

namespace PortraitPlogon;


public sealed unsafe class PortraitPlogon : IDalamudPlugin {
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPartyList PartyList { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState clientState { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    
    public Configuration configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("PortraitPlogon");
    private ConfigWindow ConfigWindow { get; init; }
    private const string ConfigCommandName = "/portcfg";
    private const string LocalPlayerModName = "own_portraits";
    private int party_list_length = -1; // we want this to always change on first load
    public readonly string folder_path;
    private readonly List<PartyMember> party_list = [];
    //private readonly string api_key = "123PLACE_HOLDER";  // TODO: load this from plugin configuration
    public string? own_name;
    public string? own_world;
    public string? own_hash;
    private bool plugin_loaded = false;
    private readonly Helpers helpers;


    public PortraitPlogon(IDalamudPluginInterface pluginInterface) {
        // TODO: clean this up
        folder_path = PluginInterface.GetPluginConfigDirectory();
        configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ConfigWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(ConfigWindow);
        CommandManager.AddHandler(ConfigCommandName, new CommandInfo(ToggleConfigCommand) {
            HelpMessage = "Open PortraitPlogon configuration window"
        });

        helpers = new Helpers(Data);

        // addon life cycle stuffs
        IAddonLifecycle.AddonEventDelegate AgentBannerHandler = PartyPortraitInterfacePostSetup;
        IAddonLifecycle.AddonEventDelegate CharaCardPostSetupHandler = AdventurePlatePostSetup;
        IAddonLifecycle.AddonEventDelegate CharaCardPreDrawHandler = AdventurePlatePreDraw;
        AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "BannerParty", AgentBannerHandler);
        //AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "CharaCard", CharaCardPostSetupHandler);
        AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "CharaCard", CharaCardPreDrawHandler);

        // events
        Framework.Update                       += on_framework_update;
        clientState.Login                      += on_login;
        clientState.Logout                     += on_logout;
        PluginInterface.UiBuilder.Draw         += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
    }

    private void DrawUI() => WindowSystem.Draw();
    public void ToggleConfigUI() => ConfigWindow.Toggle();

    private void ToggleConfigCommand(string command, string args) {
        if (!clientState.IsLoggedIn)
            return;
        ToggleConfigUI();
    }

    /// <summary>
    /// called upon login and first load
    /// </summary>
    private void on_login() {
        plugin_loaded = true;
        own_name = clientState.LocalPlayer?.Name.ToString();
        own_world = clientState.LocalPlayer?.HomeWorld.Value.Name.ToString() ?? "Unkown";
        party_list_length = -1; // forces a re-run of the party list checking
        // TODO: actually hash
        own_hash = own_name +
            clientState.LocalPlayer?.HomeWorld.RowId
            ?? "Place HolderUnknown";

        // if this character doesn't exist in the configuration dictionary yet
        if (!configuration.Portraits.ContainsKey(own_hash))
            configuration.Portraits[own_hash] = [];
        
        // Load the current setup into penumbra
        if (PenumbraIPC.CheckAvailablity())
            ReconstructTemporaryMod();
            // TODO: add an error message for if this fails & a way to re-load in the future if it becomes available
    }

    private void on_logout(int type, int code) {
        own_name = null;
        own_world = null;
        own_hash = null;
    }

    private void on_framework_update(IFramework framework) {
        // we want this to run to populate certain values. but it needs to load only once
        // it'd run either on login or when the plugin first loads up
        if (!plugin_loaded && clientState.IsLoggedIn)
            on_login();

        if (party_list_length != PartyList.Length) {
            PluginLog.Debug($"Party list changed.\n" +
                  $"old: {this.party_list_length}\n" +
                  $"new: {PartyList.Length}"
            );
            party_list_length = PartyList.Length;
            party_list.Clear();
            for (var i = 0; i < PartyList.Count; i++) {
                var member = PartyList[i];
                if (member == null) {
                    PluginLog.Debug("This code should by all accounts be unreachable. yet you reached it anyway.");
                    continue;
                }
                
                party_list.Add(new PartyMember(member));
            }
            if (party_list.Count == 0) {  // the player is solo
                PluginLog.Debug("solo play :D");
                if (clientState.LocalPlayer != null)
                party_list.Add(new PartyMember(clientState.LocalPlayer));
            }
            foreach (var member in party_list) {
                if (member.Name == own_name && helpers.CustomPortraitExists(configuration, own_hash ?? "Unknown", member.ClassJob.Value.Name.ToString())) {
                    member.Image_path = configuration.Portraits[own_hash ?? "Unknown"][member.ClassJob.Value.Name.ToString()];
                }
                PluginLog.Debug(member.Name.ToString());
            }
        }
    }

    private void AdventurePlatePostSetup(AddonEvent type, AddonArgs args) {
        // grab info here & download the image
    }

    private void AdventurePlatePreDraw(AddonEvent type, AddonArgs args) {
        // WHY DO I NEED TO RUN THIS EVERY FRAME AAAAA
        // /xldata -> Addon Inspector -> Depth Layer 5 -> CharaCard
        var CharaCard = (AtkUnitBase*)args.Addon;
        var hash = helpers.GetHashFromPlate(CharaCard);

        // setting image
        // node IDs: 1 > 19 > 2
        // are we looking at our own plate?
        if (hash == own_hash) {
            if (configuration.Portraits[own_hash ?? "Unknown"].GetValueOrDefault("adventure plate", "").IsNullOrEmpty())
                return;
            var portrait_node = (AtkComponentNode*)CharaCard->GetNodeById(19);
            var portrait = (AtkImageNode*)portrait_node->Component->UldManager.SearchNodeById(2);
            portrait->LoadTexture($"tmp/portrait_plogon/{own_hash}/adventure plate.tex");
        }
    }

    /// <summary>
    /// Hooked to run before the party portrait interface is rendered
    /// </summary>
    private void PartyPortraitInterfacePostSetup(AddonEvent type, AddonArgs args) {
        var banner = (AtkUnitBase*)args.Addon;
        for (uint i = 1; i <= 8; i++) {
            var name = helpers.GetNameByPlayerID(i, banner);
            var player_job = helpers.GetJobByPlayerID(i, banner);
            var world_id = helpers.GetWorldIDByPlayerID(i, banner);
            foreach (var member in party_list) {
                if (member.Image_path.IsNullOrEmpty())
                    continue;
                if (player_job == member.ClassJob.Value.Name && // job check
                    world_id == member.World.RowId           && // world check
                    helpers.CompareNames(name, member.Name))    // name check
                {
                    PluginLog.Debug($"Users match. {name} & {member.Name}");
                    // paths should look like `tmp/portrait_plogon/[hash]/[job].tex`
                    var hash = name + helpers.GetWorldIDByPlayerID(i, banner);
                    var path = $"tmp/portrait_plogon/{hash}/{player_job}.tex";
                    PluginLog.Debug($"Attempting portrait overwrite with path: {path}");
                    PluginLog.Debug(path);
                    helpers.GetImageNodeByPlayerID(i, banner)->LoadTexture(path);
                }
            }
        }
    }

    public void ReconstructTemporaryMod() {
        // reconstruct temporary mod & ask penumbra to load it
        var mod_dict = new Dictionary<string, string>();
        // portrait has type "KeyValuePair<string, string>"
        foreach (var portrait in configuration.Portraits[own_hash ?? "Unknown"]) {
            mod_dict.Add($"tmp/portrait_plogon/{own_hash}/{portrait.Key}.tex", portrait.Value+".tex");
            // PortraitPlogon.PluginLog.PluginLog.Debug($"tmp/portrait_plogon/{own_hash}/{portrait.Key}.tex");
        }
        // PenumbraIPC.RemoveTemporaryModAll(LocalPlayerModName);  // TODO:is this needed?
        PenumbraIPC.AddTemporaryModAll(LocalPlayerModName, mod_dict);
    }

    public void Dispose() {
        AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "BannerParty");
        //AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "CharaCard");
        AddonLifecycle.UnregisterListener(AddonEvent.PreDraw, "CharaCard");
        Framework.Update -= on_framework_update;
        clientState.Login -= on_login;
        clientState.Logout -= on_logout;
        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUI;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();

        CommandManager.RemoveHandler(ConfigCommandName);

        // Unload our mod(s) from penumbra
        // we don't edit normal game paths so this isn't *technically* needed but its the right thing to do
        if (PenumbraIPC.CheckAvailablity())
            PenumbraIPC.RemoveTemporaryModAll(LocalPlayerModName);
        // if penumbra isn't loaded our mod won't be either so no biggie lol
    }
}


