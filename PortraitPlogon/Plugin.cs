using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using PortraitPlogon.Windows;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace PortraitPlogon;


[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed unsafe class PortraitPlogon : IDalamudPlugin {
    // ReSharper disable MemberCanBePrivate.Global
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPartyList PartyList { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static INotificationManager NotificationManager { get; private set; } = null!;
    // ReSharper restore MemberCanBePrivate.Global
    
    public Configuration Configuration { get; init; }
    private readonly WindowSystem _windowSystem = new("PortraitPlogon");
    private ConfigWindow ConfigWindow { get; init; }
    private const string ConfigCommandName = "/portcfg";
    private const string LocalPlayerModName = "own_portraits";
    private int _partyListLength = -1;  // 0-8 during normal gameplay.
                                        // setting to -1 forces a re-check on first run of on_framework_update
    public readonly string FolderPath;
    private readonly List<PartyMember> _partyList = [];
    //private readonly string api_key = "123PLACE_HOLDER";  // TODO: load this from plugin configuration
    public string? OwnName;
    public string? OwnWorld;
    public string? OwnHash;
    public ulong? OwnCID;
    private bool _pluginLoaded;
    private readonly Helpers _helpers;
    
    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    public PortraitPlogon(IDalamudPluginInterface pluginInterface) {
        // TODO: clean this up
        FolderPath = PluginInterface.GetPluginConfigDirectory();

        Configuration = Configuration.Load(PluginInterface.ConfigFile.FullName);
        
        ConfigWindow = new ConfigWindow(this, ClientState, NotificationManager);
        _windowSystem.AddWindow(ConfigWindow);
        CommandManager.AddHandler(ConfigCommandName, new CommandInfo(ToggleConfigCommand) {
            HelpMessage = "Open PortraitPlogon configuration window"
        });
        
        _helpers = new Helpers(Data);

        // addon life cycle stuffs
        IAddonLifecycle.AddonEventDelegate agentBannerHandler = PartyPortraitInterfacePostSetup;
        IAddonLifecycle.AddonEventDelegate charaCardPostSetupHandler = AdventurePlatePostSetup;
        IAddonLifecycle.AddonEventDelegate charaCardPreDrawHandler = AdventurePlatePreDraw;
        AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "BannerParty", agentBannerHandler);
        AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "CharaCard", charaCardPostSetupHandler);
        AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "CharaCard", charaCardPreDrawHandler);

        // events
        Framework.Update                       += on_framework_update;
        ClientState.Login                      += on_login;
        ClientState.Logout                     += on_logout;
        PluginInterface.UiBuilder.Draw         += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
    }

    private void DrawUI() => _windowSystem.Draw();
    private void ToggleConfigUI() => ConfigWindow.Toggle();

    private void ToggleConfigCommand(string command, string args) {
        if (!ClientState.IsLoggedIn)
            return;
        ToggleConfigUI();
    }

    /// <summary>
    /// called upon login and first load
    /// </summary>
    private void on_login() {
        _pluginLoaded = true;
        OwnName = ClientState.LocalPlayer?.Name.ToString();
        OwnWorld = ClientState.LocalPlayer?.HomeWorld.Value.Name.ToString() ?? "Unknown";
        OwnCID = ((Character*)ClientState.LocalPlayer?.Address)->ContentId;
        _partyListLength = -1; // forces a re-run of the party list checking
        // TODO: actually hash
        OwnHash = OwnName + ClientState.LocalPlayer?.HomeWorld.RowId ?? "Place HolderUnknown";

        // if this character doesn't exist in the configuration dictionary yet
        if (!Configuration.Portraits.ContainsKey(OwnCID ?? 0))
            Configuration.Portraits[OwnCID ?? 0] = [];
        
        // Load the current setup into penumbra
        if (PenumbraIPC.CheckAvailability())
            ReconstructTemporaryMod();
            // TODO: add an error message for if this fails & a way to re-load in the future if it becomes available
    }

    private void on_logout(int type, int code) {
        OwnName = null;
        OwnWorld = null;
        OwnHash = null;
        OwnCID = null;
    }

    private void on_framework_update(IFramework framework) {
        // we want this to run to populate certain values. but it needs to load only once
        // it'd run either on login or when the plugin first loads up
        if (!_pluginLoaded && ClientState.IsLoggedIn)
            on_login();

        if (_partyListLength == PartyList.Length)
            return;
        PluginLog.Debug($"Party list changed.\n" +
                        $"old: {this._partyListLength}\n" +
                        $"new: {PartyList.Length}"
        );
        _partyListLength = PartyList.Length;
        _partyList.Clear();
        foreach (var member in PartyList) {
            _partyList.Add(new PartyMember(member));
        }
        if (_partyList.Count == 0) {  // the player is solo
            PluginLog.Debug("solo play :D");
            if (ClientState.LocalPlayer != null)
                _partyList.Add(new PartyMember(ClientState.LocalPlayer));
        }
        foreach (var member in _partyList) {
            if (member.Name == OwnName && _helpers.CustomPortraitExists(Configuration, OwnCID ?? 0, member.ClassJob.Value.Name.ToString())) {
                member.ImagePath = Configuration.Portraits[OwnCID ?? 0][member.ClassJob.Value.Name.ToString()];
            }
            PluginLog.Debug(member.Name);
        }
    }

    private void AdventurePlatePostSetup(AddonEvent type, AddonArgs args) {
        // grab info here & download the image
    }

    private void AdventurePlatePreDraw(AddonEvent type, AddonArgs args) {
        var charaCardStruct = (AgentCharaCard.Storage*)args.Addon;
        var cid = charaCardStruct->ContentId;

        // /xldata -> Addon Inspector -> Depth Layer 5 -> CharaCard
        var charaCard = (AtkUnitBase*)args.Addon;
        var hash = _helpers.GetHashFromPlate(charaCard);  // TODO: do we still need this?
        
        // are we looking at our own plate?
        if (cid != OwnCID)
            return;
        // is an image set
        if (Configuration.Portraits[OwnCID ?? 0].GetValueOrDefault("adventure plate", "").IsNullOrEmpty())
            return;
        
        // setting image
        // node IDs: 1 > 19 > 2
        var portraitNode = (AtkComponentNode*)charaCard->GetNodeById(19);
        var portrait = (AtkImageNode*)portraitNode->Component->UldManager.SearchNodeById(2);
        portrait->LoadTexture($"tmp/portrait_plogon/{OwnCID}/adventure plate.tex");
    }

    /// <summary>
    /// Hooked to run before the party portrait interface is rendered
    /// </summary>
    private void PartyPortraitInterfacePostSetup(AddonEvent type, AddonArgs args) {
        var banner = (AtkUnitBase*)args.Addon;
        for (uint i = 1; i <= 8; i++) {
            var name = _helpers.GetNameByPlayerID(i, banner);
            var playerJob = _helpers.GetJobByPlayerID(i, banner);
            var worldId = _helpers.GetWorldIDByPlayerID(i, banner);
            foreach (var member in _partyList) {
                if (member.ImagePath.IsNullOrEmpty())
                    continue;
                if (playerJob != member.ClassJob.Value.Name || // job check
                    worldId != member.World.RowId           || // world check
                    !_helpers.CompareNames(name, member.Name)) // name check
                    continue;
                PluginLog.Debug($"Users match: {name} & {member.Name}\n"+
                                $"class: {playerJob} & {member.ClassJob.Value.Name}"
                );
                // paths should look like `tmp/portrait_plogon/[hash]/[job].tex`
                var hash = name + _helpers.GetWorldIDByPlayerID(i, banner);
                var path = $"tmp/portrait_plogon/{hash}/{playerJob}.tex";
                PluginLog.Debug($"Attempting portrait overwrite with path: {path}");
                _helpers.GetImageNodeByPlayerID(i, banner)->LoadTexture(path);
            }
        }
    }

    public void ReconstructTemporaryMod() {
        // reconstruct temporary mod & ask penumbra to load it
        var modDict = new Dictionary<string, string>();
        // portrait has type "KeyValuePair<string, string>"
        foreach (var portrait in Configuration.Portraits[OwnCID ?? 0]) {
            modDict.Add($"tmp/portrait_plogon/{OwnHash}/{portrait.Key}.tex", portrait.Value+".tex");
        }
        // PenumbraIPC.RemoveTemporaryModAll(LocalPlayerModName);  // TODO: is this needed?
        PenumbraIPC.AddTemporaryModAll(LocalPlayerModName, modDict);
    }

    public void Dispose() {
        AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "BannerParty");
        //AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "CharaCard");
        AddonLifecycle.UnregisterListener(AddonEvent.PreDraw, "CharaCard");
        Framework.Update -= on_framework_update;
        ClientState.Login -= on_login;
        ClientState.Logout -= on_logout;
        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUI;

        _windowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();

        CommandManager.RemoveHandler(ConfigCommandName);

        // Unload our mod(s) from penumbra
        // we don't edit normal game paths so this isn't *technically* needed, but it's the right thing to do
        if (PenumbraIPC.CheckAvailability())
            PenumbraIPC.RemoveTemporaryModAll(LocalPlayerModName);
        // if penumbra isn't loaded our mod won't be either so no biggie lol
    }
}


