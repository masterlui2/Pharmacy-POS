using PharmacyPOS.Models;

namespace PharmacyPOS.Helpers;

public static class FirebaseOrderContract
{
    private static readonly string[] PosPaymentReferencePrefixes = ["COD-", "GCS-", "CRD-"];

    public static IReadOnlyList<string> ResolveDocumentIdCandidates(PharmacyOrder order, PaymentRecord? payment)
    {
        var candidates = new[]
        {
            ResolveDocumentId(order, payment),
            order.OrderNumber?.Trim() ?? string.Empty,
            payment?.ReferenceNumber?.Trim() ?? string.Empty
        };

        return candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public static string ResolveDocumentId(PharmacyOrder order, PaymentRecord? payment)
    {
        var externalOrderId = ResolveExternalOrderId(payment);
        return !string.IsNullOrWhiteSpace(externalOrderId)
            ? externalOrderId
            : order.OrderNumber;
    }

    public static string ResolveOrderReference(PharmacyOrder order, PaymentRecord? payment)
    {
        var externalOrderId = ResolveExternalOrderId(payment);
        if (!string.IsNullOrWhiteSpace(externalOrderId))
        {
            return externalOrderId;
        }

        return !string.IsNullOrWhiteSpace(order.OrderNumber)
            ? order.OrderNumber
            : payment?.ReferenceNumber?.Trim() ?? string.Empty;
    }

    public static bool UsesExternalDocumentId(PaymentRecord? payment) =>
        !string.IsNullOrWhiteSpace(ResolveExternalOrderId(payment));

    private static string ResolveExternalOrderId(PaymentRecord? payment)
    {
        var reference = payment?.ReferenceNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(reference))
        {
            return string.Empty;
        }

        return PosPaymentReferencePrefixes.Any(prefix =>
            reference.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            ? string.Empty
            : reference;
    }
}
