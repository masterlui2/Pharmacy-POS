using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmacyPOS.Data;
using PharmacyPOS.Models;
using PharmacyPOS.Models.Admin;
using PharmacyPOS.Services;

namespace PharmacyPOS.Controllers;

public class ModulesController(
    PharmacyPosDbContext dbContext,
    IMedicineService medicineService) : AdminController
{
    private const int DefaultPageSize = 10;
    private static readonly int[] AllowedPageSizes = [10, 25, 50];

    public IActionResult Pos(
        string? search,
        string? category,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        var inventory = medicineService.GetAll().ToList();
        var filteredMedicines = FilterMedicines(search, category).ToList();
        var pagination = BuildPagination(
            "Modules",
            nameof(Pos),
            page,
            pageSize,
            filteredMedicines.Count,
            ("search", search),
            ("category", category));
        var pagedMedicines = Paginate(filteredMedicines, pagination.CurrentPage, pagination.PageSize);

        var model = CreateModule(
            AdminModuleKind.Pos,
            "POS",
            "Fast checkout only: search inventory, set quantity and discount, then complete the transaction.")
            with
            {
                Search = search,
                CategoryFilter = category,
                Categories = inventory.Select(medicine => medicine.Category).Distinct().OrderBy(categoryName => categoryName).ToList(),
                Medicines = pagedMedicines,
                Pagination = pagination,
                Actions =
                [
                    ActionLink("Add Medicine", "Medicines", "Create", "bi-plus-circle", "primary", "Create a medicine record"),
                    ActionLink("Receipt", "Modules", nameof(Receipt), "bi-receipt-cutoff", "outline", "Open completed receipts")
                ],
                Metrics =
                [
                    Metric("Ready to sell", inventory.Count(medicine => medicine.Stock > 0).ToString("N0"), "Available for checkout", "bi-bag-check", "success"),
                    Metric("Low stock", inventory.Count(medicine => medicine.Stock > 0 && medicine.Stock <= 20).ToString("N0"), "Needs replenishment soon", "bi-exclamation-triangle", "warning"),
                    Metric("Out of stock", inventory.Count(medicine => medicine.Stock <= 0).ToString("N0"), "Unavailable at checkout", "bi-x-octagon", "danger"),
                    Metric("Visible rows", filteredMedicines.Count.ToString("N0"), "Current search and filter result", "bi-filter", "neutral")
                ]
            };

        return View("Module", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCounterSale(CounterSaleRequest request, CancellationToken cancellationToken)
    {
        var medicine = medicineService.GetById(request.MedicineId);
        if (medicine is null)
        {
            TempData["Error"] = "Selected medicine was not found.";
            return RedirectToAction(nameof(Pos));
        }

        if (request.Quantity <= 0)
        {
            TempData["Error"] = "Quantity must be greater than zero.";
            return RedirectToAction(nameof(Pos));
        }

        if (medicine.Stock < request.Quantity)
        {
            TempData["Error"] = $"{medicine.BrandName} only has {medicine.Stock} unit{Plural(medicine.Stock)} available.";
            return RedirectToAction(nameof(Pos), new { search = medicine.BrandName });
        }

        var paymentMethod = NormalizeCounterPaymentMethod(request.PaymentMethod);
        var subtotal = medicine.Price * request.Quantity;
        var discountPercent = Math.Clamp(request.DiscountPercent, 0m, 100m);
        var discount = Math.Round(subtotal * (discountPercent / 100m), 2, MidpointRounding.AwayFromZero);
        var taxableAmount = Math.Max(0m, subtotal - discount);
        var tax = Math.Round(taxableAmount * 0.12m, 2, MidpointRounding.AwayFromZero);
        var total = taxableAmount + tax;

        var order = new PharmacyOrder
        {
            OrderNumber = GenerateOrderNumber(),
            CustomerFullName = string.IsNullOrWhiteSpace(request.CustomerName)
                ? "Walk-in Customer"
                : request.CustomerName.Trim(),
            CustomerEmail = string.Empty,
            CustomerPhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber)
                ? "N/A"
                : request.PhoneNumber.Trim(),
            DeliveryAddress = "In-store POS counter",
            Landmark = string.Empty,
            AddressType = "Store",
            DeliveryOption = "Counter",
            PaymentMethod = paymentMethod,
            FulfillmentBranch = "SafeMed POS Counter",
            PrescriptionStatus = "NotRequired",
            OrderStatus = "Completed",
            RequiresPrescription = false,
            SubtotalAmount = subtotal,
            TaxAmount = tax,
            ShippingAmount = 0m,
            DiscountAmount = discount,
            TotalAmount = total,
            PromoCode = string.Empty,
            PrescriptionFilesJson = "[]",
            CreatedAtUtc = DateTime.UtcNow,
            Items =
            [
                new PharmacyOrderItem
                {
                    ProductId = medicine.Code,
                    ProductName = medicine.GenericName,
                    BrandName = medicine.BrandName,
                    ImageUrl = string.Empty,
                    UnitPrice = medicine.Price,
                    TaxAmount = Math.Round(medicine.Price * 0.12m, 2, MidpointRounding.AwayFromZero),
                    Quantity = request.Quantity,
                    RequiresPrescription = false
                }
            ]
        };

        var payment = new PaymentRecord
        {
            PharmacyOrder = order,
            PaymentMethod = paymentMethod,
            Status = "Paid",
            Amount = total,
            ReferenceNumber = GeneratePaymentReference(paymentMethod),
            Provider = "Counter",
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Orders.Add(order);
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync(cancellationToken);

        medicine.Stock -= request.Quantity;
        medicineService.Update(medicine);

        TempData["Success"] = $"Counter sale {order.OrderNumber} completed.";
        return RedirectToAction(nameof(Receipt), new { orderNumber = order.OrderNumber });
    }

    public async Task<IActionResult> Payment(
        string? search,
        string? method,
        string? status,
        int page = 1,
        int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var ordersQuery = dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Payment)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            ordersQuery = ordersQuery.Where(order =>
                order.OrderNumber.Contains(search) ||
                order.CustomerFullName.Contains(search) ||
                order.PaymentMethod.Contains(search) ||
                (order.Payment != null &&
                    (order.Payment.ReferenceNumber.Contains(search) ||
                     order.Payment.Provider.Contains(search))));
        }

        if (!string.IsNullOrWhiteSpace(method))
        {
            ordersQuery = ordersQuery.Where(order => order.PaymentMethod == method);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            ordersQuery = status.Equals("Paid", StringComparison.OrdinalIgnoreCase)
                ? ordersQuery.Where(order => order.Payment != null && order.Payment.Status == "Paid")
                : ordersQuery.Where(order => order.Payment == null || order.Payment.Status != "Paid");
        }

        var totalCount = await ordersQuery.CountAsync(cancellationToken);
        var pagination = BuildPagination(
            "Modules",
            nameof(Payment),
            page,
            pageSize,
            totalCount,
            ("search", search),
            ("method", method),
            ("status", status));

        var payments = await ordersQuery
            .OrderByDescending(order => order.CreatedAtUtc)
            .Skip((pagination.CurrentPage - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(order => new AdminPaymentRowViewModel
            {
                PaymentId = order.Payment != null ? order.Payment.Id : 0,
                OrderNumber = order.OrderNumber,
                CustomerName = order.CustomerFullName,
                PaymentMethod = order.PaymentMethod,
                Status = order.Payment != null && order.Payment.Status == "Paid" ? "Paid" : "Unpaid",
                OrderStatus = order.OrderStatus,
                Amount = order.Payment != null && order.Payment.Amount > 0 ? order.Payment.Amount : order.TotalAmount,
                ReferenceNumber = order.Payment != null && !string.IsNullOrWhiteSpace(order.Payment.ReferenceNumber)
                    ? order.Payment.ReferenceNumber
                    : "Pending collection",
                Provider = order.Payment != null && !string.IsNullOrWhiteSpace(order.Payment.Provider)
                    ? order.Payment.Provider
                    : order.PaymentMethod == "CashOnDelivery"
                        ? "Delivery collection"
                        : "Manual",
                CreatedAtUtc = order.Payment != null ? order.Payment.CreatedAtUtc : order.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var totalsQuery = dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Payment)
            .AsQueryable();

        var totalCollected = await totalsQuery
            .Where(order => order.Payment != null && order.Payment.Status == "Paid")
            .SumAsync(order => (decimal?)order.Payment!.Amount, cancellationToken) ?? 0m;
        var unpaidCount = await totalsQuery
            .CountAsync(order => order.Payment == null || order.Payment.Status != "Paid", cancellationToken);
        var codCount = await totalsQuery
            .CountAsync(order => order.PaymentMethod == "CashOnDelivery", cancellationToken);
        var methodsUsed = await totalsQuery
            .Select(order => order.PaymentMethod)
            .Distinct()
            .CountAsync(cancellationToken);

        var model = CreateModule(
            AdminModuleKind.Payment,
            "Payment",
            "Review payment methods, including cash on delivery, with clear paid and unpaid transaction status.")
            with
            {
                Search = search,
                PaymentFilter = method,
                StatusFilter = status,
                Payments = payments,
                Pagination = pagination,
                Actions =
                [
                    ActionLink("Export Sales", "Modules", nameof(ExportSalesCsv), "bi-download", "primary", "Export transaction history as CSV"),
                    ActionLink("Receipts", "Modules", nameof(Receipt), "bi-receipt-cutoff", "outline", "Open printable receipts")
                ],
                Metrics =
                [
                    Metric("Collected", Currency(totalCollected), "Paid transaction total", "bi-cash-stack", "success"),
                    Metric("Unpaid", unpaidCount.ToString("N0"), "Pending or COD collection", "bi-hourglass-split", "warning"),
                    Metric("COD orders", codCount.ToString("N0"), "Cash on Delivery transactions", "bi-truck", "neutral"),
                    Metric("Methods used", methodsUsed.ToString("N0"), "Distinct payment methods", "bi-credit-card-2-front", "neutral")
                ]
            };

        return View("Module", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePaymentStatus(PaymentStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        var normalizedStatus = NormalizePaymentStatus(request.Status);
        if (string.IsNullOrWhiteSpace(normalizedStatus))
        {
            TempData["Error"] = "Choose a valid payment status.";
            return RedirectToAction(nameof(Payment));
        }

        var payment = await dbContext.Payments
            .Include(entry => entry.PharmacyOrder)
            .FirstOrDefaultAsync(entry => entry.Id == request.PaymentId, cancellationToken);

        if (payment is null)
        {
            TempData["Error"] = "Payment record was not found.";
            return RedirectToAction(nameof(Payment));
        }

        payment.Status = normalizedStatus;
        if (payment.PharmacyOrder is not null)
        {
            payment.PharmacyOrder.OrderStatus = normalizedStatus switch
            {
                "Paid" when payment.PharmacyOrder.DeliveryOption == "Counter" => "Completed",
                "Paid" => "Processing",
                "Failed" => "PaymentFailed",
                "Refunded" => "Refunded",
                "PendingCollection" => "Pending",
                _ => "AwaitingPayment"
            };
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        TempData["Success"] = $"Payment {payment.ReferenceNumber} is now {normalizedStatus}.";
        return RedirectToAction(nameof(Payment));
    }

    public async Task<IActionResult> Receipt(
        string? search,
        string? orderNumber,
        int page = 1,
        int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var ordersQuery = dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Payment)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            ordersQuery = ordersQuery.Where(order =>
                order.OrderNumber.Contains(search) ||
                order.CustomerFullName.Contains(search) ||
                order.CustomerPhoneNumber.Contains(search));
        }

        var totalCount = await ordersQuery.CountAsync(cancellationToken);
        var pagination = BuildPagination(
            "Modules",
            nameof(Receipt),
            page,
            pageSize,
            totalCount,
            ("search", search),
            ("orderNumber", orderNumber));

        var pagedReceipts = await ordersQuery
            .OrderByDescending(order => order.CreatedAtUtc)
            .Skip((pagination.CurrentPage - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(order => new AdminReceiptSummaryViewModel
            {
                OrderNumber = order.OrderNumber,
                CustomerName = order.CustomerFullName,
                TotalAmount = order.TotalAmount,
                CreatedAtUtc = order.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var selectedOrderNumber = string.IsNullOrWhiteSpace(orderNumber)
            ? pagedReceipts.FirstOrDefault()?.OrderNumber
            : orderNumber;

        AdminReceiptViewModel? receipt = null;
        if (!string.IsNullOrWhiteSpace(selectedOrderNumber))
        {
            var selectedOrder = await dbContext.Orders
                .AsNoTracking()
                .Include(order => order.Items)
                .Include(order => order.Payment)
                .FirstOrDefaultAsync(order => order.OrderNumber == selectedOrderNumber, cancellationToken);
            receipt = selectedOrder is null ? null : BuildReceipt(selectedOrder);
        }

        var model = CreateModule(
            AdminModuleKind.Receipt,
            "Receipt",
            "Find a completed order, review the summary, then print or download the receipt.")
            with
            {
                Search = search,
                SelectedOrderNumber = selectedOrderNumber,
                ReceiptOrders = pagedReceipts,
                Receipt = receipt,
                Pagination = pagination,
                Actions =
                [
                    ActionLink("Sales History", "Modules", nameof(SalesHistory), "bi-clock-history", "outline", "Find an order from sales history")
                ],
                Metrics =
                [
                    Metric("Receipts", totalCount.ToString("N0"), "Matching the current search", "bi-receipt", "neutral"),
                    Metric("Selected total", receipt is null ? Currency(0m) : Currency(receipt.TotalAmount), receipt?.OrderNumber ?? "No order selected", "bi-printer", "success"),
                    Metric("Receipt items", (receipt?.Items.Count ?? 0).ToString("N0"), "Line items on current receipt", "bi-list-check", "neutral"),
                    Metric("Payment status", receipt?.PaymentStatus ?? "None", receipt?.PaymentMethod ?? "No payment record", "bi-credit-card-2-front", "neutral")
                ]
            };

        return View("Module", model);
    }

    [HttpGet]
    public async Task<IActionResult> ReceiptPreview(string orderNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return BadRequest();
        }

        var order = await dbContext.Orders
            .AsNoTracking()
            .Include(entry => entry.Items)
            .Include(entry => entry.Payment)
            .FirstOrDefaultAsync(entry => entry.OrderNumber == orderNumber, cancellationToken);

        if (order is null)
        {
            return NotFound();
        }

        return PartialView("Partials/_ReceiptPreview", BuildReceipt(order));
    }

    [HttpGet]
    public async Task<IActionResult> PrintReceipt(string orderNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return RedirectToAction(nameof(Receipt));
        }

        var order = await dbContext.Orders
            .AsNoTracking()
            .Include(entry => entry.Items)
            .Include(entry => entry.Payment)
            .FirstOrDefaultAsync(entry => entry.OrderNumber == orderNumber, cancellationToken);

        if (order is null)
        {
            return NotFound();
        }

        return View("ReceiptPrint", BuildReceipt(order));
    }

    public async Task<IActionResult> DownloadReceipt(string orderNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return RedirectToAction(nameof(Receipt));
        }

        var order = await dbContext.Orders
            .AsNoTracking()
            .Include(entry => entry.Items)
            .Include(entry => entry.Payment)
            .FirstOrDefaultAsync(entry => entry.OrderNumber == orderNumber, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        var receipt = BuildReceipt(order);
        var builder = new StringBuilder();
        builder.AppendLine("SafeMed Pharmacy");
        builder.AppendLine(receipt.Branch);
        builder.AppendLine($"Order: {receipt.OrderNumber}");
        builder.AppendLine($"Customer: {receipt.CustomerName}");
        builder.AppendLine($"Payment: {receipt.PaymentMethod} - {receipt.PaymentStatus}");
        builder.AppendLine($"Reference: {receipt.ReferenceNumber}");
        builder.AppendLine($"Created: {receipt.CreatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine();
        builder.AppendLine("Items");
        foreach (var item in receipt.Items)
        {
            builder.AppendLine($"{item.Quantity} x {item.ProductName} ({item.BrandName}) @ {item.UnitPrice:0.00} = {item.LineTotal:0.00}");
        }

        builder.AppendLine();
        builder.AppendLine($"Subtotal: {receipt.SubtotalAmount:0.00}");
        builder.AppendLine($"Tax: {receipt.TaxAmount:0.00}");
        builder.AppendLine($"Delivery: {receipt.ShippingAmount:0.00}");
        builder.AppendLine($"Discount: -{receipt.DiscountAmount:0.00}");
        builder.AppendLine($"Total: {receipt.TotalAmount:0.00}");

        return new FileContentResult(Encoding.UTF8.GetBytes(builder.ToString()), "text/plain")
        {
            FileDownloadName = $"receipt-{receipt.OrderNumber}.txt"
        };
    }

    public async Task<IActionResult> SalesHistory(
        string? search,
        DateTime? from,
        DateTime? to,
        string? status,
        string? method,
        int page = 1,
        int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var ordersQuery = dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Include(order => order.Payment)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            ordersQuery = ordersQuery.Where(order =>
                order.OrderNumber.Contains(search) ||
                order.CustomerFullName.Contains(search) ||
                order.CustomerPhoneNumber.Contains(search));
        }

        if (from.HasValue)
        {
            ordersQuery = ordersQuery.Where(order => order.CreatedAtUtc >= from.Value.Date);
        }

        if (to.HasValue)
        {
            ordersQuery = ordersQuery.Where(order => order.CreatedAtUtc < to.Value.Date.AddDays(1));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            ordersQuery = ordersQuery.Where(order => order.OrderStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(method))
        {
            ordersQuery = ordersQuery.Where(order => order.PaymentMethod == method);
        }

        var totalCount = await ordersQuery.CountAsync(cancellationToken);
        var pagination = BuildPagination(
            "Modules",
            nameof(SalesHistory),
            page,
            pageSize,
            totalCount,
            ("search", search),
            ("from", from?.ToString("yyyy-MM-dd")),
            ("to", to?.ToString("yyyy-MM-dd")),
            ("status", status),
            ("method", method));

        var filteredOrders = await ordersQuery
            .OrderByDescending(order => order.CreatedAtUtc)
            .Skip((pagination.CurrentPage - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        var totalsQuery = dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            totalsQuery = totalsQuery.Where(order =>
                order.OrderNumber.Contains(search) ||
                order.CustomerFullName.Contains(search) ||
                order.CustomerPhoneNumber.Contains(search));
        }

        if (from.HasValue)
        {
            totalsQuery = totalsQuery.Where(order => order.CreatedAtUtc >= from.Value.Date);
        }

        if (to.HasValue)
        {
            totalsQuery = totalsQuery.Where(order => order.CreatedAtUtc < to.Value.Date.AddDays(1));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            totalsQuery = totalsQuery.Where(order => order.OrderStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(method))
        {
            totalsQuery = totalsQuery.Where(order => order.PaymentMethod == method);
        }

        var totalRevenue = await totalsQuery.SumAsync(order => (decimal?)order.TotalAmount, cancellationToken) ?? 0m;
        var totalItems = await totalsQuery.SelectMany(order => order.Items).SumAsync(item => (int?)item.Quantity, cancellationToken) ?? 0;

        var model = CreateModule(
            AdminModuleKind.SalesHistory,
            "Sales History",
            "Search and filter completed or in-progress transactions with consistent paging and export controls.")
            with
            {
                Search = search,
                FromDate = from,
                ToDate = to,
                StatusFilter = status,
                PaymentFilter = method,
                Orders = filteredOrders.Select(BuildOrderRow).ToList(),
                Pagination = pagination,
                Actions =
                [
                    ActionLink("Export CSV", "Modules", nameof(ExportSalesCsv), "bi-download", "primary", "Export the filtered transaction list"),
                    ActionLink("Reports", "Modules", nameof(Reports), "bi-bar-chart-line", "outline", "Open analytics")
                ],
                Metrics =
                [
                    Metric("Revenue", Currency(totalRevenue), "Across the current filter set", "bi-graph-up-arrow", "success"),
                    Metric("Orders", totalCount.ToString("N0"), "Matching transactions", "bi-bag", "neutral"),
                    Metric("Items sold", totalItems.ToString("N0"), "Total unit quantity", "bi-box2-heart", "neutral"),
                    Metric("Average order", Currency(totalCount == 0 ? 0m : totalRevenue / totalCount), "Revenue per order", "bi-calculator", "neutral")
                ]
            };

        return View("Module", model);
    }

    public async Task<IActionResult> Reports(
        int page = 1,
        int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var monthStartUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1);

        var totalOrders = await dbContext.Orders.AsNoTracking().CountAsync(cancellationToken);
        var totalRevenue = await dbContext.Orders.AsNoTracking()
            .SumAsync(order => (decimal?)order.TotalAmount, cancellationToken) ?? 0m;
        var monthlyRevenue = await dbContext.Orders.AsNoTracking()
            .Where(order => order.CreatedAtUtc >= monthStartUtc)
            .SumAsync(order => (decimal?)order.TotalAmount, cancellationToken) ?? 0m;
        var paidRevenue = await dbContext.Payments.AsNoTracking()
            .Where(payment => payment.Status == "Paid")
            .SumAsync(payment => (decimal?)payment.Amount, cancellationToken) ?? 0m;

        var revenueByPaymentMethod = await BuildRevenueBreakdownAsync(cancellationToken);
        var topProducts = await BuildTopProductsAsync(cancellationToken);
        var pagination = BuildPagination(
            "Modules",
            nameof(Reports),
            page,
            pageSize,
            topProducts.Count);

        var model = CreateModule(
            AdminModuleKind.Reports,
            "Reports",
            "Key metrics and exportable reports only, with the layout reduced to operational summaries.")
            with
            {
                RevenueByPaymentMethod = revenueByPaymentMethod,
                TopProducts = Paginate(topProducts, pagination.CurrentPage, pagination.PageSize),
                Pagination = pagination,
                Actions =
                [
                    ActionLink("Export Sales", "Modules", nameof(ExportSalesCsv), "bi-download", "primary", "Download sales report as CSV"),
                    ActionLink("Export Inventory", "Modules", nameof(ExportInventoryCsv), "bi-box-arrow-down", "outline", "Download inventory report as CSV")
                ],
                Metrics =
                [
                    Metric("Total revenue", Currency(totalRevenue), $"{totalOrders} lifetime order{Plural(totalOrders)}", "bi-currency-exchange", "success"),
                    Metric("This month", Currency(monthlyRevenue), monthStartUtc.ToString("MMM yyyy"), "bi-calendar2-check", "neutral"),
                    Metric("Paid revenue", Currency(paidRevenue), "Collected payment total", "bi-cash-coin", "success"),
                    Metric("Top products", topProducts.Count.ToString("N0"), "Best sellers by quantity", "bi-trophy", "warning")
                ]
            };

        return View("Module", model);
    }

    public IActionResult StockAlerts(
        string? search,
        string? status,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        var medicines = medicineService.GetAll().ToList();
        var today = DateTime.Today;

        var alerts = medicines
            .Where(medicine => medicine.Stock <= 20 || medicine.ExpiryDate <= today.AddDays(60))
            .Select(medicine => BuildInventoryAlert(medicine, today))
            .Where(alert =>
                string.IsNullOrWhiteSpace(search) ||
                alert.BrandName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                alert.GenericName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                alert.Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                alert.Code.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Where(alert =>
                string.IsNullOrWhiteSpace(status) ||
                string.Equals(alert.Reason, status, StringComparison.OrdinalIgnoreCase))
            .OrderBy(alert => alert.Stock > 0)
            .ThenBy(alert => alert.Stock)
            .ThenBy(alert => alert.ExpiryDate)
            .ToList();

        var pagination = BuildPagination(
            "Modules",
            nameof(StockAlerts),
            page,
            pageSize,
            alerts.Count,
            ("search", search),
            ("status", status));

        var model = CreateModule(
            AdminModuleKind.StockAlerts,
            "Stock Alerts",
            "Low stock and expiring items, with quick restock actions and consistent paging.")
            with
            {
                Search = search,
                StatusFilter = status,
                InventoryAlerts = Paginate(alerts, pagination.CurrentPage, pagination.PageSize),
                Pagination = pagination,
                Actions =
                [
                    ActionLink("Inventory", "Medicines", "Index", "bi-capsule", "outline", "Open full inventory list"),
                    ActionLink("Export Inventory", "Modules", nameof(ExportInventoryCsv), "bi-download", "outline", "Export current stock data")
                ],
                Metrics =
                [
                    Metric("Out of stock", medicines.Count(medicine => medicine.Stock <= 0).ToString("N0"), "Immediate restock needed", "bi-exclamation-octagon", "danger"),
                    Metric("Low stock", medicines.Count(medicine => medicine.Stock > 0 && medicine.Stock <= 20).ToString("N0"), "20 units or fewer", "bi-exclamation-triangle", "warning"),
                    Metric("Expiring soon", medicines.Count(medicine => medicine.ExpiryDate <= today.AddDays(60)).ToString("N0"), "Within 60 days", "bi-calendar-x", "warning"),
                    Metric("Visible alerts", alerts.Count.ToString("N0"), "Current filter result", "bi-filter", "neutral")
                ]
            };

        return View("Module", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Restock(StockAdjustmentRequest request)
    {
        var medicine = medicineService.GetById(request.MedicineId);
        if (medicine is null)
        {
            TempData["Error"] = "Selected medicine was not found.";
            return RedirectToAction(nameof(StockAlerts));
        }

        if (request.Quantity <= 0)
        {
            TempData["Error"] = "Restock quantity must be greater than zero.";
            return RedirectToAction(nameof(StockAlerts));
        }

        medicine.Stock += request.Quantity;
        medicineService.Update(medicine);
        TempData["Success"] = $"{medicine.BrandName} stock increased by {request.Quantity} unit{Plural(request.Quantity)}.";
        return RedirectToAction(nameof(StockAlerts));
    }

    public IActionResult Storefront(
        string? search,
        string? status,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        var inventory = medicineService.GetAll().ToList();
        var filteredMedicines = FilterMedicines(search)
            .Where(medicine =>
                string.IsNullOrWhiteSpace(status) ||
                (string.Equals(status, "Available", StringComparison.OrdinalIgnoreCase) && medicine.Stock > 0) ||
                (string.Equals(status, "Low Stock", StringComparison.OrdinalIgnoreCase) && medicine.Stock > 0 && medicine.Stock <= 20) ||
                (string.Equals(status, "Unavailable", StringComparison.OrdinalIgnoreCase) && medicine.Stock <= 0))
            .ToList();

        var pagination = BuildPagination(
            "Modules",
            nameof(Storefront),
            page,
            pageSize,
            filteredMedicines.Count,
            ("search", search),
            ("status", status));

        var model = CreateModule(
            AdminModuleKind.Storefront,
            "Storefront",
            "Customer-facing product visibility, pricing, and availability with minimal operational controls.")
            with
            {
                Search = search,
                StatusFilter = status,
                Medicines = Paginate(filteredMedicines, pagination.CurrentPage, pagination.PageSize),
                Pagination = pagination,
                Actions =
                [
                    ActionLink("Open Storefront", "Home", "Index", "bi-box-arrow-up-right", "primary", "Open the customer storefront"),
                    ActionLink("Add Medicine", "Medicines", "Create", "bi-plus-circle", "outline", "Create a new customer-facing product")
                ],
                Metrics =
                [
                    Metric("Visible products", inventory.Count(medicine => medicine.Stock > 0).ToString("N0"), "Available for customers", "bi-shop-window", "success"),
                    Metric("Unavailable", inventory.Count(medicine => medicine.Stock <= 0).ToString("N0"), "Out-of-stock products", "bi-eye-slash", "danger"),
                    Metric("Low stock", inventory.Count(medicine => medicine.Stock > 0 && medicine.Stock <= 20).ToString("N0"), "Needs replenishment soon", "bi-exclamation-triangle", "warning"),
                    Metric("Visible rows", filteredMedicines.Count.ToString("N0"), "Current search and filter result", "bi-filter", "neutral")
                ]
            };

        return View("Module", model);
    }

    public async Task<IActionResult> AdminUsers(
        string? search,
        string? role,
        int page = 1,
        int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var usersQuery = dbContext.Accounts
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            usersQuery = usersQuery.Where(account =>
                account.FirstName.Contains(search) ||
                account.LastName.Contains(search) ||
                account.Email.Contains(search) ||
                account.PhoneNumber.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            usersQuery = usersQuery.Where(account => account.Role == role);
        }

        var totalCount = await usersQuery.CountAsync(cancellationToken);
        var pagination = BuildPagination(
            "Modules",
            nameof(AdminUsers),
            page,
            pageSize,
            totalCount,
            ("search", search),
            ("role", role));

        var users = await usersQuery
            .OrderBy(account => account.Role)
            .ThenBy(account => account.LastName)
            .Skip((pagination.CurrentPage - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(account => new AdminUserRowViewModel
            {
                Id = account.Id,
                Name = (account.FirstName + " " + account.LastName).Trim(),
                Email = account.Email,
                PhoneNumber = account.PhoneNumber,
                Role = account.Role,
                OrdersCount = account.Orders.Count,
                LifetimeSpend = account.Orders.Sum(order => (decimal?)order.TotalAmount) ?? 0m,
                CreatedAtUtc = account.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var allUsers = await dbContext.Accounts.AsNoTracking().ToListAsync(cancellationToken);
        var monthStartUtc = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var totalSpend = await dbContext.Orders.AsNoTracking()
            .SumAsync(order => (decimal?)order.TotalAmount, cancellationToken) ?? 0m;

        var model = CreateModule(
            AdminModuleKind.AdminUsers,
            "Admin User",
            "Search and manage admin access with a compact account table and consistent paging.")
            with
            {
                Search = search,
                RoleFilter = role,
                Users = users,
                Pagination = pagination,
                Actions =
                [
                    ActionLink("Dashboard", "Dashboard", "Index", "bi-grid-1x2", "outline", "Return to admin overview")
                ],
                Metrics =
                [
                    Metric("Accounts", allUsers.Count.ToString("N0"), $"{allUsers.Count(user => string.Equals(user.Role, "Customer", StringComparison.OrdinalIgnoreCase))} customers", "bi-people", "neutral"),
                    Metric("Admins", allUsers.Count(user => string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase)).ToString("N0"), "Accounts with admin access", "bi-shield-lock", "danger"),
                    Metric("New this month", allUsers.Count(user => user.CreatedAtUtc >= monthStartUtc).ToString("N0"), monthStartUtc.ToString("MMM yyyy"), "bi-person-plus", "success"),
                    Metric("Customer spend", Currency(totalSpend), "Total account-linked orders", "bi-cash-coin", "success")
                ]
            };

        return View("Module", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAdminRole(AdminRoleUpdateRequest request, CancellationToken cancellationToken)
    {
        var normalizedRole = request.Role.Trim();
        if (!string.Equals(normalizedRole, "Admin", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(normalizedRole, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Choose a valid role.";
            return RedirectToAction(nameof(AdminUsers));
        }

        var account = await dbContext.Accounts
            .FirstOrDefaultAsync(candidate => candidate.Id == request.AccountId, cancellationToken);
        if (account is null)
        {
            TempData["Error"] = "Account was not found.";
            return RedirectToAction(nameof(AdminUsers));
        }

        if (string.Equals(account.Role, "Admin", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(normalizedRole, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            var adminCount = await dbContext.Accounts
                .CountAsync(candidate => candidate.Role == "Admin", cancellationToken);
            if (adminCount <= 1)
            {
                TempData["Error"] = "At least one admin account must remain active.";
                return RedirectToAction(nameof(AdminUsers));
            }
        }

        account.Role = string.Equals(normalizedRole, "Admin", StringComparison.OrdinalIgnoreCase)
            ? "Admin"
            : "Customer";
        await dbContext.SaveChangesAsync(cancellationToken);

        TempData["Success"] = $"{account.Email} role updated to {account.Role}.";
        return RedirectToAction(nameof(AdminUsers));
    }

    public IActionResult ExportInventoryCsv()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Code,BrandName,GenericName,Category,Supplier,Price,Stock,ExpiryDate,Status");

        foreach (var medicine in medicineService.GetAll())
        {
            builder.AppendLine(string.Join(",",
                Csv(medicine.Code),
                Csv(medicine.BrandName),
                Csv(medicine.GenericName),
                Csv(medicine.Category),
                Csv(medicine.Supplier),
                medicine.Price.ToString("0.00"),
                medicine.Stock.ToString(),
                medicine.ExpiryDate.ToString("yyyy-MM-dd"),
                Csv(medicine.Status)));
        }

        return CsvFile(builder.ToString(), $"safemed-inventory-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    public async Task<IActionResult> ExportSalesCsv(
        string? search,
        DateTime? from,
        DateTime? to,
        string? status,
        string? method,
        CancellationToken cancellationToken)
    {
        var ordersQuery = dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Include(order => order.Payment)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            ordersQuery = ordersQuery.Where(order =>
                order.OrderNumber.Contains(search) ||
                order.CustomerFullName.Contains(search) ||
                order.CustomerPhoneNumber.Contains(search));
        }

        if (from.HasValue)
        {
            ordersQuery = ordersQuery.Where(order => order.CreatedAtUtc >= from.Value.Date);
        }

        if (to.HasValue)
        {
            ordersQuery = ordersQuery.Where(order => order.CreatedAtUtc < to.Value.Date.AddDays(1));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            ordersQuery = ordersQuery.Where(order => order.OrderStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(method))
        {
            ordersQuery = ordersQuery.Where(order => order.PaymentMethod == method);
        }

        var orders = await ordersQuery
            .OrderByDescending(order => order.CreatedAtUtc)
            .Take(1000)
            .ToListAsync(cancellationToken);
        var builder = new StringBuilder();
        builder.AppendLine("OrderNumber,Customer,PaymentMethod,PaymentStatus,OrderStatus,Items,Subtotal,Tax,Delivery,Discount,Total,CreatedAt");

        foreach (var order in orders)
        {
            builder.AppendLine(string.Join(",",
                Csv(order.OrderNumber),
                Csv(order.CustomerFullName),
                Csv(order.PaymentMethod),
                Csv(order.Payment?.Status ?? "Pending"),
                Csv(order.OrderStatus),
                order.Items.Sum(item => item.Quantity).ToString(),
                order.SubtotalAmount.ToString("0.00"),
                order.TaxAmount.ToString("0.00"),
                order.ShippingAmount.ToString("0.00"),
                order.DiscountAmount.ToString("0.00"),
                order.TotalAmount.ToString("0.00"),
                Csv(order.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"))));
        }

        return CsvFile(builder.ToString(), $"safemed-sales-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    private IEnumerable<Medicine> FilterMedicines(string? search, string? category = null)
    {
        var medicines = medicineService.GetAll().AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            medicines = medicines.Where(medicine =>
                medicine.Code.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                medicine.BrandName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                medicine.GenericName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                medicine.Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                medicine.Supplier.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            medicines = medicines.Where(medicine => medicine.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        return medicines.OrderBy(medicine => medicine.BrandName);
    }

    private static List<T> Paginate<T>(IReadOnlyList<T> source, int page, int pageSize) =>
        source.Skip((page - 1) * pageSize).Take(pageSize).ToList();

    private static AdminSalesOrderRowViewModel BuildOrderRow(PharmacyOrder order) =>
        new()
        {
            OrderNumber = order.OrderNumber,
            CustomerName = order.CustomerFullName,
            PaymentMethod = order.PaymentMethod,
            PaymentStatus = order.Payment?.Status ?? "Pending",
            OrderStatus = order.OrderStatus,
            TotalAmount = order.TotalAmount,
            ItemsCount = order.Items.Sum(item => item.Quantity),
            CreatedAtUtc = order.CreatedAtUtc
        };

    private static AdminReceiptViewModel BuildReceipt(PharmacyOrder order) =>
        new()
        {
            OrderNumber = order.OrderNumber,
            CustomerName = order.CustomerFullName,
            CustomerPhoneNumber = order.CustomerPhoneNumber,
            PaymentMethod = order.PaymentMethod,
            PaymentStatus = order.Payment?.Status ?? "Pending",
            ReferenceNumber = order.Payment?.ReferenceNumber ?? string.Empty,
            Branch = order.FulfillmentBranch,
            SubtotalAmount = order.SubtotalAmount,
            TaxAmount = order.TaxAmount,
            ShippingAmount = order.ShippingAmount,
            DiscountAmount = order.DiscountAmount,
            TotalAmount = order.TotalAmount,
            CreatedAtUtc = order.CreatedAtUtc,
            Items = order.Items
                .OrderBy(item => item.Id)
                .Select(item => new AdminReceiptItemViewModel
                {
                    ProductName = item.ProductName,
                    BrandName = item.BrandName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                })
                .ToList()
        };

    private async Task<List<AdminRevenueBreakdownViewModel>> BuildRevenueBreakdownAsync(CancellationToken cancellationToken)
    {
        var raw = await dbContext.Orders
            .AsNoTracking()
            .GroupBy(order => order.PaymentMethod)
            .Select(group => new
            {
                Label = group.Key,
                Amount = group.Sum(order => order.TotalAmount)
            })
            .OrderByDescending(entry => entry.Amount)
            .ToListAsync(cancellationToken);

        var total = raw.Sum(entry => entry.Amount);
        return raw.Select(entry => new AdminRevenueBreakdownViewModel
            {
                Label = string.IsNullOrWhiteSpace(entry.Label) ? "Unknown" : entry.Label,
                Amount = entry.Amount,
                Percentage = total <= 0m ? 0 : (double)(entry.Amount / total * 100m)
            })
            .ToList();
    }

    private async Task<List<AdminTopProductViewModel>> BuildTopProductsAsync(CancellationToken cancellationToken) =>
        await dbContext.OrderItems
            .AsNoTracking()
            .GroupBy(item => item.ProductName)
            .Select(group => new AdminTopProductViewModel
            {
                ProductName = group.Key,
                QuantitySold = group.Sum(item => item.Quantity),
                Revenue = group.Sum(item => item.Quantity * item.UnitPrice)
            })
            .OrderByDescending(item => item.QuantitySold)
            .ThenByDescending(item => item.Revenue)
            .Take(8)
            .ToListAsync(cancellationToken);

    private static AdminInventoryAlertViewModel BuildInventoryAlert(Medicine medicine, DateTime today)
    {
        var isExpiredOrExpiring = medicine.ExpiryDate <= today.AddDays(60);
        var reason = medicine.Stock <= 0
            ? "Out of Stock"
            : medicine.Stock <= 20
                ? "Low Stock"
                : "Expiring Soon";
        var severity = reason switch
        {
            "Out of Stock" => "danger",
            _ => "warning"
        };

        return new AdminInventoryAlertViewModel
        {
            MedicineId = medicine.Id,
            Code = medicine.Code,
            BrandName = medicine.BrandName,
            GenericName = medicine.GenericName,
            Category = medicine.Category,
            Stock = medicine.Stock,
            ExpiryDate = medicine.ExpiryDate,
            Status = medicine.Status,
            Severity = severity,
            Reason = reason
        };
    }

    private static AdminModuleViewModel CreateModule(
        AdminModuleKind kind,
        string title,
        string description) =>
        new()
        {
            Kind = kind,
            Title = title,
            Description = description,
            WorkflowSteps =
            [
                "List View",
                "Create/Add",
                "Edit",
                "View Details",
                "Actions"
            ]
        };

    private static AdminMetricCardViewModel Metric(
        string label,
        string value,
        string hint,
        string icon,
        string tone) =>
        new()
        {
            Label = label,
            Value = value,
            Hint = hint,
            Icon = icon,
            Tone = tone
        };

    private static AdminModuleActionViewModel ActionLink(
        string label,
        string controller,
        string action,
        string icon,
        string style,
        string tooltip) =>
        new()
        {
            Label = label,
            Controller = controller,
            Action = action,
            Icon = icon,
            Style = style,
            Tooltip = tooltip
        };

    private static AdminPaginationViewModel BuildPagination(
        string controller,
        string action,
        int requestedPage,
        int requestedPageSize,
        int totalItems,
        params (string Key, string? Value)[] routeValues)
    {
        var normalizedPageSize = NormalizePageSize(requestedPageSize);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)normalizedPageSize));
        var currentPage = Math.Clamp(requestedPage, 1, totalPages);

        return new AdminPaginationViewModel
        {
            Controller = controller,
            Action = action,
            CurrentPage = currentPage,
            PageSize = normalizedPageSize,
            TotalItems = totalItems,
            RouteValues = routeValues
                .Where(item => !string.IsNullOrWhiteSpace(item.Value))
                .ToDictionary(item => item.Key, item => item.Value!)
        };
    }

    private static int NormalizePageSize(int pageSize) =>
        AllowedPageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;

    private static FileContentResult CsvFile(string content, string fileName) =>
        new(Encoding.UTF8.GetBytes(content), "text/csv")
        {
            FileDownloadName = fileName
        };

    private static string Csv(string value)
    {
        var sanitized = value.Replace("\"", "\"\"");
        return sanitized.Contains(',') || sanitized.Contains('"') || sanitized.Contains('\n') || sanitized.Contains('\r')
            ? $"\"{sanitized}\""
            : sanitized;
    }

    private static string NormalizeCounterPaymentMethod(string paymentMethod) =>
        paymentMethod.Trim().ToLowerInvariant() switch
        {
            "gcash" => "GCash",
            "paymaya" => "PayMaya",
            "maya" => "PayMaya",
            "card" => "Card",
            _ => "Cash"
        };

    private static string NormalizePaymentStatus(string status) =>
        status.Trim().ToLowerInvariant() switch
        {
            "paid" => "Paid",
            "pendingcollection" => "PendingCollection",
            "awaitingpayment" => "AwaitingPayment",
            "redirectedtogateway" => "RedirectedToGateway",
            "failed" => "Failed",
            "refunded" => "Refunded",
            _ => string.Empty
        };

    private static string GenerateOrderNumber() =>
        $"POS-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

    private static string GeneratePaymentReference(string paymentMethod)
    {
        var prefix = paymentMethod.Trim().ToLowerInvariant() switch
        {
            "gcash" => "GCS",
            "paymaya" => "MYA",
            "card" => "CRD",
            _ => "CSH"
        };

        return $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}";
    }

    private static string Currency(decimal amount) => $"PHP {amount:N2}";

    private static string Plural(int count) => count == 1 ? string.Empty : "s";
}
