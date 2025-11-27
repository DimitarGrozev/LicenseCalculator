namespace LicenseCalculator.Models;

public sealed class Address
{
	public string Country { get; set; } = default!;
	public string City { get; set; } = default!;
	public string Street { get; set; } = default!;
	public string House { get; set; } = default!;
	public string Zip { get; set; } = default!;
}
