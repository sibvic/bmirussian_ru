namespace BMIRussian_ru.Services;

public class ShopOptions
{
    public const string SectionName = "Shop";

    /// <summary>Max length of the engraving text.</summary>
    public int MaxTextLength { get; set; } = 20;
    /// <summary>A queued job older than this many minutes is treated as abandoned.</summary>
    public int StaleTimeoutMinutes { get; set; } = 30;
    /// <summary>Font used to render the engraving text PNG.</summary>
    public string FontPath { get; set; } = "Assets/Fonts/Danger.ttf";
    /// <summary>Font size in pixels for the engraving text.</summary>
    public float FontSize { get; set; } = 96;
}
