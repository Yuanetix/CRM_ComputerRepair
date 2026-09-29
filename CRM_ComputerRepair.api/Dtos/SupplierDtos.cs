using System.ComponentModel.DataAnnotations;

namespace CRM_ComputerRepair.api.Dtos;

public class CreateSupplierRequest
{
    [Required(ErrorMessage = "Supplier code is required.")]
    [MaxLength(50, ErrorMessage = "Supplier code cannot exceed 50 characters.")]
    public string SupplierCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Supplier name is required.")]
    [MaxLength(200)]
    public string SupplierName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ContactFirstName { get; set; }

    [MaxLength(100)]
    public string? ContactLastName { get; set; }

    [MaxLength(100)]
    public string? ContactPerson
    {
        get
        {
            var name = $"{ContactFirstName} {ContactLastName}".Trim();
            return string.IsNullOrEmpty(name) ? null : name;
        }
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                ContactFirstName = null;
                ContactLastName = null;
                return;
            }
            var parts = value.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            ContactFirstName = parts[0];
            ContactLastName = parts.Length > 1 ? parts[1] : string.Empty;
        }
    }

    [MaxLength(50)]
    public string? ContactNumber { get; set; }

    [EmailAddress(ErrorMessage = "Email address is not valid.")]
    [MaxLength(200)]
    public string? EmailAddress { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? StateOrProvince { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; } = "Philippines";

    [MaxLength(1000)]
    public string? Notes { get; set; }
}