public static class MarketplaceSeedData
{
    public static Product[] GetProducts() => [];

    private static Product Item(
        int id,
        int sellerId,
        string title,
        string description,
        decimal price,
        ProductCondition condition,
        string category,
        string city,
        string state,
        string? pincode,
        bool negotiable,
        bool trade,
        int daysAgo) => new()
    {
        Id = id,
        SellerId = sellerId,
        Title = title,
        Description = description,
        Price = price,
        Condition = condition,
        Category = category,
        City = city,
        State = state,
        Pincode = pincode,
        IsNegotiable = negotiable,
        TradeAvailable = trade,
        IsSeed = true,
        CreatedAt = DateTime.UtcNow.AddDays(daysAgo)
    };
}
