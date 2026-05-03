using PharmacyPOS.Models.Security;

namespace PharmacyPOS.Models.Navigation;

public sealed record SidebarNavigationItem(
    string Section,
    string Label,
    string Controller,
    string Action,
    string Icon,
    string Tooltip,
    params string[] Roles);

public static class BackOfficeNavigation
{
    private static readonly IReadOnlyList<SidebarNavigationItem> Items =
    [
        new(
            "Overview",
            "Sales Analytics",
            "Dashboard",
            "Index",
            "bi-graph-up-arrow",
            "Revenue, order, and fulfillment overview",
            AppRoles.Admin),
        new(
            "Administration",
            "User Accounts",
            "Modules",
            "AdminUsers",
            "bi-person-gear",
            "Manage admin, pharmacist, and customer accounts",
            AppRoles.Admin),
        new(
            "Administration",
            "Medicine Inventory",
            "Medicines",
            "Index",
            "bi-capsule",
            "Maintain medicines, suppliers, pricing, and prescription flags",
            AppRoles.Admin),
        new(
            "Administration",
            "Stock Alerts",
            "Modules",
            "StockAlerts",
            "bi-exclamation-triangle",
            "Monitor low-stock and expiring medicines",
            AppRoles.Admin),
        new(
            "Administration",
            "Audit Logs",
            "Modules",
            "AuditLogs",
            "bi-journal-text",
            "Review recent role, prescription, and payment events",
            AppRoles.Admin),
        new(
            "Operations",
            "Validate Prescriptions",
            "PharmacistModules",
            "Prescriptions",
            "bi-clipboard2-pulse",
            "Review and validate prescription-dependent orders",
            AppRoles.Pharmacist),
        new(
            "Operations",
            "Process Sales / Checkout",
            "PharmacistModules",
            "Sales",
            "bi-cart3",
            "Run pharmacist checkout after prescription validation",
            AppRoles.Pharmacist),
        new(
            "Operations",
            "Handle Payments",
            "PharmacistModules",
            "Payments",
            "bi-credit-card-2-front",
            "Capture or update order payment status",
            AppRoles.Pharmacist),
        new(
            "Operations",
            "Generate Receipts",
            "PharmacistModules",
            "Receipts",
            "bi-receipt-cutoff",
            "Preview, print, and download receipts",
            AppRoles.Pharmacist),
        new(
            "Operations",
            "View Stock Levels",
            "PharmacistModules",
            "StockLevels",
            "bi-box-seam",
            "Check live stock quantities before fulfillment",
            AppRoles.Pharmacist),
        new(
            "Communication",
            "Messages",
            "PharmacistModules",
            "Messages",
            "bi-chat-left-text",
            "Inbox and message threads for pharmacist coordination",
            AppRoles.Pharmacist)
    ];

    public static IReadOnlyList<SidebarNavigationItem> GetItemsForRole(string? role) =>
        Items.Where(item => item.Roles.Any(candidate => AppRoles.Matches(role, candidate))).ToList();

    public static string GetPortalTitle(string? role) =>
        AppRoles.Matches(role, AppRoles.Admin)
            ? "Admin Control"
            : AppRoles.Matches(role, AppRoles.Pharmacist)
                ? "Pharmacist Desk"
                : "Back Office";

    public static string GetRoleLabel(string? role) =>
        AppRoles.Matches(role, AppRoles.Admin)
            ? "Administrator"
            : AppRoles.Matches(role, AppRoles.Pharmacist)
                ? "Pharmacist"
                : "User";
}
