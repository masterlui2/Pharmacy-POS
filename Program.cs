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
        policy.WithOrigins("http://localhost:53192")
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
builder.Services.AddDbContext<PharmacyPosDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHttpClient<IRecaptchaService, GoogleRecaptchaService>();
builder.Services.AddHttpClient<IPayMongoService, PayMongoService>();
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

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PharmacyPosDbContext>();
    dbContext.Database.Migrate();
    await DbInitializer.SeedAsync(scope.ServiceProvider);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
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
