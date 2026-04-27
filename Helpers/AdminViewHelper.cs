namespace PharmacyPOS.Helpers;

public static class AdminViewHelper
{
    public static string StatusClass(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "paid" or "completed" or "success" or "admin" or "in stock" or "available" => "admin-badge admin-badge--success",
            "low stock" or "warning" or "processing" or "expiring soon" or "pending" or "pendingcollection" or "awaitingpayment" or "redirectedtogateway" => "admin-badge admin-badge--warning",
            "out of stock" or "danger" or "failed" or "paymentfailed" or "refunded" or "unavailable" or "unpaid" => "admin-badge admin-badge--danger",
            _ => "admin-badge"
        };
    }

    public static string WorkflowIcon(string step)
    {
        return step switch
        {
            "List View" => "bi-list-ul",
            "Create/Add" => "bi-plus-circle",
            "Edit" => "bi-pencil-square",
            "View Details" => "bi-eye",
            "Actions" => "bi-lightning-charge",
            _ => "bi-circle"
        };
    }

    public static string ActionButtonClass(string style)
    {
        return string.Equals(style, "primary", StringComparison.OrdinalIgnoreCase)
            ? "btn btn-danger admin-hero-action"
            : "btn btn-outline-danger admin-hero-action";
    }

    public static string SelectedReceiptClass(string? selectedOrderNumber, string orderNumber)
    {
        return string.Equals(selectedOrderNumber, orderNumber, StringComparison.OrdinalIgnoreCase)
            ? "admin-list-row--active"
            : string.Empty;
    }

    public static string PaymentMethodLabel(string value)
    {
        return value.Trim() switch
        {
            "CashOnDelivery" => "Cash on Delivery",
            "GCash" => "GCash",
            "PayMaya" => "PayMaya",
            _ => value
        };
    }

    public static string PaymentStatusLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unpaid";
        }

        return value.Trim().Equals("Paid", StringComparison.OrdinalIgnoreCase)
            ? "Paid"
            : "Unpaid";
    }
}
