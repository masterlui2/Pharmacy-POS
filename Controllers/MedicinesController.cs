using Microsoft.AspNetCore.Mvc;
using PharmacyPOS.Models;
using PharmacyPOS.Services;

namespace PharmacyPOS.Controllers;

public class MedicinesController(IMedicineService medicineService) : BaseController
{
    public IActionResult Index(string? search, string? category, string? status)
    {
        var medicines = medicineService.GetAll();

        if (!string.IsNullOrWhiteSpace(search))
        {
            medicines = medicines.Where(m =>
                m.Code.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                m.BrandName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                m.GenericName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            medicines = medicines.Where(m => m.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            medicines = medicines.Where(m => m.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        var all = medicineService.GetAll();
        var vm = new MedicineIndexViewModel
        {
            Medicines = medicines.ToList(),
            Categories = all.Select(m => m.Category).Distinct().OrderBy(c => c).ToList(),
            Search = search,
            Category = category,
            Status = status
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
