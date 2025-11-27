using LicenseCalculator.Models;
using LicenseCalculator.Utilities.Exceptions;
using Microsoft.Extensions.Logging;

namespace LicenseCalculator.Services;

public sealed class OrderOrchestrator : IOrderOrchestrator
{
	private readonly ILicenseProviderClient _client;
	private readonly ILogger<OrderOrchestrator> _logger;

	public OrderOrchestrator(ILicenseProviderClient client, ILogger<OrderOrchestrator> logger)
	{
		_client = client;
		_logger = logger;
	}

	public async Task<SubmitResultResponse> ProcessOrderAsync(OrderRequest request, CancellationToken cancellationToken = default)
	{
		// 1. Get companies
		var companies = await _client.GetCompaniesAsync(request.Country, cancellationToken);

		if (companies == null || companies.Count == 0)
		{
			throw new DomainException($"No companies found in country '{request.Country}'.");
		}

		// 2. Find target company
		var company = companies
			.FirstOrDefault(c => string.Equals(
				c.CompanyName?.Trim(),
				request.Company?.Trim(),
				StringComparison.OrdinalIgnoreCase));

		if (company is null)
		{
			var availableCompanies = string.Join(", ", companies.Select(c => c.CompanyName).Take(5));
			throw new DomainException(
				$"Company '{request.Company}' not found in country '{request.Country}'. " +
				$"Available companies: {availableCompanies}{(companies.Count > 5 ? "..." : "")}");
		}

		_logger.LogInformation("Found company: {CompanyId} - {CompanyName}", company.CompanyId, company.CompanyName);

		// 3. Get company details (includes licenses in the response)
		var companyDetails = await _client.GetCompanyDetailsAsync(company.CompanyId, cancellationToken);

		// Guard against null or empty licenses
		if (companyDetails.Licenses == null || companyDetails.Licenses.Count == 0)
		{
			throw new DomainException(
				$"No licenses found for company '{company.CompanyName}' (ID: {company.CompanyId}).");
		}

		// 4. Validate all requested SKUs exist and deduplicate
		var requestedSkus = request.Licenses!
			.Where(license => !string.IsNullOrWhiteSpace(license))
			.Distinct()
			.ToList();

		var missingSkus = requestedSkus
			.Where(l => !companyDetails.Licenses.Any(license => string.Equals(license.SKU, l, StringComparison.OrdinalIgnoreCase)))
			.ToList();

		if (missingSkus.Count > 0)
		{
			var availableSkus = string.Join(", ", companyDetails.Licenses.Select(l => l.SKU).Take(10));
			throw new DomainException(
				$"Requested SKUs not found for company '{company.CompanyName}': {string.Join(", ", missingSkus)}. " +
				$"Available SKUs: {availableSkus}{(companyDetails.Licenses.Count > 10 ? "..." : "")}");
		}

		// 5. Fetch prices in parallel and calculate sums
		var priceTasks = requestedSkus
			.Select(async sku =>
			{
				try
				{
					var skuPricing = await _client.GetPriceAsync(sku, cancellationToken);
					var companyLicenseInfo = companyDetails.Licenses
						.First(l => string.Equals(l.SKU, sku, StringComparison.OrdinalIgnoreCase));

					// Validate price is non-negative
					if (skuPricing.Price < 0)
					{
						_logger.LogWarning(
							"Negative price returned for SKU {SKU}: {Price}",
							sku, skuPricing.Price);
					}

					var licenseSum = skuPricing.Price * companyLicenseInfo.Count;

					_logger.LogDebug(
						"SKU {SKU}: Price={Price}, Count={Count}, Sum={Sum}",
						sku, skuPricing.Price, companyLicenseInfo.Count, licenseSum);

					return new OrderedLicenseResult
					{
						SKU = skuPricing.SKU,
						Price = skuPricing.Price,
						Count = companyLicenseInfo.Count,
						Sum = licenseSum
					};
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed to fetch price for SKU {SKU}", sku);
					throw new ExternalApiException($"Failed to fetch price for SKU {sku}", ex);
				}
			});

		var orderedLicenseResults = await Task.WhenAll(priceTasks);

		// 6. Build submit request with defensive string handling
		var submitRequest = new SubmitResultRequest
		{
			CompanyId = company.CompanyId?.Trim() ?? string.Empty,
			CompanyName = companyDetails.Company?.Trim() ?? company.CompanyName?.Trim() ?? string.Empty,
			UserLogin = companyDetails.Login?.Trim() ?? string.Empty,
			UserName = companyDetails.Contact?.Name?.Trim() ?? "Unknown User",
			OrderedLicense = orderedLicenseResults.ToList()
		};

		_logger.LogInformation(
			"Submitting order: Company={CompanyName}, User={UserName}, Items={ItemCount}, TotalValue={TotalValue:C}",
			submitRequest.CompanyName,
			submitRequest.UserName,
			submitRequest.OrderedLicense.Count,
			submitRequest.OrderedLicense.Sum(x => x.Sum));

		// 7. Submit result
		var responseMessage = await _client.SubmitResultAsync(submitRequest, cancellationToken);
		var responseContent = await responseMessage.Content.ReadAsStringAsync(cancellationToken);

		if (!responseMessage.IsSuccessStatusCode)
		{
			throw new ExternalApiException(
				$"SubmitResult failed with status {(int)responseMessage.StatusCode}: {responseContent}");
		}

		return new SubmitResultResponse { Data = responseContent };
	}
}