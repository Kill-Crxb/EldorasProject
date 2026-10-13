/// <summary>
/// What an item costs at a vendor and what they pay for it. Pure and flat (Economy_Design.md §5):
/// value is baseValue until composition lands; likes and dislikes slot in here later.
/// </summary>
public static class Valuation
{
    public static int Value(ItemDefinition item) => item != null ? item.baseValue : 0;

    public static bool IsFree(VendorDefinition vendor) => string.IsNullOrEmpty(vendor.currencyItemId);

    /// <summary>What the player pays to take it off the shelf.</summary>
    public static int Price(ItemDefinition item, VendorDefinition vendor)
    {
        if (IsFree(vendor)) return 0;
        return Floor(Value(item) + vendor.margin);
    }

    /// <summary>What the vendor pays the player for it.</summary>
    public static int Offer(ItemDefinition item, VendorDefinition vendor)
    {
        if (IsFree(vendor)) return 0;
        return Floor(Value(item) - vendor.margin);
    }

    static int Floor(int amount) => amount < 1 ? 1 : amount;
}
