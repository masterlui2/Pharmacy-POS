using Microsoft.AspNetCore.Mvc;
using PharmacyPOS.Models;
using PharmacyPOS.Services;

namespace PharmacyPOS.Controllers;

public class MedicinesController(IMedicineService medicineService) : AdminController
{
    private const int DefaultPageSize = 10;
    private static readonly int[] AllowedPageSizes = [10, 25, 50];

    public IActionResult Index(string? search, string? category, string? status, int page = 1, int pageSize = DefaultPageSize)
    {
        var medicines = medicineService.GetAll();

        if (!string.IsNullOrWhiteSpace(search))
        {
            medicines = medicines.Where(m =>
                m.Code.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                m.BrandName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                m.GenericName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                m.Supplier.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            medicines = medicines.Where(m => m.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            medicines = medicines.Where(m => m.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        var all = medicineService.GetAll().ToList();
        var filtered = medicines.ToList();
        var normalizedPageSize = AllowedPageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;
        var totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)normalizedPageSize));
        var currentPage = Math.Clamp(page, 1, totalPages);

        var vm = new MedicineIndexViewModel
        {
            Medicines = filtered
                .Skip((currentPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToList(),
            Categories = all.Select(m => m.Category).Distinct().OrderBy(c => c).ToList(),
            Search = search,
            Category = category,
            Status = status,
            Pagination = new PharmacyPOS.Models.Admin.AdminPaginationViewModel
            {
                Controller = "Medicines",
                Action = nameof(Index),
                CurrentPage = currentPage,
                PageSize = normalizedPageSize,
                TotalItems = filtered.Count,
                RouteValues = new Dictionary<string, string>(
                    new[]
                    {
                        new KeyValuePair<string, string?>("search", search),
                        new KeyValuePair<string, string?>("category", category),
                        new KeyValuePair<string, string?>("status", status)
                    }
                    .Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
                    .ToDictionary(entry => entry.Key, entry => entry.Value!))
            }
        };

        return View(vm);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new Medicine { ExpiryDate = DateTime.Today.AddMonths(12) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Medicine medicine)
    {
        if (!ModelState.IsValid)
        {
            return View(medicine);
        }

        medicineService.Add(medicine);
        TempData["Success"] = "Medicine added successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        var medicine = medicineService.GetById(id);
        if (medicine is null) return NotFound();

        return View(medicine);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(Medicine medicine)
    {
        if (!ModelState.IsValid)
        {
            return View(medicine);
        }

        medicineService.Update(medicine);
        TempData["Success"] = "Medicine updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Details(int id)
    {
        var medicine = medicineService.GetById(id);
        if (medicine is null) return NotFound();

        return View(medicine);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        medicineService.Delete(id);
        TempData["Success"] = "Medicine deleted.";
        return RedirectToAction(nameof(Index));
    }
}
