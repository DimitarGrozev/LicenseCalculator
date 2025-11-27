namespace LicenseCalculator.Models;

public sealed class SubmitResultRequest
{
	public string CompanyId { get; set; } = default!;
	public string CompanyName { get; set; } = default!;
	public string UserLogin { get; set; } = default!;
	public string UserName { get; set; } = default!;
	public List<OrderedLicenseResult> OrderedLicense { get; set; } = new();
}
