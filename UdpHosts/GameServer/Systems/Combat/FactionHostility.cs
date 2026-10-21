using System.Collections.Generic;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbcharacter;
using Serilog;

namespace GameServer.Systems.Combat;

public class FactionHostility
{
    private readonly Dictionary<(uint, uint), sbyte> _relations;
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

        // Faction 0 is "no faction", not a wildcard: reading it as "every faction" made accord friendly to the
        // neutral faction 9, while the client shows those (e.g. Brontodon 582) grey and lets you shoot them
        foreach (var relation in SDBInterface.GetFactionRelations())
        {
            _relations[(relation.FactionA, relation.FactionB)] = relation.HostilityStance;
            if (relation.HostilityBidirectional == 1)
            {
                _relations[(relation.FactionB, relation.FactionA)] = relation.HostilityStance;
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
    // Without a relation row, a faction is friendly to itself, and otherwise the default stances decide
    // (-1 hostile to everyone, 1 friendly to everyone).
    public HostilityStance GetFactionStance(uint sourceFactionId, uint targetFactionId)
    {
        if (_relations.TryGetValue((sourceFactionId, targetFactionId), out var stance))
        {
            return stance >= 1 ? HostilityStance.Friendly : stance <= -1 ? HostilityStance.Hostile : HostilityStance.Neutral;
        }

        if (sourceFactionId == targetFactionId)
        {
            return HostilityStance.Friendly;
        }

        if (DefaultStance(sourceFactionId) <= -1 || DefaultStance(targetFactionId) <= -1)
        {
            return HostilityStance.Hostile;
        }

        if (DefaultStance(sourceFactionId) >= 1 || DefaultStance(targetFactionId) >= 1)
        {
            return HostilityStance.Friendly;
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

    private sbyte DefaultStance(uint factionId) => _factions.TryGetValue(factionId, out var faction) ? faction.DefaultStance : (sbyte)0;
}
