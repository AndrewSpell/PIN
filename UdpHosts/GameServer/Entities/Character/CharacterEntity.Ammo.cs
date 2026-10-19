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

        _clip[index, mode] = (ushort)Math.Max(0, _clip[index, mode] - amount);
        var weapon = _weaponDetailsCache?[index, mode]?.Weapon;
        if (weapon != null && weapon.ClipRegenMs > 0)
        {
            // Regeneration pauses while firing
            _nextClipRegen[index, mode] = Shard.CurrentTime + weapon.ClipRegenMs;
        }

        SyncAmmo(index, mode);
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

    // Weapons with clip_regen_ms gain a round every clip_regen_ms once they stop firing, without spending reserve
    public void TickAmmo()
    {
        var now = Shard.CurrentTime;
        for (byte index = 1; index <= 2; index++)
        {
            for (byte mode = 0; mode <= 1; mode++)
            {
                var details = _weaponDetailsCache?[index, mode];
                var weapon = details?.Weapon;
                if (weapon == null || weapon.ClipRegenMs == 0 || _clip[index, mode] >= ClipSize(details) || now < _nextClipRegen[index, mode])
                {
                    continue;
                }

                _clip[index, mode]++;
                _nextClipRegen[index, mode] = now + weapon.ClipRegenMs;
                SyncAmmo(index, mode);
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
        }

        SyncAmmo(index, mode);
    }

    // The client sizes clips from the weapon item's Weapon Magazine Size when it has one: the Smart Blaster's is 10
    // against a template clip of 40, and with 40 sent the client never allowed a reload
    private static ushort ClipSize(ActiveWeaponDetails details) => AttributeOr(details, ItemAttributeId.WeaponMagazineSize, details?.Weapon?.BaseClipSize ?? 0);

    // Max ammo stays the template's: the client showed the Bolt Driver's reserve as 200 - clip, ignoring its Max Ammo
    // Capacity of 250. It shows the reserve as min(sent reserve, max ammo - clip).
    private static ushort MaxAmmo(ActiveWeaponDetails details) => details?.Weapon?.MaxAmmo ?? 0;

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
