namespace PharmacyPOS.Models;

public sealed class FirebasePharmacistIdentity
{
    public string Uid { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string PharmacyName { get; init; } = string.Empty;
}

public sealed class FirebaseOrderChatMessage
{
    public string Id { get; init; } = string.Empty;

    public string Type { get; init; } = "text";

    public string Text { get; init; } = string.Empty;

    public string OrderId { get; init; } = string.Empty;

    public string OrderReference { get; init; } = string.Empty;

    public string SenderUid { get; init; } = string.Empty;

    public string SenderRole { get; init; } = string.Empty;

    public string SenderName { get; init; } = string.Empty;

    public string RecipientRole { get; init; } = string.Empty;

    public DateTime? CreatedAtUtc { get; init; }
}

public sealed class FirebaseOrderChatMessageWriteRequest
{
    public string SenderId { get; init; } = string.Empty;

    public string SenderRole { get; init; } = string.Empty;

    public string SenderName { get; init; } = string.Empty;

    public string RecipientRole { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public FirebasePharmacistIdentity? Pharmacist { get; init; }
}
