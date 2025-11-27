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

	public async Task<SubmitResultResponse> ProcessOrderAsync(OrderRequest request, CancellationToken ct)
	{
		if (request == null || string.IsNullOrWhiteSpace(request.CompanyName) || string.IsNullOrWhiteSpace(request.Country))
			throw new ArgumentNullException(nameof(request));

		_logger.LogInformation(
			"Processing order for company '{CompanyName}' in country '{Country}' with {LicenseCount} licenses",
			request.CompanyName, request.Country, request.OrderedLicenses?.Count ?? 0);

		// 1. Get companies
		var companies = await _client.GetCompaniesAsync(request.Country, ct);
		if (companies == null || companies.Count == 0)
		{
			throw new DomainException($"No companies found in country '{request.Country}'.");
		}

		// 2. Find target company (case-insensitive, whitespace-tolerant)
		var company = companies
			.FirstOrDefault(c => string.Equals(
				c.CompanyName?.Trim(),
				request.CompanyName?.Trim(),
				StringComparison.OrdinalIgnoreCase));

		if (company is null)
		{
			var availableCompanies = string.Join(", ", companies.Select(c => c.CompanyName).Take(5));
			throw new DomainException(
				$"Company '{request.CompanyName}' not found in country '{request.Country}'. " +
				$"Available companies: {availableCompanies}{(companies.Count > 5 ? "..." : "")}");
		}

		_logger.LogInformation("Found company: {CompanyId} - {CompanyName}", company.CompanyId, company.CompanyName);

		// 3. Get company details (includes licenses in the response)
		var companyDetails = await _client.GetCompanyDetailsAsync(company.CompanyId, ct);

		// Guard against null or empty licenses
		if (companyDetails.Licenses == null || companyDetails.Licenses.Count == 0)
		{
			throw new DomainException(
				$"No licenses found for company '{company.CompanyName}' (ID: {company.CompanyId}).");
		}

		// 4. Build SKU map from licenses (case-insensitive, whitespace-tolerant)
		var skuMap = companyDetails.Licenses
			.Where(x => !string.IsNullOrWhiteSpace(x.SKU))
			.ToDictionary(x => x.SKU.Trim(), x => x, StringComparer.OrdinalIgnoreCase);

		// 5. Validate all requested SKUs exist and deduplicate
		var requestedSkus = request.OrderedLicenses
			.Where(l => !string.IsNullOrWhiteSpace(l.Sku))
			.GroupBy(l => l.Sku.Trim(), StringComparer.OrdinalIgnoreCase)
			.Select(g => new
			{
				Sku = g.Key,
				Count = g.Sum(x => x.Count),
				OriginalCount = g.Count()
			})
			.ToList();

		// Log if duplicates were found
		var duplicates = requestedSkus.Where(x => x.OriginalCount > 1).ToList();
		if (duplicates.Any())
		{
			_logger.LogWarning(
				"Duplicate SKUs found in request and consolidated: {DuplicateSKUs}",
				string.Join(", ", duplicates.Select(d => $"{d.Sku} (x{d.OriginalCount})")));
		}

		// Check for missing SKUs
		var missingSkus = requestedSkus
			.Where(l => !skuMap.ContainsKey(l.Sku))
			.Select(l => l.Sku)
			.ToList();

		if (missingSkus.Count > 0)
		{
			var availableSkus = string.Join(", ", skuMap.Keys.Take(10));
			throw new DomainException(
				$"Requested SKUs not found for company '{company.CompanyName}': {string.Join(", ", missingSkus)}. " +
				$"Available SKUs: {availableSkus}{(skuMap.Count > 10 ? "..." : "")}");
		}

		// Validate requested quantities don't exceed available licenses
		var insufficientLicenses = requestedSkus
			.Where(l => skuMap.TryGetValue(l.Sku, out var license) && l.Count > license.Count)
			.Select(l => $"{l.Sku} (requested: {l.Count}, available: {skuMap[l.Sku].Count})")
			.ToList();

		if (insufficientLicenses.Any())
		{
			_logger.LogWarning(
				"Requested quantities exceed available licenses: {InsufficientLicenses}",
				string.Join(", ", insufficientLicenses));
			// Note: Not throwing here as API might allow this - log for monitoring
		}

		// 6. Fetch prices in parallel and build order items
		var priceTasks = requestedSkus
			.Select(async item =>
			{
				try
				{
					var priceDto = await _client.GetPriceAsync(item.Sku, ct);

					// Validate price is non-negative
					if (priceDto.Price < 0)
					{
						_logger.LogWarning(
							"Negative price returned for SKU {SKU}: {Price}",
							item.Sku, priceDto.Price);
					}

					var sum = priceDto.Price * item.Count;

					_logger.LogDebug(
						"SKU {SKU}: Price={Price}, Count={Count}, Sum={Sum}",
						item.Sku, priceDto.Price, item.Count, sum);

					return new OrderedLicenseResult
					{
						SKU = priceDto.SKU, // Use SKU from price response for consistency
						Price = priceDto.Price,
						Count = item.Count,
						Sum = sum
					};
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed to fetch price for SKU {SKU}", item.Sku);
					throw new ExternalApiException($"Failed to fetch price for SKU {item.Sku}", ex);
				}
			});

		var orderedLicenseResults = await Task.WhenAll(priceTasks);

		// 7. Build submit request with defensive string handling
		var submitRequest = new SubmitResultRequest
		{
			CompanyId = company.CompanyId?.Trim() ?? string.Empty,
			CompanyName = companyDetails.Company?.Trim() ?? company.CompanyName?.Trim() ?? string.Empty,
			UserLogin = companyDetails.Login?.Trim() ?? string.Empty,
			UserName = BuildUserName(companyDetails.Contact),
			OrderedLicense = orderedLicenseResults.ToList()
		};

		// Validate submit request before sending
		if (string.IsNullOrWhiteSpace(submitRequest.CompanyId) ||
			string.IsNullOrWhiteSpace(submitRequest.UserLogin) ||
			submitRequest.OrderedLicense.Count == 0)
		{
			throw new DomainException(
				"Cannot submit order: missing required fields (CompanyId, UserLogin, or OrderedLicense)");
		}

		_logger.LogInformation(
			"Submitting order: Company={CompanyName}, User={UserName}, Items={ItemCount}, TotalValue={TotalValue:C}",
			submitRequest.CompanyName,
			submitRequest.UserName,
			submitRequest.OrderedLicense.Count,
			submitRequest.OrderedLicense.Sum(x => x.Sum));

		// 8. Submit result
		var responseMessage = await _client.SubmitResultAsync(submitRequest, ct);
		var responseContent = await responseMessage.Content.ReadAsStringAsync(ct);

		if (!responseMessage.IsSuccessStatusCode)
		{
			throw new ExternalApiException(
				$"SubmitResult failed with status {(int)responseMessage.StatusCode}: {responseContent}");
		}

		_logger.LogInformation(
			"Order processed successfully for company {CompanyId}",
			submitRequest.CompanyId);

		return new SubmitResultResponse { Raw = responseContent };
	}

	private static string BuildUserName(Contact? contact)
	{
		if (contact == null)
			return "Unknown User";

		var firstName = contact.Name?.Trim() ?? string.Empty;
		var lastName = contact.Surname?.Trim() ?? string.Empty;

		if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(lastName))
			return "Unknown User";

		return $"{firstName} {lastName}".Trim();
	}
}