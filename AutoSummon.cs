// File: AutoSummon.cs
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using Microsoft.Xna.Framework;
using Terraria.GameInput;
using AutoSummon.UI;
using Terraria.ID;

namespace AutoSummon
{
    public class AutoSummon : ModSystem
    {
        private UserInterface draggableUI;
        private DraggableUIPanel draggableUIPanel;
        public static DraggableUIPanel DraggableUIPanelInstance; // Static reference
        private static ModKeybind toggleUIKeybind;
        private bool uiVisible;

        public override void OnModLoad()
        {
            if (!Main.dedServ)
            {
                draggableUIPanel = new DraggableUIPanel();
                draggableUI = new UserInterface();
                draggableUI.SetState(draggableUIPanel);

                // Assign the static reference
                DraggableUIPanelInstance = draggableUIPanel;

                // Register keybind
                toggleUIKeybind = KeybindLoader.RegisterKeybind(Mod, "Toggle UI", "K");
            }
        }

        public override void Unload()
        {
            draggableUIPanel = null;
            draggableUI = null;
            toggleUIKeybind = null;
            DraggableUIPanelInstance = null;
        }

        public override void UpdateUI(GameTime gameTime)
        {
            if (uiVisible)
            {
                draggableUI?.Update(gameTime);
            }

            if (toggleUIKeybind?.JustPressed == true)
            {
                ToggleUI();
            }
        }

        public override void ModifyInterfaceLayers(System.Collections.Generic.List<GameInterfaceLayer> layers)
        {
            int inventoryLayerIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
            if (inventoryLayerIndex != -1)
            {
                layers.Insert(inventoryLayerIndex + 1, new LegacyGameInterfaceLayer(
                    "AutoSummon: Draggable UI",
                    delegate
                    {
                        if (uiVisible)
                        {
                            draggableUI?.Draw(Main.spriteBatch, new GameTime());
                        }
                        return true;
                    },
                    InterfaceScaleType.UI));
            }
        }

        private void ToggleUI()
        {
            uiVisible = !uiVisible;
            if (uiVisible)
            {
                draggableUI.SetState(draggableUIPanel); // Show UI
            }
            else
            {
                draggableUI.SetState(null); // Hide UI
            }
        }
    }
}
