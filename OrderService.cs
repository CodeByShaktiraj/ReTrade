public enum PaymentMethod
{
    Mock,
    CashOnDelivery
}

public enum OrderStatus
{
    Confirmed,
    Processing,
    Shipped,
    Delivered,
    Cancelled
}

public class ShopOrder
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductTitle { get; set; } = "";
    public int BuyerId { get; set; }
    public int SellerId { get; set; }
    public string BuyerName { get; set; } = "";
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string Pincode { get; set; } = "";
    public string Phone { get; set; } = "";
    public PaymentMethod Payment { get; set; }
    public decimal Amount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Confirmed;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class OrderService
{
    private readonly ProductService _products;
    private readonly OfferService _offers;
    private readonly TradeService _trades;
    private readonly AdoNetDbService _db;

    public OrderService(ProductService products, OfferService offers, TradeService trades, AdoNetDbService db)
    {
        _products = products;
        _offers = offers;
        _trades = trades;
        _db = db;
    }

    public List<ShopOrder> ForBuyer(int userId) => _db.GetOrdersForBuyer(userId);

    public List<ShopOrder> ForSeller(int userId) => _db.GetOrdersForSeller(userId);

    public ShopOrder? GetById(int orderId) => _db.GetOrderById(orderId);

    public ShopOrder? GetOrderForProductAndBuyer(int productId, int buyerId) =>
        _db.GetOrdersForBuyer(buyerId).FirstOrDefault(o => o.ProductId == productId);

    public (bool Ok, string? Error, ShopOrder? Order) Place(
        int productId,
        int buyerId,
        string buyerName,
        string address,
        string city,
        string state,
        string pincode,
        string phone,
        PaymentMethod? payment)
    {
        if (!_products.UserCanPurchase(productId, buyerId))
            return (false, "This listing can't be purchased.", null);

        var product = _products.GetProductById(productId);
        if (product == null)
            return (false, "We couldn't load this listing.", null);

        var shippingError = ValidateShipping(buyerName, address, city, state, pincode, phone);
        if (shippingError != null)
            return (false, shippingError, null);
        if (payment is not PaymentMethod method)
            return (false, "Choose mock payment or cash on delivery.", null);

        if (!_products.TryCompleteSale(productId))
            return (false, "This listing was just sold or closed.", null);

        _offers.CloseForProduct(productId);
        _trades.CloseForProduct(productId);

        var order = new ShopOrder
        {
            ProductId = productId,
            ProductTitle = product.Title,
            BuyerId = buyerId,
            SellerId = product.SellerId,
            BuyerName = buyerName.Trim(),
            Address = address.Trim(),
            City = city.Trim(),
            State = state.Trim(),
            Pincode = pincode.Trim(),
            Phone = phone.Trim(),
            Payment = method,
            Amount = product.Price,
            Status = OrderStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };

        _db.AddOrder(order);
        return (true, null, order);
    }

    public bool UpdateStatus(int orderId, OrderStatus status) => _db.UpdateOrderStatus(orderId, status);

    public List<ShopOrder> GetAllForAdmin() => _db.GetAllOrders();

    public decimal GetTotalRevenue() => _db.GetTotalRevenue();

    private static string? ValidateShipping(string name, string address, string city, string state, string pincode, string phone)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Add the recipient name.";
        if (string.IsNullOrWhiteSpace(address) || address.Trim().Length < 5)
            return "Add a delivery address (at least 5 characters).";
        if (string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(state))
            return "Add a city and state.";
        if (pincode.Trim().Length != 6 || pincode.Trim().Any(ch => !char.IsDigit(ch)))
            return "Enter a 6-digit pincode.";
        if (phone.Trim().Length != 10 || phone.Trim().Any(ch => !char.IsDigit(ch)))
            return "Enter a 10-digit phone number.";
        return null;
    }
}
