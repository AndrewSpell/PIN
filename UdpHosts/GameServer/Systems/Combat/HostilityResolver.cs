using AeroMessages.GSS;
using GameServer.Entities;
using Serilog;

namespace GameServer.Systems.Combat;

public class HostilityResolver
{
    private readonly ILogger _logger;
    private readonly FactionHostility _factionHostility;

    public HostilityResolver()
    {
        _logger = Log.ForContext<HostilityResolver>();
        _factionHostility = new FactionHostility();
    }

    // Neutral targets can be damaged too, only yourself and friendlies are off limits
    public bool CanDamage(IEntity attacker, IEntity target)
    {
        var stance = GetStance(attacker, target);
        return stance != HostilityStance.Self && stance != HostilityStance.Friendly;
    }

    public HostilityStance GetStance(IEntity a, IEntity b)
    {
        if (a.EntityId == b.EntityId)
        {
            return HostilityStance.Self;
        }

        return GetStance(a.HostilityInfo, b.HostilityInfo);
    }

    public HostilityStance GetStance(HostilityInfoData aInfo, HostilityInfoData bInfo)
    {
        // Duel overrides
        if (IsDueling(aInfo, bInfo))
        {
            return HostilityStance.Hostile;
        }

        // Default faction relationship
        return _factionHostility.GetFactionStance(aInfo.FactionId, bInfo.FactionId);
    }

    public bool IsDueling(HostilityInfoData a, HostilityInfoData b)
    {
        return false;
    }
}