using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmacyPOS.Data;
using PharmacyPOS.Models;
using PharmacyPOS.Models.Admin;
using PharmacyPOS.Models.Messages;
using PharmacyPOS.Models.Security;
using PharmacyPOS.Services;

namespace PharmacyPOS.Controllers;

public sealed class CustomerMessagesController(
    PharmacyPosDbContext dbContext,
    IPharmacistMessagingService messagingService) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Index(string? orderNumber, CancellationToken cancellationToken = default)
    {
        var role = HttpContext.Session.GetString("Role") ?? string.Empty;
        if (AppRoles.IsBackOffice(role))
        {
            return RedirectToAction("Messages", "PharmacistModules");
        }

        var customerName = HttpContext.Session.GetString("Username") ?? string.Empty;
        var customerEmail = HttpContext.Session.GetString("Email") ?? string.Empty;
        var orders = await LoadCustomerOrdersAsync(customerEmail, cancellationToken);
        var threads = await messagingService.GetCustomerThreadsAsync(customerEmail, cancellationToken);
        var selectedOrderNumber = ResolveSelectedOrderNumber(orderNumber, orders);
        var activeStoredThread = threads.FirstOrDefault(thread =>
            string.Equals(thread.OrderNumber, selectedOrderNumber, StringComparison.OrdinalIgnoreCase));

        if (activeStoredThread is not null)
        {
            await messagingService.MarkThreadAsReadForCustomerAsync(activeStoredThread.Id, cancellationToken);
            threads = await messagingService.GetCustomerThreadsAsync(customerEmail, cancellationToken);
        }

        var model = BuildViewModel(
            customerName,
            orders,
            threads,
            selectedOrderNumber,
            await messagingService.GetUnreadCountForCustomerAsync(customerEmail, cancellationToken));

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(CustomerMessageSendRequest request, CancellationToken cancellationToken)
    {
        var role = HttpContext.Session.GetString("Role") ?? string.Empty;
        if (AppRoles.IsBackOffice(role))
        {
            return RedirectToAction("Messages", "PharmacistModules");
        }

        if (string.IsNullOrWhiteSpace(request.OrderNumber))
        {
            TempData["Error"] = "Choose an order before sending a message.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            TempData["Error"] = "Message body is required.";
            return RedirectToAction(nameof(Index), new { orderNumber = request.OrderNumber });
        }

        var customerName = HttpContext.Session.GetString("Username") ?? string.Empty;
        var customerEmail = HttpContext.Session.GetString("Email") ?? string.Empty;
        var order = await dbContext.Orders
            .AsNoTracking()
            .Include(entry => entry.Items)
            .Include(entry => entry.Payment)
            .FirstOrDefaultAsync(entry =>
                entry.OrderNumber == request.OrderNumber &&
                entry.CustomerEmail == customerEmail,
                cancellationToken);

        if (order is null)
        {
            TempData["Error"] = "The selected order chat was not found.";
            return RedirectToAction(nameof(Index));
        }

        var threadId = await messagingService.EnsureOrderThreadAsync(order, cancellationToken);
        await messagingService.SendMessageAsync(
            threadId,
            string.IsNullOrWhiteSpace(customerName) ? "Customer" : customerName.Trim(),
            AppRoles.Customer,
            request.Body,
            cancellationToken);

        TempData["Success"] = $"Message sent for order {order.OrderNumber}.";
        return RedirectToAction(nameof(Index), new { orderNumber = order.OrderNumber });
    }

    private async Task<List<PharmacyOrder>> LoadCustomerOrdersAsync(
        string customerEmail,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            return [];
        }

        return await dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Include(order => order.Payment)
            .Where(order => order.CustomerEmail == customerEmail)
            .OrderByDescending(order => order.CreatedAtUtc)
            .Take(25)
            .ToListAsync(cancellationToken);
    }

    private CustomerMessagesViewModel BuildViewModel(
        string customerName,
        IReadOnlyList<PharmacyOrder> orders,
        IReadOnlyList<PharmacistMessageThread> threads,
        string selectedOrderNumber,
        int unreadCount)
    {
        var threadsByOrder = threads
            .Where(thread => !string.IsNullOrWhiteSpace(thread.OrderNumber))
            .GroupBy(thread => thread.OrderNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(thread => thread.UpdatedAtUtc).First(), StringComparer.OrdinalIgnoreCase);

        var orderThreads = orders
            .Select(order => BuildOrderThreadViewModel(
                order,
                threadsByOrder.TryGetValue(order.OrderNumber, out var thread) ? thread : null))
            .ToList();

        var activeThread = orderThreads.FirstOrDefault(thread =>
            string.Equals(thread.OrderNumber, selectedOrderNumber, StringComparison.OrdinalIgnoreCase))
            ?? orderThreads.FirstOrDefault();

        return new CustomerMessagesViewModel
        {
            CustomerName = customerName,
            UnreadCount = unreadCount,
            SelectedOrderNumber = activeThread?.OrderNumber ?? string.Empty,
            OrderThreads = orderThreads,
            ActiveThread = activeThread
        };
    }

    private static CustomerOrderThreadViewModel BuildOrderThreadViewModel(
        PharmacyOrder order,
        PharmacistMessageThread? thread)
    {
        var orderedMessages = thread?.Messages
            .OrderBy(message => message.SentAtUtc)
            .Select(message => new CustomerMessageEntryViewModel
            {
                SenderName = message.SenderName,
                SenderRole = message.SenderRole,
                Body = message.Body,
                SentAtUtc = message.SentAtUtc,
                IsCustomerMessage = AppRoles.Matches(message.SenderRole, AppRoles.Customer)
            })
            .ToList() ?? [];
        var lastMessage = thread?.Messages
            .OrderBy(message => message.SentAtUtc)
            .LastOrDefault();

        return new CustomerOrderThreadViewModel
        {
            ThreadId = thread?.Id,
            OrderNumber = order.OrderNumber,
            OrderReference = string.IsNullOrWhiteSpace(order.Payment?.ReferenceNumber)
                ? order.OrderNumber
                : order.Payment.ReferenceNumber,
            OrderStatus = order.OrderStatus,
            PaymentStatus = order.Payment?.Status ?? "Pending",
            PrescriptionStatus = order.PrescriptionStatus,
            RequiresPrescription = order.RequiresPrescription,
            TotalAmount = order.TotalAmount,
            ItemsCount = order.Items.Sum(item => item.Quantity),
            ItemSummary = BuildItemSummary(order.Items),
            CreatedAtUtc = order.CreatedAtUtc,
            UpdatedAtUtc = thread?.UpdatedAtUtc ?? order.CreatedAtUtc,
            UnreadCount = thread?.Messages.Count(message =>
                !message.IsReadByCustomer &&
                !AppRoles.Matches(message.SenderRole, AppRoles.Customer)) ?? 0,
            LastMessagePreview = lastMessage?.Body ?? "No messages yet for this order.",
            Items = order.Items
                .OrderBy(item => item.Id)
                .Select(item => new CustomerOrderItemViewModel
                {
                    ProductName = item.ProductName,
                    BrandName = item.BrandName,
                    Quantity = item.Quantity
                })
                .ToList(),
            Messages = orderedMessages
        };
    }

    private static string ResolveSelectedOrderNumber(string? requestedOrderNumber, IReadOnlyList<PharmacyOrder> orders)
    {
        if (!string.IsNullOrWhiteSpace(requestedOrderNumber) &&
            orders.Any(order => string.Equals(order.OrderNumber, requestedOrderNumber.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return requestedOrderNumber.Trim();
        }

        return orders.FirstOrDefault()?.OrderNumber ?? string.Empty;
    }

    private static string BuildItemSummary(IReadOnlyList<PharmacyOrderItem> items)
    {
        if (items.Count == 0)
        {
            return "No items were recorded for this order.";
        }

        var visibleItems = items
            .OrderBy(item => item.Id)
            .Take(2)
            .Select(item => $"{item.ProductName} x{item.Quantity}")
            .ToList();

        if (items.Count > 2)
        {
            visibleItems.Add($"+{items.Count - 2} more");
        }

        return string.Join(", ", visibleItems);
    }
}
