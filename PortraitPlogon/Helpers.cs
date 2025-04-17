using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Collections.Generic;
using Dalamud.Utility;
using System.Linq;
using Lumina.Excel.Sheets;

namespace PortraitPlogon;  // prob a bad name but :3

public unsafe class Helpers {
    private static IDataManager _data = null!;
    public Helpers(IDataManager data) {
        _data = data;
    }
    public AtkImageNode* GetImageNodeByPlayerID(uint id, AtkUnitBase* banner) {
        // this code sucks lol
        // use /xldata and the Addon Inspector to help you make sense of what's happening here
        // Depth Layer 5 -> BannerParty
        var nodeID = (id*2)+2;  // player 1 = nodeID 4, player 2 = nodeID 6, etc
        var player = (AtkComponentNode*)banner->GetNodeById(nodeID);
        var portraitNode = (AtkComponentNode*)player->Component->UldManager.SearchNodeById(20);
        return (AtkImageNode*)portraitNode->Component->UldManager.SearchNodeById(2);
    }

    public PlayerInfo GetInfoByPlayerID(uint id, AtkUnitBase* banner) {
        // this code sucks lol
        // use /xldata and the Addon Inspector to help you make sense of what's happening here
        // Depth Layer 5 -> BannerParty
        var name = GetNameByPlayerID(id, banner);
        var worldID = GetWorldIDByPlayerID(id, banner);
        var job = GetJobByPlayerID(id, banner);

        return new PlayerInfo(name, worldID, job);
    }

    public string GetNameByPlayerID(uint id, AtkUnitBase* banner) {
        // this code sucks lol
        // use /xldata and the Addon Inspector to help you make sense of what's happening here
        // Depth Layer 5 -> BannerParty
        var nodeID = (id*2)+2;  // player 1 = nodeID 4, player 2 = nodeID 6, etc
        var player = (AtkComponentNode*)banner->GetNodeById(nodeID);
        
        // getting name
        var firstNameNode = (AtkTextNode*)player->Component->UldManager.SearchNodeById(6);
        var firstName = firstNameNode->NodeText.ToString();
        var lastNameNode = (AtkTextNode*)player->Component->UldManager.SearchNodeById(7);
        var lastName = lastNameNode->NodeText.ToString();
        return $"{firstName} {lastName}";
    }

    public uint GetWorldIDByPlayerID(uint id, AtkUnitBase* banner) {
        // this code sucks lol
        // use /xldata and the Addon Inspector to help you make sense of what's happening here
        // Depth Layer 5 -> BannerParty
        var nodeID = (id*2)+2;  // player 1 = nodeID 4, player 2 = nodeID 6, etc
        var player = (AtkComponentNode*)banner->GetNodeById(nodeID);

        // getting world
        var worldNode = (AtkTextNode*)player->Component->UldManager.SearchNodeById(12);
        var worldName = worldNode->NodeText.ToString();
        return GetWorldIDByName(worldName);
    }

    /// <summary>
    /// gets job name out of the banner interface
    /// </summary>
    public string GetJobByPlayerID(uint id, AtkUnitBase* banner) {
        // this code sucks lol
        // use /xldata and the Addon Inspector to help you make sense of what's happening here
        // Depth Layer 5 -> BannerParty
        var nodeID = (id*2)+2;  // player 1 = nodeID 4, player 2 = nodeID 6, etc
        var player = (AtkComponentNode*)banner->GetNodeById(nodeID);

        // getting job
        var jobNode = (AtkTextNode*)player->Component->UldManager.SearchNodeById(9);
        return jobNode->NodeText.ToString();
    }

    public uint GetWorldIDByName(string worldName) {
        return _data.GetExcelSheet<World>().FirstOrDefault(world => world.Name == worldName).RowId;
    }

    public string GetHashFromPlate(AtkUnitBase* charaCard) {
        // get the hash out of an adventure plate
        // getting name
        // node IDs: 1 > 4 > 5
        var nameNodeParent = (AtkComponentNode*)charaCard->GetNodeById(4);
        var nameNode = (AtkTextNode*)nameNodeParent->Component->UldManager.SearchNodeById(5);
        var name = nameNode->NodeText.ToString();

        // getting world
        // node IDs: 1 > 5 > 3
        var worldNodeParent = (AtkComponentNode*)charaCard->GetNodeById(5);
        var worldNode = (AtkTextNode*)worldNodeParent->Component->UldManager.SearchNodeById(3);
        var world = worldNode->NodeText.ToString();
        // world = $"{world_name} [{DataCenter}]"
        world = world.Split('[')[0].TrimEnd();
        var worldID = GetWorldIDByName(world);
        return name+worldID;
    }

    /// <summary>
    /// is a custom portrait set for player with job
    /// </summary>
    public bool CustomPortraitExists(Configuration configuration, ulong cid, string playerJob) {
        return !configuration.Portraits[cid].GetValueOrDefault(playerJob, "").IsNullOrEmpty();
    }

    /// <summary>
    /// compare two names by every possible configuration
    /// this sucks
    /// </summary>
    public bool CompareNames(string bannerName, string compareName) {
        // Full Name
        if (bannerName == compareName)
            return true;
        
        var nameArray = compareName.Split();
        // Surname Abbreviated
        if (bannerName == $"{nameArray[0]} {nameArray[1][0]}.")
            return true;
        // Forename Abbreviated
        if (bannerName == $"{nameArray[0][0]}. {nameArray[1]}")
            return true;
        // Initials
        if (bannerName == $"{nameArray[0][0]}. {nameArray[1][0]}.")
            return true;
        return false;
    }
}
