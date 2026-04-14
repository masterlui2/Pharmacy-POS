using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmacyPOS.Data;
using PharmacyPOS.Models.Admin;
using PharmacyPOS.Services;

namespace PharmacyPOS.Controllers;

public class DashboardController(
    PharmacyPosDbContext dbContext,
    IMedicineService medicineService) : BaseController
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var username = HttpContext.Session.GetString("Username") ?? "Admin";
        var monthStartUtc = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var medicines = medicineService.GetAll().ToList();

        var totalCustomers = await dbContext.Accounts
            .AsNoTracking()
            .CountAsync(account => account.Role == "Customer", cancellationToken);

        var totalOrders = await dbContext.Orders
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var paidOrders = await dbContext.Payments
            .AsNoTracking()
            .CountAsync(payment => payment.Status == "Paid", cancellationToken);

        var pendingOrders = await dbContext.Orders
            .AsNoTracking()
            .CountAsync(order => order.OrderStatus == "Pending", cancellationToken);

        var prescriptionOrders = await dbContext.Orders
            .AsNoTracking()
            .CountAsync(order => order.RequiresPrescription, cancellationToken);

        var totalRevenue = await dbContext.Orders
            .AsNoTracking()
            .SumAsync(order => (decimal?)order.TotalAmount, cancellationToken) ?? 0m;

        var monthlyRevenue = await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.CreatedAtUtc >= monthStartUtc)
            .SumAsync(order => (decimal?)order.TotalAmount, cancellationToken) ?? 0m;

        var revenueByPaymentMethodRaw = await dbContext.Orders
            .AsNoTracking()
            .GroupBy(order => order.PaymentMethod)
            .Select(group => new
            {
                Label = group.Key,
                Amount = group.Sum(order => order.TotalAmount),
            })
            .OrderByDescending(entry => entry.Amount)
            .ToListAsync(cancellationToken);

        var totalBreakdownRevenue = revenueByPaymentMethodRaw.Sum(entry => entry.Amount);
        var revenueByPaymentMethod = revenueByPaymentMethodRaw
            .Select(entry => new AdminRevenueBreakdownViewModel
            {
                Label = string.IsNullOrWhiteSpace(entry.Label) ? "Unknown" : entry.Label,
                Amount = entry.Amount,
                Percentage = totalBreakdownRevenue <= 0
                    ? 0
                    : (double)(entry.Amount / totalBreakdownRevenue * 100m),
            })
            .ToList();

        var topProducts = await dbContext.OrderItems
            .AsNoTracking()
            .GroupBy(item => item.ProductName)
            .Select(group => new AdminTopProductViewModel
            {
                ProductName = group.Key,
                QuantitySold = group.Sum(item => item.Quantity),
                Revenue = group.Sum(item => item.Quantity * item.UnitPrice),
            })
            .OrderByDescending(item => item.QuantitySold)
            .ThenByDescending(item => item.Revenue)
            .Take(5)
            .ToListAsync(cancellationToken);

        var recentOrders = await dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Payment)
            .OrderByDescending(order => order.CreatedAtUtc)
            .Take(6)
            .Select(order => new AdminRecentOrderViewModel
            {
                OrderNumber = order.OrderNumber,
                CustomerName = order.CustomerFullName,
                OrderStatus = order.OrderStatus,
                PaymentStatus = order.Payment != null ? order.Payment.Status : "Pending",
                TotalAmount = order.TotalAmount,
                CreatedAtUtc = order.CreatedAtUtc,
            })
            .ToListAsync(cancellationToken);

        var lowStockMedicines = medicines
            .Where(medicine => medicine.Stock > 0 && medicine.Stock <= 20)
            .OrderBy(medicine => medicine.Stock)
            .ToList();

        var outOfStockMedicines = medicines.Count(medicine => medicine.Stock <= 0);
        var inventoryAlerts = lowStockMedicines
            .Take(4)
            .Select(medicine => $"{medicine.BrandName} is running low with {medicine.Stock} units left.")
            .ToList();

        if (outOfStockMedicines > 0)
        {
            inventoryAlerts.Insert(0, $"{outOfStockMedicines} medicine {(outOfStockMedicines == 1 ? "is" : "are")} currently out of stock.");
        }

        var vm = new AdminDashboardViewModel
        {
            Username = username,
            TotalCustomers = totalCustomers,
            TotalOrders = totalOrders,
            PaidOrders = paidOrders,
            PendingOrders = pendingOrders,
            PrescriptionOrders = prescriptionOrders,
            TotalMedicines = medicines.Count,
            LowStockMedicines = lowStockMedicines.Count,
            OutOfStockMedicines = outOfStockMedicines,
            TotalRevenue = totalRevenue,
            MonthlyRevenue = monthlyRevenue,
            AverageOrderValue = totalOrders == 0 ? 0m : totalRevenue / totalOrders,
            RevenueByPaymentMethod = revenueByPaymentMethod,
            TopProducts = topProducts,
            RecentOrders = recentOrders,
            InventoryAlerts = inventoryAlerts,
        };

        return View(vm);
    }
}
