using GameServer.Entities;
using GameServer.StaticDB.Records.aptfs;
using GameServer.Systems.Combat;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class TargetByHostilityCommand : Command, ICommand
{
    private TargetByHostilityCommandDef Params;

    public TargetByHostilityCommand(TargetByHostilityCommandDef par)
: base(par)
    {
        Params = par;
    }

    // FilterType values, inferred from how the Arc Thrower's impact ability uses them: 0 picks the targets it
    // repairs, 1 and 2 the ones it damages. Other values aren't understood yet and select nothing.
    public override void Execute(Context context, ref CommandResult result)
    {
        var compareFrom = Params.CompareFromInitiator == 1 ? context.Initiator : context.Self;
        var previousTargets = context.Targets;
        var newTargets = new AptitudeTargets();

        if (Params.FilterType > 2)
        {
            Logger.Warning("{Command} {CommandId} has unknown FilterType {FilterType}, selecting no targets", nameof(TargetByHostilityCommand), Params.Id, Params.FilterType);
        }
        else if (compareFrom is IEntity from)
        {
            foreach (var target in previousTargets)
            {
                if (target is not IEntity to)
                {
                    continue;
                }

                if (to.EntityId == context.Initiator?.EntityId && Params.IncludeInitiator != 1)
                {
                    continue;
                }

                var stance = context.Shard.Hostility.GetStance(from, to);
                if (stance == HostilityStance.Self)
                {
                    if (Params.IncludeSelf == 1)
                    {
                        newTargets.Push(target);
                    }

                    continue;
                }

                var matches = Params.FilterType switch
                {
                    0 => stance == HostilityStance.Friendly,
                    1 => stance == HostilityStance.Hostile,
                    _ => stance is HostilityStance.Hostile or HostilityStance.Neutral,
                };

                if (matches)
                {
                    newTargets.Push(target);
                }
            }
        }

        context.FormerTargets = previousTargets;
        context.Targets = newTargets;

        if (Params.FailNoTargets == 1 && context.Targets.Count == 0)
        {
            result.SetFail();
        }
        else
        {
            result.SetPass();
        }
    }

    public override void Reset(Context context)
    {
        return;
    }
}