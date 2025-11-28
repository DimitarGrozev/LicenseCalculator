using LicenseCalculator.Models;
using LicenseCalculator.Utilities.Config;
using LicenseCalculator.Utilities.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace LicenseCalculator.Services;

public class LicenseProviderClient : ILicenseProviderClient
{
	private readonly HttpClient _httpClient;
	private readonly LicenseProviderOptions _options;
	private readonly ILogger<LicenseProviderClient> _logger;
	private readonly JsonSerializerOptions _jsonOptions;

	public LicenseProviderClient(
		HttpClient httpClient,
		IOptions<LicenseProviderOptions> options,
		ILogger<LicenseProviderClient> logger)
	{
		_httpClient = httpClient;
		_options = options?.Value!;
		_logger = logger;

		_jsonOptions = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
		};
	}

	public async Task<IReadOnlyList<Company>> GetCompaniesAsync(string country, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(country))
			throw new ArgumentException("Country cannot be null or empty", nameof(country));

		var url = BuildUrl(_options.GetCompaniesPath, _options.GetCompaniesCode);
		var requestBody = new { country = country.Trim() };

		_logger.LogDebug("Fetching companies for country: {Country}", country);

		var response = await PostAsync<List<Company>>(url, requestBody, ct);
		var companies = response ?? new List<Company>();

		_logger.LogInformation("Retrieved {Count} companies for country: {Country}", companies.Count, country);

		return companies;
	}

	public async Task<CompanyDetails> GetCompanyDetailsAsync(string companyId, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(companyId))
			throw new ArgumentException("Company ID cannot be null or empty", nameof(companyId));

		var url = BuildUrl(_options.GetCompanyDetailsPath, _options.GetCompanyDetailsCode);
		var requestBody = new { CompanyId = companyId.Trim() };

		_logger.LogDebug("Fetching company details for: {CompanyId}", companyId);

		var response = await PostAsync<CompanyDetails>(url, requestBody, ct);

		if (response == null)
		{
			throw new ExternalApiException($"Received null response for company details: {companyId}");
		}

		// Validate essential fields
		if (string.IsNullOrWhiteSpace(response.Company))
		{
			_logger.LogWarning("Company details missing 'Company' field for ID: {CompanyId}", companyId);
		}

		if (string.IsNullOrWhiteSpace(response.Login))
		{
			_logger.LogWarning("Company details missing 'Login' field for ID: {CompanyId}", companyId);
		}

		_logger.LogDebug(
			"Retrieved company details: {Company}, Licenses: {LicenseCount}",
			response.Company,
			response.Licenses?.Count ?? 0);

		return response;
	}

	public async Task<SkuPricing> GetPriceAsync(string sku, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(sku))
			throw new ArgumentException("SKU cannot be null or empty", nameof(sku));

		var url = BuildUrl(_options.GetPricePath, _options.GetPriceCode);
		var requestBody = new { SKU = sku.Trim() };

		_logger.LogDebug("Fetching price for SKU: {SKU}", sku);

		var response = await PostAsync<SkuPricing>(url, requestBody, ct);

		if (response == null)
		{
			throw new ExternalApiException($"Received null response for price of SKU: {sku}");
		}

		// Validate price data
		if (string.IsNullOrWhiteSpace(response.SKU))
		{
			_logger.LogWarning("Price response missing SKU field for requested SKU: {SKU}", sku);
			response.SKU = sku; // Use requested SKU as fallback
		}

		_logger.LogDebug("Retrieved price for SKU {SKU}: {Price}", response.SKU, response.Price);

		return response;
	}

	public async Task<HttpResponseMessage> SubmitResultAsync(SubmitResultRequest request, CancellationToken ct)
	{
		if (request == null)
			throw new ArgumentNullException(nameof(request));

		var url = BuildUrl(_options.SubmitResultPath, _options.SubmitResultCode);

		_logger.LogDebug(
			"Submitting result for company: {CompanyId}, Items: {ItemCount}",
			request.CompanyId,
			request.OrderedLicense?.Count ?? 0);

		var json = JsonSerializer.Serialize(request, _jsonOptions);
		var content = new StringContent(json, Encoding.UTF8, "application/json");

		var response = await _httpClient.PostAsync(url, content, ct);

		_logger.LogDebug("Submit result response: {StatusCode}", (int)response.StatusCode);

		return response;
	}

	private async Task<T?> PostAsync<T>(string url, object requestBody, CancellationToken ct)
	{
		var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
		var content = new StringContent(json, Encoding.UTF8, "application/json");

		using var response = await _httpClient.PostAsync(url, content, ct);

		var responseContent = await response.Content.ReadAsStringAsync(ct);

		if (!response.IsSuccessStatusCode)
		{
			_logger.LogError(
				"API request failed. URL: {Url}, Status: {StatusCode}, Response: {Response}",
				url.Split('?')[0], // Log URL without query params (might contain codes)
				(int)response.StatusCode,
				responseContent);

			throw new ExternalApiException(
				$"API call failed with status {(int)response.StatusCode}: {response.ReasonPhrase}");
		}

		if (string.IsNullOrWhiteSpace(responseContent))
		{
			_logger.LogWarning("Received empty response from API");
			return default;
		}

		try
		{
			var innerJson = JsonSerializer.Deserialize<string>(responseContent);
			return JsonSerializer.Deserialize<T>(innerJson!, _jsonOptions);
		}
		catch (JsonException ex)
		{
			_logger.LogError(
				ex,
				"Failed to deserialize response. Content: {Content}",
				responseContent.Length > 500 ? responseContent.Substring(0, 500) + "..." : responseContent);
			throw new ExternalApiException("Failed to deserialize API response", ex);
		}
	}

	private string BuildUrl(string path, string code)
	{
		if (string.IsNullOrWhiteSpace(path))
			throw new ArgumentException("Path cannot be null or empty", nameof(path));

		if (string.IsNullOrWhiteSpace(code))
			throw new ArgumentException("Code cannot be null or empty", nameof(code));

		var baseUrl = _options.BaseUrl.TrimEnd('/');
		var cleanPath = path.TrimStart('/');

		return $"{baseUrl}/{cleanPath}?code={code}";
	}
}