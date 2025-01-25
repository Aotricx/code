using System;
using System.Collections.Generic;
using System.IO;
using AutoSummon.UI;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using Terraria.ModLoader;

namespace AutoSummon
{
    public class AutoSummonPlayer : ModPlayer
    {
        // File Path for Saving
        private static readonly string FilePath = Path.Combine(Main.SavePath, "ModConfigs", "AutoSummonPanels.json");

        // Flags for auto-summoning behavior
        public bool isSpawning;
        public bool autoSummonEnabled = true;
        public bool tempSummonDisabled = false;

        // Minion and Sentry Data
        public int MinionQuantity = 0;
        public int SentryQuantity = 0;
        public List<Item> MinionItems = new();
        public List<Item> SentryItems = new();

        public override void Initialize()
        {
            // Initialize fields
            MinionItems.Clear();
            SentryItems.Clear();
            MinionQuantity = 0;
            SentryQuantity = 0;
        }

        public override void Unload()
        {
            // Save data on game unload
            SavePanelsToFile();
        }

        public void SavePanelsToFile()
        {
            try
            {
                var draggableUIPanel = AutoSummon.DraggableUIPanelInstance;
                if (draggableUIPanel == null)
                {
                    Main.NewText("UI Panel instance is null. Cannot save panels.", Color.Red);
                    return;
                }

                var savedData = new SavedPanelData { PlayerName = Player.name };

                // Save Minion Panels
                foreach (var panel in draggableUIPanel.interactionPanels)
                {
                    var data = panel.GetTag<InteractionPanelData>();
                    if (data?.ItemSlot?.Item != null && !data.ItemSlot.Item.IsAir)
                    {
                        Main.NewText($"Saving Minion Item: {data.ItemSlot.Item.Name} x{data.ItemSlot.Item.stack}");
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
                        Main.NewText($"Saving Sentry Item: {data.ItemSlot.Item.Name} x{data.ItemSlot.Item.stack}");
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

        public override void OnEnterWorld()
        {
            base.OnEnterWorld();

            var draggableUIPanel = AutoSummon.DraggableUIPanelInstance;
            if (draggableUIPanel == null)
            {
                Main.NewText("UI Panel instance not found.", Color.Red);
                return;
            }

            // Ensure default panels exist
            if (draggableUIPanel.interactionPanels.Count == 0)
                draggableUIPanel.CreateInteractionPanel();
            if (draggableUIPanel.sentryPanels.Count == 0)
                draggableUIPanel.CreateSentryPanel();

            // Load saved data
            LoadPanelsFromFile();
        }

        private void LoadPanelsFromFile()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    Main.NewText("Save file not found. Using default panels.", Color.Yellow);
                    return;
                }

                var json = File.ReadAllText(FilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    Main.NewText("Save file is empty. Using default panels.", Color.Yellow);
                    return;
                }

                var savedData = JsonConvert.DeserializeObject<SavedPanelData>(json);
                if (savedData == null || savedData.PlayerName != Player.name)
                {
                    Main.NewText("No matching data for this player. Using default panels.", Color.Yellow);
                    return;
                }

                var draggableUIPanel = AutoSummon.DraggableUIPanelInstance;
                if (draggableUIPanel == null)
                {
                    Main.NewText("UI Panel instance not found. Cannot load panels.", Color.Red);
                    return;
                }

                draggableUIPanel.ClearPanels(); // Clear existing panels

                // Load Minion Panels
                foreach (var itemData in savedData.MinionItems)
                {
                    var item = CreateItemFromData(itemData);
                    if (item != null && !item.IsAir)
                    {
                        draggableUIPanel.CreateInteractionPanel(0, item);
                    }
                }

                // Load Sentry Panels
                foreach (var itemData in savedData.SentryItems)
                {
                    var item = CreateItemFromData(itemData);
                    if (item != null && !item.IsAir)
                    {
                        draggableUIPanel.CreateSentryPanel(0, item);
                    }
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
            var item = new Item();

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
            else if (itemData.Type > 0)
            {
                item.SetDefaults(itemData.Type);
                item.stack = itemData.Stack;
                return item;
            }

            return new Item(); // Return an empty item if creation fails
        }

        public class SavedItemData
        {
            public string Mod { get; set; }
            public string Name { get; set; }
            public int Type { get; set; }
            public int Stack { get; set; }
        }

        public class SavedPanelData
        {
            public string PlayerName { get; set; }
            public List<SavedItemData> MinionItems { get; set; } = new();
            public List<SavedItemData> SentryItems { get; set; } = new();
        }
    }
}
