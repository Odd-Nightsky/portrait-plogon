using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.ImGuiFileDialog;
using ImGuiNET;
using Dalamud.Utility;
using System.IO;
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
    private string _selected = "";
    private IClientState _clientState;
    private INotificationManager _notificationManager;
    private readonly List<string> _jobs = [
        "Adventure Plate",
        // tanks
        "Gladiator",
        "Paladin",
        "Marauder",
        "Warrior",
        "Dark Knight",
        "Gunbreaker",
        // healers
        "Conjurer",
        "White Mage",
        "Scholar",
        "Astrologian",
        "Sage",
        // melee
        "Pugilist",
        "Lancer",
        "Dragoon",
        "Rogue",
        "Ninja",
        "Samurai",
        "Reaper",
        "Viper",
        // phys ranged
        "Archer",
        "Bard",
        "Machinist",
        "Dancer",
        // caster
        "Thaumaturge",
        "Arcanist",
        "Summoner",
        "Red Mage",
        "Pictomancer",
        "Blue Mage",
        // crafter
        "Carpenter",
        "Blacksmith",
        "Armorer",
        "Goldsmith",
        "Leatherworker",
        "Weaver",
        "Alchemist",
        "Culinarian",
        // gatherer
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
# if DEBUG
        WindowName = "Portrait Plogon DEBUG BUILD";
# else
        var ver = Assembly.GetExecutingAssembly().GetName().Version!;
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
            _selected = _clientState.LocalPlayer.ClassJob.Value.NameEnglish.ToString();
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
            foreach (var job in _jobs) {
                if (ImGui.Selectable(job, _selected == job))
                    _selected = job;
            }
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
            if (_selected.IsNullOrEmpty()){
                return;
            }
            ImGui.Text(_selected);
            if (!_errorMessage.IsNullOrEmpty()){
                ImGui.TextColored(ErrorColour, _errorMessage);
            }
            ImGui.Separator();
            if (ImGui.Button("Select Image")) {
                _fileDialogManager.OpenFileDialog("Select an image.", ".png", (success, file) => {
                    if (success) {
                        handle_file(file, _selected.ToLower());
                    }
                });
            }

            // is an image currently set?
            if (_configuration.Portraits[_portraitPlogon.OwnCID ?? 0].ContainsKey(_selected.ToLower())) {
                ImGui.SameLine();
                if (ImGui.Button("Unset Image")) {
                    // unset image
                    _configuration.Portraits[_portraitPlogon.OwnCID ?? 0].Remove(_selected.ToLower());
                    _portraitPlogon.ReconstructTemporaryMod();
                    _configuration.Save();
                    return;
                }
                // load the image
                var path = _configuration.Portraits[_portraitPlogon.OwnCID ?? 0][_selected.ToLower()]+".png";
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
        Directory.CreateDirectory($@"{_folderPath}\{_portraitPlogon.OwnWorld}\{_portraitPlogon.OwnName}\{job}");
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

