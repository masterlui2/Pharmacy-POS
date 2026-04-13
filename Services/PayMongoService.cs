using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PharmacyPOS.Models;

namespace PharmacyPOS.Services;

public class PayMongoService(
    HttpClient httpClient,
    IOptions<PayMongoOptions> options) : IPayMongoService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly PayMongoOptions _options = options.Value;

    public bool IsConfigured() =>
        (_options.Enabled || !string.IsNullOrWhiteSpace(_options.SecretKey)) &&
        !string.IsNullOrWhiteSpace(_options.SecretKey);

    public async Task<PayMongoCheckoutSessionResult> CreateCheckoutSessionAsync(
        PharmacyOrder order,
        IReadOnlyList<PharmacyOrderItem> items,
        string paymentMethod,
        string? successReturnUrl,
        string? cancelReturnUrl,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured())
        {
            return new PayMongoCheckoutSessionResult
            {
                Success = false,
                Message = "PayMongo is not configured yet. Add your secret key in appsettings first."
            };
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.paymongo.com/v1/checkout_sessions");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.SecretKey}:")));

        var lineItems = items
            .Select(item => new
            {
                currency = "PHP",
                amount = ToMinorAmount(item.UnitPrice),
                name = item.ProductName,
                quantity = item.Quantity,
                description = item.BrandName
            })
            .Cast<object>()
            .ToList();

        if (order.ShippingAmount > 0)
        {
            lineItems.Add(new
            {
                currency = "PHP",
                amount = ToMinorAmount(order.ShippingAmount),
                name = "Delivery fee",
                quantity = 1,
                description = order.DeliveryOption
            });
        }

        var payload = new
        {
            data = new
            {
                attributes = new
                {
                    billing = new
                    {
                        name = order.CustomerFullName,
                        email = order.CustomerEmail,
                        phone = order.CustomerPhoneNumber
                    },
                    cancel_url = BuildReturnUrl(cancelReturnUrl, _options.CancelUrl, order.OrderNumber, "cancelled"),
                    success_url = BuildReturnUrl(successReturnUrl, _options.SuccessUrl, order.OrderNumber, "success"),
                    description = $"SafeMed order {order.OrderNumber}",
                    send_email_receipt = false,
                    show_description = true,
                    show_line_items = true,
                    payment_method_types = GetPaymentMethodTypes(paymentMethod),
                    metadata = new
                    {
                        order_number = order.OrderNumber,
                        branch = order.FulfillmentBranch,
                        payment_method = paymentMethod
                    },
                    line_items = lineItems
                }
            }
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new PayMongoCheckoutSessionResult
            {
                Success = false,
                Message = $"PayMongo checkout could not be created. {content}"
            };
        }

        using var document = JsonDocument.Parse(content);
        var data = document.RootElement.GetProperty("data");
        var attributes = data.GetProperty("attributes");

        return new PayMongoCheckoutSessionResult
        {
            Success = true,
            CheckoutId = data.GetProperty("id").GetString() ?? string.Empty,
            CheckoutUrl = attributes.GetProperty("checkout_url").GetString() ?? string.Empty,
            Message = "Redirecting to PayMongo checkout."
        };
    }

    private static int ToMinorAmount(decimal amount) => (int)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    private static string[] GetPaymentMethodTypes(string paymentMethod) =>
        string.Equals(paymentMethod, "GCash", StringComparison.OrdinalIgnoreCase)
            ? ["gcash"]
            : ["card"];

    private static string BuildReturnUrl(string? runtimeUrl, string configuredUrl, string orderNumber, string status)
    {
        var baseUrl = !string.IsNullOrWhiteSpace(runtimeUrl)
            ? runtimeUrl
            : configuredUrl;

        var separator = baseUrl.Contains("?") ? "&" : "?";
        return $"{baseUrl}{separator}order={Uri.EscapeDataString(orderNumber)}&payment={Uri.EscapeDataString(status)}";
    }
}
