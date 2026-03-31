using PharmacyPOS.Models;

namespace PharmacyPOS.Services;

public interface IMedicineService
{
    IEnumerable<Medicine> GetAll();
    Medicine? GetById(int id);
    void Add(Medicine medicine);
    void Update(Medicine medicine);
    bool Delete(int id);
}