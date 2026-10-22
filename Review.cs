public class Review
{
    public int Id { get; set; }
    public int? OrderId { get; set; }
    public int ProductId { get; set; }
    public string ProductTitle { get; set; } = "";
    public int ReviewerId { get; set; }
    public string ReviewerName { get; set; } = "";
    public int RevieweeId { get; set; }
    public string RevieweeName { get; set; } = "";
    public int Rating { get; set; } // 1 - 5 stars
    public string Comment { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
