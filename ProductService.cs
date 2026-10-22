public sealed record ListingQuery
{
    public string? Text { get; init; }
    public string? Category { get; init; }
    public string? City { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public ProductCondition? Condition { get; init; }
    public bool TradeOnly { get; init; }
    public bool NegotiableOnly { get; init; }
}

public class ProductService
{
    private readonly AdoNetDbService _db;

    public ProductService(AdoNetDbService db)
    {
        _db = db;
    }

    public Product? GetProductById(int id) => _db.GetProductById(id);

    public List<Product> ForSeller(int sellerId) => _db.GetProductsForSeller(sellerId);

    public List<Product> Recent(int take) => _db.GetRecentActiveProducts(take);

    public List<Product> TradeOpportunities(int take) => _db.GetTradeOpportunities(take);

    public int CountInCategory(string category) => _db.GetCountInCategory(category);

    public List<string> Cities() => _db.GetDistinctCities();

    public List<Product> Search(ListingQuery query) => _db.SearchProducts(query);

    public (bool Ok, string? Error, int Id) Add(int userId, Product product)
    {
        var error = Validate(product);
        if (error != null)
            return (false, error, 0);

        product.SellerId = userId;
        product.Status = ProductStatus.Active;
        product.IsSeed = false;
        product.CreatedAt = DateTime.UtcNow;
        ApplyText(product);

        var id = _db.AddProduct(product);
        return (true, null, id);
    }

    public (bool Ok, string? Error) Update(int id, int userId, Product input)
    {
        var existing = GetProductById(id);
        if (existing == null)
            return (false, "Listing not found.");
        if (existing.SellerId != userId)
            return (false, "Only the listing owner can edit this listing.");
        if (existing.Status is ProductStatus.Sold or ProductStatus.Traded)
            return (false, "Closed listings can't be edited.");

        var error = Validate(input);
        if (error != null)
            return (false, error);

        existing.Title = input.Title;
        existing.Description = input.Description;
        existing.Price = input.Price;
        existing.Condition = input.Condition;
        existing.Category = input.Category;
        existing.City = input.City;
        existing.State = input.State;
        existing.Pincode = input.Pincode;
        existing.ImageUrl = input.ImageUrl;
        existing.AdditionalImages = input.AdditionalImages;
        existing.IsNegotiable = input.IsNegotiable;
        existing.TradeAvailable = input.TradeAvailable;
        ApplyText(existing);

        _db.UpdateProduct(existing);
        return (true, null);
    }

    public (bool Ok, string? Error) Delete(int id, int userId, bool isAdmin = false)
    {
        var existing = GetProductById(id);
        if (existing == null)
            return (false, "Listing not found.");
        if (existing.SellerId != userId && !isAdmin)
            return (false, "Only the listing owner or an administrator can delete this listing.");

        _db.DeleteProduct(id);
        return (true, null);
    }

    public List<Product> Related(int id, string category, int take = 4) => _db.GetRelatedProducts(id, category, take);

    public List<Product> AllForAdmin() => _db.GetAllProductsForAdmin();

    public int TotalCount() => _db.GetTotalProductsCount();

    public int ActiveCount() => _db.GetActiveProductsCount();

    public (bool Ok, string? Error) SetStatus(int id, int userId, ProductStatus status, bool isAdmin = false)
    {
        var product = GetProductById(id);
        if (product == null)
            return (false, "Listing not found.");
        if (product.SellerId != userId && !isAdmin)
            return (false, "Only the listing owner or an administrator can change this listing.");
        if (!isAdmin && product.Status is ProductStatus.Sold or ProductStatus.Traded)
            return (false, "This listing is already closed.");

        _db.UpdateProductStatus(id, status);
        return (true, null);
    }

    public bool TryCompleteSale(int productId)
    {
        return _db.MarkProductAsSold(productId);
    }

    public bool TryCompleteTrade(int firstId, int secondId)
    {
        return _db.CompleteTradeProducts(firstId, secondId);
    }

    public bool UserCanPurchase(int productId, int userId)
    {
        var product = GetProductById(productId);
        if (product == null || product.Status != ProductStatus.Active)
            return false;
        return product.SellerId != userId;
    }

    public bool UserCanMakeOffer(int productId, int userId)
    {
        var product = GetProductById(productId);
        if (product == null || product.Status != ProductStatus.Active || !product.IsNegotiable)
            return false;
        return product.SellerId != userId;
    }

    public bool UserCanTrade(int productId, int userId)
    {
        var product = GetProductById(productId);
        if (product == null || product.Status != ProductStatus.Active || !product.TradeAvailable)
            return false;
        return product.SellerId != userId;
    }

    private static string? Validate(Product product)
    {
        if (string.IsNullOrWhiteSpace(product.Title) || product.Title.Trim().Length < 3)
            return "Add a title.";
        if (string.IsNullOrWhiteSpace(product.Description) || product.Description.Trim().Length < 10)
            return "Describe the item in a sentence or two.";
        if (product.Price < 1 || product.Price > 500_000 || product.Price != decimal.Round(product.Price))
            return "Enter a whole price in rupees, up to ₹5,00,000.";
        if (string.IsNullOrWhiteSpace(product.Category) || !Catalog.Categories.Contains(product.Category))
            return "Choose a category.";
        if (string.IsNullOrWhiteSpace(product.City))
            return "Add a city.";
        if (string.IsNullOrWhiteSpace(product.State))
            return "Add a state.";
        if (!string.IsNullOrWhiteSpace(product.Pincode) && product.Pincode.Trim().Length != 6)
            return "Pincode must be 6 digits, or leave it blank.";
        if (!string.IsNullOrWhiteSpace(product.Pincode) && product.Pincode.Trim().Any(ch => !char.IsDigit(ch)))
            return "Pincode must be 6 digits, or leave it blank.";

        return null;
    }

    private static void ApplyText(Product product)
    {
        product.Title = product.Title.Trim();
        product.Description = product.Description.Trim();
        product.Category = product.Category.Trim();
        product.City = product.City.Trim();
        product.State = product.State.Trim();
        product.Pincode = string.IsNullOrWhiteSpace(product.Pincode) ? null : product.Pincode.Trim();
    }
}
