using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using Nelknet.LibSQL.Data;

public class AdoNetDbService
{
    private string _connectionString;
    private bool _useTurso;

    public AdoNetDbService(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var tursoUrl = Environment.GetEnvironmentVariable("TURSO_DATABASE_URL")
            ?? configuration["Turso:Url"]
            ?? configuration["TursoUrl"];

        var tursoToken = Environment.GetEnvironmentVariable("TURSO_AUTH_TOKEN")
            ?? configuration["Turso:AuthToken"]
            ?? configuration["TursoAuthToken"];

        if (!string.IsNullOrWhiteSpace(tursoUrl) && !string.IsNullOrWhiteSpace(tursoToken))
        {
            _useTurso = true;
            var normalizedTursoUrl = tursoUrl.Trim();
            if (normalizedTursoUrl.StartsWith("libsql://", StringComparison.OrdinalIgnoreCase))
            {
                normalizedTursoUrl = "https://" + normalizedTursoUrl.Substring("libsql://".Length);
            }
            _connectionString = $"Data Source={normalizedTursoUrl};Auth Token={tursoToken.Trim()}";
            Console.WriteLine($"[DATABASE] Configured provider: Turso libSQL Cloud ({normalizedTursoUrl})");
        }
        else
        {
            _useTurso = false;
            var configured = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
                ?? Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTIONSTRING")
                ?? configuration.GetConnectionString("DefaultConnection") 
                ?? "Data Source=marketplace.db";

            var builder = new SqliteConnectionStringBuilder(configured);

            if (!Path.IsPathRooted(builder.DataSource))
            {
                builder.DataSource = Path.Combine(environment.ContentRootPath, builder.DataSource);
            }

            _connectionString = builder.ConnectionString;
            Console.WriteLine($"[DATABASE] Configured provider: Local SQLite ({_connectionString})");
        }

        try
        {
            InitializeDatabase();
        }
        catch (Exception ex) when (_useTurso)
        {
            Console.WriteLine($"[DATABASE WARNING] Could not connect to Turso cloud: {ex}. Falling back to local SQLite.");
            _useTurso = false;
            var localPath = Path.Combine(environment.ContentRootPath, "marketplace.db");
            _connectionString = $"Data Source={localPath}";
            InitializeDatabase();
        }
    }

    public string ConnectionString => _connectionString;
    public bool IsTurso => _useTurso;

    public DbConnection CreateConnection()
    {
        if (_useTurso)
        {
            return new LibSQLConnection(_connectionString);
        }
        return new SqliteConnection(_connectionString);
    }

    public void InitializeDatabase()
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Email TEXT UNIQUE NOT NULL,
                PasswordHash TEXT NOT NULL,
                FullName TEXT NOT NULL,
                Role TEXT NOT NULL DEFAULT 'User',
                City TEXT NOT NULL,
                State TEXT NOT NULL,
                Phone TEXT NULL,
                Bio TEXT NULL,
                CreatedAt TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1
            );

            CREATE TABLE IF NOT EXISTS Products (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SellerId INTEGER NOT NULL,
                Title TEXT NOT NULL,
                Description TEXT NOT NULL,
                Price REAL NOT NULL,
                Condition INTEGER NOT NULL,
                Status INTEGER NOT NULL,
                Category TEXT NOT NULL,
                City TEXT NOT NULL,
                State TEXT NOT NULL,
                Pincode TEXT NULL,
                ImageUrl TEXT NULL,
                AdditionalImages TEXT NULL,
                IsNegotiable INTEGER NOT NULL,
                TradeAvailable INTEGER NOT NULL,
                IsSeed INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Orders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProductId INTEGER NOT NULL,
                ProductTitle TEXT NOT NULL,
                BuyerId INTEGER NOT NULL,
                SellerId INTEGER NOT NULL,
                BuyerName TEXT NOT NULL,
                Address TEXT NOT NULL,
                City TEXT NOT NULL,
                State TEXT NOT NULL,
                Pincode TEXT NOT NULL,
                Phone TEXT NOT NULL,
                Payment INTEGER NOT NULL,
                Amount REAL NOT NULL,
                Status INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Offers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProductId INTEGER NOT NULL,
                ProductTitle TEXT NOT NULL,
                BuyerId INTEGER NOT NULL,
                BuyerName TEXT NOT NULL,
                SellerId INTEGER NOT NULL,
                Amount REAL NOT NULL,
                Message TEXT NULL,
                Status INTEGER NOT NULL,
                CounterAmount REAL NULL,
                CounterMessage TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS TradeRequests (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TargetProductId INTEGER NOT NULL,
                TargetProductTitle TEXT NOT NULL,
                TargetProductPrice REAL NOT NULL,
                OfferedProductId INTEGER NOT NULL,
                OfferedProductTitle TEXT NOT NULL,
                OfferedProductPrice REAL NOT NULL,
                FromUserId INTEGER NOT NULL,
                FromUserName TEXT NOT NULL,
                OwnerId INTEGER NOT NULL,
                ExtraCash REAL NOT NULL,
                Message TEXT NULL,
                Status INTEGER NOT NULL,
                CounterCash REAL NULL,
                CounterMessage TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Wishlists (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId INTEGER NOT NULL,
                ProductId INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UNIQUE(UserId, ProductId)
            );

            CREATE TABLE IF NOT EXISTS Reviews (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OrderId INTEGER NULL,
                ProductId INTEGER NOT NULL,
                ProductTitle TEXT NOT NULL,
                ReviewerId INTEGER NOT NULL,
                ReviewerName TEXT NOT NULL,
                RevieweeId INTEGER NOT NULL,
                Rating INTEGER NOT NULL,
                Comment TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Reports (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ReporterId INTEGER NOT NULL,
                ReporterName TEXT NOT NULL,
                ReportedProductId INTEGER NULL,
                ReportedProductTitle TEXT NULL,
                ReportedUserId INTEGER NULL,
                ReportedUserName TEXT NULL,
                Reason TEXT NOT NULL,
                Description TEXT NULL,
                Status INTEGER NOT NULL DEFAULT 0,
                AdminNotes TEXT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Categories (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT UNIQUE NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS UserSessions (
                Id TEXT PRIMARY KEY,
                UserId INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                LastAccessedAt TEXT NOT NULL,
                ExpiresAt TEXT NOT NULL,
                IsRevoked INTEGER NOT NULL DEFAULT 0,
                UserAgent TEXT NULL,
                IpAddress TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS ExternalLogins (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId INTEGER NOT NULL,
                Provider TEXT NOT NULL,
                ProviderKey TEXT NOT NULL,
                ProviderDisplayName TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UNIQUE(Provider, ProviderKey)
            );

            CREATE TABLE IF NOT EXISTS AuthExchangeCodes (
                Code TEXT PRIMARY KEY,
                UserId INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                ExpiresAt TEXT NOT NULL,
                IsUsed INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS idx_products_seller ON Products(SellerId);
            CREATE INDEX IF NOT EXISTS idx_products_status ON Products(Status);
            CREATE INDEX IF NOT EXISTS idx_products_category ON Products(Category);
            CREATE INDEX IF NOT EXISTS idx_orders_buyer ON Orders(BuyerId);
            CREATE INDEX IF NOT EXISTS idx_orders_seller ON Orders(SellerId);
            CREATE INDEX IF NOT EXISTS idx_offers_seller ON Offers(SellerId);
            CREATE INDEX IF NOT EXISTS idx_offers_buyer ON Offers(BuyerId);
            CREATE INDEX IF NOT EXISTS idx_trades_owner ON TradeRequests(OwnerId);
            CREATE INDEX IF NOT EXISTS idx_trades_fromuser ON TradeRequests(FromUserId);
            CREATE INDEX IF NOT EXISTS idx_reviews_reviewee ON Reviews(RevieweeId);
            CREATE INDEX IF NOT EXISTS idx_reviews_product ON Reviews(ProductId);
            CREATE INDEX IF NOT EXISTS idx_sessions_user ON UserSessions(UserId);
        ";
        command.ExecuteNonQuery();

        // Safely check and add columns to existing tables if needed
        TryAddColumn(connection, "Products", "AdditionalImages", "TEXT NULL");
        TryAddColumn(connection, "Orders", "Status", "INTEGER NOT NULL DEFAULT 0");

        // Seed Users if table is empty
        SeedUsersIfEmpty(connection);

        // Seed Categories if table is empty
        SeedCategoriesIfEmpty(connection);
    }

    private static void TryAddColumn(DbConnection connection, string table, string column, string typeDefinition)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {typeDefinition};";
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Column already exists, safe to ignore
        }
    }

    private static void SeedUsersIfEmpty(DbConnection connection)
    {
        using var countCmd = connection.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM Users WHERE LOWER(Email) = 'admin@retrade.com';";
        var count = Convert.ToInt64(countCmd.ExecuteScalar());
        if (count == 0)
        {
            var adminPasswordHash = PasswordHelper.HashPassword("Admin@123");
            var now = DateTime.UtcNow.ToString("o");

            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO Users (Email, PasswordHash, FullName, Role, City, State, Phone, Bio, CreatedAt, IsActive)
                VALUES 
                ('admin@retrade.com', @AdminHash, 'Admin User', 'Admin', 'Ahmedabad', 'Gujarat', '9876543210', 'System administration account for ReTrade moderation.', @Now, 1);
            ";
            insertCmd.Parameters.AddWithValue("@AdminHash", adminPasswordHash);
            insertCmd.Parameters.AddWithValue("@Now", now);
            insertCmd.ExecuteNonQuery();
        }
    }

    public void CleanFakeData()
    {
        using var connection = CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            // 1. Delete all fake and test reviews
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    DELETE FROM Reviews 
                    WHERE ProductTitle LIKE 'Test %' 
                       OR ReviewerName IN ('Neha Patel', 'Aarav Shah', 'Rohan Mehta') 
                       OR RevieweeId IN (1, 2, 3)
                       OR ProductId IN (SELECT Id FROM Products WHERE IsSeed = 1 OR Title LIKE 'Test %' OR Title LIKE 'Target Trade %' OR Title LIKE 'Offered Trade %');
                ";
                var count = cmd.ExecuteNonQuery();
                Console.WriteLine($"[CLEANUP] Deleted {count} fake/test reviews.");
            }

            // 2. Delete all fake / test / seed products
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    DELETE FROM Products 
                    WHERE IsSeed = 1 
                       OR Title LIKE 'Test %' 
                       OR Title LIKE 'Target Trade %' 
                       OR Title LIKE 'Offered Trade %' 
                       OR SellerId IN (1, 2, 3);
                ";
                var count = cmd.ExecuteNonQuery();
                Console.WriteLine($"[CLEANUP] Deleted {count} fake/test/seed products.");
            }

            // 3. Delete all orders, offers, trade requests, wishlists for fake products and fake users
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    DELETE FROM Orders WHERE ProductTitle LIKE 'Test %' OR BuyerId IN (1, 2, 3) OR SellerId IN (1, 2, 3);
                    DELETE FROM Offers WHERE ProductTitle LIKE 'Test %' OR BuyerId IN (1, 2, 3) OR SellerId IN (1, 2, 3);
                    DELETE FROM TradeRequests WHERE TargetProductTitle LIKE '%Trade%' OR OfferedProductTitle LIKE '%Trade%' OR FromUserId IN (1, 2, 3) OR OwnerId IN (1, 2, 3);
                    DELETE FROM Wishlists WHERE UserId IN (1, 2, 3);
                    DELETE FROM UserSessions WHERE UserId IN (1, 2, 3, 6);
                    DELETE FROM AuthExchangeCodes WHERE UserId IN (1, 2, 3, 6);
                    DELETE FROM ExternalLogins WHERE UserId IN (1, 2, 3, 6);
                ";
                cmd.ExecuteNonQuery();
            }

            // 4. Delete the fake / demo accounts (Aarav Shah, Neha Patel, Rohan Mehta, GoogleUser, and ephemeral test users)
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "DELETE FROM Users WHERE LOWER(Email) IN ('aarav@retrade.com', 'neha@retrade.com', 'rohan@retrade.com', 'demo.google@retrade.com') OR LOWER(Email) LIKE '%@retrade.test';";
                var count = cmd.ExecuteNonQuery();
                Console.WriteLine($"[CLEANUP] Deleted {count} fake/demo accounts.");
            }

            transaction.Commit();
            Console.WriteLine("[CLEANUP] Database successfully sanitized!");
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            Console.WriteLine($"[CLEANUP ERROR] Failed to clean fake data: {ex.Message}");
            throw;
        }
    }

    private static void SeedCategoriesIfEmpty(DbConnection connection)
    {
        using var countCmd = connection.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM Categories;";
        var count = Convert.ToInt64(countCmd.ExecuteScalar());
        if (count == 0)
        {
            var now = DateTime.UtcNow.ToString("o");
            foreach (var cat in Catalog.Categories)
            {
                using var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = "INSERT OR IGNORE INTO Categories (Name, CreatedAt) VALUES (@Name, @Now);";
                insertCmd.Parameters.AddWithValue("@Name", cat);
                insertCmd.Parameters.AddWithValue("@Now", now);
                insertCmd.ExecuteNonQuery();
            }
        }
    }

    private static void SeedProductsIfEmpty(DbConnection connection)
    {
        using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM Products;";
        var count = Convert.ToInt64(countCommand.ExecuteScalar());

        if (count == 0)
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                var seedProducts = MarketplaceSeedData.GetProducts();
                foreach (var product in seedProducts)
                {
                    using var insertCmd = connection.CreateCommand();
                    insertCmd.Transaction = transaction;
                    insertCmd.CommandText = @"
                        INSERT INTO Products (Id, SellerId, Title, Description, Price, Condition, Status, Category, City, State, Pincode, ImageUrl, AdditionalImages, IsNegotiable, TradeAvailable, IsSeed, CreatedAt)
                        VALUES (@Id, @SellerId, @Title, @Description, @Price, @Condition, @Status, @Category, @City, @State, @Pincode, @ImageUrl, @AdditionalImages, @IsNegotiable, @TradeAvailable, @IsSeed, @CreatedAt);
                    ";
                    insertCmd.Parameters.AddWithValue("@Id", product.Id);
                    insertCmd.Parameters.AddWithValue("@SellerId", product.SellerId);
                    insertCmd.Parameters.AddWithValue("@Title", product.Title);
                    insertCmd.Parameters.AddWithValue("@Description", product.Description);
                    insertCmd.Parameters.AddWithValue("@Price", Convert.ToDouble(product.Price));
                    insertCmd.Parameters.AddWithValue("@Condition", (int)product.Condition);
                    insertCmd.Parameters.AddWithValue("@Status", (int)product.Status);
                    insertCmd.Parameters.AddWithValue("@Category", product.Category);
                    insertCmd.Parameters.AddWithValue("@City", product.City);
                    insertCmd.Parameters.AddWithValue("@State", product.State);
                    insertCmd.Parameters.AddWithValue("@Pincode", (object?)product.Pincode ?? DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@ImageUrl", (object?)product.ImageUrl ?? DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@AdditionalImages", (object?)product.AdditionalImages ?? DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@IsNegotiable", product.IsNegotiable ? 1 : 0);
                    insertCmd.Parameters.AddWithValue("@TradeAvailable", product.TradeAvailable ? 1 : 0);
                    insertCmd.Parameters.AddWithValue("@IsSeed", product.IsSeed ? 1 : 0);
                    insertCmd.Parameters.AddWithValue("@CreatedAt", product.CreatedAt.ToString("o"));
                    insertCmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }

    #region User Methods

    public User? GetUserById(int id)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Email, PasswordHash, FullName, Role, City, State, Phone, Bio, CreatedAt, IsActive FROM Users WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadUser(reader) : null;
    }

    public User? GetUserByEmail(string email)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Email, PasswordHash, FullName, Role, City, State, Phone, Bio, CreatedAt, IsActive FROM Users WHERE LOWER(Email) = LOWER(@Email);";
        cmd.Parameters.AddWithValue("@Email", email.Trim());
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadUser(reader) : null;
    }

    public int AddUser(User user)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Users (Email, PasswordHash, FullName, Role, City, State, Phone, Bio, CreatedAt, IsActive)
            VALUES (@Email, @PasswordHash, @FullName, @Role, @City, @State, @Phone, @Bio, @CreatedAt, @IsActive)
            RETURNING Id;
        ";
        cmd.Parameters.AddWithValue("@Email", user.Email.Trim().ToLowerInvariant());
        cmd.Parameters.AddWithValue("@PasswordHash", user.PasswordHash);
        cmd.Parameters.AddWithValue("@FullName", user.FullName.Trim());
        cmd.Parameters.AddWithValue("@Role", user.Role);
        cmd.Parameters.AddWithValue("@City", user.City.Trim());
        cmd.Parameters.AddWithValue("@State", user.State.Trim());
        cmd.Parameters.AddWithValue("@Phone", (object?)user.Phone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Bio", (object?)user.Bio ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", user.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@IsActive", user.IsActive ? 1 : 0);

        var newId = Convert.ToInt32(cmd.ExecuteScalar());
        user.Id = newId;
        return newId;
    }

    public bool UpdateUser(User user)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE Users
            SET FullName = @FullName,
                City = @City,
                State = @State,
                Phone = @Phone,
                Bio = @Bio
            WHERE Id = @Id;
        ";
        cmd.Parameters.AddWithValue("@Id", user.Id);
        cmd.Parameters.AddWithValue("@FullName", user.FullName.Trim());
        cmd.Parameters.AddWithValue("@City", user.City.Trim());
        cmd.Parameters.AddWithValue("@State", user.State.Trim());
        cmd.Parameters.AddWithValue("@Phone", (object?)user.Phone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Bio", (object?)user.Bio ?? DBNull.Value);

        return cmd.ExecuteNonQuery() > 0;
    }

    public bool UpdateUserPassword(int userId, string newPasswordHash)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Users SET PasswordHash = @Hash WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", userId);
        cmd.Parameters.AddWithValue("@Hash", newPasswordHash);
        return cmd.ExecuteNonQuery() > 0;
    }

    public List<User> GetAllUsers()
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Email, PasswordHash, FullName, Role, City, State, Phone, Bio, CreatedAt, IsActive FROM Users ORDER BY datetime(CreatedAt) DESC;";
        using var reader = cmd.ExecuteReader();
        var list = new List<User>();
        while (reader.Read())
            list.Add(ReadUser(reader));
        return list;
    }

    public const string PrimaryAdminEmail = "admin@retrade.com";

    public bool IsProtectedAdmin(int userId)
    {
        var user = GetUserById(userId);
        return user != null && IsProtectedAdmin(user);
    }

    public static bool IsProtectedAdmin(User user)
    {
        return user.Email.Equals(PrimaryAdminEmail, StringComparison.OrdinalIgnoreCase);
    }

    public int GetActiveAdminCount()
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Users WHERE Role = 'Admin' AND IsActive = 1;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public bool SetUserActive(int userId, bool isActive)
    {
        if (!isActive)
        {
            var target = GetUserById(userId);
            if (target is null) return false;

            // 1. Primary admin account is permanently protected from deactivation
            if (IsProtectedAdmin(target))
                return false;

            // 2. The last active admin cannot be deactivated
            if (target.Role == "Admin" && GetActiveAdminCount() <= 1)
                return false;
        }

        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Users SET IsActive = @IsActive WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", userId);
        cmd.Parameters.AddWithValue("@IsActive", isActive ? 1 : 0);
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool SetUserRole(int userId, string role)
    {
        var target = GetUserById(userId);
        if (target is null) return false;

        // If demoting from Admin to another role
        if (target.Role == "Admin" && role != "Admin")
        {
            // 1. Primary admin can NEVER be demoted
            if (IsProtectedAdmin(target))
                return false;

            // 2. The last active admin cannot be demoted
            if (target.IsActive && GetActiveAdminCount() <= 1)
                return false;
        }

        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Users SET Role = @Role WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", userId);
        cmd.Parameters.AddWithValue("@Role", role);
        return cmd.ExecuteNonQuery() > 0;
    }

    public (bool Ok, string? Error) DeleteUser(int userId)
    {
        var target = GetUserById(userId);
        if (target is null)
            return (false, "User not found.");

        if (IsProtectedAdmin(target))
            return (false, "The primary administrator account (admin@retrade.com) is permanently protected and cannot be deleted.");

        if (target.Role == "Admin" && GetActiveAdminCount() <= 1)
            return (false, "Cannot delete the last active administrator.");

        using var connection = CreateConnection();
        connection.Open();
        using var trans = connection.BeginTransaction();
        try
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = trans;
                cmd.CommandText = @"
                    DELETE FROM UserSessions WHERE UserId = @Id;
                    DELETE FROM AuthExchangeCodes WHERE UserId = @Id;
                    DELETE FROM Wishlists WHERE UserId = @Id;
                    DELETE FROM ExternalLogins WHERE UserId = @Id;
                    DELETE FROM Users WHERE Id = @Id;
                ";
                cmd.Parameters.AddWithValue("@Id", userId);
                cmd.ExecuteNonQuery();
            }
            trans.Commit();
            return (true, null);
        }
        catch (Exception ex)
        {
            trans.Rollback();
            return (false, $"Failed to delete user: {ex.Message}");
        }
    }

    private static User ReadUser(DbDataReader reader)
    {
        return new User
        {
            Id = reader.GetInt32(0),
            Email = reader.GetString(1),
            PasswordHash = reader.GetString(2),
            FullName = reader.GetString(3),
            Role = reader.GetString(4),
            City = reader.GetString(5),
            State = reader.GetString(6),
            Phone = reader.IsDBNull(7) ? null : reader.GetString(7),
            Bio = reader.IsDBNull(8) ? null : reader.GetString(8),
            CreatedAt = DateTime.Parse(reader.GetString(9), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            IsActive = reader.GetInt32(10) == 1
        };
    }

    #endregion

    #region Product Methods

    public int AddProduct(Product product)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Products (SellerId, Title, Description, Price, Condition, Status, Category, City, State, Pincode, ImageUrl, AdditionalImages, IsNegotiable, TradeAvailable, IsSeed, CreatedAt)
            VALUES (@SellerId, @Title, @Description, @Price, @Condition, @Status, @Category, @City, @State, @Pincode, @ImageUrl, @AdditionalImages, @IsNegotiable, @TradeAvailable, @IsSeed, @CreatedAt)
            RETURNING Id;
        ";
        command.Parameters.AddWithValue("@SellerId", product.SellerId);
        command.Parameters.AddWithValue("@Title", product.Title);
        command.Parameters.AddWithValue("@Description", product.Description);
        command.Parameters.AddWithValue("@Price", Convert.ToDouble(product.Price));
        command.Parameters.AddWithValue("@Condition", (int)product.Condition);
        command.Parameters.AddWithValue("@Status", (int)product.Status);
        command.Parameters.AddWithValue("@Category", product.Category);
        command.Parameters.AddWithValue("@City", product.City);
        command.Parameters.AddWithValue("@State", product.State);
        command.Parameters.AddWithValue("@Pincode", (object?)product.Pincode ?? DBNull.Value);
        command.Parameters.AddWithValue("@ImageUrl", (object?)product.ImageUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("@AdditionalImages", (object?)product.AdditionalImages ?? DBNull.Value);
        command.Parameters.AddWithValue("@IsNegotiable", product.IsNegotiable ? 1 : 0);
        command.Parameters.AddWithValue("@TradeAvailable", product.TradeAvailable ? 1 : 0);
        command.Parameters.AddWithValue("@IsSeed", product.IsSeed ? 1 : 0);
        command.Parameters.AddWithValue("@CreatedAt", product.CreatedAt.ToString("o"));

        var newId = Convert.ToInt32(command.ExecuteScalar());
        product.Id = newId;
        return newId;
    }

    public Product? GetProductById(int id)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, SellerId, Title, Description, Price, Condition, Status, Category, City, State, Pincode, ImageUrl, AdditionalImages, IsNegotiable, TradeAvailable, IsSeed, CreatedAt
            FROM Products
            WHERE Id = @Id;
        ";
        command.Parameters.AddWithValue("@Id", id);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadProduct(reader) : null;
    }

    public List<Product> GetProductsForSeller(int sellerId)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, SellerId, Title, Description, Price, Condition, Status, Category, City, State, Pincode, ImageUrl, AdditionalImages, IsNegotiable, TradeAvailable, IsSeed, CreatedAt
            FROM Products
            WHERE SellerId = @SellerId
            ORDER BY datetime(CreatedAt) DESC;
        ";
        command.Parameters.AddWithValue("@SellerId", sellerId);

        using var reader = command.ExecuteReader();
        var list = new List<Product>();
        while (reader.Read())
            list.Add(ReadProduct(reader));
        return list;
    }

    public List<Product> GetRecentActiveProducts(int take)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, SellerId, Title, Description, Price, Condition, Status, Category, City, State, Pincode, ImageUrl, AdditionalImages, IsNegotiable, TradeAvailable, IsSeed, CreatedAt
            FROM Products
            WHERE Status = @Status
            ORDER BY datetime(CreatedAt) DESC
            LIMIT @Take;
        ";
        command.Parameters.AddWithValue("@Status", (int)ProductStatus.Active);
        command.Parameters.AddWithValue("@Take", take);

        using var reader = command.ExecuteReader();
        var list = new List<Product>();
        while (reader.Read())
            list.Add(ReadProduct(reader));
        return list;
    }

    public List<Product> GetTradeOpportunities(int take)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, SellerId, Title, Description, Price, Condition, Status, Category, City, State, Pincode, ImageUrl, AdditionalImages, IsNegotiable, TradeAvailable, IsSeed, CreatedAt
            FROM Products
            WHERE Status = @Status AND TradeAvailable = 1
            ORDER BY datetime(CreatedAt) DESC
            LIMIT @Take;
        ";
        command.Parameters.AddWithValue("@Status", (int)ProductStatus.Active);
        command.Parameters.AddWithValue("@Take", take);

        using var reader = command.ExecuteReader();
        var list = new List<Product>();
        while (reader.Read())
            list.Add(ReadProduct(reader));
        return list;
    }

    public List<Product> GetRelatedProducts(int currentId, string category, int take)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, SellerId, Title, Description, Price, Condition, Status, Category, City, State, Pincode, ImageUrl, AdditionalImages, IsNegotiable, TradeAvailable, IsSeed, CreatedAt
            FROM Products
            WHERE Id != @CurrentId AND Status = @Status AND LOWER(Category) = LOWER(@Category)
            ORDER BY datetime(CreatedAt) DESC
            LIMIT @Take;
        ";
        command.Parameters.AddWithValue("@CurrentId", currentId);
        command.Parameters.AddWithValue("@Status", (int)ProductStatus.Active);
        command.Parameters.AddWithValue("@Category", category);
        command.Parameters.AddWithValue("@Take", take);

        using var reader = command.ExecuteReader();
        var list = new List<Product>();
        while (reader.Read())
            list.Add(ReadProduct(reader));
        return list;
    }

    public int GetCountInCategory(string category)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*)
            FROM Products
            WHERE Status = @Status AND LOWER(Category) = LOWER(@Category);
        ";
        command.Parameters.AddWithValue("@Status", (int)ProductStatus.Active);
        command.Parameters.AddWithValue("@Category", category);

        return Convert.ToInt32(command.ExecuteScalar());
    }

    public List<string> GetDistinctCities()
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT DISTINCT City
            FROM Products
            WHERE City IS NOT NULL AND City != ''
            ORDER BY City ASC;
        ";

        using var reader = command.ExecuteReader();
        var list = new List<string>();
        while (reader.Read())
            list.Add(reader.GetString(0));
        return list;
    }

    public List<Product> SearchProducts(ListingQuery query)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        var sql = BuildSearchSql(query, command);
        command.CommandText = sql;

        using var reader = command.ExecuteReader();
        var list = new List<Product>();
        while (reader.Read())
            list.Add(ReadProduct(reader));
        return list;
    }

    public List<Product> GetAllProductsForAdmin()
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, SellerId, Title, Description, Price, Condition, Status, Category, City, State, Pincode, ImageUrl, AdditionalImages, IsNegotiable, TradeAvailable, IsSeed, CreatedAt
            FROM Products
            ORDER BY datetime(CreatedAt) DESC;
        ";

        using var reader = command.ExecuteReader();
        var list = new List<Product>();
        while (reader.Read())
            list.Add(ReadProduct(reader));
        return list;
    }

    public int GetActiveProductsCount()
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Products WHERE Status = @Status;";
        cmd.Parameters.AddWithValue("@Status", (int)ProductStatus.Active);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public int GetTotalProductsCount()
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Products;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public bool UpdateProduct(Product product)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Products
            SET Title = @Title,
                Description = @Description,
                Price = @Price,
                Condition = @Condition,
                Category = @Category,
                City = @City,
                State = @State,
                Pincode = @Pincode,
                ImageUrl = @ImageUrl,
                AdditionalImages = @AdditionalImages,
                IsNegotiable = @IsNegotiable,
                TradeAvailable = @TradeAvailable
            WHERE Id = @Id;
        ";
        command.Parameters.AddWithValue("@Id", product.Id);
        command.Parameters.AddWithValue("@Title", product.Title);
        command.Parameters.AddWithValue("@Description", product.Description);
        command.Parameters.AddWithValue("@Price", Convert.ToDouble(product.Price));
        command.Parameters.AddWithValue("@Condition", (int)product.Condition);
        command.Parameters.AddWithValue("@Category", product.Category);
        command.Parameters.AddWithValue("@City", product.City);
        command.Parameters.AddWithValue("@State", product.State);
        command.Parameters.AddWithValue("@Pincode", (object?)product.Pincode ?? DBNull.Value);
        command.Parameters.AddWithValue("@ImageUrl", (object?)product.ImageUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("@AdditionalImages", (object?)product.AdditionalImages ?? DBNull.Value);
        command.Parameters.AddWithValue("@IsNegotiable", product.IsNegotiable ? 1 : 0);
        command.Parameters.AddWithValue("@TradeAvailable", product.TradeAvailable ? 1 : 0);

        return command.ExecuteNonQuery() > 0;
    }

    public bool UpdateProductStatus(int id, ProductStatus status)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Products
            SET Status = @Status
            WHERE Id = @Id;
        ";
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@Status", (int)status);

        return command.ExecuteNonQuery() > 0;
    }

    public bool DeleteProduct(int id)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Products WHERE Id = @Id;";
        command.Parameters.AddWithValue("@Id", id);

        return command.ExecuteNonQuery() > 0;
    }

    public bool MarkProductAsSold(int id)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Products
            SET Status = @SoldStatus
            WHERE Id = @Id AND Status = @ActiveStatus;
        ";
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@SoldStatus", (int)ProductStatus.Sold);
        command.Parameters.AddWithValue("@ActiveStatus", (int)ProductStatus.Active);

        return command.ExecuteNonQuery() > 0;
    }

    public bool CompleteTradeProducts(int firstId, int secondId)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Products
            SET Status = @TradedStatus
            WHERE Id IN (@FirstId, @SecondId) AND Status = @ActiveStatus;
        ";
        command.Parameters.AddWithValue("@FirstId", firstId);
        command.Parameters.AddWithValue("@SecondId", secondId);
        command.Parameters.AddWithValue("@TradedStatus", (int)ProductStatus.Traded);
        command.Parameters.AddWithValue("@ActiveStatus", (int)ProductStatus.Active);

        return command.ExecuteNonQuery() >= 2;
    }

    private static string BuildSearchSql(ListingQuery query, DbCommand command)
    {
        var sql = new StringBuilder(@"
            SELECT Id, SellerId, Title, Description, Price, Condition, Status, Category, City, State, Pincode, ImageUrl, AdditionalImages, IsNegotiable, TradeAvailable, IsSeed, CreatedAt
            FROM Products
            WHERE Status = @Status
        ");
        command.Parameters.AddWithValue("@Status", (int)ProductStatus.Active);

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            sql.Append(@" AND (
                Title LIKE @Text OR 
                Description LIKE @Text OR 
                Category LIKE @Text OR 
                City LIKE @Text
            )");
            command.Parameters.AddWithValue("@Text", $"%{query.Text.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            sql.Append(" AND LOWER(Category) = LOWER(@Category)");
            command.Parameters.AddWithValue("@Category", query.Category.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            sql.Append(" AND LOWER(City) = LOWER(@City)");
            command.Parameters.AddWithValue("@City", query.City.Trim());
        }

        if (query.MinPrice is decimal min)
        {
            sql.Append(" AND Price >= @MinPrice");
            command.Parameters.AddWithValue("@MinPrice", Convert.ToDouble(min));
        }

        if (query.MaxPrice is decimal max)
        {
            sql.Append(" AND Price <= @MaxPrice");
            command.Parameters.AddWithValue("@MaxPrice", Convert.ToDouble(max));
        }

        if (query.Condition is ProductCondition condition)
        {
            sql.Append(" AND Condition = @Condition");
            command.Parameters.AddWithValue("@Condition", (int)condition);
        }

        if (query.TradeOnly)
        {
            sql.Append(" AND TradeAvailable = 1");
        }

        if (query.NegotiableOnly)
        {
            sql.Append(" AND IsNegotiable = 1");
        }

        sql.Append(" ORDER BY datetime(CreatedAt) DESC;");
        return sql.ToString();
    }

    private static Product ReadProduct(DbDataReader reader)
    {
        return new Product
        {
            Id = reader.GetInt32(0),
            SellerId = reader.GetInt32(1),
            Title = reader.GetString(2),
            Description = reader.GetString(3),
            Price = Convert.ToDecimal(reader.GetValue(4), CultureInfo.InvariantCulture),
            Condition = (ProductCondition)reader.GetInt32(5),
            Status = (ProductStatus)reader.GetInt32(6),
            Category = reader.GetString(7),
            City = reader.GetString(8),
            State = reader.GetString(9),
            Pincode = reader.IsDBNull(10) ? null : reader.GetString(10),
            ImageUrl = reader.IsDBNull(11) ? null : reader.GetString(11),
            AdditionalImages = reader.IsDBNull(12) ? null : reader.GetString(12),
            IsNegotiable = reader.GetInt32(13) == 1,
            TradeAvailable = reader.GetInt32(14) == 1,
            IsSeed = reader.GetInt32(15) == 1,
            CreatedAt = DateTime.Parse(reader.GetString(16), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        };
    }

    #endregion

    #region Order Methods

    public int AddOrder(ShopOrder order)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Orders (ProductId, ProductTitle, BuyerId, SellerId, BuyerName, Address, City, State, Pincode, Phone, Payment, Amount, Status, CreatedAt)
            VALUES (@ProductId, @ProductTitle, @BuyerId, @SellerId, @BuyerName, @Address, @City, @State, @Pincode, @Phone, @Payment, @Amount, @Status, @CreatedAt)
            RETURNING Id;
        ";
        command.Parameters.AddWithValue("@ProductId", order.ProductId);
        command.Parameters.AddWithValue("@ProductTitle", order.ProductTitle);
        command.Parameters.AddWithValue("@BuyerId", order.BuyerId);
        command.Parameters.AddWithValue("@SellerId", order.SellerId);
        command.Parameters.AddWithValue("@BuyerName", order.BuyerName);
        command.Parameters.AddWithValue("@Address", order.Address);
        command.Parameters.AddWithValue("@City", order.City);
        command.Parameters.AddWithValue("@State", order.State);
        command.Parameters.AddWithValue("@Pincode", order.Pincode);
        command.Parameters.AddWithValue("@Phone", order.Phone);
        command.Parameters.AddWithValue("@Payment", (int)order.Payment);
        command.Parameters.AddWithValue("@Amount", Convert.ToDouble(order.Amount));
        command.Parameters.AddWithValue("@Status", (int)order.Status);
        command.Parameters.AddWithValue("@CreatedAt", order.CreatedAt.ToString("o"));

        var newId = Convert.ToInt32(command.ExecuteScalar());
        order.Id = newId;
        return newId;
    }

    public List<ShopOrder> GetOrdersForBuyer(int buyerId)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, ProductId, ProductTitle, BuyerId, SellerId, BuyerName, Address, City, State, Pincode, Phone, Payment, Amount, Status, CreatedAt
            FROM Orders
            WHERE BuyerId = @BuyerId
            ORDER BY datetime(CreatedAt) DESC;
        ";
        command.Parameters.AddWithValue("@BuyerId", buyerId);

        using var reader = command.ExecuteReader();
        var list = new List<ShopOrder>();
        while (reader.Read())
            list.Add(ReadOrder(reader));
        return list;
    }

    public List<ShopOrder> GetOrdersForSeller(int sellerId)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, ProductId, ProductTitle, BuyerId, SellerId, BuyerName, Address, City, State, Pincode, Phone, Payment, Amount, Status, CreatedAt
            FROM Orders
            WHERE SellerId = @SellerId
            ORDER BY datetime(CreatedAt) DESC;
        ";
        command.Parameters.AddWithValue("@SellerId", sellerId);

        using var reader = command.ExecuteReader();
        var list = new List<ShopOrder>();
        while (reader.Read())
            list.Add(ReadOrder(reader));
        return list;
    }

    public ShopOrder? GetOrderById(int orderId)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, ProductId, ProductTitle, BuyerId, SellerId, BuyerName, Address, City, State, Pincode, Phone, Payment, Amount, Status, CreatedAt
            FROM Orders
            WHERE Id = @Id;
        ";
        command.Parameters.AddWithValue("@Id", orderId);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadOrder(reader) : null;
    }

    public List<ShopOrder> GetAllOrders()
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, ProductId, ProductTitle, BuyerId, SellerId, BuyerName, Address, City, State, Pincode, Phone, Payment, Amount, Status, CreatedAt
            FROM Orders
            ORDER BY datetime(CreatedAt) DESC;
        ";

        using var reader = command.ExecuteReader();
        var list = new List<ShopOrder>();
        while (reader.Read())
            list.Add(ReadOrder(reader));
        return list;
    }

    public bool UpdateOrderStatus(int orderId, OrderStatus status)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Orders SET Status = @Status WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", orderId);
        cmd.Parameters.AddWithValue("@Status", (int)status);
        return cmd.ExecuteNonQuery() > 0;
    }

    public decimal GetTotalRevenue()
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(SUM(Amount), 0) FROM Orders;";
        return Convert.ToDecimal(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static ShopOrder ReadOrder(DbDataReader reader)
    {
        return new ShopOrder
        {
            Id = reader.GetInt32(0),
            ProductId = reader.GetInt32(1),
            ProductTitle = reader.GetString(2),
            BuyerId = reader.GetInt32(3),
            SellerId = reader.GetInt32(4),
            BuyerName = reader.GetString(5),
            Address = reader.GetString(6),
            City = reader.GetString(7),
            State = reader.GetString(8),
            Pincode = reader.GetString(9),
            Phone = reader.GetString(10),
            Payment = (PaymentMethod)reader.GetInt32(11),
            Amount = Convert.ToDecimal(reader.GetValue(12), CultureInfo.InvariantCulture),
            Status = (OrderStatus)reader.GetInt32(13),
            CreatedAt = DateTime.Parse(reader.GetString(14), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        };
    }

    #endregion

    #region Offer Methods

    public int AddOffer(Offer offer)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Offers (ProductId, ProductTitle, BuyerId, BuyerName, SellerId, Amount, Message, Status, CounterAmount, CounterMessage, CreatedAt, UpdatedAt)
            VALUES (@ProductId, @ProductTitle, @BuyerId, @BuyerName, @SellerId, @Amount, @Message, @Status, @CounterAmount, @CounterMessage, @CreatedAt, @UpdatedAt)
            RETURNING Id;
        ";
        cmd.Parameters.AddWithValue("@ProductId", offer.ProductId);
        cmd.Parameters.AddWithValue("@ProductTitle", offer.ProductTitle);
        cmd.Parameters.AddWithValue("@BuyerId", offer.BuyerId);
        cmd.Parameters.AddWithValue("@BuyerName", offer.BuyerName);
        cmd.Parameters.AddWithValue("@SellerId", offer.SellerId);
        cmd.Parameters.AddWithValue("@Amount", Convert.ToDouble(offer.Amount));
        cmd.Parameters.AddWithValue("@Message", (object?)offer.Message ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Status", (int)offer.Status);
        cmd.Parameters.AddWithValue("@CounterAmount", (object?)offer.CounterAmount ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CounterMessage", (object?)offer.CounterMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", offer.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@UpdatedAt", offer.UpdatedAt.ToString("o"));

        var newId = Convert.ToInt32(cmd.ExecuteScalar());
        offer.Id = newId;
        return newId;
    }

    public Offer? GetOfferById(int id)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, ProductId, ProductTitle, BuyerId, BuyerName, SellerId, Amount, Message, Status, CounterAmount, CounterMessage, CreatedAt, UpdatedAt
            FROM Offers WHERE Id = @Id;
        ";
        cmd.Parameters.AddWithValue("@Id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadOffer(reader) : null;
    }

    public List<Offer> GetOffersForSeller(int sellerId)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, ProductId, ProductTitle, BuyerId, BuyerName, SellerId, Amount, Message, Status, CounterAmount, CounterMessage, CreatedAt, UpdatedAt
            FROM Offers WHERE SellerId = @SellerId ORDER BY datetime(UpdatedAt) DESC;
        ";
        cmd.Parameters.AddWithValue("@SellerId", sellerId);
        using var reader = cmd.ExecuteReader();
        var list = new List<Offer>();
        while (reader.Read())
            list.Add(ReadOffer(reader));
        return list;
    }

    public List<Offer> GetOffersForBuyer(int buyerId)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, ProductId, ProductTitle, BuyerId, BuyerName, SellerId, Amount, Message, Status, CounterAmount, CounterMessage, CreatedAt, UpdatedAt
            FROM Offers WHERE BuyerId = @BuyerId ORDER BY datetime(UpdatedAt) DESC;
        ";
        cmd.Parameters.AddWithValue("@BuyerId", buyerId);
        using var reader = cmd.ExecuteReader();
        var list = new List<Offer>();
        while (reader.Read())
            list.Add(ReadOffer(reader));
        return list;
    }

    public bool HasOpenOffer(int productId, int buyerId)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) FROM Offers 
            WHERE ProductId = @ProductId AND BuyerId = @BuyerId AND Status IN (0, 3);
        ";
        cmd.Parameters.AddWithValue("@ProductId", productId);
        cmd.Parameters.AddWithValue("@BuyerId", buyerId);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    public bool UpdateOfferStatus(int id, OfferStatus status)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE Offers 
            SET Status = @Status, UpdatedAt = @Now
            WHERE Id = @Id;
        ";
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Status", (int)status);
        cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow.ToString("o"));
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool CounterOffer(int id, decimal counterAmount, string? counterMessage)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE Offers 
            SET Status = @Status, 
                CounterAmount = @CounterAmount, 
                CounterMessage = @CounterMessage,
                UpdatedAt = @Now
            WHERE Id = @Id;
        ";
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Status", (int)OfferStatus.Countered);
        cmd.Parameters.AddWithValue("@CounterAmount", Convert.ToDouble(counterAmount));
        cmd.Parameters.AddWithValue("@CounterMessage", (object?)counterMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow.ToString("o"));
        return cmd.ExecuteNonQuery() > 0;
    }

    public void CloseOffersForProduct(int productId)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE Offers 
            SET Status = @Rejected, UpdatedAt = @Now
            WHERE ProductId = @ProductId AND Status IN (0, 3);
        ";
        cmd.Parameters.AddWithValue("@ProductId", productId);
        cmd.Parameters.AddWithValue("@Rejected", (int)OfferStatus.Rejected);
        cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public int GetPendingOffersCountForSeller(int sellerId)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Offers WHERE SellerId = @SellerId AND Status = 0;";
        cmd.Parameters.AddWithValue("@SellerId", sellerId);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static Offer ReadOffer(DbDataReader reader)
    {
        return new Offer
        {
            Id = reader.GetInt32(0),
            ProductId = reader.GetInt32(1),
            ProductTitle = reader.GetString(2),
            BuyerId = reader.GetInt32(3),
            BuyerName = reader.GetString(4),
            SellerId = reader.GetInt32(5),
            Amount = Convert.ToDecimal(reader.GetValue(6), CultureInfo.InvariantCulture),
            Message = reader.IsDBNull(7) ? null : reader.GetString(7),
            Status = (OfferStatus)reader.GetInt32(8),
            CounterAmount = reader.IsDBNull(9) ? null : Convert.ToDecimal(reader.GetValue(9), CultureInfo.InvariantCulture),
            CounterMessage = reader.IsDBNull(10) ? null : reader.GetString(10),
            CreatedAt = DateTime.Parse(reader.GetString(11), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            UpdatedAt = DateTime.Parse(reader.GetString(12), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        };
    }

    #endregion

    #region Trade Methods

    public int AddTradeRequest(TradeRequest trade)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO TradeRequests (TargetProductId, TargetProductTitle, TargetProductPrice, OfferedProductId, OfferedProductTitle, OfferedProductPrice, FromUserId, FromUserName, OwnerId, ExtraCash, Message, Status, CounterCash, CounterMessage, CreatedAt, UpdatedAt)
            VALUES (@TargetProductId, @TargetProductTitle, @TargetProductPrice, @OfferedProductId, @OfferedProductTitle, @OfferedProductPrice, @FromUserId, @FromUserName, @OwnerId, @ExtraCash, @Message, @Status, @CounterCash, @CounterMessage, @CreatedAt, @UpdatedAt)
            RETURNING Id;
        ";
        cmd.Parameters.AddWithValue("@TargetProductId", trade.TargetProductId);
        cmd.Parameters.AddWithValue("@TargetProductTitle", trade.TargetProductTitle);
        cmd.Parameters.AddWithValue("@TargetProductPrice", Convert.ToDouble(trade.TargetProductPrice));
        cmd.Parameters.AddWithValue("@OfferedProductId", trade.OfferedProductId);
        cmd.Parameters.AddWithValue("@OfferedProductTitle", trade.OfferedProductTitle);
        cmd.Parameters.AddWithValue("@OfferedProductPrice", Convert.ToDouble(trade.OfferedProductPrice));
        cmd.Parameters.AddWithValue("@FromUserId", trade.FromUserId);
        cmd.Parameters.AddWithValue("@FromUserName", trade.FromUserName);
        cmd.Parameters.AddWithValue("@OwnerId", trade.OwnerId);
        cmd.Parameters.AddWithValue("@ExtraCash", Convert.ToDouble(trade.ExtraCash));
        cmd.Parameters.AddWithValue("@Message", (object?)trade.Message ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Status", (int)trade.Status);
        cmd.Parameters.AddWithValue("@CounterCash", (object?)trade.CounterCash ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CounterMessage", (object?)trade.CounterMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", trade.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@UpdatedAt", trade.UpdatedAt.ToString("o"));

        var newId = Convert.ToInt32(cmd.ExecuteScalar());
        trade.Id = newId;
        return newId;
    }

    public TradeRequest? GetTradeRequestById(int id)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, TargetProductId, TargetProductTitle, TargetProductPrice, OfferedProductId, OfferedProductTitle, OfferedProductPrice, FromUserId, FromUserName, OwnerId, ExtraCash, Message, Status, CounterCash, CounterMessage, CreatedAt, UpdatedAt
            FROM TradeRequests WHERE Id = @Id;
        ";
        cmd.Parameters.AddWithValue("@Id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadTrade(reader) : null;
    }

    public List<TradeRequest> GetIncomingTrades(int userId)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, TargetProductId, TargetProductTitle, TargetProductPrice, OfferedProductId, OfferedProductTitle, OfferedProductPrice, FromUserId, FromUserName, OwnerId, ExtraCash, Message, Status, CounterCash, CounterMessage, CreatedAt, UpdatedAt
            FROM TradeRequests WHERE OwnerId = @UserId ORDER BY datetime(UpdatedAt) DESC;
        ";
        cmd.Parameters.AddWithValue("@UserId", userId);
        using var reader = cmd.ExecuteReader();
        var list = new List<TradeRequest>();
        while (reader.Read())
            list.Add(ReadTrade(reader));
        return list;
    }

    public List<TradeRequest> GetSentTrades(int userId)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, TargetProductId, TargetProductTitle, TargetProductPrice, OfferedProductId, OfferedProductTitle, OfferedProductPrice, FromUserId, FromUserName, OwnerId, ExtraCash, Message, Status, CounterCash, CounterMessage, CreatedAt, UpdatedAt
            FROM TradeRequests WHERE FromUserId = @UserId ORDER BY datetime(UpdatedAt) DESC;
        ";
        cmd.Parameters.AddWithValue("@UserId", userId);
        using var reader = cmd.ExecuteReader();
        var list = new List<TradeRequest>();
        while (reader.Read())
            list.Add(ReadTrade(reader));
        return list;
    }

    public List<TradeRequest> GetCompletedTrades(int userId)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, TargetProductId, TargetProductTitle, TargetProductPrice, OfferedProductId, OfferedProductTitle, OfferedProductPrice, FromUserId, FromUserName, OwnerId, ExtraCash, Message, Status, CounterCash, CounterMessage, CreatedAt, UpdatedAt
            FROM TradeRequests WHERE (OwnerId = @UserId OR FromUserId = @UserId) AND Status = @Completed
            ORDER BY datetime(UpdatedAt) DESC;
        ";
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@Completed", (int)TradeStatus.Completed);
        using var reader = cmd.ExecuteReader();
        var list = new List<TradeRequest>();
        while (reader.Read())
            list.Add(ReadTrade(reader));
        return list;
    }

    public bool HasOpenTrade(int targetId, int offeredId)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) FROM TradeRequests 
            WHERE (TargetProductId = @TargetId OR OfferedProductId = @OfferedId OR TargetProductId = @OfferedId OR OfferedProductId = @TargetId)
              AND Status IN (0, 3);
        ";
        cmd.Parameters.AddWithValue("@TargetId", targetId);
        cmd.Parameters.AddWithValue("@OfferedId", offeredId);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    public bool UpdateTradeStatus(int id, TradeStatus status)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE TradeRequests 
            SET Status = @Status, UpdatedAt = @Now
            WHERE Id = @Id;
        ";
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Status", (int)status);
        cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow.ToString("o"));
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool CounterTrade(int id, decimal counterCash, string? counterMessage)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE TradeRequests 
            SET Status = @Status, 
                CounterCash = @CounterCash, 
                CounterMessage = @CounterMessage,
                UpdatedAt = @Now
            WHERE Id = @Id;
        ";
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Status", (int)TradeStatus.Countered);
        cmd.Parameters.AddWithValue("@CounterCash", Convert.ToDouble(counterCash));
        cmd.Parameters.AddWithValue("@CounterMessage", (object?)counterMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow.ToString("o"));
        return cmd.ExecuteNonQuery() > 0;
    }

    public void CloseTradesForProduct(int productId, int? exceptId = null)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        var sql = "UPDATE TradeRequests SET Status = @Rejected, UpdatedAt = @Now WHERE (TargetProductId = @ProductId OR OfferedProductId = @ProductId) AND Status IN (0, 3)";
        if (exceptId.HasValue)
        {
            sql += " AND Id != @ExceptId";
            cmd.Parameters.AddWithValue("@ExceptId", exceptId.Value);
        }
        cmd.CommandText = sql + ";";
        cmd.Parameters.AddWithValue("@ProductId", productId);
        cmd.Parameters.AddWithValue("@Rejected", (int)TradeStatus.Rejected);
        cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public int GetPendingTradesCountForUser(int userId)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM TradeRequests WHERE OwnerId = @UserId AND Status = 0;";
        cmd.Parameters.AddWithValue("@UserId", userId);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public List<TradeRequest> GetAllTradesForAdmin()
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, TargetProductId, TargetProductTitle, TargetProductPrice, OfferedProductId, OfferedProductTitle, OfferedProductPrice, FromUserId, FromUserName, OwnerId, ExtraCash, Message, Status, CounterCash, CounterMessage, CreatedAt, UpdatedAt
            FROM TradeRequests ORDER BY datetime(UpdatedAt) DESC;
        ";
        using var reader = cmd.ExecuteReader();
        var list = new List<TradeRequest>();
        while (reader.Read())
            list.Add(ReadTrade(reader));
        return list;
    }

    private static TradeRequest ReadTrade(DbDataReader reader)
    {
        return new TradeRequest
        {
            Id = reader.GetInt32(0),
            TargetProductId = reader.GetInt32(1),
            TargetProductTitle = reader.GetString(2),
            TargetProductPrice = Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture),
            OfferedProductId = reader.GetInt32(4),
            OfferedProductTitle = reader.GetString(5),
            OfferedProductPrice = Convert.ToDecimal(reader.GetValue(6), CultureInfo.InvariantCulture),
            FromUserId = reader.GetInt32(7),
            FromUserName = reader.GetString(8),
            OwnerId = reader.GetInt32(9),
            ExtraCash = Convert.ToDecimal(reader.GetValue(10), CultureInfo.InvariantCulture),
            Message = reader.IsDBNull(11) ? null : reader.GetString(11),
            Status = (TradeStatus)reader.GetInt32(12),
            CounterCash = reader.IsDBNull(13) ? null : Convert.ToDecimal(reader.GetValue(13), CultureInfo.InvariantCulture),
            CounterMessage = reader.IsDBNull(14) ? null : reader.GetString(14),
            CreatedAt = DateTime.Parse(reader.GetString(15), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            UpdatedAt = DateTime.Parse(reader.GetString(16), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        };
    }

    #endregion

    #region Wishlist Methods

    public bool AddToWishlist(int userId, int productId)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO Wishlists (UserId, ProductId, CreatedAt) VALUES (@UserId, @ProductId, @CreatedAt);";
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@ProductId", productId);
        cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow.ToString("o"));
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool RemoveFromWishlist(int userId, int productId)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Wishlists WHERE UserId = @UserId AND ProductId = @ProductId;";
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@ProductId", productId);
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool IsWishlisted(int userId, int productId)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Wishlists WHERE UserId = @UserId AND ProductId = @ProductId;";
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@ProductId", productId);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    public HashSet<int> GetWishlistProductIds(int userId)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT ProductId FROM Wishlists WHERE UserId = @UserId;";
        cmd.Parameters.AddWithValue("@UserId", userId);
        using var reader = cmd.ExecuteReader();
        var set = new HashSet<int>();
        while (reader.Read())
            set.Add(reader.GetInt32(0));
        return set;
    }

    public List<Product> GetWishlistProducts(int userId)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT p.Id, p.SellerId, p.Title, p.Description, p.Price, p.Condition, p.Status, p.Category, p.City, p.State, p.Pincode, p.ImageUrl, p.AdditionalImages, p.IsNegotiable, p.TradeAvailable, p.IsSeed, p.CreatedAt
            FROM Products p
            INNER JOIN Wishlists w ON p.Id = w.ProductId
            WHERE w.UserId = @UserId
            ORDER BY datetime(w.CreatedAt) DESC;
        ";
        cmd.Parameters.AddWithValue("@UserId", userId);
        using var reader = cmd.ExecuteReader();
        var list = new List<Product>();
        while (reader.Read())
            list.Add(ReadProduct(reader));
        return list;
    }

    #endregion

    #region Review Methods

    public int AddReview(Review review)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Reviews (OrderId, ProductId, ProductTitle, ReviewerId, ReviewerName, RevieweeId, Rating, Comment, CreatedAt)
            VALUES (@OrderId, @ProductId, @ProductTitle, @ReviewerId, @ReviewerName, @RevieweeId, @Rating, @Comment, @CreatedAt)
            RETURNING Id;
        ";
        cmd.Parameters.AddWithValue("@OrderId", (object?)review.OrderId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ProductId", review.ProductId);
        cmd.Parameters.AddWithValue("@ProductTitle", review.ProductTitle);
        cmd.Parameters.AddWithValue("@ReviewerId", review.ReviewerId);
        cmd.Parameters.AddWithValue("@ReviewerName", review.ReviewerName);
        cmd.Parameters.AddWithValue("@RevieweeId", review.RevieweeId);
        cmd.Parameters.AddWithValue("@Rating", review.Rating);
        cmd.Parameters.AddWithValue("@Comment", review.Comment.Trim());
        cmd.Parameters.AddWithValue("@CreatedAt", review.CreatedAt.ToString("o"));

        var newId = Convert.ToInt32(cmd.ExecuteScalar());
        review.Id = newId;
        return newId;
    }

    public List<Review> GetReviewsForUser(int revieweeId)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, OrderId, ProductId, ProductTitle, ReviewerId, ReviewerName, RevieweeId, Rating, Comment, CreatedAt
            FROM Reviews WHERE RevieweeId = @RevieweeId ORDER BY datetime(CreatedAt) DESC;
        ";
        cmd.Parameters.AddWithValue("@RevieweeId", revieweeId);
        using var reader = cmd.ExecuteReader();
        var list = new List<Review>();
        while (reader.Read())
            list.Add(ReadReview(reader));
        return list;
    }

    public List<Review> GetReviewsForProduct(int productId)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, OrderId, ProductId, ProductTitle, ReviewerId, ReviewerName, RevieweeId, Rating, Comment, CreatedAt
            FROM Reviews WHERE ProductId = @ProductId ORDER BY datetime(CreatedAt) DESC;
        ";
        cmd.Parameters.AddWithValue("@ProductId", productId);
        using var reader = cmd.ExecuteReader();
        var list = new List<Review>();
        while (reader.Read())
            list.Add(ReadReview(reader));
        return list;
    }

    public (double Average, int Count) GetAverageRatingForUser(int revieweeId)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT AVG(Rating), COUNT(*) FROM Reviews WHERE RevieweeId = @RevieweeId;";
        cmd.Parameters.AddWithValue("@RevieweeId", revieweeId);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var count = reader.GetInt32(1);
            var avg = count > 0 && !reader.IsDBNull(0) ? reader.GetDouble(0) : 0.0;
            return (avg, count);
        }
        return (0.0, 0);
    }

    public bool HasReviewedOrder(int orderId, int reviewerId)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Reviews WHERE OrderId = @OrderId AND ReviewerId = @ReviewerId;";
        cmd.Parameters.AddWithValue("@OrderId", orderId);
        cmd.Parameters.AddWithValue("@ReviewerId", reviewerId);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static Review ReadReview(DbDataReader reader)
    {
        return new Review
        {
            Id = reader.GetInt32(0),
            OrderId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
            ProductId = reader.GetInt32(2),
            ProductTitle = reader.GetString(3),
            ReviewerId = reader.GetInt32(4),
            ReviewerName = reader.GetString(5),
            RevieweeId = reader.GetInt32(6),
            Rating = reader.GetInt32(7),
            Comment = reader.GetString(8),
            CreatedAt = DateTime.Parse(reader.GetString(9), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        };
    }

    #endregion

    #region Report Methods

    public int AddReport(Report report)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Reports (ReporterId, ReporterName, ReportedProductId, ReportedProductTitle, ReportedUserId, ReportedUserName, Reason, Description, Status, AdminNotes, CreatedAt)
            VALUES (@ReporterId, @ReporterName, @ReportedProductId, @ReportedProductTitle, @ReportedUserId, @ReportedUserName, @Reason, @Description, @Status, @AdminNotes, @CreatedAt)
            RETURNING Id;
        ";
        cmd.Parameters.AddWithValue("@ReporterId", report.ReporterId);
        cmd.Parameters.AddWithValue("@ReporterName", report.ReporterName);
        cmd.Parameters.AddWithValue("@ReportedProductId", (object?)report.ReportedProductId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ReportedProductTitle", (object?)report.ReportedProductTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ReportedUserId", (object?)report.ReportedUserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ReportedUserName", (object?)report.ReportedUserName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Reason", report.Reason);
        cmd.Parameters.AddWithValue("@Description", (object?)report.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Status", (int)report.Status);
        cmd.Parameters.AddWithValue("@AdminNotes", (object?)report.AdminNotes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", report.CreatedAt.ToString("o"));

        var newId = Convert.ToInt32(cmd.ExecuteScalar());
        report.Id = newId;
        return newId;
    }

    public List<Report> GetAllReports()
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, ReporterId, ReporterName, ReportedProductId, ReportedProductTitle, ReportedUserId, ReportedUserName, Reason, Description, Status, AdminNotes, CreatedAt
            FROM Reports ORDER BY datetime(CreatedAt) DESC;
        ";
        using var reader = cmd.ExecuteReader();
        var list = new List<Report>();
        while (reader.Read())
            list.Add(ReadReport(reader));
        return list;
    }

    public Report? GetReportById(int id)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, ReporterId, ReporterName, ReportedProductId, ReportedProductTitle, ReportedUserId, ReportedUserName, Reason, Description, Status, AdminNotes, CreatedAt
            FROM Reports WHERE Id = @Id;
        ";
        cmd.Parameters.AddWithValue("@Id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadReport(reader) : null;
    }

    public bool UpdateReportStatus(int id, ReportStatus status, string? adminNotes)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE Reports 
            SET Status = @Status, AdminNotes = @AdminNotes
            WHERE Id = @Id;
        ";
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Status", (int)status);
        cmd.Parameters.AddWithValue("@AdminNotes", (object?)adminNotes ?? DBNull.Value);
        return cmd.ExecuteNonQuery() > 0;
    }

    public int GetPendingReportsCount()
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Reports WHERE Status = 0;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static Report ReadReport(DbDataReader reader)
    {
        return new Report
        {
            Id = reader.GetInt32(0),
            ReporterId = reader.GetInt32(1),
            ReporterName = reader.GetString(2),
            ReportedProductId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
            ReportedProductTitle = reader.IsDBNull(4) ? null : reader.GetString(4),
            ReportedUserId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
            ReportedUserName = reader.IsDBNull(6) ? null : reader.GetString(6),
            Reason = reader.GetString(7),
            Description = reader.IsDBNull(8) ? null : reader.GetString(8),
            Status = (ReportStatus)reader.GetInt32(9),
            AdminNotes = reader.IsDBNull(10) ? null : reader.GetString(10),
            CreatedAt = DateTime.Parse(reader.GetString(11), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        };
    }

    #endregion

    #region Category Methods

    public List<Category> GetAllCategories()
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, CreatedAt FROM Categories ORDER BY Name ASC;";
        using var reader = cmd.ExecuteReader();
        var list = new List<Category>();
        while (reader.Read())
        {
            list.Add(new Category
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                CreatedAt = DateTime.Parse(reader.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            });
        }
        return list;
    }

    public bool AddCategory(string name)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO Categories (Name, CreatedAt) VALUES (@Name, @CreatedAt);";
        cmd.Parameters.AddWithValue("@Name", name.Trim());
        cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow.ToString("o"));
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool DeleteCategory(int id)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Categories WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    #endregion

    #region Session & External Login Methods

    public string CreateSession(int userId, TimeSpan? validity = null, string? userAgent = null, string? ipAddress = null)
    {
        using var connection = CreateConnection();
        connection.Open();

        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var expires = now.Add(validity ?? TimeSpan.FromDays(30));

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO UserSessions (Id, UserId, CreatedAt, LastAccessedAt, ExpiresAt, IsRevoked, UserAgent, IpAddress)
            VALUES (@Id, @UserId, @CreatedAt, @LastAccessedAt, @ExpiresAt, 0, @UserAgent, @IpAddress);
        ";
        cmd.Parameters.AddWithValue("@Id", token);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@CreatedAt", now.ToString("o"));
        cmd.Parameters.AddWithValue("@LastAccessedAt", now.ToString("o"));
        cmd.Parameters.AddWithValue("@ExpiresAt", expires.ToString("o"));
        cmd.Parameters.AddWithValue("@UserAgent", (object?)userAgent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IpAddress", (object?)ipAddress ?? DBNull.Value);
        cmd.ExecuteNonQuery();

        return token;
    }

    public User? ValidateSession(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        using var connection = CreateConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT s.ExpiresAt, s.IsRevoked,
                   u.Id, u.Email, u.PasswordHash, u.FullName, u.Role, u.City, u.State, u.Phone, u.Bio, u.CreatedAt, u.IsActive
            FROM UserSessions s
            JOIN Users u ON s.UserId = u.Id
            WHERE s.Id = @Token;
        ";
        cmd.Parameters.AddWithValue("@Token", token.Trim());

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        var expiresStr = reader.GetString(0);
        var isRevoked = Convert.ToInt32(reader.GetValue(1)) == 1;

        if (isRevoked) return null;

        if (DateTime.TryParse(expiresStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expires) && expires <= DateTime.UtcNow)
        {
            return null;
        }

        var isActive = Convert.ToInt32(reader.GetValue(12)) == 1;
        if (!isActive) return null;

        var user = new User
        {
            Id = Convert.ToInt32(reader.GetValue(2)),
            Email = reader.GetString(3),
            PasswordHash = reader.GetString(4),
            FullName = reader.GetString(5),
            Role = reader.GetString(6),
            City = reader.GetString(7),
            State = reader.GetString(8),
            Phone = reader.IsDBNull(9) ? null : reader.GetString(9),
            Bio = reader.IsDBNull(10) ? null : reader.GetString(10),
            CreatedAt = DateTime.Parse(reader.GetString(11), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            IsActive = true
        };
        reader.Close();

        try
        {
            using var touchCmd = connection.CreateCommand();
            touchCmd.CommandText = "UPDATE UserSessions SET LastAccessedAt = @Now WHERE Id = @Token;";
            touchCmd.Parameters.AddWithValue("@Now", DateTime.UtcNow.ToString("o"));
            touchCmd.Parameters.AddWithValue("@Token", token.Trim());
            touchCmd.ExecuteNonQuery();
        }
        catch
        {
        }

        return user;
    }

    public bool RevokeSession(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;

        using var connection = CreateConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE UserSessions SET IsRevoked = 1 WHERE Id = @Token;";
        cmd.Parameters.AddWithValue("@Token", token.Trim());
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool RevokeAllUserSessions(int userId)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE UserSessions SET IsRevoked = 1 WHERE UserId = @UserId;";
        cmd.Parameters.AddWithValue("@UserId", userId);
        return cmd.ExecuteNonQuery() > 0;
    }

    public string CreateAuthExchangeCode(int userId, TimeSpan? validity = null)
    {
        using var connection = CreateConnection();
        connection.Open();

        var code = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var expires = now.Add(validity ?? TimeSpan.FromMinutes(2));

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO AuthExchangeCodes (Code, UserId, CreatedAt, ExpiresAt, IsUsed)
            VALUES (@Code, @UserId, @CreatedAt, @ExpiresAt, 0);
        ";
        cmd.Parameters.AddWithValue("@Code", code);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@CreatedAt", now.ToString("o"));
        cmd.Parameters.AddWithValue("@ExpiresAt", expires.ToString("o"));
        cmd.ExecuteNonQuery();

        return code;
    }

    public User? ConsumeAuthExchangeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        using var connection = CreateConnection();
        connection.Open();

        int userId = 0;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT UserId, ExpiresAt, IsUsed FROM AuthExchangeCodes WHERE Code = @Code;";
            cmd.Parameters.AddWithValue("@Code", code.Trim());
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            var isUsed = Convert.ToInt32(reader.GetValue(2)) == 1;
            if (isUsed) return null;

            var expiresStr = reader.GetString(1);
            if (DateTime.TryParse(expiresStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expires) && expires <= DateTime.UtcNow)
            {
                return null;
            }

            userId = Convert.ToInt32(reader.GetValue(0));
        }

        using (var updateCmd = connection.CreateCommand())
        {
            updateCmd.CommandText = "UPDATE AuthExchangeCodes SET IsUsed = 1 WHERE Code = @Code;";
            updateCmd.Parameters.AddWithValue("@Code", code.Trim());
            updateCmd.ExecuteNonQuery();
        }

        return GetUserById(userId);
    }

    public User FindOrCreateExternalUser(string provider, string providerKey, string email, string name)
    {
        using var connection = CreateConnection();
        connection.Open();

        // 1. Check if external login is already linked
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT UserId FROM ExternalLogins WHERE Provider = @Provider AND ProviderKey = @ProviderKey;";
            cmd.Parameters.AddWithValue("@Provider", provider);
            cmd.Parameters.AddWithValue("@ProviderKey", providerKey);
            var obj = cmd.ExecuteScalar();
            if (obj != null && obj != DBNull.Value)
            {
                var existingUserId = Convert.ToInt32(obj);
                var existingUser = GetUserById(existingUserId);
                if (existingUser != null)
                {
                    return existingUser;
                }
            }
        }

        // 2. Check if a user with matching email already exists
        var userByEmail = !string.IsNullOrWhiteSpace(email) ? GetUserByEmail(email) : null;
        if (userByEmail != null)
        {
            LinkExternalLogin(userByEmail.Id, provider, providerKey, name);
            return userByEmail;
        }

        // 3. Create a new user
        var cleanEmail = string.IsNullOrWhiteSpace(email)
            ? $"{provider.ToLowerInvariant()}_{providerKey[..Math.Min(8, providerKey.Length)]}@oauth.retrade.local"
            : email.Trim().ToLowerInvariant();

        var cleanName = !string.IsNullOrWhiteSpace(name)
            ? name.Trim()
            : cleanEmail.Split('@')[0];

        var newUser = new User
        {
            Email = cleanEmail,
            PasswordHash = "OAUTH_" + Guid.NewGuid().ToString("N"),
            FullName = cleanName,
            Role = "User",
            City = "Rajkot",
            State = "Gujarat",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var newUserId = AddUser(newUser);
        newUser.Id = newUserId;

        LinkExternalLogin(newUserId, provider, providerKey, cleanName);
        return newUser;
    }

    public bool LinkExternalLogin(int userId, string provider, string providerKey, string? displayName)
    {
        try
        {
            using var connection = CreateConnection();
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT OR IGNORE INTO ExternalLogins (UserId, Provider, ProviderKey, ProviderDisplayName, CreatedAt)
                VALUES (@UserId, @Provider, @ProviderKey, @DisplayName, @CreatedAt);
            ";
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@Provider", provider);
            cmd.Parameters.AddWithValue("@ProviderKey", providerKey);
            cmd.Parameters.AddWithValue("@DisplayName", (object?)displayName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow.ToString("o"));
            return cmd.ExecuteNonQuery() > 0;
        }
        catch
        {
            return false;
        }
    }

    public List<string> GetExternalProvidersForUser(int userId)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Provider FROM ExternalLogins WHERE UserId = @UserId;";
        cmd.Parameters.AddWithValue("@UserId", userId);

        using var reader = cmd.ExecuteReader();
        var list = new List<string>();
        while (reader.Read())
        {
            list.Add(reader.GetString(0));
        }
        return list;
    }

    #endregion
}

public static class DbParameterExtensions
{
    public static void AddWithValue(this DbParameterCollection parameters, string name, object? value)
    {
        if (parameters is SqliteParameterCollection sqliteParams)
        {
            sqliteParams.AddWithValue(name, value ?? DBNull.Value);
            return;
        }

        if (parameters is LibSQLParameterCollection libsqlParams)
        {
            libsqlParams.AddWithValue(name, value ?? DBNull.Value);
            return;
        }

        throw new NotSupportedException($"Unsupported parameter collection type: {parameters.GetType().FullName}");
    }
}

