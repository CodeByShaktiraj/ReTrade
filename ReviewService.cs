public class ReviewService
{
    private readonly AdoNetDbService _db;
    private readonly OrderService _orders;

    public ReviewService(AdoNetDbService db, OrderService orders)
    {
        _db = db;
        _orders = orders;
    }

    public (bool Ok, string? Error, int Id) SubmitReview(int orderId, int reviewerId, string reviewerName, int rating, string comment)
    {
        if (rating < 1 || rating > 5)
            return (false, "Rating must be between 1 and 5 stars.", 0);
        if (string.IsNullOrWhiteSpace(comment) || comment.Trim().Length < 5)
            return (false, "Review comment must be at least 5 characters.", 0);
        if (comment.Length > 1000)
            return (false, "Review comment must be under 1,000 characters.", 0);

        var order = _orders.GetById(orderId);
        if (order == null)
            return (false, "Order transaction not found.", 0);

        if (order.BuyerId != reviewerId)
            return (false, "Only the buyer who completed this purchase can submit a review.", 0);

        if (_db.HasReviewedOrder(orderId, reviewerId))
            return (false, "You have already submitted a review for this purchase.", 0);

        var seller = _db.GetUserById(order.SellerId);

        var review = new Review
        {
            OrderId = orderId,
            ProductId = order.ProductId,
            ProductTitle = order.ProductTitle,
            ReviewerId = reviewerId,
            ReviewerName = reviewerName.Trim(),
            RevieweeId = order.SellerId,
            RevieweeName = seller?.FullName ?? "Seller",
            Rating = rating,
            Comment = comment.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        var id = _db.AddReview(review);
        return (true, null, id);
    }

    public List<Review> GetReviewsForUser(int userId) => _db.GetReviewsForUser(userId);

    public List<Review> GetReviewsForProduct(int productId) => _db.GetReviewsForProduct(productId);

    public (double Average, int Count) GetSellerRating(int userId) => _db.GetAverageRatingForUser(userId);

    public bool HasReviewedOrder(int orderId, int userId) => _db.HasReviewedOrder(orderId, userId);
}
