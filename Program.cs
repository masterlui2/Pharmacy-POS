using Microsoft.EntityFrameworkCore;
using PharmacyPOS.Data;
using PharmacyPOS.Models;
using PharmacyPOS.Models.Checkout;
using PharmacyPOS.Services;

var builder = WebApplication.CreateBuilder(args);
const string FlutterWebCorsPolicy = "FlutterWebCors";

// Allow machine-local secrets without checking them into source control.
builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddJsonFile(
        $"appsettings.{builder.Environment.EnvironmentName}.local.json",
        optional: true,
        reloadOnChange: true)
    .AddEnvironmentVariables();

if (args.Length > 0)
{
    builder.Configuration.AddCommandLine(args);
}

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddCors(options =>
{
    options.AddPolicy(FlutterWebCorsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:58861")
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
if (string.IsNullOrWhiteSpace(defaultConnection))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection must be configured for this environment.");
}

builder.Services.AddDbContext<PharmacyPosDbContext>(options =>
    options.UseSqlServer(defaultConnection));
builder.Services.AddHttpClient<IRecaptchaService, GoogleRecaptchaService>();
builder.Services.AddHttpClient<IPayMongoService, PayMongoService>();
builder.Services.AddSingleton<FirebaseAppInitializer>();
builder.Services.AddSingleton<FirebaseSyncService>();
builder.Services.AddSingleton<IFirebaseSyncService>(serviceProvider =>
    serviceProvider.GetRequiredService<FirebaseSyncService>());
builder.Services.AddSingleton<IFirebaseOrderChatService>(serviceProvider =>
    serviceProvider.GetRequiredService<FirebaseSyncService>());
builder.Services.AddSingleton<IFirebaseCustomerUidResolver, FirebaseCustomerUidResolver>();
builder.Services.AddHostedService<FirebaseOrderBackfillService>();
builder.Services.AddSingleton<IAuditLogService, FileAuditLogService>();
builder.Services.AddSingleton<FilePharmacistMessagingService>();
builder.Services.AddScoped<IPharmacistMessagingService, FirestoreBackedPharmacistMessagingService>();
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
    startupLogger.LogWarning(
        "Firebase authentication is unavailable. The app will continue without Firebase-backed features. {Reason}",
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
    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PharmacyPosDbContext>();
        dbContext.Database.Migrate();
        await DbInitializer.SeedAsync(scope.ServiceProvider);
    }
    else
    {
        startupLogger.LogInformation(
            "Skipping automatic database migrations and seeding outside Development.");
    }
}
catch (Exception exception)
{
    startupLogger.LogError(
        exception,
        "Database initialization failed during startup.");
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
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
