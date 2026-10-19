using GameServer.Entities.Character;

namespace GameServer.Systems.Admin.Commands;

[ServerCommand("Set the reserve ammo (and optionally the clip) of the weapon in hand", "setammo <reserve> [clip]", "setammo", "ammo")]
public class SetAmmoServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        var character = context.SourcePlayer?.CharacterEntity;
        if (character == null)
        {
            SourceFeedback("Cannot set ammo without a valid player character", context);
            return;
        }

        if (parameters.Length is < 1 or > 2)
        {
            SourceFeedback("Usage: setammo <reserve> [clip]", context);
            return;
        }

        var reserve = (ushort)ParseUIntParameter(parameters[0]);
        ushort? clip = parameters.Length == 2 ? (ushort)ParseUIntParameter(parameters[1]) : null;

        var index = character.WeaponIndex.Index;
        if (index is not (1 or 2))
        {
            SourceFeedback($"No weapon in hand (weapon index {index})", context);
            return;
        }

        character.SetActiveWeaponAmmo(reserve, clip);
        SourceFeedback($"Weapon {index}: reserve {reserve}{(clip.HasValue ? $", clip {clip}" : string.Empty)}", context);
    }
}
