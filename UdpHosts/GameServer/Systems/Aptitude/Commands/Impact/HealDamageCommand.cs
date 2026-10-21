using System;
using GameServer.Entities;
using GameServer.Enums;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Impact;

public class HealDamageCommand : Command, ICommand
{
    private HealDamageCommandDef Params;

    public HealDamageCommand(HealDamageCommandDef par)
: base(par)
    {
        Params = par;
    }

    // TODO: Params.Usedmgdealt, Params.Weapondamage and Params.DamageType aren't used yet. The Arc Thrower already
    // scales its register from the Weapon Damage stat, so its repair comes out right without them.
    public override void Execute(Context context, ref CommandResult result)
    {
        var amount = (int)MathF.Round(AbilitySystem.RegistryOp(context.Register, Params.Healpoints, (Operand)Params.HealpointsRegop));
        if (amount > 0)
        {
            foreach (var target in context.Targets)
            {
                if (target is IEntity entity)
                {
                    context.Shard.Damage.ApplyHeal(entity, amount, context.Initiator as IEntity);
                    Logger.Debug("{Command} {CommandId}: healed {Target} for {Amount}", nameof(HealDamageCommand), Params.Id, entity, amount);
                }
            }
        }

        result.SetPass();
    }
}