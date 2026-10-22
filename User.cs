using System.Security.Cryptography;
using System.Text;

public enum UserRole
{
    User,
    Admin
}

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "User";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string? Phone { get; set; }
    public string? Bio { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public bool IsAdmin => string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);
}

public static class PasswordHelper
{
    public static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password + "_retrade_salt_2026");
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        var computed = HashPassword(password);
        return string.Equals(computed, storedHash, StringComparison.Ordinal);
    }
}
