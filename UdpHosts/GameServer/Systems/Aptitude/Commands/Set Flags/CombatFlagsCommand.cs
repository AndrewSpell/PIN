using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;
using Flags = AeroMessages.GSS.Character.CombatFlagsData.CharacterCombatFlags;

namespace GameServer.Systems.Aptitude.Commands.SetFlags;

// Sets combat flags on the effect's target while the effect lasts, e.g. the Arc Thrower's overheat restricts the weapon.
// ImmunePhysics and ImmuneDeath have no known bit in CharacterCombatFlags, so they aren't sent.
public class CombatFlagsCommand : Command, ICommand
{
    private readonly Flags _flags;
    private CombatFlagsCommandDef Params;

    public CombatFlagsCommand(CombatFlagsCommandDef par)
: base(par)
    {
        Params = par;
        _flags |= par.RestrictMovement == 1 ? Flags.restrict_movement : 0;
        _flags |= par.RestrictWeapon == 1 ? Flags.restrict_weapon : 0;
        _flags |= par.RemoveHitboxes == 1 ? Flags.remove_hitboxes : 0;
        _flags |= par.RestrictAbilities == 1 ? Flags.restrict_abilities : 0;
        _flags |= par.ImmuneFalldamage == 1 ? Flags.immune_falldamage : 0;
        _flags |= par.RestrictSprint == 1 ? Flags.restrict_sprint : 0;
        _flags |= par.RestrictMelee == 1 ? Flags.restrict_melee : 0;
        _flags |= par.RestrictInteraction == 1 ? Flags.restrict_interaction : 0;
        _flags |= par.KnockDown == 1 ? Flags.knock_down : 0;
        _flags |= par.RestrictStumble == 1 ? Flags.restrict_stumble : 0;
        _flags |= par.MoveThroughObjects == 1 ? Flags.move_through_objects : 0;
        _flags |= par.ReversedControls == 1 ? Flags.reversed_controls : 0;
    }

    public override void Execute(Context context, ref CommandResult result)
    {
        if (context.Self is CharacterEntity)
        {
            context.Actives.TryAdd(this, null);
        }

        result.SetPass();
    }

    public override void Reset(Context context)
    {
        return;
    }

    public void OnApply(Context context, ICommandActiveContext activeCommandContext)
    {
        context.CombatFlags |= _flags;
        if (context.Self is CharacterEntity character)
        {
            character.RefreshCombatFlags();
        }
    }

    public void OnRemove(Context context, ICommandActiveContext activeCommandContext)
    {
        context.CombatFlags &= ~_flags;
        if (context.Self is CharacterEntity character)
        {
            character.RefreshCombatFlags();
        }
    }
}
