using System.ComponentModel.DataAnnotations;

namespace LicenseCalculator.Models;

public sealed class OrderRequest
{
	public string Country { get; set; } = default!;

	public string CompanyName { get; set; } = default!;

	public List<OrderLicenseItem> OrderedLicenses { get; set; } = new();
}
