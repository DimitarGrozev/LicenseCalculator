using System.Net;
using LicenseCalculator.Models;
using LicenseCalculator.Services;
using LicenseCalculator.Utilities.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace LicenseCalculator.Tests.Services;

public class OrderOrchestratorTests
{
	private readonly Mock<ILicenseProviderClient> _mockClient;
	private readonly Mock<ILogger<OrderOrchestrator>> _mockLogger;
	private readonly OrderOrchestrator _orchestrator;

	public OrderOrchestratorTests()
	{
		_mockClient = new Mock<ILicenseProviderClient>();
		_mockLogger = new Mock<ILogger<OrderOrchestrator>>();
		_orchestrator = new OrderOrchestrator(_mockClient.Object, _mockLogger.Object);
	}

	#region Happy Path Tests

	[Fact]
	public async Task ProcessOrderAsync_ValidRequest_CalculatesCorrectTotals()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "Latvia",
			Company = "LIDO",
			Licenses = new List<string> { "TPLV7893-85", "TPLV7884-85" }
		};

		var companies = new List<Company>
		{
			new() { CompanyId = "LV001", CompanyName = "LIDO" }
		};

		var companyDetails = new CompanyDetails
		{
			Company = "LIDO",
			Login = "lido_user",
			Contact = new Contact { Name = "John" },
			Licenses = new List<License>
			{
				new() { SKU = "TPLV7893-85", Count = 10 },
				new() { SKU = "TPLV7884-85", Count = 5 }
			}
		};

		_mockClient.Setup(x => x.GetCompaniesAsync("Latvia", It.IsAny<CancellationToken>()))
			.ReturnsAsync(companies);

		_mockClient.Setup(x => x.GetCompanyDetailsAsync("LV001", It.IsAny<CancellationToken>()))
			.ReturnsAsync(companyDetails);

		_mockClient.Setup(x => x.GetPriceAsync("TPLV7893-85", It.IsAny<CancellationToken>()))
			.ReturnsAsync(new SkuPricing { SKU = "TPLV7893-85", Price = 100.00m });

		_mockClient.Setup(x => x.GetPriceAsync("TPLV7884-85", It.IsAny<CancellationToken>()))
			.ReturnsAsync(new SkuPricing { SKU = "TPLV7884-85", Price = 50.00m });

		var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("{\"status\":\"success\"}")
		};

		_mockClient.Setup(x => x.SubmitResultAsync(
			It.Is<SubmitResultRequest>(r =>
				r.CompanyId == "LV001" &&
				r.CompanyName == "LIDO" &&
				r.UserLogin == "lido_user" &&
				r.OrderedLicense.Count == 2 &&
				r.OrderedLicense[0].Sum == 1000.00m && // 100 * 10
				r.OrderedLicense[1].Sum == 250.00m),   // 50 * 5
			It.IsAny<CancellationToken>()))
			.ReturnsAsync(mockResponse);

		// Act
		var result = await _orchestrator.ProcessOrderAsync(request);

		// Assert
		Assert.NotNull(result);
		Assert.Equal("{\"status\":\"success\"}", result.Data);
		_mockClient.Verify(x => x.SubmitResultAsync(It.IsAny<SubmitResultRequest>(), It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task ProcessOrderAsync_SingleSku_CalculatesCorrectly()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "Germany",
			Company = "GVS",
			Licenses = new List<string> { "TPLV7891-15" }
		};

		SetupBasicMocks("Germany", "GVS", "DE001",
			new List<License> { new() { SKU = "TPLV7891-15", Count = 25 } });

		_mockClient.Setup(x => x.GetPriceAsync("TPLV7891-15", It.IsAny<CancellationToken>()))
			.ReturnsAsync(new SkuPricing { SKU = "TPLV7891-15", Price = 75.50m });

		var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("{\"success\":true}")
		};

		_mockClient.Setup(x => x.SubmitResultAsync(It.IsAny<SubmitResultRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(mockResponse);

		// Act
		var result = await _orchestrator.ProcessOrderAsync(request);

		// Assert
		Assert.NotNull(result);
		_mockClient.Verify(x => x.GetPriceAsync("TPLV7891-15", It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task ProcessOrderAsync_LargeLicenseCount_HandlesCorrectly()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "Latvia",
			Company = "LIDO",
			Licenses = new List<string> { "TPLV7893-85" }
		};

		SetupBasicMocks("Latvia", "LIDO", "LV001",
			new List<License> { new() { SKU = "TPLV7893-85", Count = 999999 } });

		_mockClient.Setup(x => x.GetPriceAsync("TPLV7893-85", It.IsAny<CancellationToken>()))
			.ReturnsAsync(new SkuPricing { SKU = "TPLV7893-85", Price = 1.50m });

		var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("{\"ok\":true}")
		};

		_mockClient.Setup(x => x.SubmitResultAsync(
			It.Is<SubmitResultRequest>(r => r.OrderedLicense[0].Sum == 1499998.50m),
			It.IsAny<CancellationToken>()))
			.ReturnsAsync(mockResponse);

		// Act
		var result = await _orchestrator.ProcessOrderAsync(request);

		// Assert
		Assert.NotNull(result);
	}

	[Fact]
	public async Task ProcessOrderAsync_ManySkus_HandlesCorrectly()
	{
		// Arrange
		var skus = Enumerable.Range(1, 20).Select(i => $"SKU-{i}").ToList();
		var request = new OrderRequest
		{
			Country = "Latvia",
			Company = "LIDO",
			Licenses = skus
		};

		var licenses = skus.Select(sku => new License { SKU = sku, Count = 5 }).ToList();
		SetupBasicMocks("Latvia", "LIDO", "LV001", licenses);

		foreach (var sku in skus)
		{
			_mockClient.Setup(x => x.GetPriceAsync(sku, It.IsAny<CancellationToken>()))
				.ReturnsAsync(new SkuPricing { SKU = sku, Price = 100m });
		}

		var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("{\"ok\":true}")
		};

		_mockClient.Setup(x => x.SubmitResultAsync(It.IsAny<SubmitResultRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(mockResponse);

		// Act
		var result = await _orchestrator.ProcessOrderAsync(request);

		// Assert
		Assert.NotNull(result);
		_mockClient.Verify(x => x.GetPriceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(20));
	}

	#endregion

	#region Company Validation Tests

	[Fact]
	public async Task ProcessOrderAsync_NoCompaniesInCountry_ThrowsDomainException()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "UnknownCountry",
			Company = "LIDO",
			Licenses = new List<string> { "TPLV7893-85" }
		};

		_mockClient.Setup(x => x.GetCompaniesAsync("UnknownCountry", It.IsAny<CancellationToken>()))
			.ReturnsAsync(new List<Company>());

		// Act & Assert
		var exception = await Assert.ThrowsAsync<DomainException>(
			() => _orchestrator.ProcessOrderAsync(request));

		Assert.Contains("No companies found in country 'UnknownCountry'", exception.Message);
	}

	[Fact]
	public async Task ProcessOrderAsync_CompanyNotFound_ThrowsDomainExceptionWithAvailableCompanies()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "Latvia",
			Company = "NonExistent",
			Licenses = new List<string> { "TPLV7893-85" }
		};

		var companies = new List<Company>
		{
			new() { CompanyId = "LV001", CompanyName = "LIDO" },
			new() { CompanyId = "LV002", CompanyName = "ABC Corp" },
			new() { CompanyId = "LV003", CompanyName = "XYZ Ltd" }
		};

		_mockClient.Setup(x => x.GetCompaniesAsync("Latvia", It.IsAny<CancellationToken>()))
			.ReturnsAsync(companies);

		// Act & Assert
		var exception = await Assert.ThrowsAsync<DomainException>(
			() => _orchestrator.ProcessOrderAsync(request));

		Assert.Contains("Company 'NonExistent' not found", exception.Message);
		Assert.Contains("Available companies: LIDO, ABC Corp, XYZ Ltd", exception.Message);
	}

	[Fact]
	public async Task ProcessOrderAsync_CompanyNameCaseInsensitive_FindsCompany()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "Latvia",
			Company = "lido", // lowercase
			Licenses = new List<string> { "TPLV7893-85" }
		};

		SetupBasicMocks("Latvia", "LIDO", "LV001",
			new List<License> { new() { SKU = "TPLV7893-85", Count = 5 } });

		_mockClient.Setup(x => x.GetPriceAsync("TPLV7893-85", It.IsAny<CancellationToken>()))
			.ReturnsAsync(new SkuPricing { SKU = "TPLV7893-85", Price = 100m });

		var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("{\"ok\":true}")
		};

		_mockClient.Setup(x => x.SubmitResultAsync(It.IsAny<SubmitResultRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(mockResponse);

		// Act
		var result = await _orchestrator.ProcessOrderAsync(request);

		// Assert
		Assert.NotNull(result);
	}

	[Fact]
	public async Task ProcessOrderAsync_CompanyNameWithWhitespace_TrimsAndFinds()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "Latvia",
			Company = "  LIDO  ", // with spaces
			Licenses = new List<string> { "TPLV7893-85" }
		};

		SetupBasicMocks("Latvia", "LIDO", "LV001",
			new List<License> { new() { SKU = "TPLV7893-85", Count = 5 } });

		_mockClient.Setup(x => x.GetPriceAsync("TPLV7893-85", It.IsAny<CancellationToken>()))
			.ReturnsAsync(new SkuPricing { SKU = "TPLV7893-85", Price = 100m });

		var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("{\"ok\":true}")
		};

		_mockClient.Setup(x => x.SubmitResultAsync(It.IsAny<SubmitResultRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(mockResponse);

		// Act
		var result = await _orchestrator.ProcessOrderAsync(request);

		// Assert
		Assert.NotNull(result);
	}

	#endregion

	#region License Validation Tests

	[Fact]
	public async Task ProcessOrderAsync_NoLicensesForCompany_ThrowsDomainException()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "Latvia",
			Company = "LIDO",
			Licenses = new List<string> { "TPLV7893-85" }
		};

		var companies = new List<Company>
		{
			new() { CompanyId = "LV001", CompanyName = "LIDO" }
		};

		var companyDetails = new CompanyDetails
		{
			Company = "LIDO",
			Login = "lido_user",
			Contact = new Contact { Name = "John" },
			Licenses = new List<License>() // Empty licenses
		};

		_mockClient.Setup(x => x.GetCompaniesAsync("Latvia", It.IsAny<CancellationToken>()))
			.ReturnsAsync(companies);

		_mockClient.Setup(x => x.GetCompanyDetailsAsync("LV001", It.IsAny<CancellationToken>()))
			.ReturnsAsync(companyDetails);

		// Act & Assert
		var exception = await Assert.ThrowsAsync<DomainException>(
			() => _orchestrator.ProcessOrderAsync(request));

		Assert.Contains("No licenses found for company 'LIDO'", exception.Message);
	}

	[Fact]
	public async Task ProcessOrderAsync_RequestedSkuNotFound_ThrowsDomainExceptionWithAvailableSkus()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "Latvia",
			Company = "LIDO",
			Licenses = new List<string> { "INVALID-SKU-123" }
		};

		SetupBasicMocks("Latvia", "LIDO", "LV001", new List<License>
		{
			new() { SKU = "TPLV7893-85", Count = 10 },
			new() { SKU = "TPLV7884-85", Count = 5 }
		});

		// Act & Assert
		var exception = await Assert.ThrowsAsync<DomainException>(
			() => _orchestrator.ProcessOrderAsync(request));

		Assert.Contains("Requested SKUs not found for company 'LIDO': INVALID-SKU-123", exception.Message);
		Assert.Contains("Available SKUs: TPLV7893-85, TPLV7884-85", exception.Message);
	}

	[Fact]
	public async Task ProcessOrderAsync_MultipleMissingSkus_ExceptionListsAllMissing()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "Latvia",
			Company = "LIDO",
			Licenses = new List<string> { "INVALID-1", "INVALID-2", "TPLV7893-85" }
		};

		SetupBasicMocks("Latvia", "LIDO", "LV001", new List<License>
		{
			new() { SKU = "TPLV7893-85", Count = 10 }
		});

		// Act & Assert
		var exception = await Assert.ThrowsAsync<DomainException>(
			() => _orchestrator.ProcessOrderAsync(request));

		Assert.Contains("INVALID-1", exception.Message);
		Assert.Contains("INVALID-2", exception.Message);
	}

	[Fact]
	public async Task ProcessOrderAsync_SkuCaseInsensitive_FindsLicense()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "Latvia",
			Company = "LIDO",
			Licenses = new List<string> { "tplv7893-85" } // lowercase
		};

		SetupBasicMocks("Latvia", "LIDO", "LV001",
			new List<License> { new() { SKU = "TPLV7893-85", Count = 5 } });

		_mockClient.Setup(x => x.GetPriceAsync("tplv7893-85", It.IsAny<CancellationToken>()))
			.ReturnsAsync(new SkuPricing { SKU = "TPLV7893-85", Price = 100m });

		var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("{\"ok\":true}")
		};

		_mockClient.Setup(x => x.SubmitResultAsync(It.IsAny<SubmitResultRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(mockResponse);

		// Act
		var result = await _orchestrator.ProcessOrderAsync(request);

		// Assert
		Assert.NotNull(result);
	}

	[Fact]
	public async Task ProcessOrderAsync_DuplicateSkus_Deduplicates()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "Latvia",
			Company = "LIDO",
			Licenses = new List<string> { "TPLV7893-85", "TPLV7893-85", "TPLV7893-85" } // Duplicates
		};

		SetupBasicMocks("Latvia", "LIDO", "LV001",
			new List<License> { new() { SKU = "TPLV7893-85", Count = 5 } });

		_mockClient.Setup(x => x.GetPriceAsync("TPLV7893-85", It.IsAny<CancellationToken>()))
			.ReturnsAsync(new SkuPricing { SKU = "TPLV7893-85", Price = 100m });

		var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("{\"ok\":true}")
		};

		_mockClient.Setup(x => x.SubmitResultAsync(It.IsAny<SubmitResultRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(mockResponse);

		// Act
		var result = await _orchestrator.ProcessOrderAsync(request);

		// Assert
		Assert.NotNull(result);
		// Verify GetPriceAsync was called only once (deduplication worked)
		_mockClient.Verify(x => x.GetPriceAsync("TPLV7893-85", It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task ProcessOrderAsync_EmptyAndWhitespaceSkus_Filters()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "Latvia",
			Company = "LIDO",
			Licenses = new List<string> { "TPLV7893-85", "", "  ", null!, "TPLV7884-85" }
		};

		SetupBasicMocks("Latvia", "LIDO", "LV001", new List<License>
		{
			new() { SKU = "TPLV7893-85", Count = 5 },
			new() { SKU = "TPLV7884-85", Count = 3 }
		});

		_mockClient.Setup(x => x.GetPriceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new SkuPricing { SKU = "TPLV7893-85", Price = 100m });

		var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("{\"ok\":true}")
		};

		_mockClient.Setup(x => x.SubmitResultAsync(It.IsAny<SubmitResultRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(mockResponse);

		// Act
		var result = await _orchestrator.ProcessOrderAsync(request);

		// Assert
		Assert.NotNull(result);
		// Should only call GetPriceAsync for valid SKUs
		_mockClient.Verify(x => x.GetPriceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
	}

	#endregion

	#region Price Fetching Tests

	[Fact]
	public async Task ProcessOrderAsync_NegativePrice_LogsWarningButContinues()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "Latvia",
			Company = "LIDO",
			Licenses = new List<string> { "TPLV7893-85" }
		};

		SetupBasicMocks("Latvia", "LIDO", "LV001",
			new List<License> { new() { SKU = "TPLV7893-85", Count = 5 } });

		_mockClient.Setup(x => x.GetPriceAsync("TPLV7893-85", It.IsAny<CancellationToken>()))
			.ReturnsAsync(new SkuPricing { SKU = "TPLV7893-85", Price = -10.00m }); // Negative price

		var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("{\"ok\":true}")
		};

		_mockClient.Setup(x => x.SubmitResultAsync(It.IsAny<SubmitResultRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(mockResponse);

		// Act
		var result = await _orchestrator.ProcessOrderAsync(request);

		// Assert
		Assert.NotNull(result);
		// Verify warning was logged
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Warning,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Negative price")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public async Task ProcessOrderAsync_PriceFetchFails_ThrowsExternalApiException()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "Latvia",
			Company = "LIDO",
			Licenses = new List<string> { "TPLV7893-85" }
		};

		SetupBasicMocks("Latvia", "LIDO", "LV001",
			new List<License> { new() { SKU = "TPLV7893-85", Count = 5 } });

		_mockClient.Setup(x => x.GetPriceAsync("TPLV7893-85", It.IsAny<CancellationToken>()))
			.ThrowsAsync(new HttpRequestException("API unavailable"));

		// Act & Assert
		var exception = await Assert.ThrowsAsync<ExternalApiException>(
			() => _orchestrator.ProcessOrderAsync(request));

		Assert.Contains("Failed to fetch price for SKU TPLV7893-85", exception.Message);
		Assert.IsType<HttpRequestException>(exception.InnerException);
	}

	[Fact]
	public async Task ProcessOrderAsync_MultiplePrices_FetchesInParallel()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "Latvia",
			Company = "LIDO",
			Licenses = new List<string> { "SKU1", "SKU2", "SKU3" }
		};

		SetupBasicMocks("Latvia", "LIDO", "LV001", new List<License>
		{
			new() { SKU = "SKU1", Count = 5 },
			new() { SKU = "SKU2", Count = 3 },
			new() { SKU = "SKU3", Count = 7 }
		});

		var delayTime = 100;

		_mockClient.Setup(x => x.GetPriceAsync("SKU1", It.IsAny<CancellationToken>()))
			.Returns(async () =>
			{
				await Task.Delay(delayTime);
				return new SkuPricing { SKU = "SKU1", Price = 100m };
			});

		_mockClient.Setup(x => x.GetPriceAsync("SKU2", It.IsAny<CancellationToken>()))
			.Returns(async () =>
			{
				await Task.Delay(delayTime);
				return new SkuPricing { SKU = "SKU2", Price = 200m };
			});

		_mockClient.Setup(x => x.GetPriceAsync("SKU3", It.IsAny<CancellationToken>()))
			.Returns(async () =>
			{
				await Task.Delay(delayTime);
				return new SkuPricing { SKU = "SKU3", Price = 300m };
			});

		var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("{\"ok\":true}")
		};

		_mockClient.Setup(x => x.SubmitResultAsync(It.IsAny<SubmitResultRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(mockResponse);

		// Act
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		var result = await _orchestrator.ProcessOrderAsync(request);
		stopwatch.Stop();

		// Assert
		Assert.NotNull(result);
		// If parallel, should take ~100ms, not ~300ms
		Assert.True(stopwatch.ElapsedMilliseconds < 250, "Prices should be fetched in parallel");
	}

	#endregion

	#region Submit Result Tests

	[Fact]
	public async Task ProcessOrderAsync_SubmitFails_ThrowsExternalApiException()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "Latvia",
			Company = "LIDO",
			Licenses = new List<string> { "TPLV7893-85" }
		};

		SetupBasicMocks("Latvia", "LIDO", "LV001",
			new List<License> { new() { SKU = "TPLV7893-85", Count = 5 } });

		_mockClient.Setup(x => x.GetPriceAsync("TPLV7893-85", It.IsAny<CancellationToken>()))
			.ReturnsAsync(new SkuPricing { SKU = "TPLV7893-85", Price = 100m });

		var failedResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError)
		{
			Content = new StringContent("Server error")
		};

		_mockClient.Setup(x => x.SubmitResultAsync(It.IsAny<SubmitResultRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(failedResponse);

		// Act & Assert
		var exception = await Assert.ThrowsAsync<ExternalApiException>(
			() => _orchestrator.ProcessOrderAsync(request));

		Assert.Contains("SubmitResult failed with status 500", exception.Message);
	}

	[Fact]
	public async Task ProcessOrderAsync_ValidRequest_BuildsCorrectSubmitRequest()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "Latvia",
			Company = "LIDO",
			Licenses = new List<string> { "TPLV7893-85" }
		};

		var companies = new List<Company>
		{
			new() { CompanyId = "LV001", CompanyName = "LIDO" }
		};

		var companyDetails = new CompanyDetails
		{
			Company = "  LIDO Corp  ", // With spaces
			Login = "  lido_user  ", // With spaces
			Contact = new Contact { Name = "  John  " }, // With spaces
			Licenses = new List<License>
			{
				new() { SKU = "TPLV7893-85", Count = 10 }
			}
		};

		_mockClient.Setup(x => x.GetCompaniesAsync("Latvia", It.IsAny<CancellationToken>()))
			.ReturnsAsync(companies);

		_mockClient.Setup(x => x.GetCompanyDetailsAsync("LV001", It.IsAny<CancellationToken>()))
			.ReturnsAsync(companyDetails);

		_mockClient.Setup(x => x.GetPriceAsync("TPLV7893-85", It.IsAny<CancellationToken>()))
			.ReturnsAsync(new SkuPricing { SKU = "TPLV7893-85", Price = 100m });

		SubmitResultRequest? capturedRequest = null;
		var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("{\"ok\":true}")
		};

		_mockClient.Setup(x => x.SubmitResultAsync(It.IsAny<SubmitResultRequest>(), It.IsAny<CancellationToken>()))
			.Callback<SubmitResultRequest, CancellationToken>((req, ct) => capturedRequest = req)
			.ReturnsAsync(mockResponse);

		// Act
		await _orchestrator.ProcessOrderAsync(request);

		// Assert
		Assert.NotNull(capturedRequest);
		Assert.Equal("LV001", capturedRequest.CompanyId);
		Assert.Equal("LIDO Corp", capturedRequest.CompanyName); // Trimmed
		Assert.Equal("lido_user", capturedRequest.UserLogin); // Trimmed
		Assert.Equal("John", capturedRequest.UserName); // Trimmed
		Assert.Single(capturedRequest.OrderedLicense);
		Assert.Equal("TPLV7893-85", capturedRequest.OrderedLicense[0].SKU);
		Assert.Equal(100m, capturedRequest.OrderedLicense[0].Price);
		Assert.Equal(10, capturedRequest.OrderedLicense[0].Count);
		Assert.Equal(1000m, capturedRequest.OrderedLicense[0].Sum);
	}

	#endregion

	#region Edge Cases

	[Fact]
	public async Task ProcessOrderAsync_ZeroLicenseCount_CalculatesZeroSum()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "Latvia",
			Company = "LIDO",
			Licenses = new List<string> { "TPLV7893-85" }
		};

		SetupBasicMocks("Latvia", "LIDO", "LV001",
			new List<License> { new() { SKU = "TPLV7893-85", Count = 0 } }); // Zero licenses

		_mockClient.Setup(x => x.GetPriceAsync("TPLV7893-85", It.IsAny<CancellationToken>()))
			.ReturnsAsync(new SkuPricing { SKU = "TPLV7893-85", Price = 100m });

		SubmitResultRequest? capturedRequest = null;
		var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("{\"ok\":true}")
		};

		_mockClient.Setup(x => x.SubmitResultAsync(It.IsAny<SubmitResultRequest>(), It.IsAny<CancellationToken>()))
			.Callback<SubmitResultRequest, CancellationToken>((req, ct) => capturedRequest = req)
			.ReturnsAsync(mockResponse);

		// Act
		var result = await _orchestrator.ProcessOrderAsync(request);

		// Assert
		Assert.NotNull(result);
		Assert.NotNull(capturedRequest);
		
		// Verify the submit request contains correct calculations
		Assert.Single(capturedRequest.OrderedLicense);
		var orderedLicense = capturedRequest.OrderedLicense[0];
		
		Assert.Equal("TPLV7893-85", orderedLicense.SKU);
		Assert.Equal(100m, orderedLicense.Price);
		Assert.Equal(0, orderedLicense.Count); // Zero count
		Assert.Equal(0m, orderedLicense.Sum); // Zero sum (0 * 100 = 0)
		
		// Also verify that SubmitResultAsync was called with the zero sum
		_mockClient.Verify(x => x.SubmitResultAsync(
			It.Is<SubmitResultRequest>(r => 
				r.OrderedLicense.Count == 1 &&
				r.OrderedLicense[0].SKU == "TPLV7893-85" &&
				r.OrderedLicense[0].Count == 0 &&
				r.OrderedLicense[0].Sum == 0m),
			It.IsAny<CancellationToken>()), 
			Times.Once);
	}

	#endregion

	#region Cancellation Tests

	[Fact]
	public async Task ProcessOrderAsync_CancellationRequested_PropagatesCancellation()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "Latvia",
			Company = "LIDO",
			Licenses = new List<string> { "TPLV7893-85" }
		};

		var cts = new CancellationTokenSource();
		cts.Cancel(); // Cancel immediately

		_mockClient.Setup(x => x.GetCompaniesAsync("Latvia", It.IsAny<CancellationToken>()))
			.ThrowsAsync(new OperationCanceledException());

		// Act & Assert
		await Assert.ThrowsAsync<OperationCanceledException>(
			() => _orchestrator.ProcessOrderAsync(request, cts.Token));
	}

	#endregion


	#region Helper Methods

	private void SetupBasicMocks(string country, string companyName, string companyId, List<License> licenses)
	{
		var companies = new List<Company>
		{
			new() { CompanyId = companyId, CompanyName = companyName }
		};

		var companyDetails = new CompanyDetails
		{
			Company = companyName,
			Login = $"{companyName.ToLower()}_user",
			Contact = new Contact { Name = "Test User" },
			Licenses = licenses
		};

		_mockClient.Setup(x => x.GetCompaniesAsync(country, It.IsAny<CancellationToken>()))
			.ReturnsAsync(companies);

		_mockClient.Setup(x => x.GetCompanyDetailsAsync(companyId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(companyDetails);
	}

	#endregion
}