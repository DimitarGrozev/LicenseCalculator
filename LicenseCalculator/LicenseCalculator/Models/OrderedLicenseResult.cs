namespace LicenseCalculator.Models;

public sealed class OrderedLicenseResult
{
	public string SKU { get; set; } = default!;
	public decimal Price { get; set; }
	public int Count { get; set; }
	public decimal Sum { get; set; }
}
