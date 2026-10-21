using System;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Damage;

public class InflictDamageCommand : Command, ICommand
{
    private InflictDamageCommandDef Params;

    public InflictDamageCommand(InflictDamageCommandDef par)
: base(par)
    {
        Params = par;
    }

    // TODO: Splashrange, Pointblankrange, Falloff, Usedmgdealt and the damage types aren't used yet
    public override void Execute(Context context, ref CommandResult result)
    {
        // A projectile has already dealt its weapon damage, this would deal it a second time
        if (Params.Weapondamage == 1 && context.ExecutionHint == ExecutionHint.Impact)
        {
            result.SetPass();
            return;
        }

        float damage = Params.Weapondamage == 1 && context.Initiator is CharacterEntity character
            ? character.GetItemAttribute((ushort)ItemAttributeId.WeaponDamage)
            : AbilitySystem.RegistryOp(context.Register, Params.Damagepoints, (Operand)Params.DamagepointsRegop);
        var amount = (int)MathF.Round(damage);

        if (amount > 0 && context.Initiator is IEntity source)
        {
            foreach (var target in context.Targets)
            {
                // Targeting commands should have filtered friendlies already, this keeps a misread filter from hurting them
                if (target is IEntity entity && context.Shard.Hostility.CanDamage(source, entity))
                {
                    context.Shard.Damage.ApplyDamage(entity, amount, source);
                    Logger.Debug("{Command} {CommandId}: dealt {Amount} to {Target}", nameof(InflictDamageCommand), Params.Id, amount, entity);
                }
            }
        }

        result.SetPass();
    }
}