using System.ComponentModel.DataAnnotations;

namespace PharmacyPOS.Models;

public class RegisterViewModel : IValidatableObject
{
    [Required]
    [RegularExpression(@"^[A-Za-z]+(?:[ .][A-Za-z]+)*$", ErrorMessage = "Only letters, spaces, and periods are allowed.")]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^[A-Za-z]+(?:[ .][A-Za-z]+)*$", ErrorMessage = "Only letters, spaces, and periods are allowed.")]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    [StringLength(32)]
    [Display(Name = "Phone number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public bool AcceptTerms { get; set; }

    public bool AcceptPrivacyPolicy { get; set; }

    public string RecaptchaToken { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!AcceptTerms)
        {
            yield return new ValidationResult(
                "You must agree to the Terms and Conditions.",
                [nameof(AcceptTerms)]);
        }

        if (!AcceptPrivacyPolicy)
        {
            yield return new ValidationResult(
                "You must provide privacy consent.",
                [nameof(AcceptPrivacyPolicy)]);
        }

        if (!string.IsNullOrWhiteSpace(Password))
        {
            if (!Password.Any(char.IsLetter))
            {
                yield return new ValidationResult(
                    "Password must include at least one letter.",
                    [nameof(Password)]);
            }

            if (!Password.Any(char.IsUpper))
            {
                yield return new ValidationResult(
                    "Password must include at least one capital letter.",
                    [nameof(Password)]);
            }

            if (!Password.Any(char.IsDigit))
            {
                yield return new ValidationResult(
                    "Password must include at least one number.",
                    [nameof(Password)]);
            }

            if (Password.All(char.IsLetterOrDigit))
            {
                yield return new ValidationResult(
                    "Password must include at least one special character.",
                    [nameof(Password)]);
            }

            if (Password.Length < 6)
            {
                yield return new ValidationResult(
                    "Password must be at least 6 characters.",
                    [nameof(Password)]);
            }
        }
    }
}
