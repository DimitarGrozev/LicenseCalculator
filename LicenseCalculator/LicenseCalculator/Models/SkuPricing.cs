namespace LicenseCalculator.Models;

public sealed class SkuPricing
{
	public string SKU { get; set; } = default!;
	public decimal Price { get; set; }
}