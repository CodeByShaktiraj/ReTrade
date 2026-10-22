public enum OfferStatus
{
    Pending,
    Accepted,
    Rejected,
    Countered,
    Cancelled
}

public class Offer
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductTitle { get; set; } = "";
    public int BuyerId { get; set; }
    public string BuyerName { get; set; } = "";
    public int SellerId { get; set; }
    public decimal Amount { get; set; }
    public string? Message { get; set; }
    public OfferStatus Status { get; set; } = OfferStatus.Pending;
    public decimal? CounterAmount { get; set; }
    public string? CounterMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class OfferService
{
    private readonly ProductService _products;
    private readonly AdoNetDbService _db;

    public OfferService(ProductService products, AdoNetDbService db)
    {
        _products = products;
        _db = db;
    }

    public List<Offer> ReceivedBy(int sellerId) => _db.GetOffersForSeller(sellerId);

    public List<Offer> SentBy(int buyerId) => _db.GetOffersForBuyer(buyerId);

    public Offer? GetById(int offerId) => _db.GetOfferById(offerId);

    public int GetPendingCount(int sellerId) => _db.GetPendingOffersCountForSeller(sellerId);

    public (bool Ok, string? Error, int Id) Submit(int productId, int buyerId, string buyerName, decimal amount, string? message)
    {
        if (!_products.UserCanMakeOffer(productId, buyerId))
            return (false, "You can't make an offer on this listing.", 0);
        if (amount < 1 || amount > 500_000 || amount != decimal.Round(amount))
            return (false, "Enter a whole rupee amount between ₹1 and ₹5,00,000.", 0);
        if (message?.Length > 400)
            return (false, "Keep the message under 400 characters.", 0);

        var product = _products.GetProductById(productId);
        if (product == null)
            return (false, "Listing not found.", 0);

        if (_db.HasOpenOffer(productId, buyerId))
            return (false, "You already have an open offer waiting on this listing.", 0);

        var now = DateTime.UtcNow;
        var offer = new Offer
        {
            ProductId = productId,
            ProductTitle = product.Title,
            BuyerId = buyerId,
            BuyerName = string.IsNullOrWhiteSpace(buyerName) ? "Buyer" : buyerName.Trim(),
            SellerId = product.SellerId,
            Amount = amount,
            Message = Clean(message),
            Status = OfferStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        var id = _db.AddOffer(offer);
        return (true, null, id);
    }

    public (bool Ok, string? Error) Accept(int offerId, int userId)
    {
        var offer = _db.GetOfferById(offerId);
        if (offer == null)
            return (false, "Offer not found.");

        var product = _products.GetProductById(offer.ProductId);
        if (product == null)
            return (false, "Listing not found.");

        var isSeller = product.SellerId == userId;
        var isBuyer = offer.BuyerId == userId;

        if (offer.Status == OfferStatus.Pending && isSeller)
        {
            _db.UpdateOfferStatus(offerId, OfferStatus.Accepted);
            return (true, null);
        }

        if (offer.Status == OfferStatus.Countered && isBuyer)
        {
            _db.UpdateOfferStatus(offerId, OfferStatus.Accepted);
            return (true, null);
        }

        return (false, "You are not authorized to accept this offer.");
    }

    public (bool Ok, string? Error) Reject(int offerId, int userId)
    {
        var offer = _db.GetOfferById(offerId);
        if (offer == null)
            return (false, "Offer not found.");

        var product = _products.GetProductById(offer.ProductId);
        if (product == null)
            return (false, "Listing not found.");

        if (offer.Status is not (OfferStatus.Pending or OfferStatus.Countered))
            return (false, "This offer is already closed.");

        var isSeller = product.SellerId == userId;
        var isBuyer = offer.BuyerId == userId;
        if (!isSeller && !isBuyer)
            return (false, "You are not authorized to reject this offer.");

        _db.UpdateOfferStatus(offerId, OfferStatus.Rejected);
        return (true, null);
    }

    public (bool Ok, string? Error) Counter(int offerId, int userId, decimal counterAmount, string? counterMessage)
    {
        var offer = _db.GetOfferById(offerId);
        if (offer == null)
            return (false, "Offer not found.");

        var product = _products.GetProductById(offer.ProductId);
        if (product == null || product.SellerId != userId)
            return (false, "Only the seller can counter this offer.");
        if (offer.Status != OfferStatus.Pending)
            return (false, "This offer is no longer awaiting a counter.");
        if (counterAmount < 1 || counterAmount > 500_000 || counterAmount != decimal.Round(counterAmount))
            return (false, "Enter a whole rupee counter amount between ₹1 and ₹5,00,000.");
        if (counterMessage?.Length > 400)
            return (false, "Keep the message under 400 characters.");

        _db.CounterOffer(offerId, counterAmount, Clean(counterMessage));
        return (true, null);
    }

    public (bool Ok, string? Error) Cancel(int offerId, int userId)
    {
        var offer = _db.GetOfferById(offerId);
        if (offer == null)
            return (false, "Offer not found.");
        if (offer.BuyerId != userId)
            return (false, "Only the offer creator can cancel this offer.");
        if (offer.Status is not (OfferStatus.Pending or OfferStatus.Countered))
            return (false, "Only pending or countered offers can be cancelled.");

        _db.UpdateOfferStatus(offerId, OfferStatus.Cancelled);
        return (true, null);
    }

    public void CloseForProduct(int productId)
    {
        _db.CloseOffersForProduct(productId);
    }

    private static string? Clean(string? message) =>
        string.IsNullOrWhiteSpace(message) ? null : message.Trim();
}
