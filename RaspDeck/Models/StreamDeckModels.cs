using System.Collections.Generic;

namespace AnyDeck.Models
{
    public class DeckProfile
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString();
        public string Name { get; set; } = "New Profile";
        public int Rows { get; set; } = 3;
        public int Columns { get; set; } = 4;
        public bool IsDefault { get; set; } = false;
        public List<DeckButton> Buttons { get; set; } = new List<DeckButton>();
    }

    public class DeckButton
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString();
        public int Row { get; set; }
        public int Column { get; set; }
        public string? Label { get; set; }
        public string? IconBase64 { get; set; }
        public string? IconName { get; set; } // Material icon name
        public string? BackgroundColor { get; set; }
        public string? ActiveColor { get; set; } // Cor quando o botão está "ativo" (toggle: mute/discord ligado)
        public string? DynamicType { get; set; }
        public DeckAction? Action { get; set; }
        // Future: LongPressAction
    }

    public class DeckAction
    {
        public string Type { get; set; } = "none"; // launchApp, media, mixer, hotkey, etc
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    }
}
