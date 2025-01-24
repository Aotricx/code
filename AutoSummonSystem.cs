// A complete rewrite of the summon functionality based on the structure of Lanboost's AutoSummon.
// This implementation ensures robust management of minions and sentries without duplication issues.

using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria.DataStructures;
using System.Linq;
using Terraria.ModLoader.IO;

namespace AutoSummon
{
    public class AutoSummonSystem : ModSystem
    {
        public override void PostUpdateEverything()
        {
            var player = Main.LocalPlayer;
            var autoSummonPlayer = player.GetModPlayer<AutoSummonPlayer>();

            if (player.dead || player.ghost || autoSummonPlayer.tempSummonDisabled)
            {
                autoSummonPlayer.tempSummonDisabled = false; // Reset the temporary summon disabled flag
                return;
            }

            // Check and manage minion summons
            if (autoSummonPlayer.MinionItems.Count > 0 && player.maxMinions > 0)
            {
                MaintainMinions(player, autoSummonPlayer);
            }

            // Check and manage sentry summons
            if (autoSummonPlayer.SentryItems.Count > 0 && player.maxTurrets > 0)
            {
                MaintainSentries(player, autoSummonPlayer);
            }
        }

        private void MaintainMinions(Player player, AutoSummonPlayer autoSummonPlayer)
        {
            float currentMinionSlotsUsed = 0f;

            // Calculate current minion slots used
            foreach (Projectile proj in Main.projectile)
            {
                if (proj.active && proj.owner == player.whoAmI && proj.minion)
                {
                    currentMinionSlotsUsed += proj.minionSlots;
                }
            }

            // Summon more minions if slots are available
            foreach (var item in autoSummonPlayer.MinionItems)
            {
                while (currentMinionSlotsUsed < player.maxMinions && autoSummonPlayer.MinionQuantity > 0)
                {
                    SummonWithItem(player, item);
                    currentMinionSlotsUsed += item.useAnimation; // Adjusted for actual use behavior
                    autoSummonPlayer.MinionQuantity--;

                    // Recalculate minion slots
                    currentMinionSlotsUsed = 0f;
                    foreach (Projectile proj in Main.projectile)
                    {
                        if (proj.active && proj.owner == player.whoAmI && proj.minion)
                        {
                            currentMinionSlotsUsed += proj.minionSlots;
                        }
                    }

                    if (currentMinionSlotsUsed >= player.maxMinions)
                    {
                        break;
                    }
                }
            }
        }


        private void MaintainSentries(Player player, AutoSummonPlayer autoSummonPlayer)
        {
            int currentSentryCount = 0;

            // Count active sentries
            for (int i = 0; i < Main.projectile.Length; i++)
            {
                var proj = Main.projectile[i];
                if (proj.active && proj.owner == player.whoAmI && proj.sentry)
                {
                    currentSentryCount++;
                }
            }

            // Summon more sentries if slots are available
            foreach (var item in autoSummonPlayer.SentryItems)
            {
                while (currentSentryCount < player.maxTurrets)
                {
                    SummonWithItem(player, item);

                    // Recalculate sentry count
                    currentSentryCount = 0;
                    for (int i = 0; i < Main.projectile.Length; i++)
                    {
                        var proj = Main.projectile[i];
                        if (proj.active && proj.owner == player.whoAmI && proj.sentry)
                        {
                            currentSentryCount++;
                        }
                    }

                    if (currentSentryCount >= player.maxTurrets)
                    {
                        break;
                    }
                }
            }
        }

        public static void SummonWithItem(Player player, Item summonItem)
        {
            if (summonItem == null || summonItem.IsAir)
                return;

            // Ensure the item has a valid projectile to summon
            int projectileType = summonItem.shoot;
            if (projectileType <= ProjectileID.None)
            {
                return;
            }

            // Get the projectile defaults
            Projectile projectile = new Projectile();
            projectile.SetDefaults(projectileType);

            Vector2 spawnPosition = player.Center;

            // Minion Summoning Logic
            if (projectile.minion)
            {
                int projIndex = Projectile.NewProjectile(
                    player.GetSource_ItemUse(summonItem), // Source of the projectile
                    spawnPosition,                        // Spawn position
                    Vector2.Zero,                         // Velocity
                    projectileType,                       // Projectile type
                    summonItem.damage,                    // Damage
                    summonItem.knockBack,                 // Knockback
                    player.whoAmI                         // Owner (the player)
                );

                // Tie the projectile to the player and add the buff
                if (projIndex != Main.maxProjectiles)
                {
                    Main.projectile[projIndex].originalDamage = summonItem.damage;
                    player.AddBuff(summonItem.buffType, 3600); // Add buff for 1 hour
                }
            }

            // Sentry Summoning Logic
            if (projectile.sentry)
            {
                // Count current sentries
                int activeSentries = 0;
                foreach (var proj in Main.projectile)
                {
                    if (proj.active && proj.owner == player.whoAmI && proj.sentry)
                    {
                        activeSentries++;
                    }
                }

                if (activeSentries >= player.maxTurrets)
                {
                    return;
                }

                int projIndex = Projectile.NewProjectile(
                    player.GetSource_ItemUse(summonItem),
                    spawnPosition,
                    Vector2.Zero,
                    projectileType,
                    summonItem.damage,
                    summonItem.knockBack,
                    player.whoAmI
                );

                // Tie the projectile to the player and add the buff
                if (projIndex != Main.maxProjectiles)
                {
                    Main.projectile[projIndex].originalDamage = summonItem.damage;
                    player.AddBuff(summonItem.buffType, 3600); // Add buff for 1 hour
                }
            }
        }
    }
}
