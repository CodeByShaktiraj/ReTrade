using System.Globalization;

public static class MarketFormat
{
    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");

    public static string Inr(decimal amount) => amount.ToString("C0", India);

    public static string Condition(ProductCondition condition) => condition switch
    {
        ProductCondition.LikeNew => "Like new",
        _ => condition.ToString()
    };

    public static string Status(ProductStatus status) => status switch
    {
        ProductStatus.Active => "Active",
        ProductStatus.Sold => "Sold",
        ProductStatus.Inactive => "Inactive",
        ProductStatus.Traded => "Traded",
        _ => status.ToString()
    };

    public static string Place(Product product) => $"{product.City}, {product.State}";

    public static string When(DateTime utc) =>
        utc.ToLocalTime().ToString("d MMM yyyy", India);

    public static string Payment(PaymentMethod method) => method switch
    {
        PaymentMethod.Mock => "Mock payment (no charge)",
        PaymentMethod.CashOnDelivery => "Cash on delivery",
        _ => method.ToString()
    };

    public static string OrderStatus(OrderStatus status) => status switch
    {
        global::OrderStatus.Confirmed => "Confirmed",
        global::OrderStatus.Processing => "Processing",
        global::OrderStatus.Shipped => "Shipped",
        global::OrderStatus.Delivered => "Delivered",
        global::OrderStatus.Cancelled => "Cancelled",
        _ => status.ToString()
    };

    public static string OfferStatus(OfferStatus status) => status switch
    {
        global::OfferStatus.Pending => "Pending Seller Review",
        global::OfferStatus.Accepted => "Accepted",
        global::OfferStatus.Rejected => "Declined",
        global::OfferStatus.Countered => "Counter Offer Received",
        global::OfferStatus.Cancelled => "Cancelled",
        _ => status.ToString()
    };

    public static string TradeStatus(TradeStatus status) => status switch
    {
        global::TradeStatus.Pending => "Pending Review",
        global::TradeStatus.Accepted => "Accepted",
        global::TradeStatus.Rejected => "Declined",
        global::TradeStatus.Countered => "Counter Proposed",
        global::TradeStatus.Cancelled => "Cancelled",
        global::TradeStatus.Completed => "Completed Exchange",
        _ => status.ToString()
    };

    public static string ReportStatus(ReportStatus status) => status switch
    {
        global::ReportStatus.Pending => "Under Review",
        global::ReportStatus.Resolved => "Resolved",
        global::ReportStatus.Dismissed => "Dismissed",
        _ => status.ToString()
    };
}
