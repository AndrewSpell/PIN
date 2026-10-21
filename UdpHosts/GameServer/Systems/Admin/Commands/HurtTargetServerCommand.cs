using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.Entities.Deployable;

namespace GameServer.Systems.Admin.Commands;

[ServerCommand("Damage what you're aiming at (or the server command target), ignoring hostility", "hurt <amount>", "hurt", "hurttarget")]
public class HurtTargetServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        if (context.SourcePlayer?.CharacterEntity == null)
        {
            SourceFeedback("Cannot aim without a valid player character", context);
            return;
        }

        if (parameters.Length != 1)
        {
            SourceFeedback("Usage: hurt <amount>", context);
            return;
        }

        int amount = (int)ParseUIntParameter(parameters[0]);
        if (amount <= 0)
        {
            SourceFeedback("Damage amount must be positive", context);
            return;
        }

        IEntity target = GetGunTarget(context.SourcePlayer.CharacterEntity, context) ?? context.Target;
        if (target == null)
        {
            SourceFeedback("Nothing under your crosshair and no target set", context);
            return;
        }

        context.Shard.Damage.ApplyDamage(target, amount, context.SourcePlayer.CharacterEntity);

        var health = target switch
        {
            CharacterEntity character => $"{character.CurrentHealth}/{character.MaxHealth.Value}",
            DeployableEntity deployable => $"{deployable.CurrentHealth}/{deployable.MaxHealth}",
            _ => "n/a",
        };
        SourceFeedback($"Dealt {amount} damage to {target}. Health: {health}", context);
    }
}
