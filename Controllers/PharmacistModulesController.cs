using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmacyPOS.Data;
using PharmacyPOS.Helpers;
using PharmacyPOS.Models;
using PharmacyPOS.Models.Admin;
using PharmacyPOS.Models.Checkout;
using PharmacyPOS.Models.Security;
using PharmacyPOS.Services;

namespace PharmacyPOS.Controllers;

public sealed class PharmacistModulesController(
    PharmacyPosDbContext dbContext,
    IMedicineService medicineService,
    IFirebaseSyncService firebaseSyncService,
    IFirebaseOrderChatService firebaseOrderChatService,
    IPharmacistMessagingService messagingService,
    IAuditLogService auditLogService,
    IWebHostEnvironment environment,
    ILogger<PharmacistModulesController> logger) : PharmacistOnlyController
{
    private const int DefaultPageSize = 10;
    private const int MessageThreadLimit = 50;
    private static readonly int[] AllowedPageSizes = [10, 25, 50];

    public IActionResult Index() => RedirectToAction(nameof(Prescriptions));

    public async Task<IActionResult> Prescriptions(
        string? search,
        string? status,
        int page = 1,
        int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var ordersQuery = dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Include(order => order.Payment)
            .Where(order => order.RequiresPrescription);

        if (!string.IsNullOrWhiteSpace(search))
        {
            ordersQuery = ordersQuery.Where(order =>
                order.OrderNumber.Contains(search) ||
                order.CustomerFullName.Contains(search) ||
                order.CustomerPhoneNumber.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            ordersQuery = ordersQuery.Where(order => order.PrescriptionStatus == status);
        }

        var totalCount = await ordersQuery.CountAsync(cancellationToken);
        var pagination = BuildPagination(
            nameof(Prescriptions),
            page,
            pageSize,
            totalCount,
            ("search", search),
            ("status", status));

        var orders = await ordersQuery
            .OrderByDescending(order => order.CreatedAtUtc)
            .Skip((pagination.CurrentPage - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        var orderRows = orders.Select(order =>
            {
                var prescriptionFiles = BuildPrescriptionAssets(order.PrescriptionFilesJson, environment);
                return new PrescriptionQueueItemViewModel
                {
                    OrderNumber = order.OrderNumber,
                    CustomerName = order.CustomerFullName,
                    DeliveryAddress = order.DeliveryAddress,
                    PaymentMethod = order.PaymentMethod,
                    PaymentStatus = order.Payment != null ? order.Payment.Status : "AwaitingPayment",
                    PrescriptionStatus = order.PrescriptionStatus,
                    OrderStatus = order.OrderStatus,
                    ItemsCount = order.Items.Sum(item => item.Quantity),
                    PrescriptionFileCount = prescriptionFiles.Count,
                    HasPreviewablePrescription = prescriptionFiles.Count > 0,
                    CreatedAtUtc = order.CreatedAtUtc
                };
            })
            .ToList();

        var totalValidated = await dbContext.Orders
            .AsNoTracking()
            .CountAsync(order =>
                order.RequiresPrescription &&
                (order.PrescriptionStatus == "Approved" || order.PrescriptionStatus == "Valid"),
                cancellationToken);
        var totalPending = await dbContext.Orders
            .AsNoTracking()
            .CountAsync(order =>
                order.RequiresPrescription &&
                order.PrescriptionStatus != "Approved" &&
                order.PrescriptionStatus != "Valid" &&
                order.PrescriptionStatus != "NotRequired" &&
                order.PrescriptionStatus != "Rejected" &&
                order.PrescriptionStatus != "Invalid",
                cancellationToken);
        var totalRejected = await dbContext.Orders
            .AsNoTracking()
            .CountAsync(order =>
                order.RequiresPrescription &&
                (order.PrescriptionStatus == "Rejected" || order.PrescriptionStatus == "Invalid"),
                cancellationToken);

        var model = CreateModule(
            AdminModuleKind.PrescriptionValidation,
            "Validate Prescriptions",
            "Approve or reject prescription-dependent orders before any payment, receipt, or fulfillment step.")
            with
            {
                Search = search,
                StatusFilter = status,
                PrescriptionQueue = orderRows,
                Pagination = pagination,
                Actions =
                [
                    ActionLink("Sales Queue", "PharmacistModules", nameof(Sales), "bi-cart3", "outline", "Open pharmacist checkout"),
                    ActionLink("Payments", "PharmacistModules", nameof(Payments), "bi-credit-card-2-front", "outline", "Open payment handling")
                ],
                Metrics =
                [
                    Metric("Pending review", totalPending.ToString("N0"), "Requires pharmacist validation", "bi-hourglass-split", "warning"),
                    Metric("Validated", totalValidated.ToString("N0"), "Approved or valid prescriptions", "bi-patch-check", "success"),
                    Metric("Rejected", totalRejected.ToString("N0"), "Rejected or invalid submissions", "bi-x-octagon", "danger"),
                    Metric("Visible queue", totalCount.ToString("N0"), "Current filter result", "bi-filter", "neutral")
                ]
            };

        return View("~/Views/Modules/Module.cshtml", model);
    }

    [HttpGet]
    public async Task<IActionResult> PrescriptionPreview(string orderNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return BadRequest();
        }

        var order = await dbContext.Orders
            .AsNoTracking()
            .Include(entry => entry.Payment)
            .FirstOrDefaultAsync(entry => entry.OrderNumber == orderNumber, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        var files = BuildPrescriptionAssets(order.PrescriptionFilesJson, environment);
        var model = new PrescriptionPreviewViewModel
        {
            OrderNumber = order.OrderNumber,
            CustomerName = order.CustomerFullName,
            PrescriptionStatus = order.PrescriptionStatus,
            PaymentStatus = order.Payment?.Status ?? "AwaitingPayment",
            CreatedAtUtc = order.CreatedAtUtc,
            Files = files
        };

        return PartialView("~/Views/Modules/Partials/_PrescriptionPreview.cshtml", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePrescriptionStatus(
        PrescriptionStatusUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedStatus = NormalizePrescriptionStatus(request.Status);
        if (string.IsNullOrWhiteSpace(normalizedStatus))
        {
            TempData["Error"] = "Choose a valid prescription status.";
            return RedirectToAction(nameof(Prescriptions));
        }

        var order = await dbContext.Orders
            .Include(entry => entry.Payment)
            .FirstOrDefaultAsync(entry => entry.OrderNumber == request.OrderNumber, cancellationToken);
        if (order is null)
        {
            TempData["Error"] = "Prescription order was not found.";
            return RedirectToAction(nameof(Prescriptions));
        }

        if (!order.RequiresPrescription)
        {
            TempData["Error"] = "This order does not require prescription validation.";
            return RedirectToAction(nameof(Prescriptions));
        }

        var previousStatus = order.PrescriptionStatus;
        order.PrescriptionStatus = normalizedStatus;
        ApplyPrescriptionWorkflow(order, normalizedStatus);
        await dbContext.SaveChangesAsync(cancellationToken);

        await TryUpdatePharmacistAssignmentAsync(order, order.Payment, documentId: null, cancellationToken);
        await TrySyncOrderStatusAsync(order, order.Payment?.Status ?? "AwaitingPayment", cancellationToken);

        if (!string.Equals(previousStatus, normalizedStatus, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(order.CustomerUid))
        {
            if (IsPrescriptionValidated(normalizedStatus))
            {
                await TryCreateNotificationAsync(
                    order.CustomerUid,
                    order.OrderNumber,
                    "Prescription Approved",
                    string.Equals(order.PaymentMethod, "CashOnDelivery", StringComparison.OrdinalIgnoreCase)
                        ? "Your prescription has been approved. Your order can now proceed."
                        : "Your prescription has been approved. Open My Orders to continue payment.",
                    cancellationToken);
            }
            else if (string.Equals(normalizedStatus, "Rejected", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(normalizedStatus, "Invalid", StringComparison.OrdinalIgnoreCase))
            {
                await TryCreateNotificationAsync(
                    order.CustomerUid,
                    order.OrderNumber,
                    "Prescription Rejected",
                    "Your prescription was rejected. Please upload a new prescription.",
                    cancellationToken);
            }
        }

        await auditLogService.WriteAsync(
            "Prescription Validation",
            normalizedStatus,
            CurrentUsername,
            CurrentRole,
            $"Updated prescription status for order {order.OrderNumber} from {previousStatus} to {normalizedStatus}.",
            cancellationToken);

        TempData["Success"] = $"Prescription status for {order.OrderNumber} updated to {normalizedStatus}.";
        return RedirectToAction(nameof(Prescriptions));
    }

    public IActionResult Sales(
        string? search,
        string? category,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        var inventory = medicineService.GetAll().ToList();
        var filteredMedicines = FilterMedicines(search, category).ToList();
        var pagination = BuildPagination(
            nameof(Sales),
            page,
            pageSize,
            filteredMedicines.Count,
            ("search", search),
            ("category", category));
        var pagedMedicines = Paginate(filteredMedicines, pagination.CurrentPage, pagination.PageSize);
        var prescriptionMedicines = inventory.Count(medicine => medicine.RequiresPrescription);

        var model = CreateModule(
            AdminModuleKind.Pos,
            "Process Sales / Checkout",
            "Counter checkout for pharmacists. Prescription medicines must be marked as validated before the sale can proceed.")
            with
            {
                Search = search,
                CategoryFilter = category,
                Categories = inventory.Select(medicine => medicine.Category).Distinct().OrderBy(categoryName => categoryName).ToList(),
                Medicines = pagedMedicines,
                Pagination = pagination,
                Actions =
                [
                    ActionLink("Prescription Queue", "PharmacistModules", nameof(Prescriptions), "bi-clipboard2-pulse", "outline", "Validate prescription orders first"),
                    ActionLink("Receipts", "PharmacistModules", nameof(Receipts), "bi-receipt-cutoff", "outline", "Open released receipts")
                ],
                Metrics =
                [
                    Metric("Ready to sell", inventory.Count(medicine => medicine.Stock > 0).ToString("N0"), "Available for pharmacist checkout", "bi-bag-check", "success"),
                    Metric("Prescription items", prescriptionMedicines.ToString("N0"), "Require validated prescription", "bi-file-earmark-medical", "warning"),
                    Metric("Low stock", inventory.Count(medicine => medicine.Stock > 0 && medicine.Stock <= 20).ToString("N0"), "Needs replenishment soon", "bi-exclamation-triangle", "warning"),
                    Metric("Visible rows", filteredMedicines.Count.ToString("N0"), "Current search and filter result", "bi-filter", "neutral")
                ]
            };

        return View("~/Views/Modules/Module.cshtml", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCounterSale(CounterSaleRequest request, CancellationToken cancellationToken)
    {
        var medicine = medicineService.GetById(request.MedicineId);
        if (medicine is null)
        {
            TempData["Error"] = "Selected medicine was not found.";
            return RedirectToAction(nameof(Sales));
        }

        if (request.Quantity <= 0)
        {
            TempData["Error"] = "Quantity must be greater than zero.";
            return RedirectToAction(nameof(Sales));
        }

        if (medicine.RequiresPrescription && !request.PrescriptionValidated)
        {
            TempData["Error"] = $"{medicine.BrandName} requires a validated prescription before checkout can continue.";
            return RedirectToAction(nameof(Sales), new { search = medicine.BrandName });
        }

        if (medicine.Stock < request.Quantity)
        {
            TempData["Error"] = $"{medicine.BrandName} only has {medicine.Stock} unit{Plural(medicine.Stock)} available.";
            return RedirectToAction(nameof(Sales), new { search = medicine.BrandName });
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
            DeliveryAddress = "In-store pharmacist counter",
            Landmark = string.Empty,
            AddressType = "Store",
            DeliveryOption = "Counter",
            PaymentMethod = paymentMethod,
            FulfillmentBranch = "SafeMed Pharmacist Desk",
            PrescriptionStatus = medicine.RequiresPrescription ? "Valid" : "NotRequired",
            OrderStatus = "Completed",
            RequiresPrescription = medicine.RequiresPrescription,
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
                    RequiresPrescription = medicine.RequiresPrescription
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
        await TrySyncOrderAsync(order, payment, cancellationToken);

        medicine.Stock -= request.Quantity;
        medicineService.Update(medicine);

        await auditLogService.WriteAsync(
            "Pharmacist Checkout",
            "Create Counter Sale",
            CurrentUsername,
            CurrentRole,
            $"Completed counter sale {order.OrderNumber} for {medicine.BrandName} x{request.Quantity}.",
            cancellationToken);

        TempData["Success"] = $"Counter sale {order.OrderNumber} completed.";
        return RedirectToAction(nameof(Receipts), new { orderNumber = order.OrderNumber });
    }

    public async Task<IActionResult> Payments(
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
            nameof(Payments),
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
                Status = order.Payment != null ? order.Payment.Status : "AwaitingPayment",
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
                RequiresPrescription = order.RequiresPrescription,
                PrescriptionStatus = order.PrescriptionStatus,
                CanProceed = !order.RequiresPrescription || IsPrescriptionValidated(order.PrescriptionStatus),
                BlockingReason = order.RequiresPrescription && !IsPrescriptionValidated(order.PrescriptionStatus)
                    ? "Validate the prescription before collecting payment."
                    : string.Empty,
                CreatedAtUtc = order.Payment != null ? order.Payment.CreatedAtUtc : order.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var totalCollected = await dbContext.Payments
            .AsNoTracking()
            .Where(payment => payment.Status == "Paid")
            .SumAsync(payment => (decimal?)payment.Amount, cancellationToken) ?? 0m;
        var pendingValidation = await dbContext.Orders
            .AsNoTracking()
            .CountAsync(order =>
                order.RequiresPrescription &&
                order.PrescriptionStatus != "Approved" &&
                order.PrescriptionStatus != "Valid" &&
                order.PrescriptionStatus != "NotRequired",
                cancellationToken);

        var model = CreateModule(
            AdminModuleKind.Payment,
            "Handle Payments",
            "Update payment status after prescription-dependent orders have been validated.")
            with
            {
                Search = search,
                PaymentFilter = method,
                StatusFilter = status,
                Payments = payments,
                AllowPaymentUpdates = true,
                Pagination = pagination,
                Actions =
                [
                    ActionLink("Prescription Queue", "PharmacistModules", nameof(Prescriptions), "bi-clipboard2-pulse", "outline", "Review blocked prescription orders"),
                    ActionLink("Receipts", "PharmacistModules", nameof(Receipts), "bi-receipt-cutoff", "outline", "Open receipt release queue")
                ],
                Metrics =
                [
                    Metric("Collected", Currency(totalCollected), "Paid pharmacist transactions", "bi-cash-stack", "success"),
                    Metric("Blocked by Rx", pendingValidation.ToString("N0"), "Payment cannot proceed yet", "bi-shield-exclamation", "warning"),
                    Metric("Visible records", totalCount.ToString("N0"), "Current filter result", "bi-filter", "neutral"),
                    Metric("Pending attention", payments.Count(payment => !string.Equals(payment.Status, "Paid", StringComparison.OrdinalIgnoreCase)).ToString("N0"), "Unpaid or blocked orders", "bi-hourglass-split", "warning")
                ]
            };

        return View("~/Views/Modules/Module.cshtml", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePaymentStatus(PaymentStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        var normalizedStatus = NormalizePaymentStatus(request.Status);
        if (string.IsNullOrWhiteSpace(normalizedStatus))
        {
            TempData["Error"] = "Choose a valid payment status.";
            return RedirectToAction(nameof(Payments));
        }

        var payment = await dbContext.Payments
            .Include(entry => entry.PharmacyOrder)
            .FirstOrDefaultAsync(entry => entry.Id == request.PaymentId, cancellationToken);

        if (payment is null)
        {
            TempData["Error"] = "Payment record was not found.";
            return RedirectToAction(nameof(Payments));
        }

        if (payment.PharmacyOrder is not null &&
            payment.PharmacyOrder.RequiresPrescription &&
            !IsPrescriptionValidated(payment.PharmacyOrder.PrescriptionStatus))
        {
            TempData["Error"] = $"Order {payment.PharmacyOrder.OrderNumber} requires prescription validation before payment can proceed.";
            return RedirectToAction(nameof(Payments));
        }

        var previousPaymentStatus = payment.Status;
        var previousOrderStatus = payment.PharmacyOrder?.OrderStatus ?? string.Empty;
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

        if (payment.PharmacyOrder is not null)
        {
            await TryUpdatePharmacistAssignmentAsync(payment.PharmacyOrder, payment, documentId: null, cancellationToken);
            await TrySyncOrderStatusAsync(payment.PharmacyOrder, payment.Status, cancellationToken);

            if (!string.IsNullOrWhiteSpace(payment.PharmacyOrder.CustomerUid))
            {
                var notification = string.Equals(payment.Status, "Paid", StringComparison.OrdinalIgnoreCase)
                    ? ("Payment Confirmed", "Your payment was confirmed and the order can now proceed.")
                    : ("Payment Updated", $"Your payment status is now {payment.Status}.");

                await TryCreateNotificationAsync(
                    payment.PharmacyOrder.CustomerUid,
                    payment.PharmacyOrder.OrderNumber,
                    notification.Item1,
                    notification.Item2,
                    cancellationToken);
            }
        }

        await auditLogService.WriteAsync(
            "Pharmacist Payments",
            "Update Payment Status",
            CurrentUsername,
            CurrentRole,
            $"Updated payment {payment.ReferenceNumber} from {previousPaymentStatus} to {normalizedStatus} for order {payment.PharmacyOrder?.OrderNumber ?? "Unknown"} ({previousOrderStatus} -> {payment.PharmacyOrder?.OrderStatus ?? previousOrderStatus}).",
            cancellationToken);

        TempData["Success"] = $"Payment {payment.ReferenceNumber} is now {normalizedStatus}.";
        return RedirectToAction(nameof(Payments));
    }

    public async Task<IActionResult> Receipts(
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
            nameof(Receipts),
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
                RequiresPrescription = order.RequiresPrescription,
                PrescriptionStatus = order.PrescriptionStatus,
                CanRelease = !order.RequiresPrescription || IsPrescriptionValidated(order.PrescriptionStatus),
                BlockingReason = order.RequiresPrescription && !IsPrescriptionValidated(order.PrescriptionStatus)
                    ? "Prescription validation is required before generating a receipt."
                    : string.Empty,
                CreatedAtUtc = order.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var selectedOrderNumber = string.IsNullOrWhiteSpace(orderNumber)
            ? pagedReceipts.FirstOrDefault(receipt => receipt.CanRelease)?.OrderNumber
            : orderNumber;

        AdminReceiptViewModel? receipt = null;
        if (!string.IsNullOrWhiteSpace(selectedOrderNumber))
        {
            var selectedOrder = await dbContext.Orders
                .AsNoTracking()
                .Include(order => order.Items)
                .Include(order => order.Payment)
                .FirstOrDefaultAsync(order => order.OrderNumber == selectedOrderNumber, cancellationToken);
            if (selectedOrder is not null && CanReleaseReceipt(selectedOrder))
            {
                receipt = BuildReceipt(selectedOrder);
            }
            else if (selectedOrder is not null)
            {
                TempData["Error"] = "Prescription validation is required before generating a receipt for that order.";
            }
        }

        var model = CreateModule(
            AdminModuleKind.Receipt,
            "Generate Receipts",
            "Release receipts only after prescription-dependent orders have been validated.")
            with
            {
                Search = search,
                SelectedOrderNumber = selectedOrderNumber,
                ReceiptOrders = pagedReceipts,
                Receipt = receipt,
                AllowReceiptRelease = true,
                Pagination = pagination,
                Actions =
                [
                    ActionLink("Payments", "PharmacistModules", nameof(Payments), "bi-credit-card-2-front", "outline", "Open payment handling"),
                    ActionLink("Prescription Queue", "PharmacistModules", nameof(Prescriptions), "bi-clipboard2-pulse", "outline", "Open validation queue")
                ],
                Metrics =
                [
                    Metric("Receipts in queue", totalCount.ToString("N0"), "Orders matching the current search", "bi-receipt", "neutral"),
                    Metric("Blocked by Rx", pagedReceipts.Count(receiptOrder => !receiptOrder.CanRelease).ToString("N0"), "Validation still required", "bi-shield-exclamation", "warning"),
                    Metric("Selected total", receipt is null ? Currency(0m) : Currency(receipt.TotalAmount), receipt?.OrderNumber ?? "No released order selected", "bi-printer", "success"),
                    Metric("Released items", (receipt?.Items.Count ?? 0).ToString("N0"), "Line items on current receipt", "bi-list-check", "neutral")
                ]
            };

        return View("~/Views/Modules/Module.cshtml", model);
    }

    [HttpGet]
    public async Task<IActionResult> ReceiptPreview(string orderNumber, CancellationToken cancellationToken)
    {
        var order = await LoadOrderForReceiptAsync(orderNumber, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        if (!CanReleaseReceipt(order))
        {
            return Content(
                "<div class=\"admin-empty-state\">Prescription validation is required before generating a receipt.</div>",
                "text/html");
        }

        return PartialView("~/Views/Modules/Partials/_ReceiptPreview.cshtml", BuildReceipt(order));
    }

    [HttpGet]
    public async Task<IActionResult> PrintReceipt(string orderNumber, CancellationToken cancellationToken)
    {
        var order = await LoadOrderForReceiptAsync(orderNumber, cancellationToken);
        if (order is null)
        {
            return RedirectToAction(nameof(Receipts));
        }

        if (!CanReleaseReceipt(order))
        {
            TempData["Error"] = "Prescription validation is required before generating a receipt.";
            return RedirectToAction(nameof(Receipts), new { orderNumber });
        }

        return View("~/Views/Modules/ReceiptPrint.cshtml", BuildReceipt(order));
    }

    public async Task<IActionResult> DownloadReceipt(string orderNumber, CancellationToken cancellationToken)
    {
        var order = await LoadOrderForReceiptAsync(orderNumber, cancellationToken);
        if (order is null)
        {
            return RedirectToAction(nameof(Receipts));
        }

        if (!CanReleaseReceipt(order))
        {
            TempData["Error"] = "Prescription validation is required before generating a receipt.";
            return RedirectToAction(nameof(Receipts), new { orderNumber });
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

        await auditLogService.WriteAsync(
            "Pharmacist Receipts",
            "Download Receipt",
            CurrentUsername,
            CurrentRole,
            $"Downloaded receipt for order {receipt.OrderNumber}.",
            cancellationToken);

        return new FileContentResult(Encoding.UTF8.GetBytes(builder.ToString()), "text/plain")
        {
            FileDownloadName = $"receipt-{receipt.OrderNumber}.txt"
        };
    }

    public IActionResult StockLevels(
        string? search,
        string? category,
        string? status,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        var medicines = medicineService.GetAll();

        if (!string.IsNullOrWhiteSpace(search))
        {
            medicines = medicines.Where(medicine =>
                medicine.Code.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                medicine.BrandName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                medicine.GenericName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                medicine.Supplier.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            medicines = medicines.Where(medicine => medicine.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            medicines = medicines.Where(medicine => medicine.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        var all = medicineService.GetAll().ToList();
        var filtered = medicines.ToList();
        var pagination = BuildPagination(
            nameof(StockLevels),
            page,
            pageSize,
            filtered.Count,
            ("search", search),
            ("category", category),
            ("status", status));

        var model = CreateModule(
            AdminModuleKind.StockLevels,
            "View Stock Levels",
            "Read-only stock visibility for pharmacists before checkout and fulfillment.")
            with
            {
                Search = search,
                CategoryFilter = category,
                StatusFilter = status,
                Categories = all.Select(medicine => medicine.Category).Distinct().OrderBy(value => value).ToList(),
                Medicines = filtered
                    .Skip((pagination.CurrentPage - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .ToList(),
                AllowRestock = false,
                Pagination = pagination,
                Actions =
                [
                    ActionLink("Prescription Queue", "PharmacistModules", nameof(Prescriptions), "bi-clipboard2-pulse", "outline", "Open validation queue"),
                    ActionLink("Checkout", "PharmacistModules", nameof(Sales), "bi-cart3", "outline", "Open pharmacist checkout")
                ],
                Metrics =
                [
                    Metric("Visible SKUs", filtered.Count.ToString("N0"), "Current filter result", "bi-capsule", "neutral"),
                    Metric("Low stock", filtered.Count(medicine => medicine.Stock > 0 && medicine.Stock <= 20).ToString("N0"), "Shown on this page", "bi-exclamation-triangle", "warning"),
                    Metric("Out of stock", filtered.Count(medicine => medicine.Stock <= 0).ToString("N0"), "Shown on this page", "bi-x-octagon", "danger"),
                    Metric("Prescription items", filtered.Count(medicine => medicine.RequiresPrescription).ToString("N0"), "Require validated prescription", "bi-file-earmark-medical", "warning")
                ]
            };

        return View("~/Views/Modules/Module.cshtml", model);
    }

    public async Task<IActionResult> Messages(int? threadId, CancellationToken cancellationToken = default)
    {
        var threads = await messagingService.GetThreadsAsync(cancellationToken);
        var orderNumbers = threads
            .Select(thread => thread.OrderNumber)
            .Where(orderNumber => !string.IsNullOrWhiteSpace(orderNumber))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (orderNumbers.Count > 0)
        {
            var orders = await dbContext.Orders
                .AsNoTracking()
                .Include(order => order.Payment)
                .Where(order => orderNumbers.Contains(order.OrderNumber))
                .ToListAsync(cancellationToken);

            foreach (var order in orders)
            {
                await messagingService.EnsureOrderThreadAsync(order, cancellationToken);
            }

            threads = await messagingService.GetThreadsAsync(cancellationToken);
        }

        var selectedThreadId = threadId ?? threads.FirstOrDefault()?.Id;

        if (selectedThreadId.HasValue)
        {
            await messagingService.MarkThreadAsReadAsync(selectedThreadId.Value, cancellationToken);
            threads = await messagingService.GetThreadsAsync(cancellationToken);
        }

        var threadViewModels = threads
            .Take(MessageThreadLimit)
            .Select(BuildThreadViewModel)
            .OrderByDescending(thread => thread.UpdatedAtUtc)
            .ToList();
        var activeThread = threadViewModels.FirstOrDefault(thread => thread.Id == selectedThreadId);

        var model = CreateModule(
            AdminModuleKind.Messages,
            "Messages",
            "Customer order chats stored locally in the POS and grouped by order.")
            with
            {
                SelectedThreadId = selectedThreadId,
                MessageThreads = threadViewModels,
                ActiveMessageThread = activeThread,
                WorkflowSteps = [],
                Actions = []
            };

        return View("~/Views/Modules/Module.cshtml", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(PharmacistMessageSendRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
        {
            TempData["Error"] = "Message body is required.";
            return RedirectToAction(nameof(Messages), new { threadId = request.ThreadId });
        }

        if (!request.ThreadId.HasValue)
        {
            TempData["Error"] = "Select an order thread before sending a reply.";
            return RedirectToAction(nameof(Messages));
        }

        var thread = await messagingService.GetThreadAsync(request.ThreadId.Value, cancellationToken);
        if (thread is null)
        {
            TempData["Error"] = "Order chat thread was not found.";
            return RedirectToAction(nameof(Messages));
        }

        try
        {
            await messagingService.SendMessageAsync(
                thread.Id,
                string.IsNullOrWhiteSpace(CurrentUsername) ? "Pharmacist" : CurrentUsername.Trim(),
                AppRoles.Pharmacist,
                request.Body,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to send pharmacist reply for order {OrderNumber}.",
                thread.OrderNumber);
            TempData["Error"] = "The pharmacist reply could not be sent.";
            return RedirectToAction(nameof(Messages), new { threadId = request.ThreadId });
        }

        await auditLogService.WriteAsync(
            "Pharmacist Messages",
            "Send Reply",
            CurrentUsername,
            CurrentRole,
            $"Sent a pharmacist reply for order {thread.OrderNumber}.",
            cancellationToken);

        TempData["Success"] = $"Reply sent for order {thread.OrderNumber}.";
        return RedirectToAction(nameof(Messages), new { threadId = request.ThreadId });
    }

    private static bool IsPrescriptionValidated(string? prescriptionStatus) =>
        string.Equals(prescriptionStatus, "Approved", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(prescriptionStatus, "Valid", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(prescriptionStatus, "NotRequired", StringComparison.OrdinalIgnoreCase);

    private static bool CanReleaseReceipt(PharmacyOrder order) =>
        !order.RequiresPrescription || IsPrescriptionValidated(order.PrescriptionStatus);

    private static void ApplyPrescriptionWorkflow(PharmacyOrder order, string normalizedStatus)
    {
        if (IsPrescriptionValidated(normalizedStatus))
        {
            if (order.Payment is not null &&
                !string.Equals(order.Payment.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                order.Payment.Status = string.Equals(order.PaymentMethod, "CashOnDelivery", StringComparison.OrdinalIgnoreCase)
                    ? "PendingCollection"
                    : "AwaitingPayment";
            }

            order.OrderStatus = string.Equals(order.PaymentMethod, "CashOnDelivery", StringComparison.OrdinalIgnoreCase)
                ? "Pending"
                : "AwaitingPayment";
            return;
        }

        if (string.Equals(normalizedStatus, "Rejected", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedStatus, "Invalid", StringComparison.OrdinalIgnoreCase))
        {
            if (order.Payment is not null &&
                !string.Equals(order.Payment.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                order.Payment.Status = "Rejected";
            }

            order.OrderStatus = "Rejected";
            return;
        }

        if (order.Payment is not null &&
            !string.Equals(order.Payment.Status, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            order.Payment.Status = "AwaitingApproval";
        }

        order.OrderStatus = "PendingReview";
    }

    private async Task<PharmacyOrder?> LoadOrderForReceiptAsync(string orderNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return null;
        }

        return await dbContext.Orders
            .AsNoTracking()
            .Include(entry => entry.Items)
            .Include(entry => entry.Payment)
            .FirstOrDefaultAsync(entry => entry.OrderNumber == orderNumber, cancellationToken);
    }

    private static PharmacistMessageThreadViewModel BuildThreadViewModel(
        PharmacistMessageThread thread)
    {
        var orderedMessages = thread.Messages
            .OrderBy(message => message.SentAtUtc)
            .ToList();
        var lastMessage = orderedMessages.LastOrDefault();
        var needsReply = lastMessage is not null &&
            AppRoles.Matches(lastMessage.SenderRole, AppRoles.Customer);

        return new PharmacistMessageThreadViewModel
        {
            Id = thread.Id,
            OrderNumber = thread.OrderNumber,
            OrderReference = string.IsNullOrWhiteSpace(thread.OrderReference) ? thread.OrderNumber : thread.OrderReference,
            CustomerName = thread.CustomerName,
            CustomerUid = thread.CustomerEmail,
            OrderStatus = thread.OrderStatus,
            PaymentStatus = thread.PaymentStatus,
            PrescriptionStatus = thread.PrescriptionStatus,
            RequiresPrescription = thread.RequiresPrescription,
            CreatedAtUtc = thread.CreatedAtUtc == default ? thread.UpdatedAtUtc : thread.CreatedAtUtc,
            UpdatedAtUtc = lastMessage?.SentAtUtc ?? thread.UpdatedAtUtc,
            MessageCount = orderedMessages.Count,
            NeedsReply = needsReply,
            LastMessagePreview = lastMessage?.Body ?? "No messages yet.",
            Messages = orderedMessages
                .Select(message => new PharmacistMessageEntryViewModel
                {
                    SenderUid = string.Empty,
                    SenderName = message.SenderName,
                    SenderRole = message.SenderRole,
                    RecipientRole = AppRoles.Matches(message.SenderRole, AppRoles.Customer)
                        ? AppRoles.Pharmacist
                        : AppRoles.Customer,
                    Body = message.Body,
                    SentAtUtc = message.SentAtUtc
                })
                .ToList()
        };
    }

    private async Task TryUpdatePharmacistAssignmentAsync(
        PharmacyOrder order,
        PaymentRecord? payment,
        string? documentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(order.CustomerUid))
        {
            return;
        }

        try
        {
            await firebaseOrderChatService.UpdatePharmacistAssignmentAsync(
                order,
                payment,
                ResolveCurrentPharmacistIdentity(order),
                documentId,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to update Firestore pharmacist assignment for order {OrderNumber}.",
                order.OrderNumber);
        }
    }

    private FirebasePharmacistIdentity ResolveCurrentPharmacistIdentity(PharmacyOrder order) =>
        new()
        {
            Uid = string.IsNullOrWhiteSpace(CurrentEmail)
                ? ResolveFallbackStaffUid(CurrentUsername)
                : CurrentEmail.Trim().ToLowerInvariant(),
            Name = string.IsNullOrWhiteSpace(CurrentUsername)
                ? "Pharmacist"
                : CurrentUsername.Trim(),
            PharmacyName = string.IsNullOrWhiteSpace(order.FulfillmentBranch)
                ? "SafeMed Pharmacy"
                : order.FulfillmentBranch.Trim()
        };


    private static string ResolveFallbackStaffUid(string staffName)
    {
        if (string.IsNullOrWhiteSpace(staffName))
        {
            return "pharmacist";
        }

        var slugCharacters = staffName
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray();
        var slug = new string(slugCharacters).Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "pharmacist" : $"pharmacist-{slug}";
    }

    private static List<PrescriptionAssetViewModel> BuildPrescriptionAssets(string? json, IWebHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var references = JsonSerializer.Deserialize<List<PrescriptionFileReference>>(json);
            if (references is { Count: > 0 })
            {
                return references
                    .Where(file => !string.IsNullOrWhiteSpace(file.Url))
                    .Select((file, index) => BuildPrescriptionAsset(file, index, environment))
                    .ToList();
            }
        }
        catch
        {
        }

        try
        {
            var urls = JsonSerializer.Deserialize<List<string>>(json) ?? [];
            return urls
                .Where(file => !string.IsNullOrWhiteSpace(file))
                .Select((file, index) => BuildPrescriptionAsset(
                    new PrescriptionFileReference
                    {
                        Name = Path.GetFileName(file),
                        Url = file
                    },
                    index,
                    environment))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static PrescriptionAssetViewModel BuildPrescriptionAsset(
        PrescriptionFileReference file,
        int index,
        IWebHostEnvironment environment)
    {
        var normalizedUrl = ResolvePrescriptionUrl(file.Url, environment);
        var derivedName = ResolvePrescriptionFileName(file, normalizedUrl, index);
        var contentType = file.ContentType?.Trim() ?? string.Empty;
        var canOpen = !string.IsNullOrWhiteSpace(normalizedUrl);
        var isPdf = IsPdfFile(normalizedUrl, derivedName, contentType);
        var isImage = IsImageFile(normalizedUrl, derivedName, contentType);

        return new PrescriptionAssetViewModel
        {
            Url = normalizedUrl,
            Label = $"Prescription file {index + 1}",
            FileName = derivedName,
            ContentType = contentType,
            CanOpen = canOpen,
            IsImage = isImage,
            IsPdf = isPdf,
            UnavailableMessage = canOpen
                ? string.Empty
                : "This order only kept the original file name. The actual uploaded file is not available on the server."
        };
    }

    private static string ResolvePrescriptionFileName(
        PrescriptionFileReference file,
        string resolvedUrl,
        int index)
    {
        if (!string.IsNullOrWhiteSpace(file.Name))
        {
            return file.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(resolvedUrl))
        {
            var absolutePath = Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var absoluteUri)
                ? absoluteUri.AbsolutePath
                : resolvedUrl;
            var candidate = Path.GetFileName(absolutePath);
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        if (!string.IsNullOrWhiteSpace(file.Url))
        {
            var candidate = Path.GetFileName(file.Url);
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return $"Prescription file {index + 1}";
    }

    private static string ResolvePrescriptionUrl(string? candidate, IWebHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return string.Empty;
        }

        var trimmed = candidate.Trim();
        if (trimmed.StartsWith("/", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var safeFileName = Path.GetFileName(trimmed);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            return string.Empty;
        }

        var webRootPath = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        var uploadsRoot = Path.Combine(webRootPath, "uploads", "prescriptions");
        if (!Directory.Exists(uploadsRoot))
        {
            return string.Empty;
        }

        var matchedPath = Directory
            .EnumerateFiles(uploadsRoot, safeFileName, SearchOption.AllDirectories)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(matchedPath))
        {
            return string.Empty;
        }

        var relativePath = Path.GetRelativePath(webRootPath, matchedPath).Replace("\\", "/");
        return "/" + relativePath;
    }

    private static bool IsImageFile(string file, string fileName, string contentType)
    {
        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (file.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
               file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
               file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               file.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
               file.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPdfFile(string file, string fileName, string contentType)
    {
        if (string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return file.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
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

    private AdminModuleViewModel CreateModule(AdminModuleKind kind, string title, string description) =>
        new()
        {
            Kind = kind,
            Title = title,
            Description = description,
            ModuleController = "PharmacistModules",
            CurrentRole = CurrentRole,
            Eyebrow = "Pharmacist module",
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
            Controller = "PharmacistModules",
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

    private static string NormalizePrescriptionStatus(string status) =>
        status.Trim().ToLowerInvariant() switch
        {
            "approved" => "Approved",
            "valid" => "Valid",
            "rejected" => "Rejected",
            "invalid" => "Invalid",
            "pendingreview" => "PendingReview",
            "pending" => "Pending",
            _ => string.Empty
        };

    private async Task TrySyncOrderAsync(
        PharmacyOrder order,
        PaymentRecord? payment,
        CancellationToken cancellationToken)
    {
        try
        {
            await firebaseSyncService.SyncOrderAsync(order, payment, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Firebase order sync failed for order {OrderNumber}.", order.OrderNumber);
        }
    }

    private async Task TrySyncOrderStatusAsync(
        PharmacyOrder order,
        string paymentStatus,
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
            logger.LogError(exception, "Firebase order status sync failed for order {OrderNumber}.", order.OrderNumber);
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
            await firebaseSyncService.CreateNotificationAsync(customerUid, orderNumber, title, message, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Firebase notification creation failed for order {OrderNumber}.", orderNumber);
        }
    }

    private static string GenerateOrderNumber() =>
        $"PHARM-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

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
