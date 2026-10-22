public enum TradeStatus
{
    Pending,
    Accepted,
    Rejected,
    Countered,
    Cancelled,
    Completed
}

public class TradeRequest
{
    public int Id { get; set; }
    public int TargetProductId { get; set; }
    public string TargetProductTitle { get; set; } = "";
    public decimal TargetProductPrice { get; set; }
    public int OfferedProductId { get; set; }
    public string OfferedProductTitle { get; set; } = "";
    public decimal OfferedProductPrice { get; set; }
    public int FromUserId { get; set; }
    public string FromUserName { get; set; } = "";
    public int OwnerId { get; set; }
    public decimal ExtraCash { get; set; }
    public string? Message { get; set; }
    public TradeStatus Status { get; set; } = TradeStatus.Pending;
    public decimal? CounterCash { get; set; }
    public string? CounterMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class TradeService
{
    private readonly ProductService _products;
    private readonly AdoNetDbService _db;

    public TradeService(ProductService products, AdoNetDbService db)
    {
        _products = products;
        _db = db;
    }

    public List<TradeRequest> Incoming(int userId) => _db.GetIncomingTrades(userId);

    public List<TradeRequest> Sent(int userId) => _db.GetSentTrades(userId);

    public List<TradeRequest> CompletedTrades(int userId) => _db.GetCompletedTrades(userId);

    public List<TradeRequest> GetAllForAdmin() => _db.GetAllTradesForAdmin();

    public int GetPendingCount(int userId) => _db.GetPendingTradesCountForUser(userId);

    public TradeRequest? GetById(int tradeId) => _db.GetTradeRequestById(tradeId);

    public List<Product> MyTradeableListings(int userId) =>
        _products.ForSeller(userId)
            .Where(product => product.Status == ProductStatus.Active)
            .ToList();

    public (bool Ok, string? Error, int Id) Submit(int targetId, int offeredId, int userId, string userName, decimal extraCash, string? message)
    {
        if (!_products.UserCanTrade(targetId, userId))
            return (false, "You can't request a trade for this listing.", 0);

        var offered = _products.GetProductById(offeredId);
        if (offered == null || offered.SellerId != userId)
            return (false, "Pick one of your own active listings.", 0);
        if (offered.Status != ProductStatus.Active)
            return (false, "That listing is sold or inactive, so it can't be offered.", 0);
        if (offered.Id == targetId)
            return (false, "You can't trade a listing with itself.", 0);

        var target = _products.GetProductById(targetId);
        if (target == null)
            return (false, "Target listing not found.", 0);
        if (target.SellerId == offered.SellerId)
            return (false, "You can't trade your own item with yourself.", 0);
        if (extraCash < 0 || extraCash != decimal.Round(extraCash))
            return (false, "Extra cash must be a whole rupee amount, or zero.", 0);
        if (message?.Length > 400)
            return (false, "Keep the message under 400 characters.", 0);

        if (_db.HasOpenTrade(targetId, offeredId))
            return (false, "An open trade proposal already exists between these items.", 0);

        var now = DateTime.UtcNow;
        var trade = new TradeRequest
        {
            TargetProductId = targetId,
            TargetProductTitle = target.Title,
            TargetProductPrice = target.Price,
            OfferedProductId = offeredId,
            OfferedProductTitle = offered.Title,
            OfferedProductPrice = offered.Price,
            FromUserId = userId,
            FromUserName = string.IsNullOrWhiteSpace(userName) ? "Trader" : userName.Trim(),
            OwnerId = target.SellerId,
            ExtraCash = extraCash,
            Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
            Status = TradeStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        var id = _db.AddTradeRequest(trade);
        return (true, null, id);
    }

    public (bool Ok, string? Error) Accept(int tradeId, int userId)
    {
        var trade = _db.GetTradeRequestById(tradeId);
        if (trade == null)
            return (false, "Trade request not found.");

        var target = _products.GetProductById(trade.TargetProductId);
        var offered = _products.GetProductById(trade.OfferedProductId);
        if (target == null || offered == null)
            return (false, "One of the listings in this trade could not be found.");

        var isOwner = target.SellerId == userId;
        var isProposer = trade.FromUserId == userId;

        if (trade.Status == TradeStatus.Pending && isOwner)
            return Complete(trade);

        if (trade.Status == TradeStatus.Countered && isProposer)
            return Complete(trade);

        return (false, "You are not authorized to accept this trade.");
    }

    public (bool Ok, string? Error) Reject(int tradeId, int userId)
    {
        var trade = _db.GetTradeRequestById(tradeId);
        if (trade == null)
            return (false, "Trade request not found.");

        var target = _products.GetProductById(trade.TargetProductId);
        if (target == null)
            return (false, "Listing not found.");

        if (trade.Status is not (TradeStatus.Pending or TradeStatus.Countered))
            return (false, "This trade request is already closed.");

        var isOwner = target.SellerId == userId;
        var isProposer = trade.FromUserId == userId;
        if (!isOwner && !isProposer)
            return (false, "You are not authorized to reject this trade.");

        _db.UpdateTradeStatus(tradeId, TradeStatus.Rejected);
        return (true, null);
    }

    public (bool Ok, string? Error) Counter(int tradeId, int userId, decimal extraCash, string? counterMessage)
    {
        var trade = _db.GetTradeRequestById(tradeId);
        if (trade == null)
            return (false, "Trade request not found.");

        var target = _products.GetProductById(trade.TargetProductId);
        if (target == null || target.SellerId != userId)
            return (false, "Only the listing owner can counter this trade.");

        if (trade.Status != TradeStatus.Pending)
            return (false, "This trade is no longer awaiting a counter.");
        if (extraCash < 0 || extraCash != decimal.Round(extraCash))
            return (false, "Enter a whole rupee extra cash amount, or zero.");
        if (counterMessage?.Length > 400)
            return (false, "Keep counter message under 400 characters.");

        _db.CounterTrade(tradeId, extraCash, string.IsNullOrWhiteSpace(counterMessage) ? null : counterMessage.Trim());
        return (true, null);
    }

    public (bool Ok, string? Error) Cancel(int tradeId, int userId)
    {
        var trade = _db.GetTradeRequestById(tradeId);
        if (trade == null)
            return (false, "Trade request not found.");
        if (trade.FromUserId != userId)
            return (false, "Only the trade proposer can cancel this request.");
        if (trade.Status is not (TradeStatus.Pending or TradeStatus.Countered))
            return (false, "Only pending or countered trades can be cancelled.");

        _db.UpdateTradeStatus(tradeId, TradeStatus.Cancelled);
        return (true, null);
    }

    public void CloseForProduct(int productId, int? exceptId = null)
    {
        _db.CloseTradesForProduct(productId, exceptId);
    }

    private (bool Ok, string? Error) Complete(TradeRequest trade)
    {
        if (!_products.TryCompleteTrade(trade.TargetProductId, trade.OfferedProductId))
            return (false, "One of these listings is no longer available.");

        _db.UpdateTradeStatus(trade.Id, TradeStatus.Completed);
        _db.CloseTradesForProduct(trade.TargetProductId, trade.Id);
        _db.CloseTradesForProduct(trade.OfferedProductId, trade.Id);
        return (true, null);
    }
}
