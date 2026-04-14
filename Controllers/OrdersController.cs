using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmacyPOS.Data;
using PharmacyPOS.Models.Orders;

namespace PharmacyPOS.Controllers;

public class OrdersController(PharmacyPosDbContext dbContext) : BaseController
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

        var orders = await ordersQuery
            .Skip((normalizedPage - 1) * OrdersPageSize)
            .Take(OrdersPageSize)
            .Select(entry => new OrderSummaryViewModel
            {
                OrderNumber = entry.OrderNumber,
                OrderStatus = entry.OrderStatus,
                PaymentStatus = entry.Payment != null ? entry.Payment.Status : "Pending",
                PaymentMethod = entry.PaymentMethod,
                FulfillmentBranch = entry.FulfillmentBranch,
                DeliveryOption = entry.DeliveryOption,
                CreatedAtUtc = entry.CreatedAtUtc,
                TotalAmount = entry.TotalAmount,
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
            .ToListAsync(cancellationToken);

        var vm = new OrdersIndexViewModel
        {
            ShowSuccessBanner = !string.IsNullOrWhiteSpace(order) &&
                (string.Equals(payment, "success", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(payment, "placed", StringComparison.OrdinalIgnoreCase)),
            HighlightOrderNumber = order ?? string.Empty,
            Orders = orders,
            CurrentPage = normalizedPage,
            PageSize = OrdersPageSize,
            TotalOrders = totalOrders
        };

        return View(vm);
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
        }
    }
}
