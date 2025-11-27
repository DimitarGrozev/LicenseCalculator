using System.ComponentModel.DataAnnotations;

namespace LicenseCalculator.Models;

public sealed class OrderLicenseItem
{
	[Required(ErrorMessage = "SKU is required")]
	public string Sku { get; set; } = default!;

	[Range(1, int.MaxValue, ErrorMessage = "Count must be greater than 0")]
	public int Count { get; set; }
}
