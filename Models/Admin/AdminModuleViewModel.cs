namespace PharmacyPOS.Models.Admin;

public class AdminModuleViewModel
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public List<string> Checklist { get; init; } = [];
}
