namespace Oracle.Models;

public enum ReplayPotionQuality { PreferHq, HqOnly, NormalOnly }

public sealed class ReplayPotionSettings
{
    // Importing a log never opts the player into consuming inventory items.
    public bool Enabled { get; set; }
    public uint ItemId { get; set; }
    public ReplayPotionQuality Quality { get; set; }
    public ReplayPotionSettings Copy() => new() { Enabled = Enabled, ItemId = ItemId, Quality = Quality };
}
