using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;

namespace SearchableChests
{
    public class ModEntry : Mod
    {
        private static ModConfig? Config;
        private static IMonitor? sMonitor;

        /// <summary> The filter field (search bar) object. </summary>
        private static readonly PerScreen<TextBox> filterField = new();

        /// <summary> The string that was in the filter field the last time it was checked. </summary>
        private static readonly PerScreen<string> lastFilterString = new PerScreen<string>(() => "");

        public override void Entry(IModHelper helper)
        {
            sMonitor = Monitor;
            Config = Helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;

            Harmony harmony = new Harmony(ModManifest.UniqueID);

            harmony.Patch(
               original: AccessTools.Method(typeof(ItemGrabMenu), nameof(ItemGrabMenu.draw), new[] { typeof(SpriteBatch) }),
               postfix: new HarmonyMethod(typeof(ModEntry), nameof(ItemGrabMenu_draw_Postfix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(ItemGrabMenu), nameof(ItemGrabMenu.SetupBorderNeighbors)),
               prefix: new HarmonyMethod(typeof(ModEntry), nameof(ItemGrabMenu_SetupBorderNeighbors_Prefix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(ItemGrabMenu), nameof(ItemGrabMenu.receiveKeyPress)),
               prefix: new HarmonyMethod(typeof(ModEntry), nameof(ItemGrabMenu_receiveKeyPress_Prefix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(ItemGrabMenu), nameof(ItemGrabMenu.receiveLeftClick)),
               prefix: new HarmonyMethod(typeof(ModEntry), nameof(ItemGrabMenu_receiveLeftClick_Prefix))
            );

            harmony.Patch(
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

                filterField.Value.Selected = true;
                filterField.Value.Width = (int)(__instance.width * (Config.FilterWidth / 100f));
                filterField.Value.OnEnterPressed += sender => sender.Selected = false;
                filterField.Value.OnTabPressed += sender => sender.Selected = false;
            }

            filterField.Value.Text = "";
            filterField.Value.Width = (int)(__instance.width * (Config.FilterWidth / 100f));
        }

        public static void ItemGrabMenu_draw_Postfix(SpriteBatch b, ItemGrabMenu __instance)
        {
            if (filterField.Value.Text != lastFilterString.Value)
            {
                lastFilterString.Value = filterField.Value.Text;
            }

            filterField.Value.X = __instance.xPositionOnScreen + __instance.width / 2 - filterField.Value.Width / 2 + Config.FilterOffsetX;
            filterField.Value.Y = Math.Min(__instance.yPositionOnScreen + __instance.height - IClickableMenu.spaceToClearTopBorder, Game1.uiViewport.Height - 50) + Config.FilterOffsetY;
            filterField.Value.Draw(b);

            __instance.drawMouse(b);
        }
    }
}
