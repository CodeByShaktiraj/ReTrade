using System.Security.Claims;
using AspNet.Security.OAuth.GitHub;
using Marketplace.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<AdoNetDbService>();
builder.Services.AddScoped<DemoSession>();
builder.Services.AddSingleton<ProductService>();
builder.Services.AddSingleton<OfferService>();
builder.Services.AddSingleton<TradeService>();
builder.Services.AddSingleton<OrderService>();
builder.Services.AddSingleton<WishlistService>();
builder.Services.AddSingleton<ReviewService>();
builder.Services.AddSingleton<ReportService>();
builder.Services.AddSingleton<CategoryService>();

var googleClientId = builder.Configuration["Authentication:Google:ClientId"]
    ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]
    ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET");

var githubClientId = builder.Configuration["Authentication:GitHub:ClientId"]
    ?? Environment.GetEnvironmentVariable("GITHUB_CLIENT_ID");
var githubClientSecret = builder.Configuration["Authentication:GitHub:ClientSecret"]
    ?? Environment.GetEnvironmentVariable("GITHUB_CLIENT_SECRET");

var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Cookie.Name = ".ReTrade.ExternalAuth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
    options.SlidingExpiration = false;
});

if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authBuilder.AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.CallbackPath = "/signin-google";
    });
}

if (!string.IsNullOrWhiteSpace(githubClientId) && !string.IsNullOrWhiteSpace(githubClientSecret))
{
    authBuilder.AddGitHub(GitHubAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.ClientId = githubClientId;
        options.ClientSecret = githubClientSecret;
        options.CallbackPath = "/signin-github";
        options.Scope.Add("user:email");
        options.Scope.Add("read:user");
    });
}

builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStatusCodePagesWithReExecute("/not-found");
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapGet("/auth/challenge/{provider}", (string provider, string? returnUrl, IConfiguration config) =>
{
    var isGoogle = string.Equals(provider, "google", StringComparison.OrdinalIgnoreCase);
    var isGitHub = string.Equals(provider, "github", StringComparison.OrdinalIgnoreCase);

    if (!isGoogle && !isGitHub)
    {
        return Results.Redirect($"/login?error={Uri.EscapeDataString("Unsupported login provider.")}");
    }

    var gId = config["Authentication:Google:ClientId"] ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
    var ghId = config["Authentication:GitHub:ClientId"] ?? Environment.GetEnvironmentVariable("GITHUB_CLIENT_ID");

    var configured = isGoogle ? !string.IsNullOrWhiteSpace(gId) : !string.IsNullOrWhiteSpace(ghId);
    var providerName = isGoogle ? "Google" : "GitHub";

    if (!configured)
    {
        var msg = $"{providerName} authentication is not yet configured. Please set Authentication:{providerName}:ClientId and ClientSecret in configuration or environment variables.";
        return Results.Redirect($"/login?error={Uri.EscapeDataString(msg)}");
    }

    var scheme = isGoogle ? GoogleDefaults.AuthenticationScheme : GitHubAuthenticationDefaults.AuthenticationScheme;
    var redirectUrl = $"/auth/external-callback?returnUrl={Uri.EscapeDataString(returnUrl ?? "/dashboard")}";
    var props = new AuthenticationProperties { RedirectUri = redirectUrl };
    return Results.Challenge(props, [scheme]);
});

app.MapGet("/auth/external-callback", async (string? returnUrl, HttpContext httpContext, AdoNetDbService db) =>
{
    var authResult = await httpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    if (!authResult.Succeeded || authResult.Principal == null)
    {
        return Results.Redirect($"/login?error={Uri.EscapeDataString("Social authentication could not be completed. Please try again.")}");
    }

    var principal = authResult.Principal;
    var provider = authResult.Properties?.Items.TryGetValue(".AuthScheme", out var schemeVal) == true && !string.IsNullOrWhiteSpace(schemeVal)
        ? schemeVal
        : "External";

    var providerKey = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? principal.FindFirst("sub")?.Value
        ?? principal.FindFirst("id")?.Value;

    var email = principal.FindFirst(ClaimTypes.Email)?.Value
        ?? principal.FindFirst("email")?.Value;

    var name = principal.FindFirst(ClaimTypes.Name)?.Value
        ?? principal.FindFirst("name")?.Value
        ?? (!string.IsNullOrWhiteSpace(email) ? email.Split('@')[0] : "User");

    if (string.IsNullOrWhiteSpace(providerKey))
    {
        return Results.Redirect($"/login?error={Uri.EscapeDataString("Could not retrieve unique account ID from social provider.")}");
    }

    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    var user = db.FindOrCreateExternalUser(provider, providerKey, email ?? "", name);
    var exchangeCode = db.CreateAuthExchangeCode(user.Id, TimeSpan.FromSeconds(60));

    var target = string.IsNullOrWhiteSpace(returnUrl) ? "/dashboard" : returnUrl;
    return Results.Redirect($"/auth-complete?code={Uri.EscapeDataString(exchangeCode)}&returnUrl={Uri.EscapeDataString(target)}");
});

if (app.Environment.IsDevelopment())
{
    app.MapGet("/auth/dev-mock-login", (string provider, string email, string name, string? returnUrl, AdoNetDbService db) =>
    {
        var mockSub = "mock_" + Math.Abs((provider + email).GetHashCode());
        var user = db.FindOrCreateExternalUser(provider, mockSub, email, name);
        var exchangeCode = db.CreateAuthExchangeCode(user.Id, TimeSpan.FromSeconds(60));
        var target = string.IsNullOrWhiteSpace(returnUrl) ? "/dashboard" : returnUrl;
        return Results.Redirect($"/auth-complete?code={Uri.EscapeDataString(exchangeCode)}&returnUrl={Uri.EscapeDataString(target)}");
    });
}

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

if (args.Contains("--setup-shakti-items"))
{
    var db = app.Services.GetRequiredService<AdoNetDbService>();
    var products = app.Services.GetRequiredService<ProductService>();

    Console.WriteLine("[SHAKTI SETUP] Looking up user jadejaqe@gmail.com...");
    var user = db.GetUserByEmail("jadejaqe@gmail.com");
    if (user == null)
    {
        user = new User
        {
            Email = "jadejaqe@gmail.com",
            FullName = "Shaktirajsinh Jadeja",
            PasswordHash = PasswordHelper.HashPassword("#shaktisinh063"),
            Role = "User",
            City = "Rajkot",
            State = "Gujarat",
            Phone = "9879012345",
            Bio = "Tech & photography enthusiast from Rajkot. Buying, selling and trading pre-owned gear.",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        var newId = db.AddUser(user);
        user.Id = newId;
        Console.WriteLine($"[SHAKTI SETUP] Created new user #{newId}.");
    }
    else
    {
        var newHash = PasswordHelper.HashPassword("#shaktisinh063");
        db.UpdateUserPassword(user.Id, newHash);
        user.City = "Rajkot";
        user.State = "Gujarat";
        user.Phone = user.Phone ?? "9879012345";
        user.Bio = string.IsNullOrWhiteSpace(user.Bio) ? "Tech & photography enthusiast from Rajkot. Buying, selling and trading pre-owned gear." : user.Bio;
        db.UpdateUser(user);
        Console.WriteLine($"[SHAKTI SETUP] Password for User #{user.Id} ({user.Email}) updated to '#shaktisinh063'.");
    }

    // Verify login
    using (var scope = app.Services.CreateScope())
    {
        var session = scope.ServiceProvider.GetRequiredService<DemoSession>();
        var (loginOk, loginErr) = session.Login("jadejaqe@gmail.com", "#shaktisinh063");
        if (!loginOk) throw new Exception($"Login verification failed: {loginErr}");
        Console.WriteLine($"[SHAKTI SETUP] Login test verified: Authenticated as {session.Current.Name} ({session.Current.Email}).");
    }

    var itemsToAdd = new List<Product>
    {
        new()
        {
            Title = "Canon EOS 1500D DSLR Camera (18-55mm IS II Lens)",
            Description = "24.1 MP DSLR with built-in Wi-Fi and NFC, DIGIC 4+ processor, EF-S 18-55mm lens. Shutter count around 3,200 only. Includes 16GB SanDisk Ultra SD card, neck strap, lens hood, battery and charger.",
            Price = 26000,
            Condition = ProductCondition.LikeNew,
            Category = "Electronics",
            City = "Rajkot",
            State = "Gujarat",
            Pincode = "360001",
            IsNegotiable = true,
            TradeAvailable = false
        },
        new()
        {
            Title = "OnePlus Nord CE 3 5G (128GB, Aqua Surge)",
            Description = "Snapdragon 782G, 120Hz Fluid AMOLED display, 50MP Sony IMX890 OIS camera, 80W SUPERVOOC charging. Back skin applied, no scratches on screen. Original 80W charger and bill included.",
            Price = 13500,
            Condition = ProductCondition.Good,
            Category = "Mobiles",
            City = "Rajkot",
            State = "Gujarat",
            Pincode = "360001",
            IsNegotiable = true,
            TradeAvailable = true
        },
        new()
        {
            Title = "Casio fx-991EX Classwiz Scientific Calculator",
            Description = "High-resolution LCD with 552 functions, QR code generator for graphs, spreadsheet mode. Essential for GTU engineering students. Purchased for 2nd semester, excellent battery and slide cover.",
            Price = 950,
            Condition = ProductCondition.LikeNew,
            Category = "Books",
            City = "Rajkot",
            State = "Gujarat",
            Pincode = "360001",
            IsNegotiable = false,
            TradeAvailable = false
        },
        new()
        {
            Title = "Hero Sprint Pro 26T 21-Speed Mountain Bicycle",
            Description = "Dual disc brakes, front suspension fork, Shimano 21-speed thumb shifters, sturdy lightweight alloy frame. Serviced last month with new brake pads and chain lubricant. Mudguards and water bottle cage attached.",
            Price = 6200,
            Condition = ProductCondition.Good,
            Category = "Sports",
            City = "Rajkot",
            State = "Gujarat",
            Pincode = "360001",
            IsNegotiable = true,
            TradeAvailable = true
        },
        new()
        {
            Title = "Solid Sheesham Wood Coffee Table with Shelf",
            Description = "Genuine Sheesham wood centre table (3ft x 2ft) with teak finish and lower magazine shelf. Sturdy build, warm natural wood grain. Minor surface scratch on corner. Ready for living room pickup.",
            Price = 4200,
            Condition = ProductCondition.Good,
            Category = "Furniture",
            City = "Rajkot",
            State = "Gujarat",
            Pincode = "360001",
            IsNegotiable = true,
            TradeAvailable = false
        },
        new()
        {
            Title = "Prestige Iris 750 Watt Mixer Grinder (4 Jars)",
            Description = "750W heavy-duty motor, 3 stainless steel jars (wet grinding, dry grinding, chutney) plus 1 juicer jar with blade. All rubber gaskets and lids are in working order. Clean and hygienic.",
            Price = 1700,
            Condition = ProductCondition.Good,
            Category = "Home Appliances",
            City = "Rajkot",
            State = "Gujarat",
            Pincode = "360001",
            IsNegotiable = false,
            TradeAvailable = false
        },
        new()
        {
            Title = "JBL Flip 5 Portable Bluetooth Speaker (Squad Edition)",
            Description = "IPX7 waterproof rugged portable speaker with 12 hours playtime, deep bass radiator, USB-C fast charging. Camouflage edition. Zero dents, sounds punchy and crisp. Cable included.",
            Price = 4800,
            Condition = ProductCondition.LikeNew,
            Category = "Electronics",
            City = "Rajkot",
            State = "Gujarat",
            Pincode = "360001",
            IsNegotiable = true,
            TradeAvailable = true
        },
        new()
        {
            Title = "Samsung 28L Convection Microwave Oven (MC28H5025VK)",
            Description = "28 Litre capacity convection microwave with Slim Fry technology, tandoor & curd making functions, ceramic enamel cavity (scratch resistant & antibacterial). Clean turntable and roller ring included.",
            Price = 7500,
            Condition = ProductCondition.Good,
            Category = "Home Appliances",
            City = "Rajkot",
            State = "Gujarat",
            Pincode = "360001",
            IsNegotiable = true,
            TradeAvailable = false
        },
        new()
        {
            Title = "Yamaha F280 Acoustic Guitar with Padded Gig Bag",
            Description = "Traditional Western body, spruce top, rosewood fingerboard. Rich acoustic tone, comfortable low action setup. Played sparingly at home. Comes with Fender strap, 3 picks, capo, and padded carry bag.",
            Price = 6400,
            Condition = ProductCondition.LikeNew,
            Category = "Other",
            City = "Rajkot",
            State = "Gujarat",
            Pincode = "360001",
            IsNegotiable = true,
            TradeAvailable = true
        },
        new()
        {
            Title = "S. Chand Physics & Chemistry Textbook Bundle (11th & 12th)",
            Description = "Complete set of S. Chand foundation and reference guides for Physics and Chemistry. Neat handwriting notes in pencil, no torn pages. Helpful for CBSE / GSEB board exams and competitive prep.",
            Price = 850,
            Condition = ProductCondition.Good,
            Category = "Books",
            City = "Rajkot",
            State = "Gujarat",
            Pincode = "360001",
            IsNegotiable = false,
            TradeAvailable = true
        },
        new()
        {
            Title = "Anker 65W GaN II 3-Port Fast Charger (2C + 1A)",
            Description = "Compact 65W fast wall charger with PowerIQ 3.0 technology. Can charge MacBook Air, iPhone and AirPods simultaneously. Foldable prongs, barely used. Original packaging.",
            Price = 2100,
            Condition = ProductCondition.LikeNew,
            Category = "Electronics",
            City = "Rajkot",
            State = "Gujarat",
            Pincode = "360001",
            IsNegotiable = false,
            TradeAvailable = false
        },
        new()
        {
            Title = "HP Pavilion Gaming Backpack 500 (Water Resistant)",
            Description = "Dedicated padded sleeve for up to 17.3-inch gaming laptops, water-resistant exterior, headset loop, and multiple accessory pockets. All zippers and shoulder straps are in solid condition.",
            Price = 1100,
            Condition = ProductCondition.Good,
            Category = "Laptops",
            City = "Rajkot",
            State = "Gujarat",
            Pincode = "360001",
            IsNegotiable = true,
            TradeAvailable = true
        }
    };

    Console.WriteLine($"[SHAKTI SETUP] Adding {itemsToAdd.Count} items for Seller #{user.Id}...");
    foreach (var item in itemsToAdd)
    {
        var (ok, err, prodId) = products.Add(user.Id, item);
        if (!ok)
        {
            Console.WriteLine($"[ERROR] Failed to add '{item.Title}': {err}");
        }
        else
        {
            Console.WriteLine($"[ADDED] #{prodId}: '{item.Title}' - Rs.{item.Price} ({item.Category})");
        }
    }

    Console.WriteLine("[SHAKTI SETUP] 12 items successfully listed for Shaktirajsinh Jadeja!");
    return;
}

if (args.Contains("--setup-bhavy-items"))
{
    var db = app.Services.GetRequiredService<AdoNetDbService>();
    var products = app.Services.GetRequiredService<ProductService>();

    Console.WriteLine("[BHAVY SETUP] Looking up user patelbhavypatel683@gmail.com...");
    var user = db.GetUserByEmail("patelbhavypatel683@gmail.com");
    if (user == null)
    {
        user = new User
        {
            Email = "patelbhavypatel683@gmail.com",
            FullName = "Bhavy Patel",
            PasswordHash = PasswordHelper.HashPassword("Bhavy@15289"),
            Role = "User",
            City = "Ahmedabad",
            State = "Gujarat",
            Phone = "9825012345",
            Bio = "Student & gadget enthusiast. Selling quality pre-owned tech, electronics, and books.",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        var newId = db.AddUser(user);
        user.Id = newId;
        Console.WriteLine($"[BHAVY SETUP] Created new user #{newId}.");
    }
    else
    {
        // Update password hash to requested Bhavy@15289
        var newHash = PasswordHelper.HashPassword("Bhavy@15289");
        db.UpdateUserPassword(user.Id, newHash);
        user.City = "Ahmedabad";
        user.State = "Gujarat";
        user.Phone = user.Phone ?? "9825012345";
        user.Bio = string.IsNullOrWhiteSpace(user.Bio) ? "Student & gadget enthusiast. Selling quality pre-owned tech, electronics, and books." : user.Bio;
        db.UpdateUser(user);
        Console.WriteLine($"[BHAVY SETUP] Password for User #{user.Id} ({user.Email}) updated to 'Bhavy@15289'.");
    }

    // Verify login
    using (var scope = app.Services.CreateScope())
    {
        var session = scope.ServiceProvider.GetRequiredService<DemoSession>();
        var (loginOk, loginErr) = session.Login("patelbhavypatel683@gmail.com", "Bhavy@15289");
        if (!loginOk) throw new Exception($"Login verification failed: {loginErr}");
        Console.WriteLine($"[BHAVY SETUP] Login test verified: Authenticated as {session.Current.Name} ({session.Current.Email}).");
    }

    var itemsToAdd = new List<Product>
    {
        new()
        {
            Title = "Logitech MX Master 3 Wireless Mouse",
            Description = "Ergonomic wireless mouse with MagSpeed electromagnetic scroll wheel, USB-C fast charging. Works perfectly on Mac and Windows. Selling because I upgraded to Master 3S. Original box and cable included.",
            Price = 4200,
            Condition = ProductCondition.LikeNew,
            Category = "Electronics",
            City = "Ahmedabad",
            State = "Gujarat",
            Pincode = "380015",
            IsNegotiable = true,
            TradeAvailable = false
        },
        new()
        {
            Title = "Sony WH-1000XM4 Noise Cancelling Headphones",
            Description = "Industry-leading active noise cancelling over-ear headphones with 30-hour battery life and touch sensor controls. Headband and ear pads in clean condition. Includes original carrying case and 3.5mm cable.",
            Price = 14500,
            Condition = ProductCondition.Good,
            Category = "Electronics",
            City = "Ahmedabad",
            State = "Gujarat",
            Pincode = "380015",
            IsNegotiable = true,
            TradeAvailable = true
        },
        new()
        {
            Title = "Apple iPad 9th Gen (64GB Wi-Fi, Space Grey)",
            Description = "10.2-inch Retina display with True Tone, A13 Bionic chip. Battery health is solid, tempered glass applied from day one. Great for college note-taking and multimedia. Comes with original charger.",
            Price = 18500,
            Condition = ProductCondition.Good,
            Category = "Mobiles",
            City = "Ahmedabad",
            State = "Gujarat",
            Pincode = "380015",
            IsNegotiable = true,
            TradeAvailable = false
        },
        new()
        {
            Title = "Kindle Paperwhite 10th Gen (8GB, Waterproof)",
            Description = "Glare-free 300 ppi display that reads like real paper, waterproof design (IPX8), built-in adjustable warm light. Battery lasts weeks on a single charge. Barely used, magnetic sleep-wake cover included.",
            Price = 5800,
            Condition = ProductCondition.LikeNew,
            Category = "Books",
            City = "Ahmedabad",
            State = "Gujarat",
            Pincode = "380015",
            IsNegotiable = false,
            TradeAvailable = true
        },
        new()
        {
            Title = "Keychron K2 V2 Wireless Mechanical Keyboard",
            Description = "75% layout Bluetooth wireless mechanical keyboard with Gateron Brown switches and RGB backlighting. Dual compatibility for Mac and Windows. Includes keycap puller, extra keycaps and braided USB-C cable.",
            Price = 5200,
            Condition = ProductCondition.Good,
            Category = "Electronics",
            City = "Ahmedabad",
            State = "Gujarat",
            Pincode = "380015",
            IsNegotiable = true,
            TradeAvailable = true
        },
        new()
        {
            Title = "Dell 24-inch FHD IPS Monitor (SE2422HX)",
            Description = "24-inch Full HD (1920x1080) 75Hz monitor with ultra-thin bezels, AMD FreeSync, HDMI and VGA ports. Zero dead pixels or backlight bleed. Used for home office desk setup. Includes HDMI cable and power cord.",
            Price = 6800,
            Condition = ProductCondition.LikeNew,
            Category = "Laptops",
            City = "Ahmedabad",
            State = "Gujarat",
            Pincode = "380015",
            IsNegotiable = true,
            TradeAvailable = false
        },
        new()
        {
            Title = "Quechua 2-Person Waterproof Camping Tent (MH100)",
            Description = "Decathlon Quechua 2-person self-standing dome tent. Easy pitch design, waterproof flysheet and wind resistant up to 40 km/h. Used on two weekend treks in Saputara. Clean and complete with pegs and poles.",
            Price = 1800,
            Condition = ProductCondition.Good,
            Category = "Sports",
            City = "Ahmedabad",
            State = "Gujarat",
            Pincode = "380015",
            IsNegotiable = true,
            TradeAvailable = true
        },
        new()
        {
            Title = "Harry Potter Complete Paperback Boxed Set (Books 1-7)",
            Description = "Complete 7-volume Bloomsbury edition series by J.K. Rowling. All pages intact with clean spines, no highlighting or missing pages. Stored in bookshelf. Perfect for readers and collectors.",
            Price = 2200,
            Condition = ProductCondition.Good,
            Category = "Books",
            City = "Ahmedabad",
            State = "Gujarat",
            Pincode = "380015",
            IsNegotiable = false,
            TradeAvailable = true
        },
        new()
        {
            Title = "Ergonomic Mesh High-Back Office Chair",
            Description = "Breathable mesh back support, pneumatic height adjustment, smooth 360-degree swivel casters. Some minor wear on the armrests but pneumatic cylinder and tilt mechanism function smoothly. Local pickup preferred.",
            Price = 3500,
            Condition = ProductCondition.Fair,
            Category = "Furniture",
            City = "Ahmedabad",
            State = "Gujarat",
            Pincode = "380015",
            IsNegotiable = true,
            TradeAvailable = false
        },
        new()
        {
            Title = "Philips Air Fryer HD9200 (4.1L Capacity)",
            Description = "Rapid Air technology with adjustable time and temperature control. 4.1L basket capacity, non-stick removable parts that are easy to wash. Works great for healthy low-oil snacks. Used for around 8 months.",
            Price = 3900,
            Condition = ProductCondition.Good,
            Category = "Home Appliances",
            City = "Ahmedabad",
            State = "Gujarat",
            Pincode = "380015",
            IsNegotiable = true,
            TradeAvailable = false
        }
    };

    Console.WriteLine($"[BHAVY SETUP] Adding {itemsToAdd.Count} items for Seller #{user.Id}...");
    foreach (var item in itemsToAdd)
    {
        var (ok, err, prodId) = products.Add(user.Id, item);
        if (!ok)
        {
            Console.WriteLine($"[ERROR] Failed to add '{item.Title}': {err}");
        }
        else
        {
            Console.WriteLine($"[ADDED] #{prodId}: '{item.Title}' - Rs.{item.Price} ({item.Category})");
        }
    }

    Console.WriteLine("[BHAVY SETUP] 10 items successfully listed for Bhavy Patel!");
    return;
}

if (args.Contains("--inspect-db"))
{
    var db = app.Services.GetRequiredService<AdoNetDbService>();
    Console.WriteLine($"[INSPECT] Provider: {(db.IsTurso ? "Turso Cloud" : "SQLite")}");
    Console.WriteLine("--- USERS ---");
    foreach (var u in db.GetAllUsers())
    {
        Console.WriteLine($"User #{u.Id}: {u.Email} | {u.FullName} | Role={u.Role} | Active={u.IsActive}");
    }
    Console.WriteLine("--- PRODUCTS ---");
    foreach (var p in db.GetAllProductsForAdmin())
    {
        Console.WriteLine($"Product #{p.Id}: '{p.Title}' | SellerId={p.SellerId} | Price={p.Price} | IsSeed={p.IsSeed} | Status={p.Status}");
    }
    Console.WriteLine("--- REVIEWS ---");
    using (var conn = db.CreateConnection())
    {
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, OrderId, ProductId, ProductTitle, ReviewerName, RevieweeId, Rating, Comment FROM Reviews;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            Console.WriteLine($"Review #{reader.GetInt32(0)}: Product '{reader.GetString(3)}' | Reviewer={reader.GetString(4)} | RevieweeId={reader.GetInt32(5)} | Stars={reader.GetInt32(6)} | '{reader.GetString(7)}'");
        }
    }
    return;
}

if (args.Contains("--test"))
{
    Console.WriteLine("Running ADO.NET and Buy Now functionality verification...");
    var db = app.Services.GetRequiredService<AdoNetDbService>();
    var products = app.Services.GetRequiredService<ProductService>();
    var orders = app.Services.GetRequiredService<OrderService>();

    Console.WriteLine($"Database connection string: {db.ConnectionString}");

    // Ephemeral test accounts for testing pipeline
    var testSellerEmail = $"test.seller.{Guid.NewGuid():N}"[..20] + "@retrade.test";
    var testBuyerEmail = $"test.buyer.{Guid.NewGuid():N}"[..20] + "@retrade.test";
    var sellerUser = new User
    {
        Email = testSellerEmail,
        FullName = "Test Seller",
        PasswordHash = PasswordHelper.HashPassword("Password@123"),
        Role = "User",
        City = "Rajkot",
        State = "Gujarat",
        CreatedAt = DateTime.UtcNow,
        IsActive = true
    };
    var buyerUser = new User
    {
        Email = testBuyerEmail,
        FullName = "Test Buyer",
        PasswordHash = PasswordHelper.HashPassword("Password@123"),
        Role = "User",
        City = "Ahmedabad",
        State = "Gujarat",
        CreatedAt = DateTime.UtcNow,
        IsActive = true
    };
    var sellerId = db.AddUser(sellerUser);
    var buyerId = db.AddUser(buyerUser);

    try
    {
        // 1. Verify products loading
        var allRecent = products.Recent(20);
        Console.WriteLine($"[TEST 1] Loaded {allRecent.Count} active products from database.");

        // 2. Test uploading item via ADO.NET
        var testProduct = new Product
        {
            Title = "Test ADO.NET Mechanical Keyboard",
            Description = "A tested item uploaded directly through ADO.NET pipeline.",
            Price = 1500,
            Condition = ProductCondition.LikeNew,
            Category = "Electronics",
            City = "Rajkot",
            State = "Gujarat",
            Pincode = "360001",
            IsNegotiable = true
        };
        var (addOk, addError, newId) = products.Add(sellerId, testProduct);
        if (!addOk) throw new Exception($"Failed to upload product: {addError}");
        Console.WriteLine($"[TEST 2] Product uploaded successfully with ID: {newId} via ADO.NET.");

        var fetched = products.GetProductById(newId);
        if (fetched == null || fetched.Title != testProduct.Title)
            throw new Exception("Uploaded product not found via ADO.NET query.");
        Console.WriteLine($"[TEST 3] Uploaded product successfully fetched: {fetched.Title} (Status: {fetched.Status})");

        // 3. Test Buy Now functionality
        var (buyOk, buyError, placedOrder) = orders.Place(
            productId: newId,
            buyerId: buyerId,
            buyerName: buyerUser.FullName,
            address: "402 Royal Arcade",
            city: "Ahmedabad",
            state: "Gujarat",
            pincode: "380001",
            phone: "9876543210",
            payment: PaymentMethod.CashOnDelivery
        );
        if (!buyOk || placedOrder == null) throw new Exception($"Buy Now failed: {buyError}");
        Console.WriteLine($"[TEST 4] Buy Now successful! Order #{placedOrder.Id} created for '{placedOrder.ProductTitle}' amount: Rs.{placedOrder.Amount}");

        // 4. Verify product is now marked Sold in DB
        var postBuyProduct = products.GetProductById(newId);
        if (postBuyProduct == null || postBuyProduct.Status != ProductStatus.Sold)
            throw new Exception($"Expected product status to be Sold, got: {postBuyProduct?.Status}");
        Console.WriteLine($"[TEST 5] Product #{newId} status updated to {postBuyProduct.Status} in database.");

        // 5. Verify purchase history for buyer
        var buyerHistory = orders.ForBuyer(buyerId);
        var foundOrder = buyerHistory.FirstOrDefault(o => o.Id == placedOrder.Id);
        if (foundOrder == null) throw new Exception("Order not found in buyer purchase history.");
        Console.WriteLine($"[TEST 6] Order #{foundOrder.Id} successfully retrieved in buyer purchase history (Total orders for buyer: {buyerHistory.Count}).");

        // 6. Test Offer System (Negotiation)
        var offers = app.Services.GetRequiredService<OfferService>();
        var testNegotiableProduct = new Product
        {
            Title = "Test Negotiable Monitor",
            Description = "4K 27-inch monitor in great condition.",
            Price = 12000,
            Condition = ProductCondition.LikeNew,
            Category = "Electronics",
            City = "Surat",
            State = "Gujarat",
            IsNegotiable = true
        };
        var (_, _, monitorId) = products.Add(sellerId, testNegotiableProduct);
        var (offerOk, offerErr, offerId) = offers.Submit(monitorId, buyerId, buyerUser.FullName, 10500, "Would you accept 10.5k?");
        if (!offerOk) throw new Exception($"Offer failed: {offerErr}");
        var sellerOffers = offers.ReceivedBy(sellerId);
        if (!sellerOffers.Any(o => o.Id == offerId)) throw new Exception("Offer not found in seller offers.");
        Console.WriteLine($"[TEST 7] Offer #{offerId} placed and retrieved for product #{monitorId}.");

        var (counterOk, counterErr) = offers.Counter(offerId, sellerId, 11000, "I can do 11,000.");
        if (!counterOk) throw new Exception($"Counter offer failed: {counterErr}");
        var counteredOffer = offers.GetById(offerId);
        if (counteredOffer?.Status != OfferStatus.Countered || counteredOffer.CounterAmount != 11000)
            throw new Exception("Counter offer state mismatch.");
        Console.WriteLine($"[TEST 8] Counter-offer successfully submitted: Rs.{counteredOffer.CounterAmount}.");

        var (acceptOk, acceptErr) = offers.Accept(offerId, buyerId);
        if (!acceptOk) throw new Exception($"Accept offer failed: {acceptErr}");
        Console.WriteLine($"[TEST 9] Offer #{offerId} successfully accepted.");

        // 7. Test Item Trading System
        var trades = app.Services.GetRequiredService<TradeService>();
        var targetTradeItem = new Product
        {
            Title = "Target Trade Guitar",
            Description = "Acoustic guitar with gig bag.",
            Price = 4000,
            Condition = ProductCondition.Good,
            Category = "Other",
            City = "Rajkot",
            State = "Gujarat",
            TradeAvailable = true
        };
        var (_, _, targetTradeId) = products.Add(sellerId, targetTradeItem);

        var offeredTradeItem = new Product
        {
            Title = "Offered Trade Amplifier",
            Description = "15W practice amp, works great.",
            Price = 3500,
            Condition = ProductCondition.Good,
            Category = "Electronics",
            City = "Ahmedabad",
            State = "Gujarat",
            TradeAvailable = true
        };
        var (_, _, offeredTradeId) = products.Add(buyerId, offeredTradeItem);

        var (tradeOk, tradeErr, tradeReqId) = trades.Submit(targetTradeId, offeredTradeId, buyerId, buyerUser.FullName, 500, "Amp plus 500 cash for your guitar.");
        if (!tradeOk) throw new Exception($"Trade submit failed: {tradeErr}");
        Console.WriteLine($"[TEST 10] Trade request #{tradeReqId} created between items #{targetTradeId} and #{offeredTradeId}.");

        var (tradeAcceptOk, tradeAcceptErr) = trades.Accept(tradeReqId, sellerId);
        if (!tradeAcceptOk) throw new Exception($"Trade accept failed: {tradeAcceptErr}");
        var targetPostTrade = products.GetProductById(targetTradeId);
        var offeredPostTrade = products.GetProductById(offeredTradeId);
        if (targetPostTrade?.Status != ProductStatus.Traded || offeredPostTrade?.Status != ProductStatus.Traded)
            throw new Exception("Traded products were not marked as Traded in DB.");
        Console.WriteLine($"[TEST 11] Trade completed! Both items marked as Traded in database.");

        // 8. Test Wishlist System
        var wishlist = app.Services.GetRequiredService<WishlistService>();
        var wasWishlisted = wishlist.IsWishlisted(buyerId, monitorId);
        wishlist.Toggle(buyerId, monitorId);
        if (wishlist.IsWishlisted(buyerId, monitorId) == wasWishlisted)
            throw new Exception("Wishlist toggle did not change state.");
        Console.WriteLine($"[TEST 12] Wishlist item toggled and verified.");

        // 9. Test Review System
        var reviews = app.Services.GetRequiredService<ReviewService>();
        var (revOk, revErr, revId) = reviews.SubmitReview(placedOrder.Id, buyerId, buyerUser.FullName, 5, "Excellent seller! Item arrived in great condition.");
        if (!revOk) throw new Exception($"Review failed: {revErr}");
        var (avgRating, revCount) = reviews.GetSellerRating(placedOrder.SellerId);
        Console.WriteLine($"[TEST 13] Review #{revId} placed. Seller rating: {avgRating:F1} ({revCount} reviews).");

        // Test duplicate review prevention
        var (dupRevOk, dupRevErr, _) = reviews.SubmitReview(placedOrder.Id, buyerId, buyerUser.FullName, 4, "Trying to review again.");
        if (dupRevOk) throw new Exception("Duplicate review was improperly accepted!");
        Console.WriteLine($"[DUPLICATE REVIEW CHECK] Duplicate review correctly rejected: {dupRevErr}");

        // 10. Test Report System
        var reports = app.Services.GetRequiredService<ReportService>();
        var (repOk, repErr, repId) = reports.SubmitReport(buyerId, buyerUser.FullName, 1, "Seed Item", null, null, "Counterfeit/Fraud", "Testing reporting pipeline.");
        if (!repOk) throw new Exception($"Report failed: {repErr}");
        Console.WriteLine($"[TEST 14] Report #{repId} created and logged for moderation.");

        // 11. Test Authentication & Multi-Tab Session Isolation
        using var tab1Scope = app.Services.CreateScope();
        var sessionTab1 = tab1Scope.ServiceProvider.GetRequiredService<DemoSession>();
        if (sessionTab1.IsAuthenticated) throw new Exception("Session should initially be unauthenticated.");

        // Tab 1: Seller logs in
        var (loginOk1, loginErr1) = sessionTab1.Login(sellerUser.Email, "Password@123");
        if (!loginOk1) throw new Exception($"Login failed: {loginErr1}");
        if (!sessionTab1.IsAuthenticated || string.IsNullOrWhiteSpace(sessionTab1.Current.Name)) throw new Exception("Session state mismatch after valid login.");
        Console.WriteLine($"[TEST 15] Tab 1 user login verified: {sessionTab1.Current.Name} ({sessionTab1.Current.Role}).");

        // Tab 2: Independent browser tab circuit opens in same session - must be Guest
        using var tab2Scope = app.Services.CreateScope();
        var sessionTab2 = tab2Scope.ServiceProvider.GetRequiredService<DemoSession>();
        if (sessionTab2.IsAuthenticated) throw new Exception("Tab 2 should be completely isolated and unauthenticated.");
        Console.WriteLine("[TEST 16] Tab 2 verified as isolated unauthenticated guest while Tab 1 is logged in.");

        // Tab 2: Buyer logs in
        var (loginOk2, loginErr2) = sessionTab2.Login(buyerUser.Email, "Password@123");
        if (!loginOk2 || string.IsNullOrWhiteSpace(sessionTab2.Current.Name)) throw new Exception($"Tab 2 login failed: {loginErr2}");
        Console.WriteLine($"[TEST 17] Tab 2 login verified: {sessionTab2.Current.Name} ({sessionTab2.Current.Role}).");

        // Verify Tab 1 is STILL Seller and Tab 2 is STILL Buyer
        if (sessionTab1.Current.Email != sellerUser.Email || sessionTab2.Current.Email != buyerUser.Email)
            throw new Exception("Tab isolation failure: accounts cross-pollinated across tabs!");
        Console.WriteLine($"[TAB ISOLATION CHECK] Verified: Tab 1 = {sessionTab1.Current.Name}, Tab 2 = {sessionTab2.Current.Name}.");

        // Logout Tab 1: Tab 1 becomes Guest, Tab 2 MUST remain Buyer
        sessionTab1.Logout();
        if (sessionTab1.IsAuthenticated) throw new Exception("Tab 1 should be unauthenticated after logout.");
        if (!sessionTab2.IsAuthenticated || sessionTab2.Current.Email != buyerUser.Email)
            throw new Exception("Logging out Tab 1 improperly affected Tab 2!");
        Console.WriteLine($"[TAB ISOLATION CHECK] Tab 1 logged out; Tab 2 remains securely logged in as {sessionTab2.Current.Name}.");

        // 12. Test Administrator Account Protection & Anti-Privilege Escalation
        var adminUser = db.GetUserByEmail("admin@retrade.com");
        if (adminUser == null) throw new Exception("Primary admin user not found.");

        // Primary admin cannot be deactivated
        var deactAttempt = db.SetUserActive(adminUser.Id, false);
        if (deactAttempt) throw new Exception("Primary admin was improperly allowed to be deactivated!");
        var adminCheck = db.GetUserById(adminUser.Id);
        if (!adminCheck!.IsActive) throw new Exception("Primary admin active state was corrupted!");
        Console.WriteLine("[TEST 18] Protected Admin deactivation prevention verified.");

        // Primary admin cannot be demoted
        var demoteAttempt = db.SetUserRole(adminUser.Id, "User");
        if (demoteAttempt) throw new Exception("Primary admin was improperly allowed to be demoted!");
        adminCheck = db.GetUserById(adminUser.Id);
        if (adminCheck!.Role != "Admin") throw new Exception("Primary admin role was corrupted!");
        Console.WriteLine("[TEST 19] Protected Admin demotion prevention verified.");

        // Primary admin cannot be deleted
        var (delOk, delErr) = db.DeleteUser(adminUser.Id);
        if (delOk) throw new Exception("Primary admin was improperly allowed to be deleted!");
        adminCheck = db.GetUserById(adminUser.Id);
        if (adminCheck == null) throw new Exception("Primary admin was deleted from DB!");
        Console.WriteLine($"[TEST 20] Protected Admin deletion prevention verified: '{delErr}'.");

        Console.WriteLine("=================================================");
        Console.WriteLine("ALL 20 END-TO-END, SECURITY & ADMIN TESTS PASSED 100%!");
        Console.WriteLine("=================================================");
    }
    finally
    {
        // Teardown: Clean up ephemeral test artifacts immediately so production database stays pristine
        db.CleanFakeData();
        db.DeleteUser(sellerId);
        db.DeleteUser(buyerId);
    }
    return;
}

app.Run();
