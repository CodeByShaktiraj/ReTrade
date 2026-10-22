using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

public sealed class DemoAccount
{
    public int Id { get; }
    public string Name { get; }
    public string Email { get; }
    public string Role { get; }
    public string City { get; }
    public string State { get; }
    public bool IsAdmin => string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);

    public DemoAccount(int id, string name, string email, string role, string city, string state)
    {
        Id = id;
        Name = name;
        Email = email;
        Role = role;
        City = city;
        State = state;
    }
}

public sealed class DemoSession
{
    private readonly AdoNetDbService _db;
    private readonly ProtectedSessionStorage? _sessionStorage;
    private DemoAccount? _current;
    private string? _sessionToken;
    private bool _initialized;
    private static readonly DemoAccount Guest = new(0, "Guest", "", "Guest", "", "");

    public const string SessionStorageKey = "retrade_tab_session_token";

    public event Action? OnChange;

    /// <summary>
    /// Indicates whether a user is currently signed in to this tab circuit.
    /// </summary>
    public bool IsAuthenticated => _current != null;

    /// <summary>
    /// Returns the currently authenticated user in this tab circuit, or a safe Guest dummy account.
    /// </summary>
    public DemoAccount Current => _current ?? Guest;

    /// <summary>
    /// Returns the server-issued opaque session token for this tab.
    /// </summary>
    public string? CurrentSessionToken => _sessionToken;

    public DemoSession(AdoNetDbService db, ProtectedSessionStorage? sessionStorage = null)
    {
        _db = db;
        _sessionStorage = sessionStorage;
        _current = null;
    }

    /// <summary>
    /// Initializes and restores the session for the current browser tab circuit from ProtectedSessionStorage.
    /// Validates the token against the server UserSessions table.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        if (_sessionStorage != null)
        {
            try
            {
                var result = await _sessionStorage.GetAsync<string>(SessionStorageKey);
                if (result.Success && !string.IsNullOrWhiteSpace(result.Value))
                {
                    var token = result.Value;
                    var user = _db.ValidateSession(token);
                    if (user != null)
                    {
                        _sessionToken = token;
                        _current = new DemoAccount(user.Id, user.FullName, user.Email, user.Role, user.City, user.State);
                        NotifyStateChanged();
                        return;
                    }
                    else
                    {
                        // Token was revoked, expired, or invalid - clear tab storage
                        try
                        {
                            await _sessionStorage.DeleteAsync(SessionStorageKey);
                        }
                        catch { }
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // Prerendering or JS interop not yet ready in circuit
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SESSION] Could not restore tab session: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Authenticates with email and password asynchronously, creating an isolated server-side session token
    /// and persisting it into the current tab's protected session storage.
    /// </summary>
    public async Task<(bool Ok, string? Error)> LoginAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return (false, "Email and password are required.");

        var user = _db.GetUserByEmail(email.Trim());
        if (user == null)
            return (false, "No account found with that email address.");

        if (!user.IsActive)
            return (false, "This account is currently suspended by an administrator.");

        if (!PasswordHelper.VerifyPassword(password, user.PasswordHash))
            return (false, "Incorrect password. Please verify and try again.");

        await CompleteLoginAsync(user);
        return (true, null);
    }

    /// <summary>
    /// Synchronous login overload for compatibility and CLI verification tests.
    /// </summary>
    public (bool Ok, string? Error) Login(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return (false, "Email and password are required.");

        var user = _db.GetUserByEmail(email.Trim());
        if (user == null)
            return (false, "No account found with that email address.");

        if (!user.IsActive)
            return (false, "This account is currently suspended by an administrator.");

        if (!PasswordHelper.VerifyPassword(password, user.PasswordHash))
            return (false, "Incorrect password. Please verify and try again.");

        _sessionToken = _db.CreateSession(user.Id);
        _current = new DemoAccount(user.Id, user.FullName, user.Email, user.Role, user.City, user.State);
        NotifyStateChanged();
        return (true, null);
    }

    /// <summary>
    /// Completes the login process for a validated user, generating a new DB session and writing to ProtectedSessionStorage.
    /// </summary>
    public async Task CompleteLoginAsync(User user)
    {
        _sessionToken = _db.CreateSession(user.Id);
        _current = new DemoAccount(user.Id, user.FullName, user.Email, user.Role, user.City, user.State);

        if (_sessionStorage != null)
        {
            try
            {
                await _sessionStorage.SetAsync(SessionStorageKey, _sessionToken);
            }
            catch
            {
                // Prerender or non-interactive fallback
            }
        }

        NotifyStateChanged();
    }

    /// <summary>
    /// Exchanges a single-use OAuth authorization exchange code for an isolated tab session.
    /// </summary>
    public async Task<(bool Ok, string? Error)> ExchangeAuthCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return (false, "Invalid exchange code.");

        var user = _db.ConsumeAuthExchangeCode(code);
        if (user == null)
            return (false, "The authentication session has expired or is invalid. Please sign in again.");

        if (!user.IsActive)
            return (false, "This account is currently suspended.");

        await CompleteLoginAsync(user);
        return (true, null);
    }

    /// <summary>
    /// Asynchronously logs out the current tab, revoking the session token in the database
    /// and removing it from the tab's storage. Other tabs are unaffected.
    /// </summary>
    public async Task LogoutAsync()
    {
        if (!string.IsNullOrWhiteSpace(_sessionToken))
        {
            _db.RevokeSession(_sessionToken);
        }

        _sessionToken = null;
        _current = null;

        if (_sessionStorage != null)
        {
            try
            {
                await _sessionStorage.DeleteAsync(SessionStorageKey);
            }
            catch
            {
            }
        }

        NotifyStateChanged();
    }

    /// <summary>
    /// Synchronous logout for backward compatibility.
    /// </summary>
    public void Logout()
    {
        if (!string.IsNullOrWhiteSpace(_sessionToken))
        {
            _db.RevokeSession(_sessionToken);
        }

        _sessionToken = null;
        _current = null;

        if (_sessionStorage != null)
        {
            _ = Task.Run(async () =>
            {
                try { await _sessionStorage.DeleteAsync(SessionStorageKey); } catch { }
            });
        }

        NotifyStateChanged();
    }

    public async Task<(bool Ok, string? Error, int Id)> RegisterAsync(string fullName, string email, string password, string city, string state, string? phone)
    {
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length < 2)
            return (false, "Enter a valid full name (at least 2 characters).", 0);
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || !email.Contains('.'))
            return (false, "Enter a valid email address.", 0);
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            return (false, "Password must be at least 6 characters long.", 0);
        if (string.IsNullOrWhiteSpace(city))
            return (false, "City is required.", 0);
        if (string.IsNullOrWhiteSpace(state))
            return (false, "State is required.", 0);

        var existing = _db.GetUserByEmail(email.Trim().ToLowerInvariant());
        if (existing != null)
            return (false, "An account with this email address already exists.", 0);

        var newUser = new User
        {
            FullName = fullName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = PasswordHelper.HashPassword(password),
            Role = "User",
            City = city.Trim(),
            State = state.Trim(),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var id = _db.AddUser(newUser);
        newUser.Id = id;
        await CompleteLoginAsync(newUser);
        return (true, null, id);
    }

    public (bool Ok, string? Error, int Id) Register(string fullName, string email, string password, string city, string state, string? phone)
    {
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length < 2)
            return (false, "Enter a valid full name (at least 2 characters).", 0);
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || !email.Contains('.'))
            return (false, "Enter a valid email address.", 0);
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            return (false, "Password must be at least 6 characters long.", 0);
        if (string.IsNullOrWhiteSpace(city))
            return (false, "City is required.", 0);
        if (string.IsNullOrWhiteSpace(state))
            return (false, "State is required.", 0);

        var existing = _db.GetUserByEmail(email.Trim().ToLowerInvariant());
        if (existing != null)
            return (false, "An account with this email address already exists.", 0);

        var newUser = new User
        {
            FullName = fullName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = PasswordHelper.HashPassword(password),
            Role = "User",
            City = city.Trim(),
            State = state.Trim(),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var id = _db.AddUser(newUser);
        _sessionToken = _db.CreateSession(id);
        _current = new DemoAccount(newUser.Id, newUser.FullName, newUser.Email, newUser.Role, newUser.City, newUser.State);
        NotifyStateChanged();
        return (true, null, id);
    }

    public string NameFor(int id)
    {
        var user = _db.GetUserById(id);
        return user?.FullName ?? "Seller";
    }

    public User? GetFullUser(int id) => _db.GetUserById(id);

    public bool UpdateProfile(int id, string fullName, string city, string state, string? phone, string? bio)
    {
        if (!IsAuthenticated || Current.Id != id) return false;

        var user = _db.GetUserById(id);
        if (user == null) return false;

        user.FullName = fullName;
        user.City = city;
        user.State = state;
        user.Phone = phone;
        user.Bio = bio;

        var ok = _db.UpdateUser(user);
        if (ok && Current.Id == id)
        {
            _current = new DemoAccount(user.Id, user.FullName, user.Email, user.Role, user.City, user.State);
            NotifyStateChanged();
        }
        return ok;
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
