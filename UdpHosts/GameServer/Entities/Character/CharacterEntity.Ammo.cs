using System;
using GameServer.Enums;

namespace GameServer.Entities.Character;

// The server owns clip and reserve ammo: the client counts its clip down while firing, but snaps back to the clip and
// reserve the server sends. Without them it falls back to its own count, which is why weapons that regenerate their
// clip (clip_regen_ms) never recovered.
public sealed partial class CharacterEntity
{
    // Indexed by weapon index (1 primary, 2 secondary) and fire mode (0 main, 1 alt)
    private readonly ushort[,] _clip = new ushort[3, 2];
    private readonly ushort[,] _reserve = new ushort[3, 2];
    private readonly uint[,] _nextClipRegen = new uint[3, 2];
    private readonly bool[,] _firing = new bool[3, 2];

    // Full clips and reserves for every weapon, on spawning, respawning and changing loadout
    public void RefillAmmo()
    {
        for (byte index = 1; index <= 2; index++)
        {
            for (byte mode = 0; mode <= 1; mode++)
            {
                var details = _weaponDetailsCache?[index, mode];
                var clipSize = ClipSize(details);
                _clip[index, mode] = clipSize;
                _reserve[index, mode] = (ushort)Math.Max(0, MaxAmmo(details) - clipSize);
                _firing[index, mode] = false;
                SyncAmmo(index, mode);
            }
        }
    }

    public void ConsumeAmmo(int amount)
    {
        var (index, mode) = (WeaponIndex.Index, GetActiveFireModeIndex());
        if (amount <= 0 || index is not (1 or 2))
        {
            return;
        }

        var before = _clip[index, mode];
        _clip[index, mode] = (ushort)Math.Max(0, before - amount);
        _firing[index, mode] = true;
        SyncAmmo(index, mode);

        // Running the clip dry triggers the weapon's clip-empty ability. The Arc Thrower's overheats it: sparks, and the
        // weapon is restricted for 1.5 s.
        var weapon = _weaponDetailsCache?[index, mode]?.Weapon;
        if (before > 0 && _clip[index, mode] == 0 && weapon?.EmptyAbility > 0)
        {
            Shard.Abilities.HandleActivateAbility(Shard, this, weapon.EmptyAbility);
        }
    }

    // Regeneration pauses while firing, until the client reports the trigger released (FireEnd or FireCancel)
    public void StopFiring()
    {
        var now = Shard.CurrentTime;
        for (byte index = 1; index <= 2; index++)
        {
            for (byte mode = 0; mode <= 1; mode++)
            {
                if (_firing[index, mode])
                {
                    _firing[index, mode] = false;
                    _nextClipRegen[index, mode] = now + ClipRegenIntervalMs(_weaponDetailsCache?[index, mode]);
                }
            }
        }
    }

    public void ReloadActiveWeapon()
    {
        var (index, mode) = (WeaponIndex.Index, GetActiveFireModeIndex());
        var details = index is 1 or 2 ? _weaponDetailsCache?[index, mode] : null;
        if (details?.Weapon == null)
        {
            return;
        }

        var moved = Math.Min(ClipSize(details) - _clip[index, mode], (int)_reserve[index, mode]);
        if (moved > 0)
        {
            _clip[index, mode] += (ushort)moved;
            _reserve[index, mode] -= (ushort)moved;
            SyncAmmo(index, mode);
        }
    }

    // Weapons with clip_regen_ms regain rounds while not firing, without spending reserve
    public void TickAmmo()
    {
        var now = Shard.CurrentTime;
        for (byte index = 1; index <= 2; index++)
        {
            for (byte mode = 0; mode <= 1; mode++)
            {
                if (_firing[index, mode])
                {
                    continue;
                }

                var details = _weaponDetailsCache?[index, mode];
                var interval = ClipRegenIntervalMs(details);
                if (interval == 0)
                {
                    continue;
                }

                // Ticks are 50 ms apart, so faster regen (the Firecat's 25/s) gains more than one round per tick
                var clipSize = ClipSize(details);
                var before = _clip[index, mode];
                while (_clip[index, mode] < clipSize && now >= _nextClipRegen[index, mode])
                {
                    _clip[index, mode]++;
                    _nextClipRegen[index, mode] += interval;
                }

                if (_clip[index, mode] != before)
                {
                    SyncAmmo(index, mode);
                }
            }
        }
    }

    // Debug: set the weapon in hand's reserve, and optionally its clip
    public void SetActiveWeaponAmmo(ushort reserve, ushort? clip)
    {
        var (index, mode) = (WeaponIndex.Index, GetActiveFireModeIndex());
        if (index is not (1 or 2))
        {
            return;
        }

        _reserve[index, mode] = reserve;
        if (clip.HasValue)
        {
            _clip[index, mode] = clip.Value;
            _nextClipRegen[index, mode] = Shard.CurrentTime + ClipRegenIntervalMs(_weaponDetailsCache?[index, mode]);
        }

        SyncAmmo(index, mode);
    }

    // The client sizes clips from the weapon item's Weapon Magazine Size when it has one: the Smart Blaster's is 10
    // against a template clip of 40, and with 40 sent the client never allowed a reload
    private static ushort ClipSize(ActiveWeaponDetails details) => AttributeOr(details, ItemAttributeId.WeaponMagazineSize, details?.Weapon?.BaseClipSize ?? 0);

    // Max ammo stays the template's: the client showed the Bolt Driver's reserve as 200 - clip, ignoring its Max Ammo
    // Capacity of 250. It shows the reserve as min(sent reserve, max ammo - clip).
    private static ushort MaxAmmo(ActiveWeaponDetails details) => details?.Weapon?.MaxAmmo ?? 0;

    // Items give their regen as Ammo Regen (Magazine) in rounds per second, over the template's clip_regen_ms: most Arc
    // Throwers regain 13/s, against 20/s from the template's 50 ms. 0 means the weapon doesn't regenerate.
    private static uint ClipRegenIntervalMs(ActiveWeaponDetails details)
    {
        var weapon = details?.Weapon;
        if (weapon == null || weapon.ClipRegenMs == 0)
        {
            return 0;
        }

        return details.Attributes.TryGetValue((ushort)ItemAttributeId.AmmoRegeneration, out var perSecond) && perSecond > 0
                   ? (uint)MathF.Round(1000f / perSecond)
                   : weapon.ClipRegenMs;
    }

    private static ushort AttributeOr(ActiveWeaponDetails details, ItemAttributeId attribute, ushort fallback)
    {
        if (details?.Weapon == null)
        {
            return 0;
        }

        return details.Attributes.TryGetValue((ushort)attribute, out var value) && value > 0 ? (ushort)MathF.Round(value) : fallback;
    }

    // Weapon index 1 maps to the _0 fields and 2 to _1, alt fire to the Alt fields. NPCs have no combat controller.
    private void SyncAmmo(byte index, byte mode)
    {
        var clip = _clip[index, mode];
        var reserve = _reserve[index, mode];
        var view = Character_CombatView;
        var controller = Character_CombatController;

        switch ((index, mode))
        {
            case (1, 0):
                if (view != null)
                {
                    view.Ammo_0Prop = reserve;
                }

                if (controller != null)
                {
                    controller.Clip_0Prop = clip;
                    controller.Ammo_0Prop = reserve;
                }

                break;
            case (2, 0):
                if (view != null)
                {
                    view.Ammo_1Prop = reserve;
                }

                if (controller != null)
                {
                    controller.Clip_1Prop = clip;
                    controller.Ammo_1Prop = reserve;
                }

                break;
            case (1, 1):
                if (view != null)
                {
                    view.AltAmmo_0Prop = reserve;
                }

                if (controller != null)
                {
                    controller.AltClip_0Prop = clip;
                    controller.AltAmmo_0Prop = reserve;
                }

                break;
            case (2, 1):
                if (view != null)
                {
                    view.AltAmmo_1Prop = reserve;
                }

                if (controller != null)
                {
                    controller.AltClip_1Prop = clip;
                    controller.AltAmmo_1Prop = reserve;
                }

                break;
        }
    }
}
