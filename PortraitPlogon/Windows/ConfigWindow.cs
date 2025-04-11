using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Configuration;
using ImGuiNET;
using Dalamud.Utility;
using System.Drawing;
using System.IO;
using Dalamud.Plugin.Services;
using Dalamud.IoC;
using System.Diagnostics;
using System.Reflection;

namespace PortraitPlogon.Windows;


public class ConfigWindow : Window, IDisposable {
    [PluginService] internal static IClientState clientState { get; private set; } = null!;
    
    private readonly Configuration configuration;
    private readonly string folder_path;
    private readonly FileDialogManager fileDialogManager;
    private string error_message = "";
    private static Vector4 ErrorColour = new(255, 0, 0, 255);  // Red, Green, Blue, Alpha
    private readonly PortraitPlogon portraitPlogon;
    private string selected = "";
    private readonly List<string> jobs = [
        "Adventure Plate",
        "Gladiator",
        "Paladin",
        "Marauder",
        "Warrior",
        "Dark Knight",
        "Gunbreaker",
        "Conjurer",
        "White Mage",
        "Scholar",
        "Astrologian",
        "Sage",
        "Pugilist",
        "Lancer",
        "Dragoon",
        "Rogue",
        "Ninja",
        "Samurai",
        "Reaper",
        "Viper",
        "Archer",
        "Bard",
        "Machinist",
        "Dancer",
        "Thaumaturge",
        "Arcanist",
        "Summoner",
        "Red Mage",
        "Pictomancer",
        "Blue Mage",
        "Carpenter",
        "Blacksmith",
        "Armorer",
        "Goldsmith",
        "Leatherworker",
        "Weaver",
        "Alchemist",
        "Culinarian",
        "Miner",
        "Botanist",
        "Fisher"
    ];

    public ConfigWindow(PortraitPlogon PortraitPlogon) : base("Portrait Plogon###PortraitPlogonCfg") {
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoResize;
        portraitPlogon = PortraitPlogon;
        //Size = new Vector2(232, 90);
        //SizeCondition = ImGuiCond.Always;
        configuration = PortraitPlogon.configuration;
        folder_path = PortraitPlogon.folder_path;
        fileDialogManager = new FileDialogManager();

        Size = new Vector2(350, 420);
        SizeCondition = ImGuiCond.Always;
        var ver = Assembly.GetExecutingAssembly().GetName().Version!;
        WindowName = $"Portrait Plogon ver-{ver.Major}.{ver.Minor}";
    }

    public override void Draw() {
        if (!PenumbraIPC.CheckAvailablity()) {
            ImGui.TextColored(ErrorColour, "Penumbra not available or wrong version.");
            return;
        }

        fileDialogManager.Draw();
        ImGui.TextWrapped("The images selected must be 256x420.");
        
        // (mostly) copied from Dear imgui_demo (specifically the "Simple Layout" demo)
        // https://github.com/ocornut/imgui/blob/master/imgui_demo.cpp#L8815
        // Left pane
        ImGui.BeginChild("left pane", new Vector2(150, 0), true, ImGuiWindowFlags.None);
            foreach (var job in jobs) {
                if (ImGui.Selectable(job, selected == job))
                    selected = job;
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
            if (selected.IsNullOrEmpty()){
                return;
            }
            ImGui.Text(selected);
            if (!error_message.IsNullOrEmpty()){
                ImGui.TextColored(ErrorColour, error_message);
            }
            ImGui.Separator();
            if (ImGui.Button("Select Image")) {
                fileDialogManager.OpenFileDialog("Select an image.", ".png", (success, file) => {
                    if (success) {
                        handle_file(file, selected.ToLower());
                    }
                });
            }

            // is an image currently set?
            if (configuration.Portraits[portraitPlogon.own_hash ?? "Unknown"].ContainsKey(selected.ToLower())) {
                ImGui.SameLine();
                if (ImGui.Button("Unset Image")) {
                    // unset image
                    configuration.Portraits[portraitPlogon.own_hash ?? "Unknown"].Remove(selected.ToLower());
                    portraitPlogon.ReconstructTemporaryMod();
                    configuration.Save();
                    return;
                } else {
                    // load the image
                    var path = configuration.Portraits[portraitPlogon.own_hash ?? "Unknown"][selected.ToLower()]+".png";
                    var image = PortraitPlogon.TextureProvider.GetFromFile(path).GetWrapOrDefault();
                    if (image != null)
                        // show the image
                        ImGui.Image(image.ImGuiHandle, new Vector2(171, 279));
                    else
                        ImGui.TextColored(ErrorColour, "Failure to load image.");
                }
            }
        ImGui.EndChild();
    }

    public void handle_file(string file, string job) {
        // configuration.Portraits["paladin"] = file;
        var image = System.Drawing.Image.FromFile(file);
        if (image.Width != 256 || image.Height != 420) {
            error_message = "Image must be 256x420!";
            return;
        } else if (new System.IO.FileInfo(file).Length > 500 * 1000) {
            error_message = "Max file size is 500KB!";
            return;
        }
        error_message = "";

        // copy file to our folder
        Directory.CreateDirectory($"{folder_path}\\{portraitPlogon.own_world}\\{portraitPlogon.own_name}\\{job}");
        var path = $"{folder_path}\\{portraitPlogon.own_world}\\{portraitPlogon.own_name}\\{job}";
        File.Copy(file, path+".png", true);
        configuration.Portraits[portraitPlogon.own_hash ?? "Unknown"][job] = path;
        configuration.Save();

        // convert to tex
        // we can assume penumbra is available because it'd have already been checked for at the start of Draw();
        PenumbraIPC.ConvertPngToTexAsIs(path+".png", path+".tex");
        portraitPlogon.ReconstructTemporaryMod();
    }

    public void Dispose() {
        // no clue what this is or if my code *needs* it
        // vscode will however mark this if its not here so /shrug
        GC.SuppressFinalize(this);
    }
}

