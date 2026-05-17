using System.ComponentModel.DataAnnotations;

namespace PharmacyPOS.Models;

public class PharmacyOrder
{
    public int Id { get; set; }

    [Required]
    public string OrderNumber { get; set; } = string.Empty;

    public int? AccountId { get; set; }

    [Required]
    public string CustomerFullName { get; set; } = string.Empty;

    public string CustomerUid { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;

    [Required]
    public string CustomerPhoneNumber { get; set; } = string.Empty;

    [Required]
    public string DeliveryAddress { get; set; } = string.Empty;

    public string Landmark { get; set; } = string.Empty;

    [Required]
    public string AddressType { get; set; } = "Home";

    [Required]
    public string DeliveryOption { get; set; } = "Standard";

    [Required]
    public string PaymentMethod { get; set; } = "CashOnDelivery";

    [Required]
    public string FulfillmentBranch { get; set; } = string.Empty;

    [Required]
    public string PrescriptionStatus { get; set; } = "Missing";

    [Required]
    public string OrderStatus { get; set; } = "Pending";

    public bool RequiresPrescription { get; set; }

    public int EstimatedDeliveryMinMinutes { get; set; }

    public int EstimatedDeliveryMaxMinutes { get; set; }

    public decimal SubtotalAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal ShippingAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string PromoCode { get; set; } = string.Empty;

    public string PrescriptionFilesJson { get; set; } = "[]";

    public string PerformedByName { get; set; } = string.Empty;

    public string PerformedByRole { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Account? Account { get; set; }

    public List<PharmacyOrderItem> Items { get; set; } = [];

    public PaymentRecord? Payment { get; set; }
}
