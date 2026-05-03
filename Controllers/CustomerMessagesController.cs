using Microsoft.AspNetCore.Mvc;
using PharmacyPOS.Models.Messages;
using PharmacyPOS.Models.Security;
using PharmacyPOS.Services;

namespace PharmacyPOS.Controllers;

public sealed class CustomerMessagesController(
    IPharmacistMessagingService messagingService) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var role = HttpContext.Session.GetString("Role") ?? string.Empty;
        if (AppRoles.IsBackOffice(role))
        {
            return RedirectToAction("Messages", "PharmacistModules");
        }

        var customerName = HttpContext.Session.GetString("Username") ?? string.Empty;
        var customerEmail = HttpContext.Session.GetString("Email") ?? string.Empty;
        var thread = await messagingService.GetCustomerThreadAsync(customerEmail, cancellationToken);

        if (thread is not null)
        {
            await messagingService.MarkThreadAsReadForCustomerAsync(thread.Id, cancellationToken);
            thread = await messagingService.GetCustomerThreadAsync(customerEmail, cancellationToken);
        }

        var model = new CustomerMessagesViewModel
        {
            CustomerName = customerName,
            Subject = string.IsNullOrWhiteSpace(thread?.Subject) ? "Pharmacist Support" : thread!.Subject,
            UnreadCount = await messagingService.GetUnreadCountForCustomerAsync(customerEmail, cancellationToken),
            HasThread = thread is not null,
            UpdatedAtUtc = thread?.UpdatedAtUtc,
            Messages = thread?.Messages
                .OrderBy(message => message.SentAtUtc)
                .Select(message => new CustomerMessageEntryViewModel
                {
                    SenderName = message.SenderName,
                    SenderRole = message.SenderRole,
                    Body = message.Body,
                    SentAtUtc = message.SentAtUtc,
                    IsCustomerMessage = AppRoles.Matches(message.SenderRole, AppRoles.Customer)
                })
                .ToList() ?? []
        };

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

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            TempData["Error"] = "Message body is required.";
            return RedirectToAction(nameof(Index));
        }

        var customerName = HttpContext.Session.GetString("Username") ?? string.Empty;
        var customerEmail = HttpContext.Session.GetString("Email") ?? string.Empty;
        var customerPhone = HttpContext.Session.GetString("PhoneNumber") ?? string.Empty;

        await messagingService.SendCustomerMessageAsync(
            customerName,
            customerEmail,
            customerPhone,
            request.Subject,
            request.Body,
            cancellationToken);

        TempData["Success"] = "Message sent to the pharmacist.";
        return RedirectToAction(nameof(Index));
    }
}
