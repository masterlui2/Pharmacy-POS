using PharmacyPOS.Models;

namespace PharmacyPOS.Services;

public class InMemoryMedicineService : IMedicineService
{
    private readonly List<Medicine> _medicines =
    [
        new() { Id = 1, Code = "MED-001", BrandName = "Biogesic", GenericName = "Paracetamol", Category = "Analgesic", Price = 6.50m, Stock = 120, ExpiryDate = DateTime.Today.AddMonths(16) },
        new() { Id = 2, Code = "MED-002", BrandName = "Amoxil", GenericName = "Amoxicillin", Category = "Antibiotic", Price = 18.75m, Stock = 35, ExpiryDate = DateTime.Today.AddMonths(10) },
        new() { Id = 3, Code = "MED-003", BrandName = "Neozep", GenericName = "Phenylephrine + Chlorphenamine + Paracetamol", Category = "Cold & Flu", Price = 8.25m, Stock = 14, ExpiryDate = DateTime.Today.AddMonths(8) },
        new() { Id = 4, Code = "MED-004", BrandName = "Diatabs", GenericName = "Loperamide", Category = "Gastrointestinal", Price = 12.00m, Stock = 0, ExpiryDate = DateTime.Today.AddMonths(12) }
    ];

    public IEnumerable<Medicine> GetAll() => _medicines.OrderBy(m => m.BrandName);

    public Medicine? GetById(int id) => _medicines.FirstOrDefault(m => m.Id == id);

    public void Add(Medicine medicine)
    {
        medicine.Id = _medicines.Count == 0 ? 1 : _medicines.Max(m => m.Id) + 1;
        _medicines.Add(medicine);
    }

    public void Update(Medicine medicine)
    {
        var existing = GetById(medicine.Id);
        if (existing is null) return;

        existing.Code = medicine.Code;
        existing.BrandName = medicine.BrandName;
        existing.GenericName = medicine.GenericName;
        existing.Category = medicine.Category;
        existing.Price = medicine.Price;
        existing.Stock = medicine.Stock;
        existing.ExpiryDate = medicine.ExpiryDate;
    }

    public bool Delete(int id)
    {
        var existing = GetById(id);
        if (existing is null) return false;

        _medicines.Remove(existing);
        return true;
    }
}