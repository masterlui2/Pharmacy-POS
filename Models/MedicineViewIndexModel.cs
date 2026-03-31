namespace PharmacyPOS.Models;

public class MedicineIndexViewModel
{
    public List<Medicine> Medicines { get; set; } = [];
    public List<string> Categories { get; set; } = [];
    public string? Search { get; set; }
    public string? Category { get; set; }
    public string? Status { get; set; }
}