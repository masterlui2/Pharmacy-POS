using Microsoft.EntityFrameworkCore;
using PharmacyPOS.Data;
using PharmacyPOS.Models;
using PharmacyPOS.Models.Checkout;
using PharmacyPOS.Services;

var builder = WebApplication.CreateBuilder(args);
const string FlutterWebCorsPolicy = "FlutterWebCors";

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddControllersWithViews();
builder.Services.AddCors(options =>
{
    options.AddPolicy(FlutterWebCorsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:54939")
            .WithMethods("POST", "OPTIONS")
            .AllowAnyHeader();
    });
});
builder.Services.Configure<RecaptchaOptions>(
    builder.Configuration.GetSection(RecaptchaOptions.SectionName));
builder.Services.Configure<PayMongoOptions>(
    builder.Configuration.GetSection(PayMongoOptions.SectionName));
builder.Services.Configure<GoogleMapsDeliveryOptions>(
    builder.Configuration.GetSection(GoogleMapsDeliveryOptions.SectionName));
builder.Services.Configure<FirebaseOptions>(
    builder.Configuration.GetSection(FirebaseOptions.SectionName));
builder.Services.AddDbContext<PharmacyPosDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHttpClient<IRecaptchaService, GoogleRecaptchaService>();
builder.Services.AddHttpClient<IPayMongoService, PayMongoService>();
builder.Services.AddSingleton<FirebaseAppInitializer>();
builder.Services.AddSingleton<IFirebaseSyncService, FirebaseSyncService>();
builder.Services.AddSingleton<IAuditLogService, FileAuditLogService>();
builder.Services.AddSingleton<IPharmacistMessagingService, FilePharmacistMessagingService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<IWishlistService, WishlistService>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddSingleton<IMedicineService, InMemoryMedicineService>();
builder.Services.AddScoped<IAccountService, DatabaseAccountService>();

var app = builder.Build();

var firebaseAppInitializer = app.Services.GetRequiredService<FirebaseAppInitializer>();
var startupLogger = app.Services
    .GetRequiredService<ILoggerFactory>()
    .CreateLogger("Startup");

if (!firebaseAppInitializer.IsAuthenticationAvailable)
{
    startupLogger.LogError(
        "Firebase authentication is unavailable. {Reason}",
        firebaseAppInitializer.AuthenticationUnavailableReason ??
            "No additional details were provided.");
}
else
{
    startupLogger.LogInformation("Firebase authentication initialized successfully.");
}

if (!firebaseAppInitializer.IsFirestoreAvailable)
{
    startupLogger.LogWarning(
        "Cloud Firestore is unavailable. {Reason}",
        firebaseAppInitializer.FirestoreUnavailableReason ??
            "No additional details were provided.");
}
else
{
    startupLogger.LogInformation("Cloud Firestore initialized successfully.");
}

try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<PharmacyPosDbContext>();
    dbContext.Database.Migrate();
    await DbInitializer.SeedAsync(scope.ServiceProvider);
}
catch (Exception exception) when (app.Environment.IsDevelopment())
{
    startupLogger.LogError(
        exception,
        "Database initialization failed in Development. The app will continue with limited functionality until SQL Server is available.");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors(FlutterWebCorsPolicy);
app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
