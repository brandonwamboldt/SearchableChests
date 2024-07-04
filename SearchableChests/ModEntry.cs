using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using static StardewValley.Menus.CoopMenu;
using static StardewValley.Menus.ShopMenu;

namespace SearchableChests
{
    public class ModEntry : Mod
    {
        private static ModConfig? Config;
        private static IMonitor? sMonitor;
        private static Harmony? Harmony;

        /// <summary> The filter field (search bar) object. </summary>
        private static readonly PerScreen<TextBox> filterField = new();

        /// <summary> The string that was in the filter field the last time it was checked. </summary>
        private static readonly PerScreen<string> lastFilterString = new PerScreen<string>(() => "");

        public override void Entry(IModHelper helper)
        {
            sMonitor = Monitor;
            Config = Helper.ReadConfig<ModConfig>();

            var dresser = Helper.ModRegistry.Get("Dragoon23.CustomizeDresser");

            helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;

            Harmony = new Harmony(ModManifest.UniqueID);

            if (dresser != null || true)
            {
                sMonitor.Log("Enabling compatibility with CustomizeDresser mod", LogLevel.Info);

                Harmony.Patch(
                   original: AccessTools.Method(typeof(ShopMenu), nameof(ShopMenu.setUpShopOwner)),
                   postfix: new HarmonyMethod(typeof(ModEntry), nameof(ShopMenu_setUpShopOwner_Postfix))
                );

                Harmony.Patch(
                   original: AccessTools.Method(typeof(ShopMenu), nameof(ShopMenu.draw), new[] { typeof(SpriteBatch) }),
                   prefix: new HarmonyMethod(typeof(ModEntry), nameof(ShopMenu_draw_Prefix)),
                   postfix: new HarmonyMethod(typeof(ModEntry), nameof(ShopMenu_draw_Postfix))
                );

                Harmony.Patch(
                   original: AccessTools.Method(typeof(ShopMenu), nameof(ShopMenu.receiveLeftClick)),
                   prefix: new HarmonyMethod(typeof(ModEntry), nameof(ShopMenu_receiveLeftClick_Prefix))
                );

                Harmony.Patch(
                   original: AccessTools.Method(typeof(ShopMenu), nameof(ShopMenu.receiveKeyPress)),
                   prefix: new HarmonyMethod(typeof(ModEntry), nameof(ShopMenu_receiveKeyPress_Prefix))
                );

                Harmony.Patch(
                   original: AccessTools.Method(typeof(ShopMenu), nameof(ShopMenu.applyTab)),
                   postfix: new HarmonyMethod(typeof(ModEntry), nameof(ShopMenu_applyTab_Postfix))
                );
            }

            Harmony.Patch(
               original: AccessTools.Method(typeof(ItemGrabMenu), nameof(ItemGrabMenu.draw), new[] { typeof(SpriteBatch) }),
               prefix: new HarmonyMethod(typeof(ModEntry), nameof(ItemGrabMenu_draw_Prefix)),
               postfix: new HarmonyMethod(typeof(ModEntry), nameof(ItemGrabMenu_draw_Postfix))
            );

            Harmony.Patch(
               original: AccessTools.Method(typeof(ItemGrabMenu), nameof(ItemGrabMenu.SetupBorderNeighbors)),
               prefix: new HarmonyMethod(typeof(ModEntry), nameof(ItemGrabMenu_SetupBorderNeighbors_Prefix))
            );

            Harmony.Patch(
               original: AccessTools.Method(typeof(ItemGrabMenu), nameof(ItemGrabMenu.receiveKeyPress)),
               prefix: new HarmonyMethod(typeof(ModEntry), nameof(ItemGrabMenu_receiveKeyPress_Prefix))
            );

            Harmony.Patch(
               original: AccessTools.Method(typeof(ItemGrabMenu), nameof(ItemGrabMenu.receiveLeftClick)),
               prefix: new HarmonyMethod(typeof(ModEntry), nameof(ItemGrabMenu_receiveLeftClick_Prefix))
            );

            Harmony.Patch(
               original: AccessTools.Method(typeof(MenuWithInventory), nameof(MenuWithInventory.readyToClose)),
               postfix: new HarmonyMethod(typeof(ModEntry), nameof(MenuWithInventory_readyToClose_Postfix))
            );
        }

        private void GameLoop_GameLaunched(object sender, GameLaunchedEventArgs e)
        {

            // Get Generic Mod Config Menu's API (if it's installed)
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            configMenu.Register(
                mod: ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => Helper.WriteConfig(Config)
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Search X Offset",
                getValue: () => Config.FilterOffsetX,
                setValue: value => Config.FilterOffsetX = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Search Y Offset",
                getValue: () => Config.FilterOffsetY,
                setValue: value => Config.FilterOffsetY = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Search Bar Width",
                min: 0,
                max: 200,
                getValue: () => Config.FilterWidth,
                setValue: value => Config.FilterWidth = value
            );
        }

        public static void ItemGrabMenu_receiveLeftClick_Prefix(int x, int y, bool playSound = true)
        {
            if (x >= filterField.Value.X && x <= filterField.Value.X + filterField.Value.Width)
            {
                if (y >= filterField.Value.Y && y <= filterField.Value.Y + filterField.Value.Height)
                {
                    filterField.Value.SelectMe();
                    return;
                }
            }

            filterField.Value.Selected = false;
        }

        public static bool ItemGrabMenu_receiveKeyPress_Prefix(GameMenu __instance, Keys key)
        {
            if (key == Keys.Escape)
            {
                lastFilterString.Value = "";
                filterField.Value.Selected = false;
                return true;
            }
            else if (key == Keys.E && !filterField.Value.Selected)
            {
                lastFilterString.Value = "";
                filterField.Value.Selected = false;
                return true;
            } 
            else if (!filterField.Value.Selected && key != Keys.Back)
            {
                filterField.Value.Selected = true;
                filterField.Value.Text += key.ToString();
            }

            return false;
        }

        public static void MenuWithInventory_readyToClose_Postfix(IClickableMenu __instance, ref bool __result)
        {
            if (__instance is not ItemGrabMenu || filterField.Value is null)
                return;

            __result &= !filterField.Value.Selected;
        }

        public static void ItemGrabMenu_SetupBorderNeighbors_Prefix(ItemGrabMenu __instance, ref bool ___HasUpdateTicked)
        {
            if (___HasUpdateTicked)
            {
                return;
            }

            __instance.inventory.highlightMethod = item =>
            {
                if (item.DisplayName.Contains(lastFilterString.Value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return false;
            };

            __instance.ItemsToGrabMenu.highlightMethod = item =>
            {
                if (item.DisplayName.Contains(lastFilterString.Value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return false;
            };

            if (filterField.Value is null)
            {
                filterField.Value = new TextBox(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Game1.textColor)
                {
                    Text = ""
                };

                filterField.Value.Width = (int)(__instance.width * (Config.FilterWidth / 100f));
                filterField.Value.OnEnterPressed += sender => sender.Selected = false;
                filterField.Value.OnTabPressed += sender => sender.Selected = false;
            }

            filterField.Value.Text = "";
            filterField.Value.Width = (int)(__instance.width * (Config.FilterWidth / 100f));
        }

        public static void ItemGrabMenu_draw_Prefix(SpriteBatch b, ItemGrabMenu __instance, out bool __state)
        {
            __state = __instance.drawBG;
            __instance.drawBG = false;

            if (__state && !Game1.options.showClearBackgrounds)
            {
                b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * 0.5f);
            }

            if (filterField.Value.Text != lastFilterString.Value)
            {
                lastFilterString.Value = filterField.Value.Text;
            }

            filterField.Value.X = __instance.xPositionOnScreen + __instance.width / 2 - filterField.Value.Width / 2 + Config.FilterOffsetX;
            filterField.Value.Y = Math.Min(__instance.yPositionOnScreen + __instance.height - IClickableMenu.spaceToClearTopBorder, Game1.uiViewport.Height - 50) + Config.FilterOffsetY;
            filterField.Value.Draw(b);
        }

        public static void ItemGrabMenu_draw_Postfix(SpriteBatch b, ItemGrabMenu __instance, bool __state)
        {
            __instance.drawBG = __state;
        }

        public static bool ShopMenu_receiveKeyPress_Prefix(ShopMenu __instance, Keys key)
        {
            if (__instance.ShopId != "Dresser")
            {
                return true;
            }

            if (key == Keys.Escape)
            {
                lastFilterString.Value = "";
                filterField.Value.Selected = false;
                return true;
            }
            else if (key == Keys.E && !filterField.Value.Selected)
            {
                lastFilterString.Value = "";
                filterField.Value.Selected = false;
                return true;
            }
            else if (!filterField.Value.Selected && key != Keys.Back)
            {
                filterField.Value.Selected = true;
                filterField.Value.Text += key.ToString();
            }

            return false;
        }

        public static bool ShopMenu_receiveLeftClick_Prefix(ShopMenu __instance, int x, int y, bool playSound = true)
        {
            if (filterField.Value is null || __instance.ShopId != "Dresser")
            {
                return true;
            }

            if (x >= filterField.Value.X && x <= filterField.Value.X + filterField.Value.Width)
            {
                if (y >= filterField.Value.Y && y <= filterField.Value.Y + filterField.Value.Height)
                {
                    filterField.Value.SelectMe();
                    return false;
                }
            }

            filterField.Value.Selected = false;
            return true;
        }

        public static void ShopMenu_setUpShopOwner_Postfix(string who, string shopId, ShopMenu __instance)
        {
            if (__instance.ShopId != "Dresser")
            {
                return;
            }

            if (filterField.Value is null)
            {
                filterField.Value = new TextBox(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Game1.textColor)
                {
                    Text = ""
                };

                filterField.Value.Width = 100;
                filterField.Value.OnEnterPressed += sender => sender.Selected = false;
                filterField.Value.OnTabPressed += sender => sender.Selected = false;
            }

            filterField.Value.Text = "";
        }

        public static void ShopMenu_draw_Prefix(SpriteBatch b, ShopMenu __instance, out bool __state)
        {
            __state = Game1.options.showClearBackgrounds;

            if (__instance.ShopId != "Dresser")
            {
                return;
            }

            if (!Game1.options.showMenuBackground && !Game1.options.showClearBackgrounds)
            {
                b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);
            }

            // Temporarily disable the background so we don't double render it
            Game1.options.showClearBackgrounds = true;

            if (filterField.Value.Text != lastFilterString.Value)
            {
                lastFilterString.Value = filterField.Value.Text;
                __instance.applyTab();
            }

            filterField.Value.Width = (int)(__instance.width * (Config.FilterWidth / 100f));
            filterField.Value.X = __instance.xPositionOnScreen + __instance.width / 2 - filterField.Value.Width / 2 + Config.FilterOffsetX;
            filterField.Value.Y = Math.Min(__instance.yPositionOnScreen + __instance.height + 60, Game1.uiViewport.Height - 50) + Config.FilterOffsetY;
            filterField.Value.Draw(b);
        }

        public static void ShopMenu_applyTab_Postfix(ShopMenu __instance, int ___currentTab, Rectangle ___scrollBarRunner)
        {
            if (__instance.ShopId != "Dresser")
            {
                return;
            }

            ShopTabClickableTextureComponent tab = __instance.tabButtons[___currentTab];
            if (tab.Filter == null)
            {
                tab.Filter = (ISalable _) => true;
            }

            __instance.forSale.Clear();
            foreach (ISalable item in __instance.itemPriceAndStock.Keys)
            {
                if (tab.Filter(item) && item.DisplayName.Contains(lastFilterString.Value, StringComparison.OrdinalIgnoreCase))
                {
                    __instance.forSale.Add(item);
                }
            }
            __instance.currentItemIndex = 0;

            if (__instance.forSale.Count > 0)
            {
                float percentage = (float)___scrollBarRunner.Height / (float)Math.Max(1, __instance.forSale.Count - 4 + 1);
                __instance.scrollBar.bounds.Y = (int)(percentage * (float)__instance.currentItemIndex + (float)__instance.upArrow.bounds.Bottom + 4f);
                if (__instance.currentItemIndex == __instance.forSale.Count - 4)
                {
                    __instance.scrollBar.bounds.Y = __instance.downArrow.bounds.Y - __instance.scrollBar.bounds.Height - 4;
                }
            }

            __instance.updateSaleButtonNeighbors();
        }

        public static void ShopMenu_draw_Postfix(SpriteBatch b, bool __state)
        {
            Game1.options.showClearBackgrounds = __state;
        }
    }
}
