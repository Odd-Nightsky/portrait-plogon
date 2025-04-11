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

namespace PortraitPlogon;  // prob a bad name but :3

public unsafe class Helpers {
    internal static IDataManager Data;
    public Helpers(IDataManager data) {
        Data = data;
    }
    public AtkImageNode* GetImageNodeByPlayerID(uint ID, AtkUnitBase* banner) {
        // this code sucks lol
        // use /xldata and the Addon Inspector to help you make sense of whats happening here
        // Depth Layer 5 -> BannerParty
        var nodeID = (ID*2)+2;  // player 1 = nodeID 4, player 2 = nodeID 6, etc
        var player = (AtkComponentNode*)banner->GetNodeById(nodeID);
        var portrait_node = (AtkComponentNode*)player->Component->UldManager.SearchNodeById(20);
        return (AtkImageNode*)portrait_node->Component->UldManager.SearchNodeById(2);
    }

    public PlayerInfo GetInfoByPlayerID(uint ID, AtkUnitBase* banner) {
        // this code sucks lol
        // use /xldata and the Addon Inspector to help you make sense of whats happening here
        // Depth Layer 5 -> BannerParty
        var nodeID = (ID*2)+2;  // player 1 = nodeID 4, player 2 = nodeID 6, etc
        var player = (AtkComponentNode*)banner->GetNodeById(nodeID);
        
        var name = GetNameByPlayerID(ID, banner);
        var world_id = GetWorldIDByPlayerID(ID, banner);
        var job = GetJobByPlayerID(ID, banner);

        return new PlayerInfo(name, world_id, job);
    }

    public string GetNameByPlayerID(uint ID, AtkUnitBase* banner) {
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

    public uint GetWorldIDByPlayerID(uint ID, AtkUnitBase* banner) {
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

    /// <summary>
    /// gets job name out of the banner interface
    /// </summary>
    public string GetJobByPlayerID(uint ID, AtkUnitBase* banner) {
        // this code sucks lol
        // use /xldata and the Addon Inspector to help you make sense of whats happening here
        // Depth Layer 5 -> BannerParty
        var nodeID = (ID*2)+2;  // player 1 = nodeID 4, player 2 = nodeID 6, etc
        var player = (AtkComponentNode*)banner->GetNodeById(nodeID);

        // getting job
        var job_node = (AtkTextNode*)player->Component->UldManager.SearchNodeById(9);
        return job_node->NodeText.ToString();
    }

    public uint GetWorldIDByName(string world_name) {
        return Data.GetExcelSheet<World>()!
            .Where(world => world.Name == world_name).SingleOrDefault().RowId;
    }

    public string GetHashFromPlate(AtkUnitBase* CharaCard) {
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

    /// <summary>
    /// is a custom portrait set for player with job
    /// </summary>
    public bool CustomPortraitExists(Configuration configuration, string hash, string player_job) {
        return !configuration.Portraits[hash ?? "Unknown"].GetValueOrDefault(player_job, "").IsNullOrEmpty();
    }

    /// <summary>
    /// compare two names by every possible configuration
    /// this sucks
    /// </summary>
    public bool CompareNames(string BannerName, string CompareName) {
        // Full Name
        if (BannerName == CompareName)
            return true;
        
        var NameArray = CompareName.Split();
        // Surname Abbreviated
        if (BannerName == $"{NameArray[0]} {NameArray[1][0]}.")
            return true;
        // Forename Abbreviated
        if (BannerName == $"{NameArray[0][0]}. {NameArray[1]}")
            return true;
        // Initials
        if (BannerName == $"{NameArray[0][0]}. {NameArray[1][0]}.")
            return true;
        return false;
    }
}
