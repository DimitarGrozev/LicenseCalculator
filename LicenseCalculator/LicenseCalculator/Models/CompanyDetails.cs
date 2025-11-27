namespace LicenseCalculator.Models;

public sealed class CompanyDetails
{
	public string Company { get; set; } = default!;
	public string Login { get; set; } = default!;
	public Contact Contact { get; set; } = default!;
	public List<License> Licenses { get; set; } = new();
}
