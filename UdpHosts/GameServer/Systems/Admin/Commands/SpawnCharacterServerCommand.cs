using System;
using System.Linq;
using System.Numerics;
using GameServer.StaticDB;

namespace GameServer.Systems.Admin.Commands;

[ServerCommand("Spawn a character by characterTypeId, optionally at a location.", "npc <characterTypeId> [<x> <y> <z>] [level=<n>]", "npc", "character", "monster", "spawn_npc", "spawn_character", "spawn_monster")]
public class SpawnCharacterServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        // level=<n> can go anywhere, without it the monster takes its zone's level
        byte? level = null;
        var levelParam = parameters.FirstOrDefault(p => p.StartsWith("level=", StringComparison.OrdinalIgnoreCase));
        if (levelParam != null)
        {
            if (!byte.TryParse(levelParam["level=".Length..], out var parsedLevel) || parsedLevel == 0 || SDBInterface.GetMonsterScaling(parsedLevel) == null)
            {
                SourceFeedback("Invalid level, use level=<n> with a level that has monster scaling data (1-80)", context);
                return;
            }

            level = parsedLevel;
            parameters = parameters.Where(p => p != levelParam).ToArray();
        }

        if (parameters.Length != 1 && parameters.Length != 4)
        {
            SourceFeedback("Invalid number of parameters for spawn character command", context);
            return;
        }

        if (context.SourcePlayer?.CharacterEntity == null && parameters.Length != 4)
        {
            SourceFeedback("Must provide position if player character is not available", context);
            return;
        }

        uint typeId = ParseUIntParameter(parameters[0]);
        if (SDBInterface.GetMonster(typeId) == null)
        {
            SourceFeedback("No monster data for this typeId", context);
            return;
        }

        if (parameters.Length == 4)
        {
            Vector3? paramPosition = ParseVector3Parameters(parameters, 1);
            if (paramPosition != null)
            {
                context.Shard.EntityMan.SpawnCharacter(typeId, (Vector3)paramPosition, level: level);
            }
            else
            {
                SourceFeedback("Failed to parse position", context);
                return;
            }
        }
        else
        {
            var position = context.SourcePlayer.CharacterEntity.Position;
            context.Shard.EntityMan.SpawnCharacter(typeId, position, level: level);
        }
    }
}