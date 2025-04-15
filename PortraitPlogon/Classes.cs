using Dalamud.Game.ClientState.Party;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace PortraitPlogon;


// Custom Party member object for my stupid dumb dumb brain
public class PartyMember {

    public PartyMember(IPartyMember member) {
        Name = member.Name.TextValue;
        World = member.World;
        ClassJob = member.ClassJob;
        ClassJobId = member.ClassJob.RowId;
        Hash = $"{Name}{World.RowId}";
        // always hash with english world name. its what the server expects
        // TODO: actually hash this
    }
    public PartyMember(IPlayerCharacter member) {
        Name = member.Name.TextValue;
        World = member.HomeWorld;
        ClassJob = member.ClassJob;
        ClassJobId = member.ClassJob.RowId;
        Hash = $"{Name}{World.RowId}";
        // always hash with english world name. its what the server expects
        // TODO: actually hash this
    }

    [JsonIgnore]
    public string Name { get; set; }
    public string Hash { get; set; }
    [JsonIgnore]
    public string? ImagePath { get; set; }
    [JsonIgnore]
    public RowRef<World> World { get; }
    [JsonIgnore]
    public RowRef<ClassJob> ClassJob { get; }
    [JsonPropertyName("ClassJob")]
    public uint ClassJobId { get; set; }
}

public class JsonData(List<PartyMember> list, string key, string ownHash)
{
    public string APIKey { get; } = key;
    public string Self { get; } = ownHash;
    public List<PartyMember> PartyMembers { get; set; } = list;
}

public class PlayerInfo(string name, uint worldId, string job)
{
    public string Name = name;
    public uint WorldId = worldId;
    public string Job = job;
}

