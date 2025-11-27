using System.ComponentModel.DataAnnotations;

namespace LicenseCalculator.Utilities.Config;

public class LicenseProviderOptions
{
	public const string SectionName = "LicenseProvider";	

	[Required(ErrorMessage = "LicenseProvider:BaseUrl is required")]
	[Url(ErrorMessage = "LicenseProvider:BaseUrl must be a valid URL")]
	public string BaseUrl { get; set; } = default!;

	[Required(ErrorMessage = "LicenseProvider:GetCompaniesPath is required")]
	public string GetCompaniesPath { get; set; } = default!;

	[Required(ErrorMessage = "LicenseProvider:GetCompanyDetailsPath is required")]
	public string GetCompanyDetailsPath { get; set; } = default!;

	[Required(ErrorMessage = "LicenseProvider:GetPricePath is required")]
	public string GetPricePath { get; set; } = default!;

	[Required(ErrorMessage = "LicenseProvider:SubmitResultPath is required")]
	public string SubmitResultPath { get; set; } = default!;

	[Required(ErrorMessage = "LicenseProvider:GetCompaniesCode is required")]
	public string GetCompaniesCode { get; set; } = default!;

	[Required(ErrorMessage = "LicenseProvider:GetCompanyDetailsCode is required")]
	public string GetCompanyDetailsCode { get; set; } = default!;

	[Required(ErrorMessage = "LicenseProvider:GetPriceCode is required")]
	public string GetPriceCode { get; set; } = default!;

	[Required(ErrorMessage = "LicenseProvider:SubmitResultCode is required")]
	public string SubmitResultCode { get; set; } = default!;

	[Range(1, 300, ErrorMessage = "LicenseProvider:DefaultTimeoutSeconds must be between 1 and 300 seconds")]
	public int DefaultTimeoutSeconds { get; set; } = 10;
}