using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Bindings.ImGui;
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
    private readonly Vector4 _errorColour = new(255, 0, 0, 255);  // Red, Green, Blue, Alpha
    private readonly PortraitPlogon _portraitPlogon;
    private string _selectedJob = "";
    private readonly IClientState _clientState;
    private readonly INotificationManager _notificationManager;
    private bool _fileDialogOpen;

    private bool _tanksSelected;
    private bool _healersSelected;
    private bool _meleeSelected;
    private bool _physRangedSelected;
    private bool _castersSelected;
    private bool _classesSelected;
    private bool _craftersSelected;
    
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
        // disabling for one line only because it's too stupid to SEE THE LINE RIGHT THERE THAT USES IT
        // ReSharper disable UnusedVariable
        var ver = Assembly.GetExecutingAssembly().GetName().Version!;
        // ReSharper restore UnusedVariable
# if DEBUG
        WindowName = "Portrait Plogon DEBUG BUILD";
# else
        // "unused variable" my ass
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
        if (!IsOpen)
            return;
        _selectedJob = _clientState.LocalPlayer.ClassJob.Value.NameEnglish.ToString();
        
        if (_tanks.Contains(_selectedJob))
            _tanksSelected = true;
        if (_healers.Contains(_selectedJob))
            _healersSelected = true;
        if (_melees.Contains(_selectedJob))
            _meleeSelected = true;
        if (_physRanged.Contains(_selectedJob))
            _physRangedSelected = true;
        if (_casters.Contains(_selectedJob))
            _castersSelected = true;
        if (_classes.Contains(_selectedJob))
            _classesSelected = true;
        if (_crafters.Contains(_selectedJob))
            _craftersSelected = true;
    }

    private void JobSelector(string job, bool selected) {
        if (ImGui.Selectable(job, selected)) {
            _selectedJob = job;
        }
    }

    private void CategoryCollapse(string category, List<string> jobList, ref bool open) {
        ImGui.SetNextItemOpen(open);
        if (!ImGui.CollapsingHeader($"{category}###Portrait{category}")) {
            open = false;
            return;
        }
        open = true;
        foreach (var job in jobList) {
            var jobSelected = _selectedJob == job;
            JobSelector(job, jobSelected);
        }
    }

    public override void Draw() {
        if (!PenumbraIPC.CheckAvailability()) {
            ImGui.TextColored(_errorColour, "Penumbra not available or wrong version.");
            return;
        }

        _fileDialogManager.Draw();
        ImGui.TextWrapped("The images selected must be 256x420.");
        
        // (mostly) copied from Dear imgui_demo (specifically the "Simple Layout" demo)
        // https://github.com/ocornut/imgui/blob/master/imgui_demo.cpp#L8815
        // Left pane
        ImGui.BeginChild("left pane", new Vector2(150, 0), true, ImGuiWindowFlags.None);
            JobSelector("Adventure Plate", _selectedJob == "Adventure Plate");
            CategoryCollapse("Tanks",       _tanks,      ref _tanksSelected);
            CategoryCollapse("Healers",     _healers,    ref _healersSelected);
            CategoryCollapse("Melee",       _melees,     ref _meleeSelected);
            CategoryCollapse("Phys Ranged", _physRanged, ref _physRangedSelected);
            CategoryCollapse("Casters",     _casters,    ref _castersSelected);
            CategoryCollapse("Classes",     _classes,    ref _classesSelected);
            CategoryCollapse("Crafters",    _crafters,   ref _craftersSelected);
        ImGui.EndChild();
        ImGui.SameLine();

        // Right pane
        ImGui.BeginGroup();
        ImGui.BeginChild(
            /*label*/  "right pane",
            /*size*/   new Vector2(0, -ImGui.GetFrameHeight()),
            /*border*/ false,
            /*flags*/  ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse
        );
            if (_selectedJob.IsNullOrEmpty()){
                return;
            }
            ImGui.Text(_selectedJob);
            if (!_errorMessage.IsNullOrEmpty()){
                ImGui.TextColored(_errorColour, _errorMessage);
            }
            ImGui.Separator();
            if (ImGui.Button("Select Image")) {
                if (!_fileDialogOpen) {
                    _fileDialogOpen = true;
                    _fileDialogManager.OpenFileDialog("Select an image.", ".png", (success, file) => {
                        _fileDialogOpen = false;
                        if (success) {
                            handle_file(file, _selectedJob.ToLower());
                        }
                    });
                }
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
                    ImGui.Image(image.Handle, new Vector2(185, 304));
                else
                    ImGui.TextColored(_errorColour, "Failure to load image.");
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
        
        // verify the folders exist & create them if not
        if (!Directory.Exists($@"{_folderPath}\{_portraitPlogon.OwnWorld}")) {
            Directory.CreateDirectory($@"{_folderPath}\{_portraitPlogon.OwnWorld}");
        }
        if (!Directory.Exists($@"{_folderPath}\{_portraitPlogon.OwnWorld}\{_portraitPlogon.OwnName}")) {
            Directory.CreateDirectory($@"{_folderPath}\{_portraitPlogon.OwnWorld}\{_portraitPlogon.OwnName}\");
        }
        
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

