using StardewModdingAPI;

namespace SearchableChests
{
    public class ModConfig
    {
        public int FilterOffsetX { get; set; } = 0;
        public int FilterOffsetY { get; set; } = 0;
        public int FilterWidth { get; set; } = 80;
        public bool FilterAutoFocus { get; set; } = true;
        public SButton FilterFocusKeybind { get; set; } = SButton.Space;
    }
}