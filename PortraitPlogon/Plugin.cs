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
    private int party_list_length = 0;
    public readonly string folder_path;
    private readonly List<PartyMember> party_list = [];
    //private readonly string api_key = "123PLACE_HOLDER";  // TODO: load this from plugin configuration
    public string? own_name;
    public string? own_world;
    public string? own_hash;
    private bool plugin_loaded = false;


    public PortraitPlogon(IDalamudPluginInterface pluginInterface) {
        // TODO: clean this up
        folder_path = PluginInterface.GetPluginConfigDirectory();
        configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ConfigWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(ConfigWindow);
        CommandManager.AddHandler(ConfigCommandName, new CommandInfo(ToggleConfigCommand) {
            HelpMessage = "Open PortraitPlogon configuration window"
        });

        // addon life cycle stuffs
        IAddonLifecycle.AddonEventDelegate AgentBannerHandler = AgentBannerInterfacePostSetup;
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

    private static void Debug(string s) {
        // literally just a shorthand for calling PluginLog.Debug because I'm too lazy to write the full thing I guess
        PluginLog.Debug(s);
    }

    // called upon login and first load
    private void on_login() {
        plugin_loaded = true;
        own_name = clientState.LocalPlayer?.Name.ToString();
        own_world = clientState.LocalPlayer?.HomeWorld.Value.Name.ToString() ?? "Unkown";
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
            Debug($"Party list changed.\n" +
                  $"old: {this.party_list_length}\n" +
                  $"new: {PartyList.Length}"
            );
            party_list_length = PartyList.Length;
            party_list.Clear();
            for (var i = 0; i < PartyList.Count; i++) {
                var member = PartyList[i];
                if (member == null) {
                    Debug("This code should by all accounts be unreachable. yet you reached it anyway.");
                    continue;
                }
                
                party_list.Add(new PartyMember(member));
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
        var hash = GetHashFromPlate(CharaCard);

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

    private string GetHashFromPlate(AtkUnitBase* CharaCard) {
        // get the hash out of an adventure plate
        // getting name
        // node IDs: 1 > 4 > 5
        var name_node_parent = (AtkComponentNode*)CharaCard->GetNodeById(4);
        var name_node = (AtkTextNode*)name_node_parent->Component->UldManager.SearchNodeById(5);
        var name = name_node->NodeText.ToString();

        // getting world
        // node IDs: 1 > 5 > 3
        var world_node_parent = (AtkComponentNode*)CharaCard->GetNodeById(5);
        var world_node = (AtkTextNode*)world_node_parent->Component->UldManager.SearchNodeById(3);
        var world = world_node->NodeText.ToString();
        // world = $"{world_name} [{DataCenter}]"
        world = world.Split('[')[0].TrimEnd();
        var world_id = GetWorldIDByName(world);
        return name+world_id;
    }

    private void AgentBannerInterfacePostSetup(AddonEvent type, AddonArgs args) {
        var banner = (AtkUnitBase*)args.Addon;

        // this is the player. we do not have to worry about shit here lol.
        Debug($"Attempting portrait replacement of playerID 1");
        var player_job = GetPlayerJobByPlayerID(1, banner);
        // var hash = player_info.Name+player_info.World; // TODO: hash this

        // paths should look like `tmp/portrait_plogon/[hash]/[job].tex`
        if (!configuration.Portraits[own_hash ?? "Unknown"].GetValueOrDefault(player_job, "").IsNullOrEmpty()) {
            var path = $"tmp/portrait_plogon/{own_hash}/{player_job}.tex";
            GetImageNodeByPlayerID(1, banner)->LoadTexture(path);
        }
    }

    private AtkImageNode* GetImageNodeByPlayerID(uint ID, AtkUnitBase* banner) {
        // this code sucks lol
        // use /xldata and the Addon Inspector to help you make sense of whats happening here
        // Depth Layer 5 -> BannerParty
        var nodeID = (ID*2)+2;  // player 1 = nodeID 4, player 2 = nodeID 6, etc
        var player = (AtkComponentNode*)banner->GetNodeById(nodeID);
        var portrait_node = (AtkComponentNode*)player->Component->UldManager.SearchNodeById(20);
        return (AtkImageNode*)portrait_node->Component->UldManager.SearchNodeById(2);
    }

    private PlayerInfo GetPlayerInfoByPlayerID(uint ID, AtkUnitBase* banner) {
        // this code sucks lol
        // use /xldata and the Addon Inspector to help you make sense of whats happening here
        // Depth Layer 5 -> BannerParty
        var nodeID = (ID*2)+2;  // player 1 = nodeID 4, player 2 = nodeID 6, etc
        var player = (AtkComponentNode*)banner->GetNodeById(nodeID);
        
        var name = GetPlayerNameByPlayerID(ID, banner);
        var world_id = GetPlayerWorldIDByPlayerID(ID, banner);
        var job = GetPlayerJobByPlayerID(ID, banner);

        return new PlayerInfo(name, world_id, job);
    }

    private string GetPlayerNameByPlayerID(uint ID, AtkUnitBase* banner) {
        // this code sucks lol
        // use /xldata and the Addon Inspector to help you make sense of whats happening here
        // Depth Layer 5 -> BannerParty
        var nodeID = (ID*2)+2;  // player 1 = nodeID 4, player 2 = nodeID 6, etc
        var player = (AtkComponentNode*)banner->GetNodeById(nodeID);
        
        // getting name
        var first_name_node = (AtkTextNode*)player->Component->UldManager.SearchNodeById(6);
        var first_name = first_name_node->NodeText.ToString();
        var last_name_node = (AtkTextNode*)player->Component->UldManager.SearchNodeById(7);
        var last_name = last_name_node->NodeText.ToString();
        return $"{first_name} {last_name}";
    }

    private uint GetPlayerWorldIDByPlayerID(uint ID, AtkUnitBase* banner) {
        // this code sucks lol
        // use /xldata and the Addon Inspector to help you make sense of whats happening here
        // Depth Layer 5 -> BannerParty
        var nodeID = (ID*2)+2;  // player 1 = nodeID 4, player 2 = nodeID 6, etc
        var player = (AtkComponentNode*)banner->GetNodeById(nodeID);

        // getting world
        var world_node = (AtkTextNode*)player->Component->UldManager.SearchNodeById(12);
        var world_name = world_node->NodeText.ToString();
        return GetWorldIDByName(world_name);
    }

    private string GetPlayerJobByPlayerID(uint ID, AtkUnitBase* banner) {
        // this code sucks lol
        // use /xldata and the Addon Inspector to help you make sense of whats happening here
        // Depth Layer 5 -> BannerParty
        var nodeID = (ID*2)+2;  // player 1 = nodeID 4, player 2 = nodeID 6, etc
        var player = (AtkComponentNode*)banner->GetNodeById(nodeID);

        // getting job
        var job_node = (AtkTextNode*)player->Component->UldManager.SearchNodeById(9);
        return job_node->NodeText.ToString();
    }

    private static uint GetWorldIDByName(string world_name) {
        return Data.GetExcelSheet<World>()!
            .Where(world => world.Name == world_name).SingleOrDefault().RowId;
    }

    public void ReconstructTemporaryMod() {
        // reconstruct temporary mod & ask penumbra to load it
        var mod_dict = new Dictionary<string, string>();
        // portrait has type "KeyValuePair<string, string>"
        foreach (var portrait in configuration.Portraits[own_hash ?? "Unknown"]) {
            mod_dict.Add($"tmp/portrait_plogon/{own_hash}/{portrait.Key}.tex", portrait.Value+".tex");
            PortraitPlogon.PluginLog.Debug($"tmp/portrait_plogon/{own_hash}/{portrait.Key}.tex");
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


