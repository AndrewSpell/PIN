using GameServer.Entities;
using GameServer.StaticDB.Records.aptfs;
using GameServer.Systems.Combat;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class TargetHostilesCommand : Command, ICommand
{
    private TargetHostilesCommandDef Params;

    public TargetHostilesCommand(TargetHostilesCommandDef par)
: base(par)
    {
        Params = par;
    }

    // Keeps the current targets that are hostile to Self, minus Self, the initiator and Self's owner unless included
    public override void Execute(Context context, ref CommandResult result)
    {
        var previousTargets = context.Targets;
        var newTargets = new AptitudeTargets();

        foreach (var target in previousTargets)
        {
            if ((target == context.Self && Params.IncludeSelf == 0)
                || (target == context.Initiator && Params.IncludeInitiator == 0)
                || (target == context.Self.Owner && Params.IncludeOwner == 0))
            {
                continue;
            }

            if (target == context.Self || target == context.Initiator || target == context.Self.Owner
                || (context.Self is IEntity self && target is IEntity entity && context.Shard.Hostility.GetStance(self, entity) == HostilityStance.Hostile))
            {
                newTargets.Push(target);
            }
        }

        context.FormerTargets = previousTargets;
        context.Targets = newTargets;

        if (Params.FailNoTargets == 1 && context.Targets.Count == 0)
        {
            result.SetFail();
            return;
        }

        result.SetPass();
        return;
    }

    public override void Reset(Context context)
    {
        return;
    }
}