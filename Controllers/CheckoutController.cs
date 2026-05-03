using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using PharmacyPOS.Models.Checkout;
using PharmacyPOS.Services;

namespace PharmacyPOS.Controllers;

[Route("checkout")]
public class CheckoutController(
    ICheckoutService checkoutService,
    IWebHostEnvironment environment) : Controller
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".webp"
    };

    private const long MaxFileBytes = 10 * 1024 * 1024;

    [HttpPost("place-order")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        var customerEmail = HttpContext.Session.GetString("Email") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            return Unauthorized(new PlaceOrderResult
            {
                Success = false,
                Message = "Sign in to continue with checkout."
            });
        }

        var successReturnUrl = Url.Action("Index", "Orders", values: null, protocol: Request.Scheme);
        var cancelReturnUrl = Url.Action("Cart", "Home", values: null, protocol: Request.Scheme);
        var result = await checkoutService.PlaceOrderAsync(
            request,
            customerEmail,
            successReturnUrl,
            cancelReturnUrl,
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Json(result);
    }

    [HttpPost("upload-prescriptions")]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> UploadPrescriptions(List<IFormFile> files, CancellationToken cancellationToken)
    {
        var customerEmail = HttpContext.Session.GetString("Email") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            return Unauthorized(new PrescriptionUploadResponse
            {
                Success = false,
                Message = "Sign in to upload prescription files."
            });
        }

        if (files.Count == 0)
        {
            return BadRequest(new PrescriptionUploadResponse
            {
                Success = false,
                Message = "Choose at least one prescription file."
            });
        }

        var webRootPath = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        var relativeFolder = Path.Combine("uploads", "prescriptions", DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        var absoluteFolder = Path.Combine(webRootPath, relativeFolder);
        Directory.CreateDirectory(absoluteFolder);

        var uploadedFiles = new List<PrescriptionFileReference>();
        foreach (var file in files)
        {
            if (file.Length <= 0)
            {
                continue;
            }

            if (file.Length > MaxFileBytes)
            {
                return BadRequest(new PrescriptionUploadResponse
                {
                    Success = false,
                    Message = $"{file.FileName} exceeds the 10MB upload limit."
                });
            }

            var extension = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.Contains(extension))
            {
                return BadRequest(new PrescriptionUploadResponse
                {
                    Success = false,
                    Message = $"{file.FileName} is not a supported prescription format."
                });
            }

            var safeBaseName = Path.GetFileNameWithoutExtension(file.FileName);
            safeBaseName = string.Concat(safeBaseName.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')).Trim('-');
            if (string.IsNullOrWhiteSpace(safeBaseName))
            {
                safeBaseName = "prescription";
            }

            var savedFileName = $"{safeBaseName}-{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var absolutePath = Path.Combine(absoluteFolder, savedFileName);

            await using var stream = System.IO.File.Create(absolutePath);
            await file.CopyToAsync(stream, cancellationToken);

            uploadedFiles.Add(new PrescriptionFileReference
            {
                Name = file.FileName,
                Url = "/" + Path.Combine(relativeFolder, savedFileName).Replace("\\", "/"),
                ContentType = file.ContentType
            });
        }

        return Json(new PrescriptionUploadResponse
        {
            Success = true,
            Message = "Prescription uploaded successfully.",
            Files = uploadedFiles
        });
    }
}
