using LicenseCalculator.Models;

namespace LicenseCalculator.Services;

public interface ILicenseProviderClient
{
	Task<IReadOnlyList<Company>> GetCompaniesAsync(string country, CancellationToken ct);
	Task<CompanyDetails> GetCompanyDetailsAsync(string companyId, CancellationToken ct);
	Task<SkuPricing> GetPriceAsync(string sku, CancellationToken ct);
	Task<HttpResponseMessage> SubmitResultAsync(SubmitResultRequest request, CancellationToken ct);
}