using System.Net;
using System.Text.Json;
using LicenseCalculator.Models;
using LicenseCalculator.Services;
using LicenseCalculator.Utilities.Config;
using LicenseCalculator.Utilities.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace LicenseCalculator.Tests.Services;

public class LicenseProviderClientTests
{
	private readonly Mock<ILogger<LicenseProviderClient>> _mockLogger;
	private readonly LicenseProviderOptions _options;
	private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
	private readonly HttpClient _httpClient;
	private readonly LicenseProviderClient _client;

	public LicenseProviderClientTests()
	{
		_mockLogger = new Mock<ILogger<LicenseProviderClient>>();

		_options = new LicenseProviderOptions
		{
			BaseUrl = "https://api.example.com",
			GetCompaniesPath = "companies",
			GetCompanyDetailsPath = "company-details",
			GetPricePath = "price",
			SubmitResultPath = "submit",
			GetCompaniesCode = "code123",
			GetCompanyDetailsCode = "code456",
			GetPriceCode = "code789",
			SubmitResultCode = "code000"
		};

		_mockHttpMessageHandler = new Mock<HttpMessageHandler>();
		_httpClient = new HttpClient(_mockHttpMessageHandler.Object);

		_client = new LicenseProviderClient(
			_httpClient,
			Options.Create(_options),
			_mockLogger.Object);
	}

	#region GetCompaniesAsync Tests

	[Fact]
	public async Task GetCompaniesAsync_ValidCountry_ReturnsCompanies()
	{
		// Arrange
		var companies = new List<Company>
		{
			new() { CompanyId = "C1", CompanyName = "Company One" },
			new() { CompanyId = "C2", CompanyName = "Company Two" }
		};

		SetupHttpResponse(companies);

		// Act
		var result = await _client.GetCompaniesAsync("Latvia", CancellationToken.None);

		// Assert
		Assert.NotNull(result);
		Assert.Equal(2, result.Count);
		Assert.Equal("C1", result[0].CompanyId);
		Assert.Equal("Company One", result[0].CompanyName);
	}

	[Fact]
	public async Task GetCompaniesAsync_EmptyResponse_ReturnsEmptyList()
	{
		// Arrange
		SetupHttpResponse(new List<Company>());

		// Act
		var result = await _client.GetCompaniesAsync("Latvia", CancellationToken.None);

		// Assert
		Assert.NotNull(result);
		Assert.Empty(result);
	}

	[Fact]
	public async Task GetCompaniesAsync_NullCountry_ThrowsArgumentException()
	{
		// Act & Assert
		await Assert.ThrowsAsync<ArgumentException>(
			() => _client.GetCompaniesAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetCompaniesAsync_EmptyCountry_ThrowsArgumentException()
	{
		// Act & Assert
		await Assert.ThrowsAsync<ArgumentException>(
			() => _client.GetCompaniesAsync("", CancellationToken.None));
	}

	[Fact]
	public async Task GetCompaniesAsync_WhitespaceCountry_ThrowsArgumentException()
	{
		// Act & Assert
		await Assert.ThrowsAsync<ArgumentException>(
			() => _client.GetCompaniesAsync("   ", CancellationToken.None));
	}

	[Fact]
	public async Task GetCompaniesAsync_ApiReturnsError_ThrowsExternalApiException()
	{
		// Arrange
		SetupHttpErrorResponse(HttpStatusCode.InternalServerError, "Server error");

		// Act & Assert
		var exception = await Assert.ThrowsAsync<ExternalApiException>(
			() => _client.GetCompaniesAsync("Latvia", CancellationToken.None));

		Assert.Contains("API call failed with status 500", exception.Message);
	}

	#endregion

	#region GetCompanyDetailsAsync Tests

	[Fact]
	public async Task GetCompanyDetailsAsync_ValidCompanyId_ReturnsDetails()
	{
		// Arrange
		var details = new CompanyDetails
		{
			Company = "Test Company",
			Login = "testuser",
			Contact = new Contact { Name = "John", Surname = "Doe" },
			Licenses = new List<License>
			{
				new() { SKU = "SKU1", Count = 10 }
			}
		};

		SetupHttpResponse(details);

		// Act
		var result = await _client.GetCompanyDetailsAsync("C123", CancellationToken.None);

		// Assert
		Assert.NotNull(result);
		Assert.Equal("Test Company", result.Company);
		Assert.Equal("testuser", result.Login);
		Assert.Single(result.Licenses);
	}

	[Fact]
	public async Task GetCompanyDetailsAsync_NullCompanyId_ThrowsArgumentException()
	{
		// Act & Assert
		await Assert.ThrowsAsync<ArgumentException>(
			() => _client.GetCompanyDetailsAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetCompanyDetailsAsync_NullResponse_ThrowsExternalApiException()
	{
		// Arrange
		SetupHttpResponse<CompanyDetails>(null);

		// Act & Assert
		var exception = await Assert.ThrowsAsync<ExternalApiException>(
			() => _client.GetCompanyDetailsAsync("C123", CancellationToken.None));

		Assert.Contains("Received null response", exception.Message);
	}

	[Fact]
	public async Task GetCompanyDetailsAsync_ApiReturnsError_ThrowsExternalApiException()
	{
		// Arrange
		SetupHttpErrorResponse(HttpStatusCode.NotFound, "Not found");

		// Act & Assert
		await Assert.ThrowsAsync<ExternalApiException>(
			() => _client.GetCompanyDetailsAsync("C123", CancellationToken.None));
	}

	#endregion

	#region GetPriceAsync Tests

	[Fact]
	public async Task GetPriceAsync_ValidSku_ReturnsPrice()
	{
		// Arrange
		var pricing = new SkuPricing { SKU = "SKU123", Price = 99.99m };
		SetupHttpResponse(pricing);

		// Act
		var result = await _client.GetPriceAsync("SKU123", CancellationToken.None);

		// Assert
		Assert.NotNull(result);
		Assert.Equal("SKU123", result.SKU);
		Assert.Equal(99.99m, result.Price);
	}

	[Fact]
	public async Task GetPriceAsync_NullSku_ThrowsArgumentException()
	{
		// Act & Assert
		await Assert.ThrowsAsync<ArgumentException>(
			() => _client.GetPriceAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetPriceAsync_EmptySku_ThrowsArgumentException()
	{
		// Act & Assert
		await Assert.ThrowsAsync<ArgumentException>(
			() => _client.GetPriceAsync("", CancellationToken.None));
	}

	[Fact]
	public async Task GetPriceAsync_NullResponse_ThrowsExternalApiException()
	{
		// Arrange
		SetupHttpResponse<SkuPricing>(null);

		// Act & Assert
		var exception = await Assert.ThrowsAsync<ExternalApiException>(
			() => _client.GetPriceAsync("SKU123", CancellationToken.None));

		Assert.Contains("Received null response", exception.Message);
	}

	[Fact]
	public async Task GetPriceAsync_ApiReturnsError_ThrowsExternalApiException()
	{
		// Arrange
		SetupHttpErrorResponse(HttpStatusCode.BadRequest, "Invalid SKU");

		// Act & Assert
		await Assert.ThrowsAsync<ExternalApiException>(
			() => _client.GetPriceAsync("INVALID", CancellationToken.None));
	}

	#endregion

	#region SubmitResultAsync Tests

	[Fact]
	public async Task SubmitResultAsync_ValidRequest_ReturnsSuccessResponse()
	{
		// Arrange
		var request = new SubmitResultRequest
		{
			CompanyId = "C123",
			CompanyName = "Test Company",
			UserLogin = "user123",
			UserName = "John Doe",
			OrderedLicense = new List<OrderedLicenseResult>
			{
				new() { SKU = "SKU1", Price = 100m, Count = 5, Sum = 500m }
			}
		};

		var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("{\"status\":\"success\"}")
		};

		_mockHttpMessageHandler
			.Protected()
			.Setup<Task<HttpResponseMessage>>(
				"SendAsync",
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>())
			.ReturnsAsync(mockResponse);

		// Act
		var result = await _client.SubmitResultAsync(request, CancellationToken.None);

		// Assert
		Assert.NotNull(result);
		Assert.Equal(HttpStatusCode.OK, result.StatusCode);
	}

	[Fact]
	public async Task SubmitResultAsync_NullRequest_ThrowsArgumentNullException()
	{
		// Act & Assert
		await Assert.ThrowsAsync<ArgumentNullException>(
			() => _client.SubmitResultAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task SubmitResultAsync_ApiReturnsError_ReturnsErrorResponse()
	{
		// Arrange
		var request = new SubmitResultRequest
		{
			CompanyId = "C123",
			CompanyName = "Test Company",
			UserLogin = "user123",
			UserName = "John Doe",
			OrderedLicense = new List<OrderedLicenseResult>()
		};

		var mockResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError)
		{
			Content = new StringContent("Server error")
		};

		_mockHttpMessageHandler
			.Protected()
			.Setup<Task<HttpResponseMessage>>(
				"SendAsync",
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>())
			.ReturnsAsync(mockResponse);

		// Act
		var result = await _client.SubmitResultAsync(request, CancellationToken.None);

		// Assert
		Assert.NotNull(result);
		Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
	}

	#endregion

	#region Error Handling Tests

	[Fact]
	public async Task GetCompaniesAsync_InvalidJson_ThrowsExternalApiException()
	{
		// Arrange
		var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("\"invalid json that won't deserialize to List<Company>\"")
		};

		_mockHttpMessageHandler
			.Protected()
			.Setup<Task<HttpResponseMessage>>(
				"SendAsync",
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>())
			.ReturnsAsync(mockResponse);

		// Act & Assert
		await Assert.ThrowsAsync<ExternalApiException>(
			() => _client.GetCompaniesAsync("Latvia", CancellationToken.None));
	}

	[Fact]
	public async Task GetCompaniesAsync_EmptyResponseContent_ReturnsEmptyList()
	{
		// Arrange
		var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("")
		};

		_mockHttpMessageHandler
			.Protected()
			.Setup<Task<HttpResponseMessage>>(
				"SendAsync",
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>())
			.ReturnsAsync(mockResponse);

		// Act
		var result = await _client.GetCompaniesAsync("Latvia", CancellationToken.None);

		// Assert
		Assert.NotNull(result);
		Assert.Empty(result);
	}

	#endregion

	#region Helper Methods

	private void SetupHttpResponse<T>(T data)
	{
		var innerJson = JsonSerializer.Serialize(data);
		var outerJson = JsonSerializer.Serialize(innerJson);

		var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(outerJson)
		};

		_mockHttpMessageHandler
			.Protected()
			.Setup<Task<HttpResponseMessage>>(
				"SendAsync",
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>())
			.ReturnsAsync(mockResponse);
	}

	private void SetupHttpErrorResponse(HttpStatusCode statusCode, string content)
	{
		var mockResponse = new HttpResponseMessage(statusCode)
		{
			Content = new StringContent(content),
			ReasonPhrase = content
		};

		_mockHttpMessageHandler
			.Protected()
			.Setup<Task<HttpResponseMessage>>(
				"SendAsync",
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>())
			.ReturnsAsync(mockResponse);
	}

	#endregion
}