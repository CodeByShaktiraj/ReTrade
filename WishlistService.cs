public class WishlistService
{
    private readonly AdoNetDbService _db;

    public WishlistService(AdoNetDbService db)
    {
        _db = db;
    }

    public bool Toggle(int userId, int productId)
    {
        if (_db.IsWishlisted(userId, productId))
        {
            _db.RemoveFromWishlist(userId, productId);
            return false;
        }
        else
        {
            _db.AddToWishlist(userId, productId);
            return true;
        }
    }

    public bool IsWishlisted(int userId, int productId) => _db.IsWishlisted(userId, productId);

    public HashSet<int> GetUserWishlistIds(int userId) => _db.GetWishlistProductIds(userId);

    public List<Product> GetUserWishlist(int userId) => _db.GetWishlistProducts(userId);

    public bool Remove(int userId, int productId) => _db.RemoveFromWishlist(userId, productId);
}
