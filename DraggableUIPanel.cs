using Terraria;
using Terraria.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using CustomSlot.UI;
using System;
using System.Collections.Generic;
using Terraria.ID;
using System.Linq;
using Terraria.GameContent;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AutoSummon.UI
{

    public class DraggableUIPanel : UIState
    {
        private UIPanel mainPanel;
        private UIPanel headerBar;
        private UIText titleText;
        private UIText minionSlotsLabel;
        private UIText sentrySlotsLabel;
        public List<UIPanel> interactionPanels = new();
        public List<UIPanel> sentryPanels = new();
        private Dictionary<CustomItemSlot, Item> lastItemStates = new();
        private Vector2 offset;
        private bool dragging = false;
        public bool isSpawning = false;

        public override void OnInitialize()
        {
            // Dynamically set the width for mainPanel
            const int panelPadding = 20; // Extra padding for the main panel
            const int interactionPanelWidth = 360; // Width of each interaction panel
            const int baseHeight = 100; // Base height for main panel

            mainPanel = new UIPanel
            {
                Width = { Pixels = interactionPanelWidth + panelPadding }, // Adjust mainPanel width
                Height = { Pixels = baseHeight },
                HAlign = 0.5f,
                VAlign = 0.5f,
                BackgroundColor = new Color(50, 50, 70, 200)
            };
            Append(mainPanel);

            // Header bar
            headerBar = new UIPanel
            {
                Width = { Pixels = interactionPanelWidth }, // Match interaction panel width
                Height = { Pixels = 30 },
                BackgroundColor = new Color(30, 30, 50, 255)
            };
            headerBar.OnLeftMouseDown += StartDrag;
            headerBar.OnLeftMouseUp += EndDrag;
            mainPanel.Append(headerBar);

            // Title text
            titleText = new UIText("Kunaii's Auto Summon", 0.85f)
            {
                HAlign = 0.5f,
                VAlign = 0.5f
            };
            headerBar.Append(titleText);

            // Minion slots label
            minionSlotsLabel = new UIText("Minion Slots: 0/0", 1f)
            {
                HAlign = 0.5f,
                Top = { Pixels = 40 }
            };
            mainPanel.Append(minionSlotsLabel);

            // Sentry slots label
            sentrySlotsLabel = new UIText("Sentry Slots: 0/0", 1f)
            {
                HAlign = 0.5f,
                Top = { Pixels = 80 + interactionPanels.Count * 70 } // Positioned below minion panels
            };
            mainPanel.Append(sentrySlotsLabel);

            CreateSentryRespawnButton();
            CreateRespawnButton();
        }

        private UIImageButton sentryButton;
        public static bool respawnSentriesEnabled { get; private set; } = true;
        private UIImageButton RefreshButton;
        private void CreateRespawnButton()
        {
            // Load the book icon (Confuse button texture)
            var bookIcon = ModContent.Request<Texture2D>("AutoSummon/Assets/UI/RefreshButton");

            // Create the settings button with the book icon
            RefreshButton = new UIImageButton(bookIcon)
            {
                Width = { Pixels = 20 },
                Height = { Pixels = 20 },
                Top = { Pixels = 4 },  // Align with the title
                Left = { Pixels = 5 } // Position to the right of the title
            };

            RefreshButton.OnLeftClick += (evt, element) =>
            {
                // Toggle the respawn functionality
                RefreshSummons();

                if (Main.netMode != NetmodeID.Server)
                {
                    Main.NewText("Summons refreshed!", Color.Cyan);
                }
            };

            mainPanel.Append(RefreshButton);
        }

        public void CreateSentryRespawnButton()
        {
            // Load the book icon (Confuse button texture)
            var Yes = ModContent.Request<Texture2D>("AutoSummon/Assets/UI/RespawnSentry");
            var No = ModContent.Request<Texture2D>("AutoSummon/Assets/UI/NoRespawnSentry");
            // Assume 'headerBar' is your UI element for the title/header bar
            float titleWidth = headerBar.GetDimensions().Width; // Get the title's width
            float titleLeft = headerBar.Left.Pixels;           // Get the headerBar's left position

            // Calculate the button's left position
            float buttonLeftPosition = titleLeft + titleWidth - 25; // Add padding

            // Create the settings button with the book icon
            sentryButton = new UIImageButton(Yes)
            {
                Width = { Pixels = 20 },
                Height = { Pixels = 20 },
                Top = { Pixels = 4 },  // Align with the title
                Left = { Pixels = buttonLeftPosition } // Position to the right of the title
            };

            sentryButton.OnLeftClick += (evt, element) =>
            {
                // Toggle the respawn functionality
                respawnSentriesEnabled = !respawnSentriesEnabled;
                sentryButton.SetImage(respawnSentriesEnabled ? Yes : No);
                // Provide feedback via chat
                if (Main.netMode != NetmodeID.Server){ // Ensure it runs only for the local client
                    Main.NewText(respawnSentriesEnabled
                    ? "Sentries respawn when they're off screen: On"
                    : "Sentries respawn when they're off screen: Off", Color.Cyan);
                }
            };

            mainPanel.Append(sentryButton);
        }


        public void CreateSentryPanel(int index = 0, Item item = null)
        {
            const int itemSlotSize = 48;
            const int buttonWidth = 50;
            const int labelWidth = 100;
            const int spacing = 10;
            const int panelHeight = 60;
            const int buttonHeight = 30;
            const int labelHeight = 30; // Height of the Sentries label

            int topOffset = 80 + interactionPanels.Count * 70 + 40 + index * 70;

            // Create the panel
            var panel = new UIPanel
            {
                Width = { Pixels = 360 },
                Height = { Pixels = panelHeight },
                Top = { Pixels = topOffset },
                BackgroundColor = new Color(35, 35, 50, 200)
            };
            panel.SetPadding(0); // Remove internal padding
            mainPanel.Append(panel);

            // Center the item slot vertically within the panel
            var itemSlot = new CustomItemSlot(ItemSlot.Context.InventoryItem, 1f)
            {
                Width = { Pixels = itemSlotSize },
                Height = { Pixels = itemSlotSize },
                Left = { Pixels = spacing },
                Top = { Pixels = 4 }, // Center vertically
                IsValidItem = item => IsSentrySummoningItem(item) // Restrict to valid sentry items
            };
            panel.Append(itemSlot);
            itemSlot.SetPadding(0); // Remove internal padding

            // Store the initial state of the item in the slot
            lastItemStates[itemSlot] = itemSlot.Item.Clone();

            // Create buttons and labels
            var minusButton = CreateButton(
                "-1",
                new Vector2(itemSlot.Left.Pixels + itemSlotSize + spacing, (panelHeight - buttonHeight) / 2),
                (evt, element) => UpdateSentryQuantity(panel, -1));

            var plusButton = CreateButton(
                "+1",
                new Vector2(minusButton.Left.Pixels + buttonWidth + spacing, (panelHeight - buttonHeight) / 2),
                (evt, element) => UpdateSentryQuantity(panel, 1));

            var fillButton = CreateButton(
                "Fill",
                new Vector2(plusButton.Left.Pixels + buttonWidth + spacing, (panelHeight - buttonHeight) / 2),
                (evt, element) => ToggleFill(panel));

            int maxSentries = Main.LocalPlayer.maxTurrets;
            int currentSentryCount = GetCurrentSentryCount();
            int startingQuantity = 0;

            var quantityLabel = new UIText($"Sentries: {startingQuantity}", 1f)
            {
                Width = { Pixels = labelWidth },
                Left = { Pixels = fillButton.Left.Pixels + buttonWidth + spacing },
                Top = { Pixels = (panelHeight - labelHeight) / 2 + 6 } // Slight downward adjustment
            };

            // Store the panel's metadata
            panel.SetTag(new InteractionPanelData
            {
                MinusButton = minusButton,
                PlusButton = plusButton,
                FillButton = fillButton,
                QuantityLabel = quantityLabel,
                ItemSlot = itemSlot
            });

            // Add the panel to the list
            sentryPanels.Add(panel);
            UpdateMainPanelHeight();
        }

        private bool IsSentrySummoningItem(Item item)
        {
            if (item != null && item.DamageType == DamageClass.Summon)
            {
                Projectile projectile = new Projectile();
                projectile.SetDefaults(item.shoot);
                return projectile.sentry && !projectile.minion;
            }
            return false;
        }
    


        protected bool shouldSummon()
        {
            var player = Main.LocalPlayer;
            var player2 = Main.LocalPlayer.GetModPlayer<AutoSummonPlayer>();

            // Do nothing if dead
            if (player.dead)
            {
                player2.tempSummonDisabled = false;
                return false;
            }

            // Do notthing if holding an item
            if (Main.mouseItem != null && !Main.mouseItem.IsAir) {
                return false;
            }

            // Do nothing if in middle of swing
            if(player.itemAnimation != 0)
            {
                return false;
            }


            return !player2.tempSummonDisabled && isSpawning;
        }

        protected float GetCurrentMinionCount()
        {
            float minCount = 0;
            for (int i = 0; i < 1000; i++)
            {
                if (Main.projectile[i].active && Main.projectile[i].minion && Main.projectile[i].owner == Main.myPlayer)
                {
                    minCount += Main.projectile[i].minionSlots;
                }
            }
            return minCount;
        }

        protected int GetCurrentSentryCount()
        {
            int turrets = 0;
            for (int i = 0; i < 1000; i++)
            {
                if (Main.projectile[i].active && Main.projectile[i].WipableTurret && Main.projectile[i].owner == Main.myPlayer)
                {
                    turrets += 1;
                }
            }
            return turrets;
        }

        private void UpdateQuantity(UIPanel panel, int change)
        {
            int maxMinions = Main.LocalPlayer.maxMinions;      // Maximum allowed minions
            int totalFromPanels = GetTotalMinions();           // Total minion slots requested from all panels

            foreach (var element in panel.Children)
            {
                if (element is UIText quantityLabel)
                {
                    // Parse the current quantity from the label
                    string currentText = quantityLabel.Text.Replace("Minions: ", "");
                    int currentQuantity = int.TryParse(currentText, out int parsedQuantity) ? parsedQuantity : 0;

                    // Calculate the new total minions if the quantity changes
                    int newQuantity = Math.Max(0, currentQuantity + change);

                    // Validate that the new total minions do not exceed maxMinions
                    if (newQuantity > 0 && totalFromPanels - currentQuantity + newQuantity > maxMinions)
                    {
                        return;
                    }

                    // Update the quantity label
                    quantityLabel.SetText($"Minions: {newQuantity}");

                    // Get the panel's data and update its state
                    var data = panel.GetTag<InteractionPanelData>();
                    if (data == null) return;

                    if (change == -1)
                    {
                        data.FillButton.SetText("Fill"); // Update button text
                        data.IsFilled = false;          // Mark as not filled
                    }
                    else if (change == +1 && newQuantity == maxMinions)
                    {
                        // If incrementing and the quantity fills all remaining slots, mark the panel as filled
                        data.FillButton.SetText("Unfill");
                        data.IsFilled = true;
                    }

                    // Recalculate and adjust quantities for all other filled panels
                    RecalculateFilledPanels();

                    // Trigger summoning/desummoning logic
                    RefreshSummons();
                    UpdateMinionSlotsLabel();
                    break;
                }
            }
        }


        private void RecalculateFilledPanels()
        {
            var player = Main.LocalPlayer;

            // Handle Minion Panels
            int maxMinions = player.maxMinions; // Maximum allowed minions
            int totalUsedMinionSlots = GetTotalMinions(); // Total currently used minion slots

            foreach (var panel in interactionPanels)
            {
                var data = panel.GetTag<InteractionPanelData>();
                if (data == null || !data.IsFilled) continue; // Skip panels that aren't marked as filled

                // Parse the current quantity
                int currentQuantity = int.Parse(data.QuantityLabel.Text.Replace("Minions: ", ""));

                // Calculate the remaining available slots for this panel
                int remainingSlots = maxMinions - (totalUsedMinionSlots - currentQuantity);
                int newQuantity = Math.Min(remainingSlots, maxMinions);

                // Update the panel's quantity if it has changed
                if (newQuantity != currentQuantity)
                {
                    data.QuantityLabel.SetText($"Minions: {newQuantity}");
                }
            }

            // Handle Sentry Panels
            int maxSentries = player.maxTurrets; // Maximum allowed sentries
            int totalUsedSentrySlots = GetTotalSentries(); // Total currently used sentry slots

            foreach (var panel in sentryPanels)
            {
                var data = panel.GetTag<InteractionPanelData>();
                if (data == null || !data.IsFilled) continue; // Skip panels that aren't marked as filled

                // Parse the current quantity
                int currentQuantity = int.Parse(data.QuantityLabel.Text.Replace("Sentries: ", ""));

                // Calculate the remaining available slots for this panel
                int remainingSlots = maxSentries - (totalUsedSentrySlots - currentQuantity);
                int newQuantity = Math.Min(remainingSlots, maxSentries);

                // Update the panel's quantity if it has changed
                if (newQuantity != currentQuantity)
                {
                    data.QuantityLabel.SetText($"Sentries: {newQuantity}");
                }
            }
        }



        private void UpdateMinionSlotsLabel()
        {
            float currentMinions = GetCurrentMinionCount();
            int maxMinions = Main.LocalPlayer.maxMinions;

            minionSlotsLabel.SetText($"Minion Slots: {currentMinions}/{maxMinions}");
        }

        private void UpdateSentryQuantity(UIPanel panel, int change)
        {
            var data = panel.GetTag<InteractionPanelData>();
            if (data == null) return;

            int maxSentries = Main.LocalPlayer.maxTurrets;     // Maximum allowed sentries
            int totalFromPanels = GetTotalSentries();          // Total sentry slots requested from all panels

            foreach (var element in panel.Children)
            {
                if (element is UIText quantityLabel)
                {
                    // Parse the current quantity from the label
                    string currentText = quantityLabel.Text.Replace("Sentries: ", "");
                    int currentQuantity = int.TryParse(currentText, out int parsedQuantity) ? parsedQuantity : 0;

                    // Calculate the new quantity
                    int newQuantity = Math.Max(0, currentQuantity + change);

                    // Validate against max sentries
                    if (newQuantity > 0 && totalFromPanels - currentQuantity + newQuantity > maxSentries)
                    {
                        return; // Stop processing if the limit is exceeded
                    }

                    // Update the quantity label
                    quantityLabel.SetText($"Sentries: {newQuantity}");

                    if (change == -1)
                    {
                        data.FillButton.SetText("Fill"); // Update button text
                        data.IsFilled = false; // Mark as not filled
                    }
                    else if (change == +1 && newQuantity == maxSentries)
                    {
                        // If incrementing and the quantity fills all remaining slots, mark the panel as filled
                        data.FillButton.SetText("Unfill");
                        data.IsFilled = true;
                    }

                    // Recalculate and adjust quantities for all other filled panels
                    RecalculateFilledPanels();

                    // Trigger updated summoning/desummoning logic
                    RefreshSummons();
                    break;
                }
            }
        }


        public void RefreshSummons()
        {
            // Clears and re-summons all minions and sentries
            DesummonAllMinions();
            DesummonAllSentries();
            SummonAllItems(Main.LocalPlayer);

            // Update the slots labels
            UpdateMinionSlotsLabel();
            UpdateSentrySlotsLabel();
        }



        private void UpdateSentrySlotsLabel()
        {
            int currentSentries = GetCurrentSentryCount();
            int maxSentries = Main.LocalPlayer.maxTurrets;

            sentrySlotsLabel?.SetText($"Sentry Slots: {currentSentries}/{maxSentries}");
        }

        public int GetTotalMinions()
        {
            int total = 0;
            foreach (var panel in interactionPanels)
            {
                var data = panel.GetTag<InteractionPanelData>();
                if (data != null && data.QuantityLabel != null)
                {
                    var currentText = data.QuantityLabel.Text;
                    int quantity = int.Parse(currentText.Replace("Minions: ", ""));
                    total += quantity;
                }
            }
            return total;
        }

        public int GetTotalSentries()
        {
            int total = 0;
            foreach (var panel in sentryPanels)
            {
                var data = panel.GetTag<InteractionPanelData>();
                if (data != null && data.QuantityLabel != null)
                {
                    var currentText = data.QuantityLabel.Text;
                    int quantity = int.Parse(currentText.Replace("Sentries: ", ""));
                    total += quantity;
                }
            }
            return total;
        }

        public void CreateInteractionPanel(int index = 0, Item item = null)
        {
            const int itemSlotSize = 48;  // Width/Height of the item slot
            const int buttonWidth = 50;  // Width of buttons
            const int labelWidth = 100;  // Increased width for the Minions label
            const int spacing = 10;      // Spacing between elements
            const int extraPadding = 20; // Extra padding for margin on the right

            int panelWidth = spacing        // Left margin
                            + itemSlotSize  // Item slot
                            + spacing        // Spacing after item slot
                            + buttonWidth    // -1 button
                            + spacing        // Spacing after -1 button
                            + buttonWidth    // +1 button
                            + spacing        // Spacing after +1 button
                            + buttonWidth    // Fill button
                            + spacing        // Spacing after Fill button
                            + labelWidth     // Minions label
                            + extraPadding;  // Right margin
            const int panelHeight = 60; // Height of the panel
            const int buttonHeight = 30; // Height of buttons
            const int labelHeight = 30; // Height of the Minions label

            // Create panel
            var panel = new UIPanel
            {
                Width = { Pixels = panelWidth },
                Height = { Pixels = panelHeight },
                Top = { Pixels = 80 + index * 70 },
                BackgroundColor = new Color(35, 35, 50, 200)
            };
            panel.SetPadding(0); // Remove any padding
            mainPanel.Append(panel);

            int maxMinions = Main.LocalPlayer.maxMinions;
            float currentMinionCount = GetCurrentMinionCount();
            int totalFromPanels = GetTotalMinions();
            int startingQuantity = 0;

            var summonSlot = new CustomItemSlot(ItemSlot.Context.InventoryItem, 1f)
            {
                Width = { Pixels = itemSlotSize },
                Height = { Pixels = itemSlotSize },
                Left = { Pixels = spacing },
                Top = { Pixels = 4},
                IsValidItem = IsMinionSummoningItem // Validate only minion summoning items
            };
            panel.Append(summonSlot);
            summonSlot.SetPadding(0); // Remove padding

            lastItemStates[summonSlot] = summonSlot.Item.Clone();

            // Add -1 button
            var minusButton = CreateButton(
                "-1",
                new Vector2(summonSlot.Left.Pixels + itemSlotSize + spacing, (panelHeight - buttonHeight) / 2),
                (evt, element) => UpdateQuantity(panel, -1));
            minusButton.Remove();

            // Add +1 button
            var plusButton = CreateButton(
                "+1",
                new Vector2(minusButton.Left.Pixels + buttonWidth + spacing, (panelHeight - buttonHeight) / 2),
                (evt, element) => UpdateQuantity(panel, 1));
            plusButton.Remove();

            // Add Fill button
            var fillButton = CreateButton(
                "Fill",
                new Vector2(plusButton.Left.Pixels + buttonWidth + spacing, (panelHeight - buttonHeight) / 2),
                (evt, element) => ToggleFill(panel));
            fillButton.Remove();

            // Add quantity label with fine-tuned vertical alignment
            var quantityLabel = new UIText($"Minions: {startingQuantity}", 1f)
            {
                Width = { Pixels = labelWidth },
                Height = { Pixels = labelHeight },
                Left = { Pixels = fillButton.Left.Pixels + buttonWidth + spacing },
                Top = { Pixels = (panelHeight - labelHeight) / 2 + 6 } // Slight downward adjustment
            };
            quantityLabel.Remove();

            // Store panel data
            panel.SetTag(new InteractionPanelData
            {
                MinusButton = minusButton,
                PlusButton = plusButton,
                FillButton = fillButton,
                QuantityLabel = quantityLabel,
                ItemSlot = summonSlot
            });

            interactionPanels.Add(panel);
            UpdateMainPanelHeight();
        }

        private bool IsMinionSummoningItem(Item item)
        {
            if (item == null || item.IsAir) return false;

            if (item.shoot > ProjectileID.None)
            {
                var projectile = new Projectile();
                projectile.SetDefaults(item.shoot);
                return projectile.minion && !projectile.sentry;
            }

            return false;
        }



        public override void OnActivate()
        {
            base.OnActivate();

            // Add 5 Raven Staffs to the player's inventory
            var player = Main.LocalPlayer;
            for (int i = 0; i < 5; i++)
            {
                player.QuickSpawnItem(null, ItemID.RavenStaff);
            }
            for (int i = 0; i < 5; i++)
            {
                player.QuickSpawnItem(null, ItemID.StaffoftheFrostHydra);
            }
            for (int i = 0; i < 1; i++)
            {
                player.QuickSpawnItem(null, ItemID.PygmyNecklace);
            }
            for (int i = 0; i < 1; i++)
            {
                player.QuickSpawnItem(null, ItemID.SquireGreatHelm);
            }
        }

        private void DesummonAllSentries()
        {
            int desummonedCount = 0;

            for (int i = 0; i < Main.projectile.Length; i++)
            {
                var proj = Main.projectile[i];

                // Check if the projectile is active, owned by the player, and is a sentry
                if (proj.active && proj.owner == Main.myPlayer && proj.WipableTurret)
                {
                    proj.Kill(); // Despawn the sentry
                    desummonedCount++;
                }
            }
        }


        private void DesummonAllMinions()
        {
            int desummonedCount = 0;

            for (int i = 0; i < Main.projectile.Length; i++)
            {
                var proj = Main.projectile[i];

                // Check if the projectile is active, owned by the player, and is a minion
                if (proj.active && proj.owner == Main.myPlayer && proj.minion)
                {
                    proj.Kill(); // Despawn the minion
                    desummonedCount++;
                }
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            UpdateMinionSlotsLabel();
            UpdateSentrySlotsLabel();

            // Update Minion Panels
            for (int i = 0; i < interactionPanels.Count; i++)
            {
                var panel = interactionPanels[i];
                var data = panel.GetTag<InteractionPanelData>();
                var slot = data.ItemSlot;

                if (!lastItemStates.TryGetValue(slot, out var lastItem) ||
                    !lastItem.IsAir && slot.Item.IsAir ||
                    slot.Item.type != lastItem.type)
                {
                    lastItemStates[slot] = slot.Item.Clone();
                    HandleItemSlotChanged(i, slot.Item);
                }
            }

            // Update Sentry Panels
            for (int i = 0; i < sentryPanels.Count; i++)
            {
                var panel = sentryPanels[i];
                var data = panel.GetTag<InteractionPanelData>();
                var slot = data.ItemSlot;

                if (!lastItemStates.TryGetValue(slot, out var lastItem) ||
                    !lastItem.IsAir && slot.Item.IsAir ||
                    slot.Item.type != lastItem.type)
                {
                    lastItemStates[slot] = slot.Item.Clone();
                    HandleSentryPanelChanged(i, slot.Item);
                }
            }

            // Dragging Logic
            if (dragging)
            {
                mainPanel.Left.Pixels = Main.mouseX - offset.X;
                mainPanel.Top.Pixels = Main.mouseY - offset.Y;
                Recalculate();
            }
        }

        private void HandleSentryPanelChanged(int index, Item item)
        {
            var panel = sentryPanels[index];
            var data = panel.GetTag<InteractionPanelData>();

            if (item != null && !item.IsAir && IsSentrySummoningItem(item)) // Ensure it's a valid sentry item
            {
                // Determine starting quantity
                int maxSentries = Main.LocalPlayer.maxTurrets;
                int currentSentryCount = GetCurrentSentryCount();
                int totalFromPanels = GetTotalSentries();

                int startingQuantity = 0;

                // Update the quantity label
                data.QuantityLabel.SetText($"Sentries: {startingQuantity}");

                // Item added: ensure buttons and label are visible
                data.MinusButton.Recalculate();
                data.PlusButton.Recalculate();
                data.FillButton.Recalculate();
                data.QuantityLabel.Recalculate();

                panel.Append(data.MinusButton);
                panel.Append(data.PlusButton);
                panel.Append(data.FillButton);
                panel.Append(data.QuantityLabel);

                // Dynamically add a new panel if this is the last one
                if (index == sentryPanels.Count - 1)
                {
                    CreateSentryPanel(sentryPanels.Count);
                }
                lastItemStates[data.ItemSlot] = item.Clone();
            }
            else if (lastItemStates[data.ItemSlot]?.IsAir == true)
            {
                // Only remove if the item is genuinely empty
                data.MinusButton.Remove();
                data.PlusButton.Remove();
                data.FillButton.Remove();
                data.QuantityLabel.Remove();

                sentryPanels.RemoveAt(index);
                mainPanel.RemoveChild(panel);

                // Recalculate positions of remaining panels
                for (int i = 0; i < sentryPanels.Count; i++)
                {
                    var remainingPanel = sentryPanels[i];
                    remainingPanel.Top.Set(80 + interactionPanels.Count * 70 + 40 + i * 70, 0f);
                }
            }
            UpdateMainPanelHeight();
            RefreshSummons();
            lastItemStates[data.ItemSlot] = item?.Clone();
        }


        private void HandleItemSlotChanged(int index, Item item)
        {
            var panel = interactionPanels[index];
            var data = panel.GetTag<InteractionPanelData>();

            if (item != null && !item.IsAir && IsMinionSummoningItem(item)) // Ensure it's a valid minion item
            {
                // Determine starting quantity
                int maxMinions = Main.LocalPlayer.maxMinions;
                float currentMinionCount = GetCurrentMinionCount();
                int totalFromPanels = GetTotalMinions();

                int startingQuantity = 0;

                // Update the quantity label
                data.QuantityLabel.SetText($"Minions: {startingQuantity}");

                // Item added: ensure buttons and label are visible
                data.MinusButton.Recalculate();
                data.PlusButton.Recalculate();
                data.FillButton.Recalculate();
                data.QuantityLabel.Recalculate();

                panel.Append(data.MinusButton);
                panel.Append(data.PlusButton);
                panel.Append(data.FillButton);
                panel.Append(data.QuantityLabel);

                // Dynamically add a new panel if this is the last one
                if (index == interactionPanels.Count - 1)
                {
                    CreateInteractionPanel(interactionPanels.Count);
                }
                lastItemStates[data.ItemSlot] = item.Clone();
            }
            else
            {
                // Remove invalid item and reset the panel
                data.MinusButton.Remove();
                data.PlusButton.Remove();
                data.FillButton.Remove();
                data.QuantityLabel.Remove();

                interactionPanels.RemoveAt(index);
                mainPanel.RemoveChild(panel);

                // Recalculate positions of remaining panels
                for (int i = 0; i < interactionPanels.Count; i++)
                {
                    var remainingPanel = interactionPanels[i];
                    remainingPanel.Top.Set(80 + i * 70, 0f);
                }

                UpdateMainPanelHeight();
            }
            RefreshSummons();
            lastItemStates[data.ItemSlot] = item?.Clone();
            RecalculateSentryPanelPositions();
        }

        private UITextButton CreateButton(string text, Vector2 position, UIElement.MouseEvent action)
        {
            var button = new UITextButton(text, 1f)
            {
                Width = { Pixels = 50 },
                Height = { Pixels = 30 },
                Left = { Pixels = position.X },
                Top = { Pixels = position.Y }
            };
            button.OnLeftClick += action;
            return button;
        }

        private void ToggleFill(UIPanel panel)
        {
            var player = Main.LocalPlayer;

            // Determine if it's a minion or sentry panel
            bool isSentryPanel = sentryPanels.Contains(panel);

            // Get the InteractionPanelData for the selected panel
            var data = panel.GetTag<InteractionPanelData>();
            if (data == null || data.ItemSlot.Item == null || data.ItemSlot.Item.IsAir)
                return;

            // Parse the current quantity for this panel
            int currentQuantity = int.Parse(data.QuantityLabel.Text.Replace(isSentryPanel ? "Sentries: " : "Minions: ", ""));

            // If the panel is already filled
            if (data.IsFilled)
            {
                // Unfill: Reset the quantity to 0
                data.QuantityLabel.SetText($"{(isSentryPanel ? "Sentries" : "Minions")}: 0");
                data.FillButton.SetText("Fill"); // Update button label
                data.IsFilled = false; // Mark as unfilled
            }
            else
            {
                // Unfill other panels first to ensure only one is filled
                var relevantPanels = isSentryPanel ? sentryPanels : interactionPanels;
                foreach (var otherPanel in relevantPanels)
                {
                    if (otherPanel == panel)
                        continue;

                    var otherData = otherPanel.GetTag<InteractionPanelData>();
                    if (otherData != null && otherData.IsFilled)
                    {
                        otherData.QuantityLabel.SetText($"{(isSentryPanel ? "Sentries" : "Minions")}: 0");
                        otherData.FillButton.SetText("Fill");
                        otherData.IsFilled = false;
                    }
                }

                // Fill the current panel
                int maxSlots = isSentryPanel ? player.maxTurrets : player.maxMinions;
                int totalUsedSlots = isSentryPanel ? GetTotalSentries() : GetTotalMinions();
                int remainingSlots = maxSlots - (totalUsedSlots - currentQuantity);
                int newQuantity = Math.Min(remainingSlots, maxSlots);

                data.QuantityLabel.SetText($"{(isSentryPanel ? "Sentries" : "Minions")}: {newQuantity}");
                data.FillButton.SetText("Unfill"); // Update button label
                data.IsFilled = true; // Mark as filled
            }

            // Recalculate and resummon
            RefreshSummons();
        }

        public void ClearPanels()
        {
            foreach (var panel in interactionPanels)
            {
                mainPanel.RemoveChild(panel);
            }
            foreach (var panel in sentryPanels)
            {
                mainPanel.RemoveChild(panel);
            }
            interactionPanels.Clear();
            sentryPanels.Clear();
        }

        private void StartDrag(UIMouseEvent evt, UIElement listeningElement)
        {
            dragging = true;
            offset = evt.MousePosition - new Vector2(mainPanel.Left.Pixels, mainPanel.Top.Pixels);
        }

        private void EndDrag(UIMouseEvent evt, UIElement listeningElement)
        {
            dragging = false;
        }

        private void UpdateMainPanelHeight()
        {
            const int baseHeight = 100;
            const int panelHeight = 70;

            // Calculate total height for minion and sentry panels
            int minionHeight = interactionPanels.Count * panelHeight;
            int sentryHeight = sentryPanels.Count * panelHeight;

            // Update mainPanel height
            mainPanel.Height.Set(baseHeight + minionHeight + sentryHeight + 40, 0f);

            // Position the sentry label dynamically below the minion panels
            sentrySlotsLabel.Top.Set(80 + minionHeight, 0f);

            // Recalculate the layout
            mainPanel.Recalculate();
        }

        private void RecalculateSentryPanelPositions()
        {
            const int panelHeight = 70;

            for (int i = 0; i < sentryPanels.Count; i++)
            {
                var panel = sentryPanels[i];
                panel.Top.Set(80 + interactionPanels.Count * panelHeight + 40 + i * panelHeight, 0f);
            }
        }
        public void SummonAllItems(Player player)
        {
            foreach (var panel in interactionPanels)
            {
                var data = panel.GetTag<InteractionPanelData>();
                if (data == null || data.ItemSlot.Item == null || data.ItemSlot.Item.IsAir)
                    continue;

                var summonItem = data.ItemSlot.Item;
                int quantity = int.Parse(data.QuantityLabel.Text.Replace("Minions: ", ""));

                for (int i = 0; i < quantity; i++)
                {
                    AutoSummonSystem.SummonWithItem(player, summonItem);
                }
            }

            // Summon Sentries
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
        }

        public static void CheckAndRespawnSentries(Player player)
        {
            // Screen bounds (expanded slightly to include buffer space for "off-screen" detection)
            int screenPadding = 100; // Add padding to screen bounds
            Rectangle screenBounds = new Rectangle(
                (int)(Main.screenPosition.X - screenPadding),
                (int)(Main.screenPosition.Y - screenPadding),
                Main.screenWidth + screenPadding * 2,
                Main.screenHeight + screenPadding * 2
            );

            // Iterate through all projectiles to find sentries
            foreach (Projectile proj in Main.projectile)
            {
                if (proj.active && proj.owner == player.whoAmI && proj.sentry)
                {
                    // Check if the sentry is off-screen
                    if (!screenBounds.Contains(proj.Center.ToPoint()))
                    {
                        // Kill the old sentry
                        proj.Kill();

                        // Respawn the sentry at the player's position
                        Projectile.NewProjectile(
                            player.GetSource_Misc("RespawnSentry"), // Source for respawn
                            player.Center,                         // Respawn at player's position
                            Vector2.Zero,                          // No velocity
                            proj.type,                             // Same projectile type
                            proj.originalDamage,                   // Use original damage
                            proj.knockBack,                        // Use original knockback
                            player.whoAmI                          // Owner (player)
                        );
                    }
                }
            }
        }

    }

    public class UITextButton : UIPanel
    {
        private UIText buttonText;

        public UITextButton(string text, float textScale = 1f)
        {
            Width.Set(100, 0);
            Height.Set(40, 0);
            BackgroundColor = new Color(63, 82, 151) * 0.7f;

            buttonText = new UIText(text, textScale)
            {
                HAlign = 0.5f,
                VAlign = 0.5f
            };
            Append(buttonText);
        }

        public void SetText(string text) => buttonText.SetText(text);
    }

    public class InteractionPanelData
    {
        public UITextButton MinusButton { get; set; }
        public UITextButton PlusButton { get; set; }
        public UITextButton FillButton { get; set; }
        public UIText QuantityLabel { get; set; }
        public CustomItemSlot ItemSlot { get; set; }
        public bool IsFilled { get; set; } = false; // Tracks whether the panel is filled
    }

    public static class UIExtensions
    {
        private static readonly Dictionary<UIElement, object> Tags = new();

        public static void SetTag<T>(this UIElement element, T tag) => Tags[element] = tag;

        public static T GetTag<T>(this UIElement element) =>
            Tags.TryGetValue(element, out var tag) && tag is T value ? value : default;
    }


}
