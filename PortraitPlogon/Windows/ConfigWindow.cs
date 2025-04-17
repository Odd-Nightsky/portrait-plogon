using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.ImGuiFileDialog;
using ImGuiNET;
using Dalamud.Utility;
using System.IO;
using System.Reflection;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;

namespace PortraitPlogon.Windows;


public class ConfigWindow : Window, IDisposable {
    //[PluginService] internal static IClientState ClientState { get; private set; } = null!;
    
    private readonly Configuration _configuration;
    private readonly string _folderPath;
    private readonly FileDialogManager _fileDialogManager;
    private string _errorMessage = "";
    private static readonly Vector4 ErrorColour = new(255, 0, 0, 255);  // Red, Green, Blue, Alpha
    private readonly PortraitPlogon _portraitPlogon;
    private string _selectedJob = "";
    private readonly IClientState _clientState;
    private readonly INotificationManager _notificationManager;

    // private bool _tanksSelected      = true;
    // private bool _healersSelected    = true;
    // private bool _meleeSelected      = true;
    // private bool _physRangedSelected = true;
    // private bool _castersSelected    = true;
    // private bool _classesSelected    = true;
    // private bool _craftersSelected   = true;
    
    private readonly List<string> _tanks = [
        // tanks
        "Paladin",
        "Warrior",
        "Dark Knight",
        "Gunbreaker"
    ];
    private readonly List<string> _healers = [
        // healers
        "White Mage",
        "Scholar",
        "Astrologian",
        "Sage",
    ];
    private readonly List<string> _melees = [
        // melee
        "Monk",
        "Dragoon",
        "Ninja",
        "Samurai",
        "Reaper",
        "Viper",
    ];
    private readonly List<string> _physRanged = [
        // phys ranged
        "Bard",
        "Machinist",
        "Dancer",
    ];
    private readonly List<string> _casters = [
        // casters
        "Black Mage",
        "Summoner",
        "Red Mage",
        "Pictomancer",
        "Blue Mage",
    ];
    private readonly List<string> _classes = [
        // classes
        "Gladiator",
        "Marauder",
        "Conjurer",
        "Pugilist",
        "Lancer",
        "Rogue",
        "Archer",
        "Thaumaturge",
        "Arcanist",
    ];
    private readonly List<string> _crafters = [
        // crafters
        "Carpenter",
        "Blacksmith",
        "Armorer",
        "Goldsmith",
        "Leatherworker",
        "Weaver",
        "Alchemist",
        "Culinarian",
        // gatherers
        "Miner",
        "Botanist",
        "Fisher"
    ];

    public ConfigWindow(PortraitPlogon portraitPlogon, IClientState clientState, INotificationManager notificationManager) : base("Portrait Plogon###PortraitPlogonCfg") {
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoResize;
        _portraitPlogon = portraitPlogon;
        _configuration = portraitPlogon.Configuration;
        _folderPath = portraitPlogon.FolderPath;
        _fileDialogManager = new FileDialogManager();
        _clientState = clientState;
        _notificationManager = notificationManager;

        Size = new Vector2(360, 420);
        SizeCondition = ImGuiCond.Always;
        var ver = Assembly.GetExecutingAssembly().GetName().Version!;
# if DEBUG
        WindowName = "Portrait Plogon DEBUG BUILD";
# else
        WindowName = $"Portrait Plogon ver: {ver.Major}.{ver.Minor}.{ver.Build}";
# endif
    }

    public new void Toggle() {
        if (_clientState.LocalPlayer == null) {
            _notificationManager.AddNotification(new Notification {
                Title = "Unable to open portrait configuration",
                Content = "Please login before you open the settings",
                Type = NotificationType.Error
            });
            return;
        }

        base.Toggle();
        if (IsOpen){
            _selectedJob = _clientState.LocalPlayer.ClassJob.Value.NameEnglish.ToString();
        }
    }

    private void JobSelector(string job, bool selected) {
        if (ImGui.Selectable(job, selected)) {
            _selectedJob = job;
        }
    }

    private void CategoryCollapse(string category, List<string> jobList) {
        if (!ImGui.CollapsingHeader(category))
            return;
        foreach (var job in jobList) {
            var jobSelected = _selectedJob == job;
            JobSelector(job, jobSelected);
        }
    }

    public override void Draw() {
        if (!PenumbraIPC.CheckAvailability()) {
            ImGui.TextColored(ErrorColour, "Penumbra not available or wrong version.");
            return;
        }

        _fileDialogManager.Draw();
        ImGui.TextWrapped("The images selected must be 256x420.");
        
        // (mostly) copied from Dear imgui_demo (specifically the "Simple Layout" demo)
        // https://github.com/ocornut/imgui/blob/master/imgui_demo.cpp#L8815
        // Left pane
        ImGui.BeginChild("left pane", new Vector2(150, 0), true, ImGuiWindowFlags.None);
            JobSelector("Adventure Plate", _selectedJob == "Adventure Plate");
            CategoryCollapse("Tanks",       _tanks);
            CategoryCollapse("Healers",     _healers);
            CategoryCollapse("Melee",       _melees);
            CategoryCollapse("Phys Ranged", _physRanged);
            CategoryCollapse("Casters",     _casters);
            CategoryCollapse("Classes",     _classes);
            CategoryCollapse("Crafters",    _crafters);
            ImGui.EndChild();
        ImGui.SameLine();

        // Right pane
        ImGui.BeginGroup();
        ImGui.BeginChild(
            /*label*/  "item view",
            /*size*/   new Vector2(0, -ImGui.GetFrameHeight()),
            /*border*/ false,
            /*flags*/  ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse
        );
            if (_selectedJob.IsNullOrEmpty()){
                return;
            }
            ImGui.Text(_selectedJob);
            if (!_errorMessage.IsNullOrEmpty()){
                ImGui.TextColored(ErrorColour, _errorMessage);
            }
            ImGui.Separator();
            if (ImGui.Button("Select Image")) {
                _fileDialogManager.OpenFileDialog("Select an image.", ".png", (success, file) => {
                    if (success) {
                        handle_file(file, _selectedJob.ToLower());
                    }
                });
            }

            // is an image currently set?
            if (_configuration.Portraits[_portraitPlogon.OwnCID ?? 0].ContainsKey(_selectedJob.ToLower())) {
                ImGui.SameLine();
                if (ImGui.Button("Unset Image")) {
                    // unset image
                    _configuration.Portraits[_portraitPlogon.OwnCID ?? 0].Remove(_selectedJob.ToLower());
                    _portraitPlogon.ReconstructTemporaryMod();
                    _configuration.Save();
                    return;
                }
                // load the image
                var path = _configuration.Portraits[_portraitPlogon.OwnCID ?? 0][_selectedJob.ToLower()]+".png";
                var image = PortraitPlogon.TextureProvider.GetFromFile(path).GetWrapOrDefault();
                if (image != null)
                    // show the image
                    ImGui.Image(image.ImGuiHandle, new Vector2(185, 304));
                else
                    ImGui.TextColored(ErrorColour, "Failure to load image.");
            }
        ImGui.EndChild();
    }

    private void handle_file(string file, string job) {
        // configuration.Portraits["paladin"] = file;
        var image = System.Drawing.Image.FromFile(file);
        if (image.Width != 256 || image.Height != 420) {
            _errorMessage = "Image must be 256x420!";
            return;
        }
        if (new FileInfo(file).Length > 500 * 1000) {
            _errorMessage = "Max file size is 500KB!";
            return;
        }
        _errorMessage = "";

        // copy file to our folder
        var path = $@"{_folderPath}\{_portraitPlogon.OwnWorld}\{_portraitPlogon.OwnName}\{job}";
        File.Copy(file, path+".png", true);
        _configuration.Portraits[_portraitPlogon.OwnCID ?? 0][job] = path;
        _configuration.Save();

        // convert to tex
        // we can assume penumbra is available because it'd have already been checked for at the start of Draw();
        PenumbraIPC.ConvertPngToTexAsIs(path+".png", path+".tex");
        _portraitPlogon.ReconstructTemporaryMod();
    }

    public void Dispose() {
        // no clue what this is or if my code *needs* it
        // vscode will however mark this if it's not here so /shrug
        GC.SuppressFinalize(this);
    }
}

