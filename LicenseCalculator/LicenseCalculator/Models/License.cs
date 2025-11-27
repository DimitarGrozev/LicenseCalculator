namespace LicenseCalculator.Models;

public sealed class License
{
	public string SKU { get; set; } = default!;
	public int Count { get; set; }
}
