namespace LicenseCalculator.Models;

public sealed class OrderRequest
{
	public string Country { get; set; } = default!;

	public string Company { get; set; } = default!;

	public List<string> Licenses { get; set; } = new();
}
