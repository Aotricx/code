using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Achievements;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.UI;
using Terraria.UI.Chat;

namespace CustomSlot.UI
{
    public class CustomItemSlot : UIElement
    {
        public enum ArmorType
        {
            Head,
            Chest,
            Leg
        }

        public static class DefaultColors
        {
            public static readonly Color EmptyTexture = Color.White * 0.35f;
            public static readonly Color InventoryItemBack = Main.inventoryBack;
            public static readonly Color EquipBack = Color.White * 0.8f;
        }

        internal const int TickOffsetX = 6;
        internal const int TickOffsetY = 2;

        protected Item item;
        protected CroppedTexture2D backgroundTexture;
        protected float scale;
        protected ToggleVisibilityButton toggleButton;
        protected bool forceToggleButton;
        public event ItemChangedEventHandler ItemChanged;
        public event ItemVisiblityChangedEventHandler ItemVisibilityChanged;

        public int Context { get; }
        public bool ItemVisible { get; set; }
        public string HoverText { get; set; }
        public Func<Item, bool> IsValidItem { get; set; }
        public CroppedTexture2D EmptyTexture { get; set; }
        public CustomItemSlot Partner { get; set; }
        public Item Item
        {
            get => item;
            set
            {
                if (item == value) return; // No change, so exit early

                var oldItem = item.Clone(); // Keep track of the old item
                item = value.Clone(); // Clone the new item to avoid direct reference issues

                ItemChanged?.Invoke(this, new ItemChangedEventArgs(oldItem, item)); // Trigger the event
                Recipe.FindRecipes(); // Update crafting recipes
            }
        }
        public float Scale
        {
            get => scale;
            set
            {
                scale = value;
                CalculateSize();
            }
        }

        public void SetItem(Item newItem)
        {
            item = newItem.Clone();
            ItemChanged?.Invoke(this, new ItemChangedEventArgs(item, newItem));
            Recalculate(); // Update the UI to reflect the new item
        }


        public CroppedTexture2D BackgroundTexture
        {
            get => backgroundTexture;
            set
            {
                backgroundTexture = value;
                CalculateSize();
            }
        }

        public bool ForceToggleButton
        {
            get => forceToggleButton;
            set
            {
                forceToggleButton = value;
                bool hasButton = forceToggleButton || HasToggleButton(Context);

                if (!hasButton)
                {
                    if (toggleButton == null) return;

                    RemoveChild(toggleButton);
                    toggleButton = null;
                }
                else
                {
                    toggleButton = new ToggleVisibilityButton();
                    Append(toggleButton);
                }
            }
        }

        public CustomItemSlot(int context = ItemSlot.Context.InventoryItem, float scale = 1f,
            ArmorType defaultArmorIcon = ArmorType.Head)
        {
            Context = context;
            this.scale = scale;
            backgroundTexture = GetBackgroundTexture(context);
            EmptyTexture = GetEmptyTexture(context, defaultArmorIcon);
            ItemVisible = true;
            ForceToggleButton = false;

            item = new Item();
            item.SetDefaults();

            CalculateSize();
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            DoDraw(spriteBatch);

            if (ContainsPoint(Main.MouseScreen) && !PlayerInput.IgnoreMouseInterface)
            {
                Main.LocalPlayer.mouseInterface = true;

                if (toggleButton != null && toggleButton.ContainsPoint(Main.MouseScreen))
                    return;

                if (Main.mouseItem.IsAir || IsValidItem == null || IsValidItem(Main.mouseItem))
                {
                    int tempContext = Context;
                    Item tempItem = Item.Clone();

                    // Display inventory icon near cursor when Ctrl is held
                    if ((Main.keyState.IsKeyDown(Keys.LeftControl) || Main.keyState.IsKeyDown(Keys.RightControl)) && Item.stack > 0)
                    {
                        Main.cursorOverride = 9; // 9 corresponds to the inventory arrow icon in Terraria

                        if (Main.mouseLeftRelease && Main.mouseLeft)
                        {
                            // Move the item to the inventory
                            MoveItemToInventory();
                            ItemChanged?.Invoke(this, new ItemChangedEventArgs(tempItem, Item));
                            return; // Prevent further processing
                        }
                    }
                    else if (Main.mouseRightRelease && Main.mouseRight)
                    {
                        // Handle right-click behavior for vanity slots
                        if (Context == ItemSlot.Context.EquipArmorVanity)
                            tempContext = ItemSlot.Context.EquipArmor;
                        else if (Context == ItemSlot.Context.EquipAccessoryVanity)
                            tempContext = ItemSlot.Context.EquipAccessory;

                        if (Partner != null)
                        {
                            SwapWithPartner();
                            ItemChanged?.Invoke(this, new ItemChangedEventArgs(tempItem, Item));
                        }
                        else
                        {
                            ItemSlot.Handle(ref item, tempContext);

                            if (tempItem.type != Item.type)
                                ItemChanged?.Invoke(this, new ItemChangedEventArgs(tempItem, Item));
                        }
                    }
                    else
                    {
                        // Default item handling (placing or swapping items)
                        ItemSlot.Handle(ref item, tempContext);

                        if (tempItem.type != Item.type)
                        {
                            // Open the inventory if it was closed and the item was removed
                            if (!Main.playerInventory && Item.IsAir && !tempItem.IsAir)
                            {
                                Main.playerInventory = true; // Open the inventory
                                SoundEngine.PlaySound(SoundID.MenuOpen); // Play inventory open sound
                            }

                            ItemChanged?.Invoke(this, new ItemChangedEventArgs(tempItem, Item));
                        }
                    }

                    // Show hover text, if applicable
                    if (!string.IsNullOrEmpty(HoverText))
                    {
                        Main.hoverItemName = HoverText;
                    }
                }
            }
        }

        private void MoveItemToInventory()
        {
            Player player = Main.LocalPlayer;

            // Find the first available inventory slot
            for (int i = 0; i < player.inventory.Length; i++)
            {
                if (player.inventory[i].IsAir)
                {
                    player.inventory[i] = Item.Clone();
                    Item.TurnToAir();
                    Recipe.FindRecipes();
                    SoundEngine.PlaySound(SoundID.Grab);
                    return;
                }
            }
        }

        protected void DoDraw(SpriteBatch spriteBatch)
        {
            Rectangle rectangle = GetDimensions().ToRectangle();
            Texture2D itemTexture = EmptyTexture.Texture;
            Rectangle itemRectangle = EmptyTexture.Rectangle;
            Color color = EmptyTexture.Color;
            float itemLightScale = 1f;

            if (Item.stack > 0)
            {
                itemTexture = TextureAssets.Item[Item.type].Value;
                itemRectangle = Main.itemAnimations[Item.type] != null
                    ? Main.itemAnimations[Item.type].GetFrame(itemTexture)
                    : itemTexture.Bounds;

                color = Color.White;
                ItemSlot.GetItemLight(ref color, ref itemLightScale, Item);
            }

            if (BackgroundTexture.Texture != null)
            {
                spriteBatch.Draw(
                    BackgroundTexture.Texture,
                    rectangle.TopLeft(),
                    BackgroundTexture.Rectangle,
                    BackgroundTexture.Color,
                    0f,
                    Vector2.Zero,
                    Scale,
                    SpriteEffects.None,
                    1f);
            }

            if (itemTexture != null)
            {
                float oversizedScale = 1f;
                if (itemRectangle.Width > 32 || itemRectangle.Height > 32)
                {
                    oversizedScale = Math.Min(32f / itemRectangle.Width, 32f / itemRectangle.Height);
                }

                oversizedScale *= Scale;

                spriteBatch.Draw(
                    itemTexture,
                    rectangle.Center(),
                    itemRectangle,
                    Item.GetAlpha(color),
                    0f,
                    itemRectangle.Size() * 0.5f,
                    oversizedScale * itemLightScale,
                    SpriteEffects.None,
                    0f);
            }

            if (Item.stack > 1)
            {
                ChatManager.DrawColorCodedStringWithShadow(
                    spriteBatch,
                    FontAssets.ItemStack.Value,
                    Item.stack.ToString(),
                    rectangle.TopLeft() + new Vector2(10f, 26f) * Scale,
                    Color.White,
                    0f,
                    Vector2.Zero,
                    new Vector2(Scale),
                    -1f,
                    Scale);
            }
        }

        private void SwapWithPartner()
        {
            Utils.Swap(ref item, ref Partner.item);
            SoundEngine.PlaySound(SoundID.Grab);
            Recipe.FindRecipes();

            if (Item.stack <= 0) return;

            if (Context != 0)
            {
                if (Context - 8 <= 4 || Context - 16 <= 1)
                {
                    AchievementsHelper.HandleOnEquip(Main.LocalPlayer, Item, Context);
                }
            }
            else
            {
                AchievementsHelper.NotifyItemPickup(Main.LocalPlayer, Item);
            }
        }

        internal void CalculateSize()
        {
            if (BackgroundTexture == CroppedTexture2D.Empty) return;

            float width = BackgroundTexture.Texture.Width * Scale;
            float height = BackgroundTexture.Texture.Height * Scale;

            Width.Set(width, 0f);
            Height.Set(height, 0f);
        }

        public static CroppedTexture2D GetBackgroundTexture(int context)
        {
            Texture2D texture;
            Color color = Main.inventoryBack;

            switch (context)
            {
                case ItemSlot.Context.EquipAccessory:
                case ItemSlot.Context.EquipArmor:
                case ItemSlot.Context.EquipGrapple:
                case ItemSlot.Context.EquipMount:
                case ItemSlot.Context.EquipMinecart:
                case ItemSlot.Context.EquipPet:
                case ItemSlot.Context.EquipLight:
                    color = DefaultColors.EquipBack;
                    texture = TextureAssets.InventoryBack3.Value;
                    break;
                case ItemSlot.Context.EquipArmorVanity:
                case ItemSlot.Context.EquipAccessoryVanity:
                    color = DefaultColors.EquipBack;
                    texture = TextureAssets.InventoryBack8.Value;
                    break;
                case ItemSlot.Context.EquipDye:
                    color = DefaultColors.EquipBack;
                    texture = TextureAssets.InventoryBack12.Value;
                    break;
                case ItemSlot.Context.ChestItem:
                    color = DefaultColors.InventoryItemBack;
                    texture = TextureAssets.InventoryBack5.Value;
                    break;
                case ItemSlot.Context.BankItem:
                    color = DefaultColors.InventoryItemBack;
                    texture = TextureAssets.InventoryBack2.Value;
                    break;
                case ItemSlot.Context.GuideItem:
                case ItemSlot.Context.PrefixItem:
                case ItemSlot.Context.CraftingMaterial:
                    color = DefaultColors.InventoryItemBack;
                    texture = TextureAssets.InventoryBack4.Value;
                    break;
                case ItemSlot.Context.TrashItem:
                    color = DefaultColors.InventoryItemBack;
                    texture = TextureAssets.InventoryBack7.Value;
                    break;
                case ItemSlot.Context.ShopItem:
                    color = DefaultColors.InventoryItemBack;
                    texture = TextureAssets.InventoryBack6.Value;
                    break;
                default:
                    texture = TextureAssets.InventoryBack.Value;
                    break;
            }

            return new CroppedTexture2D(texture, color);
        }

        public static CroppedTexture2D GetEmptyTexture(int context, ArmorType armorType = ArmorType.Head)
        {
            int frame = -1;

            switch (context)
            {
                case ItemSlot.Context.EquipArmor:
                    switch (armorType)
                    {
                        case ArmorType.Head:
                            frame = 0;
                            break;
                        case ArmorType.Chest:
                            frame = 6;
                            break;
                        case ArmorType.Leg:
                            frame = 12;
                            break;
                    }
                    break;
                case ItemSlot.Context.EquipArmorVanity:
                    switch (armorType)
                    {
                        case ArmorType.Head:
                            frame = 3;
                            break;
                        case ArmorType.Chest:
                            frame = 9;
                            break;
                        case ArmorType.Leg:
                            frame = 15;
                            break;
                    }
                    break;
                case ItemSlot.Context.EquipAccessory:
                    frame = 11;
                    break;
                case ItemSlot.Context.EquipAccessoryVanity:
                    frame = 2;
                    break;
                case ItemSlot.Context.EquipDye:
                    frame = 1;
                    break;
                case ItemSlot.Context.EquipGrapple:
                    frame = 4;
                    break;
                case ItemSlot.Context.EquipMount:
                    frame = 13;
                    break;
                case ItemSlot.Context.EquipMinecart:
                    frame = 7;
                    break;
                case ItemSlot.Context.EquipPet:
                    frame = 10;
                    break;
                case ItemSlot.Context.EquipLight:
                    frame = 17;
                    break;
            }

            if (frame == -1) return CroppedTexture2D.Empty;

            Texture2D extraTextures = TextureAssets.Extra[54].Value;
            Rectangle rectangle = extraTextures.Frame(3, 6, frame % 3, frame / 3);
            rectangle.Width -= 2;
            rectangle.Height -= 2;

            return new CroppedTexture2D(extraTextures, DefaultColors.EmptyTexture, rectangle);
        }

        public static bool HasToggleButton(int context)
        {
            return context == ItemSlot.Context.EquipAccessory ||
                   context == ItemSlot.Context.EquipLight ||
                   context == ItemSlot.Context.EquipPet;
        }

        protected internal class ToggleVisibilityButton : UIElement
        {
            internal ToggleVisibilityButton()
            {
                Width.Set(TextureAssets.InventoryTickOn.Value.Width, 0f);
                Height.Set(TextureAssets.InventoryTickOn.Value.Height, 0f);
            }

            protected override void DrawSelf(SpriteBatch spriteBatch)
            {
                if (Parent is not CustomItemSlot slot) return;

                DoDraw(spriteBatch, slot);

                if (ContainsPoint(Main.MouseScreen) && !PlayerInput.IgnoreMouseInterface)
                {
                    Main.LocalPlayer.mouseInterface = true;
                    Main.hoverItemName = Language.GetTextValue(slot.ItemVisible ? "LegacyInterface.59" : "LegacyInterface.60");

                    if (Main.mouseLeftRelease && Main.mouseLeft)
                    {
                        SoundEngine.PlaySound(SoundID.MenuTick);
                        slot.ItemVisible = !slot.ItemVisible;
                        slot.ItemVisibilityChanged?.Invoke(slot, new ItemVisibilityChangedEventArgs(slot.ItemVisible));
                    }
                }
            }

            protected void DoDraw(SpriteBatch spriteBatch, CustomItemSlot slot)
            {
                Rectangle parentRectangle = Parent.GetDimensions().ToRectangle();
                Texture2D tickTexture =
                    slot.ItemVisible ? TextureAssets.InventoryTickOn.Value : TextureAssets.InventoryTickOff.Value;

                Left.Set(parentRectangle.Width - Width.Pixels + TickOffsetX, 0f);
                Top.Set(-TickOffsetY, 0f);

                spriteBatch.Draw(
                    tickTexture,
                    GetDimensions().Position(),
                    Color.White * 0.7f);
            }
        }
    }
}
