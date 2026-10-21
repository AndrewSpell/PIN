using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

public class RequireItemAttributeCommand : Command, ICommand
{
    private RequireItemAttributeCommandDef Params;

    public RequireItemAttributeCommand(RequireItemAttributeCommandDef par)
: base(par)
    {
        Params = par;
    }

    // Passes when Self has the item attribute at all, e.g. the Frostbite Cryo Grenade Flag (1242) a perk adds
    public override void Execute(Context context, ref CommandResult result)
    {
        if (context.Self is CharacterEntity character && character.GetItemAttribute((ushort)Params.AttributeId) != 0)
        {
            result.SetPass();
        }
        else
        {
            result.SetFail();
        }
    }

    public override void Reset(Context context)
    {
    }
}