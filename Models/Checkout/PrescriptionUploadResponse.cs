namespace PharmacyPOS.Models.Checkout;

public sealed class PrescriptionUploadResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public List<PrescriptionFileReference> Files { get; init; } = [];
}

public sealed class PrescriptionFileReference
{
    public string Name { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
}
