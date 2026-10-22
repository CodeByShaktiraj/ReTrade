public enum ProductCondition
{
    New,
    LikeNew,
    Good,
    Fair,
    Poor
}

public enum ProductStatus
{
    Active,
    Sold,
    Inactive,
    Traded
}

public class Product
{
    public int Id { get; set; }
    public int SellerId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
    public ProductCondition Condition { get; set; }
    public ProductStatus Status { get; set; } = ProductStatus.Active;
    public string Category { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string? Pincode { get; set; }
    public string? ImageUrl { get; set; }
    public string? AdditionalImages { get; set; }
    public bool IsNegotiable { get; set; } = true;
    public bool TradeAvailable { get; set; } = false;
    public bool IsSeed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<string> AllImages
    {
        get
        {
            var list = new List<string>();
            if (!string.IsNullOrWhiteSpace(ImageUrl))
                list.Add(ImageUrl.Trim());
            if (!string.IsNullOrWhiteSpace(AdditionalImages))
            {
                var extra = AdditionalImages.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var img in extra)
                {
                    if (!string.IsNullOrWhiteSpace(img) && !list.Contains(img))
                        list.Add(img);
                }
            }
            return list;
        }
    }
}

public static class Catalog
{
    public static readonly string[] Categories =
    [
        "Electronics",
        "Gaming",
        "Books",
        "Furniture",
        "Sports",
        "Mobiles",
        "Laptops",
        "Home Appliances",
        "Fashion",
        "Other"
    ];
}