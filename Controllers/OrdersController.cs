using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmacyPOS.Data;
using PharmacyPOS.Models.Orders;
using PharmacyPOS.Services;

namespace PharmacyPOS.Controllers;

public class OrdersController(
    PharmacyPosDbContext dbContext,
    IPayMongoService payMongoService,
    IFirebaseSyncService firebaseSyncService,
    ILogger<OrdersController> logger) : BaseController
{
    private const int OrdersPageSize = 5;

    [HttpGet]
    public async Task<IActionResult> Index(
        int page = 1,
        string? order = null,
        string? payment = null,
        CancellationToken cancellationToken = default)
    {
        var customerEmail = HttpContext.Session.GetString("Email") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            return RedirectToAction("Login", "Auth");
        }

        if (!string.IsNullOrWhiteSpace(order) && string.Equals(payment, "success", StringComparison.OrdinalIgnoreCase))
        {
            await MarkOrderPaidAsync(customerEmail, order, cancellationToken);
        }

        var normalizedPage = Math.Max(1, page);
        var ordersQuery = dbContext.Orders
            .AsNoTracking()
            .Include(entry => entry.Items)
            .Include(entry => entry.Payment)
            .Where(entry => entry.CustomerEmail == customerEmail)
            .OrderByDescending(entry => entry.CreatedAtUtc);

        var totalOrders = await ordersQuery.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalOrders / (double)OrdersPageSize));
        if (normalizedPage > totalPages)
        {
            normalizedPage = totalPages;
        }

        var pagedOrders = await ordersQuery
            .Skip((normalizedPage - 1) * OrdersPageSize)
            .Take(OrdersPageSize)
            .ToListAsync(cancellationToken);

        var orders = pagedOrders
            .Select(entry => new OrderSummaryViewModel
            {
                OrderNumber = entry.OrderNumber,
                OrderStatus = entry.OrderStatus,
                PaymentStatus = entry.Payment != null ? entry.Payment.Status : "Pending",
                PaymentMethod = entry.PaymentMethod,
                FulfillmentBranch = entry.FulfillmentBranch,
                DeliveryOption = entry.DeliveryOption,
                RequiresPrescription = entry.RequiresPrescription,
                PrescriptionStatus = entry.PrescriptionStatus,
                CreatedAtUtc = entry.CreatedAtUtc,
                TotalAmount = entry.TotalAmount,
                CanContinuePayment =
                    !entry.RequiresPrescription || IsPrescriptionValidated(entry.PrescriptionStatus),
                ShowContinuePayment =
                    !string.Equals(entry.PaymentMethod, "CashOnDelivery", StringComparison.OrdinalIgnoreCase) &&
                    (!entry.RequiresPrescription || IsPrescriptionValidated(entry.PrescriptionStatus)) &&
                    entry.Payment != null &&
                    entry.Payment.Status != "Paid",
                ContinuePaymentLabel =
                    entry.Payment != null && entry.Payment.Status == "RedirectedToGateway"
                        ? "Resume Payment"
                        : "Continue Payment",
                Items = entry.Items
                    .OrderBy(item => item.Id)
                    .Select(item => new OrderItemViewModel
                    {
                        ProductName = item.ProductName,
                        BrandName = item.BrandName,
                        ImageUrl = item.ImageUrl,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        RequiresPrescription = item.RequiresPrescription
                    })
                    .ToList()
            })
            .ToList();

        var (bannerTitle, bannerMessage, bannerTone) = ResolveBanner(order, payment);

        var vm = new OrdersIndexViewModel
        {
            HighlightOrderNumber = order ?? string.Empty,
            BannerTitle = bannerTitle,
            BannerMessage = bannerMessage,
            BannerTone = bannerTone,
            Orders = orders,
            CurrentPage = normalizedPage,
            PageSize = OrdersPageSize,
            TotalOrders = totalOrders
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ContinuePayment(string orderNumber, CancellationToken cancellationToken)
    {
        var customerEmail = HttpContext.Session.GetString("Email") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            return RedirectToAction("Login", "Auth");
        }

        var order = await dbContext.Orders
            .Include(entry => entry.Items)
            .Include(entry => entry.Payment)
            .FirstOrDefaultAsync(
                entry => entry.CustomerEmail == customerEmail && entry.OrderNumber == orderNumber,
                cancellationToken);
        if (order is null)
        {
            TempData["Error"] = "Order was not found.";
            return RedirectToAction(nameof(Index));
        }

        if (string.Equals(order.PaymentMethod, "CashOnDelivery", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Cash on Delivery orders do not require online payment.";
            return RedirectToAction(nameof(Index));
        }

        if (order.Payment is null)
        {
            TempData["Error"] = "Payment details are missing for this order.";
            return RedirectToAction(nameof(Index));
        }

        if (order.RequiresPrescription && !IsPrescriptionValidated(order.PrescriptionStatus))
        {
            TempData["Error"] = "Wait for pharmacist approval before continuing payment.";
            return RedirectToAction(nameof(Index));
        }

        if (string.Equals(order.Payment.Status, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "This order is already paid.";
            return RedirectToAction(nameof(Index));
        }

        var successReturnUrl = Url.Action("Index", "Orders", values: null, protocol: Request.Scheme);
        var cancelReturnUrl = Url.Action("Index", "Orders", values: null, protocol: Request.Scheme);
        var checkoutSession = await payMongoService.CreateCheckoutSessionAsync(
            order,
            order.Items,
            order.PaymentMethod,
            successReturnUrl,
            cancelReturnUrl,
            cancellationToken);

        if (!checkoutSession.Success)
        {
            TempData["Error"] = checkoutSession.Message;
            return RedirectToAction(nameof(Index));
        }

        order.Payment.Provider = "PayMongo";
        order.Payment.ProviderCheckoutId = checkoutSession.CheckoutId;
        order.Payment.CheckoutUrl = checkoutSession.CheckoutUrl;
        order.Payment.Status = "RedirectedToGateway";
        order.OrderStatus = "AwaitingPayment";
        await dbContext.SaveChangesAsync(cancellationToken);
        await TrySyncOrderStatusAsync(
            order,
            cancellationToken);

        return Redirect(checkoutSession.CheckoutUrl);
    }

    private async Task MarkOrderPaidAsync(string customerEmail, string orderNumber, CancellationToken cancellationToken)
    {
        var targetOrder = await dbContext.Orders
            .Include(entry => entry.Payment)
            .FirstOrDefaultAsync(
                entry => entry.CustomerEmail == customerEmail && entry.OrderNumber == orderNumber,
                cancellationToken);

        if (targetOrder is null)
        {
            return;
        }

        var previousPaymentStatus = targetOrder.Payment?.Status ?? "Pending";
        var hasChanges = false;

        if (!string.Equals(targetOrder.OrderStatus, "Paid", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(targetOrder.OrderStatus, "Processing", StringComparison.OrdinalIgnoreCase))
        {
            targetOrder.OrderStatus = "Processing";
            hasChanges = true;
        }

        if (targetOrder.Payment is not null &&
            !string.Equals(targetOrder.Payment.Status, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            targetOrder.Payment.Status = "Paid";
            hasChanges = true;
        }

        if (hasChanges)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await TrySyncOrderStatusAsync(
                targetOrder,
                cancellationToken);

            if (!string.Equals(previousPaymentStatus, targetOrder.Payment?.Status, StringComparison.OrdinalIgnoreCase))
            {
                await TryCreateNotificationAsync(
                    targetOrder.CustomerUid,
                    targetOrder.OrderNumber,
                    "Order Approved",
                    "Your payment was confirmed and your order is now being prepared.",
                    cancellationToken);
            }
        }
    }

    private async Task TrySyncOrderStatusAsync(
        PharmacyPOS.Models.PharmacyOrder order,
        CancellationToken cancellationToken)
    {
        try
        {
            await firebaseSyncService.UpdateOrderStatusAsync(
                order,
                order.Payment,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Firebase order status sync failed for order {OrderNumber}.",
                order.OrderNumber);
        }
    }

    private async Task TryCreateNotificationAsync(
        string customerUid,
        string orderNumber,
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            await firebaseSyncService.CreateNotificationAsync(
                customerUid,
                orderNumber,
                title,
                message,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Firebase notification creation failed for order {OrderNumber}.",
                orderNumber);
        }
    }

    private static bool IsPrescriptionValidated(string? prescriptionStatus) =>
        string.Equals(prescriptionStatus, "Approved", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(prescriptionStatus, "Valid", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(prescriptionStatus, "NotRequired", StringComparison.OrdinalIgnoreCase);

    private static (string Title, string Message, string Tone) ResolveBanner(string? orderNumber, string? payment)
    {
        if (string.IsNullOrWhiteSpace(orderNumber) || string.IsNullOrWhiteSpace(payment))
        {
            return (string.Empty, string.Empty, "success");
        }

        if (string.Equals(payment, "success", StringComparison.OrdinalIgnoreCase))
        {
            return (
                $"Order {orderNumber} completed.",
                "Your payment was confirmed and your order is now being prepared.",
                "success");
        }

        if (string.Equals(payment, "placed", StringComparison.OrdinalIgnoreCase))
        {
            return (
                $"Order {orderNumber} placed.",
                "Your cart was cleared. You can now start a new medicine order.",
                "success");
        }

        if (string.Equals(payment, "review", StringComparison.OrdinalIgnoreCase))
        {
            return (
                $"Order {orderNumber} submitted for prescription review.",
                "Wait for the pharmacist to approve the prescription before payment can continue.",
                "info");
        }

        if (string.Equals(payment, "cancelled", StringComparison.OrdinalIgnoreCase))
        {
            return (
                $"Payment cancelled for order {orderNumber}.",
                "You can continue the payment again from My Orders when you are ready.",
                "info");
        }

        return (string.Empty, string.Empty, "success");
    }
}
