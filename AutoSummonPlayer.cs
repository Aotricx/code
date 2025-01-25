using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using System.Collections.Generic;
using Terraria.ModLoader.IO;
using AutoSummon.UI;
using Terraria.ID;
using System.IO;
using Newtonsoft.Json;
using System.Linq;
using System;
using Terraria.IO;

namespace AutoSummon
{
    public class AutoSummonPlayer : ModPlayer
    {

        public static void SavePanelsToFile()
        {
            try
            {
                var draggableUIPanel = AutoSummon.DraggableUIPanelInstance;
                if (draggableUIPanel == null)
                {
                    Main.NewText("UI Panel instance is null. Cannot save panels.", Color.Red);
                    return;
                }

                var savedData = new SavedPanelData();

                // Save Minion Panels
                foreach (var panel in draggableUIPanel.interactionPanels)
                {
                    var data = panel.GetTag<InteractionPanelData>();
                    if (data?.ItemSlot?.Item != null && !data.ItemSlot.Item.IsAir)
                    {
                        savedData.MinionItems.Add(new SavedItemData
                        {
                            Mod = data.ItemSlot.Item.ModItem?.Mod?.Name ?? "Terraria",
                            Name = data.ItemSlot.Item.ModItem?.Name,
                            Type = data.ItemSlot.Item.type,
                            Stack = data.ItemSlot.Item.stack
                        });
                    }
                }

                // Save Sentry Panels
                foreach (var panel in draggableUIPanel.sentryPanels)
                {
                    var data = panel.GetTag<InteractionPanelData>();
                    if (data?.ItemSlot?.Item != null && !data.ItemSlot.Item.IsAir)
                    {
                        savedData.SentryItems.Add(new SavedItemData
                        {
                            Mod = data.ItemSlot.Item.ModItem?.Mod?.Name ?? "Terraria",
                            Name = data.ItemSlot.Item.ModItem?.Name,
                            Type = data.ItemSlot.Item.type,
                            Stack = data.ItemSlot.Item.stack
                        });
                    }
                }

                // Write to the JSON file
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(savedData, Formatting.Indented));
                Main.NewText("Panels saved successfully!", Color.Green);
            }
            catch (Exception ex)
            {
                Main.NewText($"Error saving panels: {ex.Message}", Color.Red);
            }
        }


        private static void CreateDefaultPanels()
        {
            var draggableUIPanel = AutoSummon.DraggableUIPanelInstance;
            if (draggableUIPanel == null)
                return;

            draggableUIPanel.CreateInteractionPanel();
            draggableUIPanel.CreateSentryPanel();
        }

        private static readonly string FilePath = Path.Combine(Main.SavePath, "AutoSummonPanels.json");


        public static void LoadPanelsFromFile()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    Main.NewText("Save file not found. Creating default panels.", Color.Yellow);
                    return;
                }

                var json = File.ReadAllText(FilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    Main.NewText("Save file is empty. Creating default panels.", Color.Yellow);
                    return;
                }

                var savedData = JsonConvert.DeserializeObject<SavedPanelData>(json);
                if (savedData == null)
                {
                    Main.NewText("Failed to parse save data. Creating default panels.", Color.Red);
                    return;
                }

                // Get the DraggableUIPanel instance
                var draggableUIPanel = AutoSummon.DraggableUIPanelInstance;
                if (draggableUIPanel == null)
                {
                    Main.NewText("DraggableUIPanel instance not found. Cannot load panels.", Color.Red);
                    return;
                }

                // Clear existing panels
                draggableUIPanel.ClearPanels();

                // Load Minion Panels
                bool createdMinionPanel = false;
                foreach (var itemData in savedData.MinionItems)
                {
                    var item = CreateItemFromData(itemData);
                    if (item != null && !item.IsAir)
                    {
                        draggableUIPanel.CreateInteractionPanel(draggableUIPanel.interactionPanels.Count, item);
                        createdMinionPanel = true;
                    }
                }

                // If no minion panels were created, create a default one
                if (!createdMinionPanel)
                {
                    draggableUIPanel.CreateInteractionPanel(0);
                }

                // Load Sentry Panels
                bool createdSentryPanel = false;
                foreach (var itemData in savedData.SentryItems)
                {
                    var item = CreateItemFromData(itemData);
                    if (item != null && !item.IsAir)
                    {
                        draggableUIPanel.CreateSentryPanel(draggableUIPanel.sentryPanels.Count, item);
                        createdSentryPanel = true;
                    }
                }

                // If no sentry panels were created, create a default one
                if (!createdSentryPanel)
                {
                    draggableUIPanel.CreateSentryPanel(0);
                }

                Main.NewText("Panels loaded successfully!", Color.Green);
            }
            catch (Exception ex)
            {
                Main.NewText($"Error loading panels: {ex.Message}", Color.Red);
            }
        }



        private static Item CreateItemFromData(SavedItemData itemData)
        {
            try
            {
                var item = new Item();

                // Handle modded items
                if (!string.IsNullOrEmpty(itemData.Mod) && itemData.Mod != "Terraria")
                {
                    var mod = ModLoader.GetMod(itemData.Mod);
                    if (mod != null)
                    {
                        var modItem = mod.Find<ModItem>(itemData.Name);
                        if (modItem != null)
                        {
                            item.SetDefaults(modItem.Type);
                            item.stack = itemData.Stack;
                            return item;
                        }
                    }
                }
                else
                {
                    // Handle vanilla items
                    if (itemData.Type > 0)
                    {
                        item.SetDefaults(itemData.Type);
                        item.stack = itemData.Stack;
                        return item;
                    }
                }
            }
            catch (Exception ex)
            {
                Main.NewText($"Error creating item: {ex.Message}", Color.Red);
            }

            return new Item(); // Return an empty item if anything fails
        }


        public class SavedItemData
        {
            public string Mod { get; set; } // Mod name or "Terraria" for vanilla
            public string Name { get; set; } // Item name (optional)
            public int Type { get; set; } // Item type ID
            public int Stack { get; set; } // Stack size
        }

        public class SavedPanelData
        {
            public List<SavedItemData> MinionItems { get; set; } = new();
            public List<SavedItemData> SentryItems { get; set; } = new();
        }



        public override void Unload()
        {
            SavePanelsToFile(); // Save when the mod is unloaded
        }

        // Auto-summon flags
        public bool isSpawning;
        public bool autoSummonEnabled = true;
        public bool tempSummonDisabled = false;
        private bool isWaitingForRespawn = false;

        // Minion and sentry quantities
        public int MinionQuantity = 0;
        public int MaxMinionCount => Player.maxMinions; // Use Player.maxMinions directly
        public int SentryQuantity = 0;
        public int MaxSentryCount => Player.maxTurrets; // Use Player.maxTurrets directly

        // List of minion and sentry items
        public List<Item> MinionItems = new();
        public List<Item> SentryItems = new();

        private int respawnDelayTimer = 0;

        // Respawn toggle
        public bool respawnSentriesEnabled = DraggableUIPanel.respawnSentriesEnabled;

        public override void Initialize()
        {
            MinionItems.Clear();
            SentryItems.Clear();
            MinionQuantity = 0;
            SentryQuantity = 0;
        }

        public override void OnRespawn()
        {
            base.OnRespawn();
            isWaitingForRespawn = true;
        }

        public override void PlayerConnect()
        {
            base.PlayerConnect();
            isSpawning = true;
            SummonAllItemsIfPossible(Player);
        }

        public override void OnEnterWorld()
        {
            base.OnEnterWorld();

            LoadPanelsFromFile();
            if (AutoSummon.DraggableUIPanelInstance.interactionPanels.Count == 0)
            {
                AutoSummon.DraggableUIPanelInstance.CreateInteractionPanel();
            }

            if (AutoSummon.DraggableUIPanelInstance.sentryPanels.Count == 0)
            {
                AutoSummon.DraggableUIPanelInstance.CreateSentryPanel();
            }

            // Summon all items when entering the world
            SummonAllItemsIfPossible(Player);

            // Sync items and quantities with the UI
            var draggableUIPanel = AutoSummon.DraggableUIPanelInstance;
            if (draggableUIPanel == null)
                return;

            // Sync Minion Panels
            for (int i = 0; i < draggableUIPanel.interactionPanels.Count && i < MinionItems.Count; i++)
            {
                var panel = draggableUIPanel.interactionPanels[i];
                var data = panel.GetTag<InteractionPanelData>();
                if (data != null && MinionItems.Count > i)
                {
                    data.ItemSlot.Item.SetDefaults(MinionItems[i].type); // Set the item type
                    data.ItemSlot.Item.stack = MinionItems[i].stack;    // Set the item stack size
                    data.QuantityLabel.SetText($"Minions: {MinionQuantity}");
                }
            }

            // Sync Sentry Panels
            for (int i = 0; i < draggableUIPanel.sentryPanels.Count && i < SentryItems.Count; i++)
            {
                var panel = draggableUIPanel.sentryPanels[i];
                var data = panel.GetTag<InteractionPanelData>();
                if (data != null && SentryItems.Count > i)
                {
                    data.ItemSlot.Item.SetDefaults(SentryItems[i].type); // Set the item type
                    data.ItemSlot.Item.stack = SentryItems[i].stack;    // Set the item stack size
                    data.QuantityLabel.SetText($"Sentries: {SentryQuantity}");
                }
            }
        }


        private void SummonAllItemsIfPossible(Player player)
        {
            // Reference the DraggableUIPanel instance
            var draggableUIPanel = AutoSummon.DraggableUIPanelInstance;

            if (draggableUIPanel == null)
            {
                return;
            }

            // Call the SummonAllItems function
            draggableUIPanel.SummonAllItems(player);
        }



        public override void PostUpdate()
        {
            if (!DraggableUIPanel.respawnSentriesEnabled)
                return; // Don't refresh sentries if the toggle is off

            var player = Main.LocalPlayer;

            foreach (var projectile in Main.projectile)
            {
                // Skip non-active, non-sentry, or non-local player's projectiles
                if (!projectile.active || !projectile.sentry || projectile.owner != player.whoAmI)
                    continue;

                if (IsOffScreen(projectile))
                {
                    int currentTime = (int)Main.GameUpdateCount; // Explicitly cast from uint to int

                    // Check cooldown before refreshing
                    if (!sentryRefreshCooldowns.TryGetValue(projectile.whoAmI, out int lastRefreshTime) ||
                        currentTime - lastRefreshTime > 120) // Cooldown of 2 seconds (120 ticks)
                    {
                        RefreshSentries(player); // Resummon sentries
                        sentryRefreshCooldowns[projectile.whoAmI] = currentTime; // Update cooldown
                    }
                }
            }

            // Check if we are waiting for the player to fully respawn
            if (isWaitingForRespawn)
            {
                if (!player.dead && player.statLife > 0) // Fully respawned
                {
                    isWaitingForRespawn = false; // Reset the flag
                    SummonAllItemsIfPossible(player); // Summon items when fully respawned
                }
            }
        }

        private void RefreshSentries(Player player)
        {
            // Reference the DraggableUIPanel instance
            var draggableUIPanel = AutoSummon.DraggableUIPanelInstance;
            if (draggableUIPanel == null)
            {
                return;
            }

            // Iterate through all projectiles and refresh only local player's off-screen sentries
            foreach (var projectile in Main.projectile)
            {
                if (projectile.active && projectile.sentry && projectile.owner == player.whoAmI)
                {
                    if (IsOffScreen(projectile)) // Refresh only off-screen sentries
                    {
                        projectile.Kill(); // Kill the off-screen sentry
                    }
                }
            }

            // Access sentry panels from DraggableUIPanel
            var sentryPanels = draggableUIPanel.sentryPanels;

            foreach (var panel in sentryPanels)
            {
                var data = panel.GetTag<InteractionPanelData>();
                if (data == null || data.ItemSlot.Item == null || data.ItemSlot.Item.IsAir)
                    continue;

                var summonItem = data.ItemSlot.Item;
                int quantity = int.Parse(data.QuantityLabel.Text.Replace("Sentries: ", ""));

                for (int i = 0; i < quantity; i++)
                {
                    AutoSummonSystem.SummonWithItem(player, summonItem);
                }
            }

            if (Main.LocalPlayer == player) // Only show text for the local player
            {
                Main.NewText("Sentries refreshed!", Color.Cyan);
            }
        }


        // Helper method to check if a projectile is off-screen
        private bool IsOffScreen(Projectile projectile)
        {
            return projectile.position.X < Main.screenPosition.X - 100 ||
                   projectile.position.X > Main.screenPosition.X + Main.screenWidth + 100 ||
                   projectile.position.Y < Main.screenPosition.Y - 100 ||
                   projectile.position.Y > Main.screenPosition.Y + Main.screenHeight + 100;
        }

        private Dictionary<int, int> sentryRefreshCooldowns = new();
    }
}
