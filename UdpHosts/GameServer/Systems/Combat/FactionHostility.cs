using System.Collections.Generic;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbcharacter;
using Serilog;

namespace GameServer.Systems.Combat;

public class FactionHostility
{
    private readonly Dictionary<(uint, uint), (int Precedence, sbyte Stance)> _relations;
    private readonly Dictionary<uint, Faction> _factions;
    private readonly ILogger _logger;

    public FactionHostility()
    {
        _logger = Log.ForContext<FactionHostility>();
        _relations = [];
        _factions = [];
        LoadFromSDB();
    }

    public void LoadFromSDB()
    {
        foreach (var faction in SDBInterface.GetFactions())
        {
            _factions[faction.Id] = faction;
        }

        foreach (var relation in SDBInterface.GetFactionRelations())
        {
            // A row naming both factions beats a row with one wildcard (faction 0), which beats the all-factions row
            var precedence = (relation.FactionA == 0 ? 0 : 1) + (relation.FactionB == 0 ? 0 : 1) + 1;
            foreach (var a in Expand(relation.FactionA))
            {
                foreach (var b in Expand(relation.FactionB))
                {
                    SetRelation((a, b), precedence, relation.HostilityStance);
                    if (relation.HostilityBidirectional == 1)
                    {
                        SetRelation((b, a), precedence, relation.HostilityStance);
                    }
                }
            }
        }

        _logger.Debug("FactionHostility initialized with {Count} faction pairs", _relations.Count);
    }

    public bool IsFriendlyFaction(uint sourceFactionId, uint targetFactionId)
    {
        return GetFactionStance(sourceFactionId, targetFactionId) == HostilityStance.Friendly;
    }

    public bool IsHostileFaction(uint sourceFactionId, uint targetFactionId)
    {
        return GetFactionStance(sourceFactionId, targetFactionId) == HostilityStance.Hostile;
    }

    // FactionRelations.hostility_stance runs from -2 to 2: negative is hostile, 0 neutral, positive friendly.
    // A neutral relation falls back to the factions' default stance, where -1 means hostile to everyone.
    public HostilityStance GetFactionStance(uint sourceFactionId, uint targetFactionId)
    {
        var stance = _relations.TryGetValue((sourceFactionId, targetFactionId), out var relation) ? relation.Stance : (sbyte)0;
        if (stance >= 1)
        {
            return HostilityStance.Friendly;
        }

        if (stance <= -1 || DefaultStance(sourceFactionId) <= -1 || DefaultStance(targetFactionId) <= -1)
        {
            return HostilityStance.Hostile;
        }

        return HostilityStance.Neutral;
    }

    /*
    public static void ComputePersonalFactionStance(uint factionId)
    {
        var factions = SDBInterface.GetFactions();
        var totalBytes = (((uint)factions.Count >> 6) + 1) << 3; // 8
        var byteIndex = 0;
        var bitIndex = 0;
        var friendly = new byte[totalBytes];
        var hostile = new byte[totalBytes];
        foreach (var faction in factions)
        {

        }
    }
    */

    private IEnumerable<uint> Expand(uint factionId) => factionId == 0 ? _factions.Keys : [factionId];

    private sbyte DefaultStance(uint factionId) => _factions.TryGetValue(factionId, out var faction) ? faction.DefaultStance : (sbyte)0;

    private void SetRelation((uint, uint) key, int precedence, sbyte stance)
    {
        // Equal precedence keeps the old behaviour of the later row winning
        if (!_relations.TryGetValue(key, out var existing) || precedence >= existing.Precedence)
        {
            _relations[key] = (precedence, stance);
        }
    }
}
