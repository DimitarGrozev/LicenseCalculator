using System.Net;
using System.Text;
using FluentValidation;
using FluentValidation.Results;
using LicenseCalculator.Functions;
using LicenseCalculator.Models;
using LicenseCalculator.Services;
using LicenseCalculator.Utilities.Exceptions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LicenseCalculator.Tests.Functions;

public class SubmitLicensesFunctionTests
{
	private readonly Mock<IOrderOrchestrator> _mockOrchestrator;
	private readonly Mock<ILogger<SubmitLicensesFunction>> _mockLogger;
	private readonly Mock<IValidator<OrderRequest>> _mockValidator;
	private readonly SubmitLicensesFunction _function;
	private readonly Mock<FunctionContext> _mockFunctionContext;

	public SubmitLicensesFunctionTests()
	{
		_mockOrchestrator = new Mock<IOrderOrchestrator>();
		_mockLogger = new Mock<ILogger<SubmitLicensesFunction>>();
		_mockValidator = new Mock<IValidator<OrderRequest>>();
		_function = new SubmitLicensesFunction(
			_mockOrchestrator.Object,
			_mockLogger.Object,
			_mockValidator.Object);
		_mockFunctionContext = new Mock<FunctionContext>();
	}

	#region Happy Path Tests

	[Fact]
	public async Task Run_ValidRequest_ReturnsSuccessResponse()
	{
		// Arrange
		var request = CreateValidOrderRequest();
		var httpRequest = CreateHttpRequestData(request);

		SetupValidValidation(request);

		var orchestratorResponse = new SubmitResultResponse
		{
			Data = "{\"status\":\"success\",\"total\":1250.00}"
		};

		_mockOrchestrator
			.Setup(x => x.ProcessOrderAsync(It.IsAny<OrderRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(orchestratorResponse);

		// Act
		var response = await _function.Run(httpRequest, _mockFunctionContext.Object);

		// Assert
		Assert.NotNull(response);
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		_mockOrchestrator.Verify(x => x.ProcessOrderAsync(
			It.Is<OrderRequest>(r => r.Company == "LIDO" && r.Country == "Latvia"),
			It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Run_ValidRequest_LogsInformationMessages()
	{
		// Arrange
		var request = CreateValidOrderRequest();
		var httpRequest = CreateHttpRequestData(request);
		SetupValidValidation(request);
		SetupSuccessfulOrchestrator();

		// Act
		await _function.Run(httpRequest, _mockFunctionContext.Object);

		// Assert
		// Verify "Request started" log
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Request started")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);

		// Verify "Processing request" log
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Processing request for company")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);

		// Verify "Request completed" log
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Request completed successfully")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public async Task Run_ValidRequestWithMultipleLicenses_ProcessesCorrectly()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "Germany",
			Company = "GVS",
			Licenses = new List<string> { "SKU1", "SKU2", "SKU3", "SKU4", "SKU5" }
		};

		var httpRequest = CreateHttpRequestData(request);
		SetupValidValidation(request);
		SetupSuccessfulOrchestrator();

		// Act
		var response = await _function.Run(httpRequest, _mockFunctionContext.Object);

		// Assert
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		_mockOrchestrator.Verify(x => x.ProcessOrderAsync(
			It.Is<OrderRequest>(r => r.Licenses.Count == 5),
			It.IsAny<CancellationToken>()), Times.Once);
	}

	#endregion

	#region Validation Tests

	[Fact]
	public async Task Run_InvalidRequest_ReturnsValidationError()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "", // Invalid - empty
			Company = "LIDO",
			Licenses = new List<string> { "SKU1" }
		};

		var httpRequest = CreateHttpRequestData(request);

		var validationFailures = new List<ValidationFailure>
		{
			new ValidationFailure("Country", "Country is required")
		};

		_mockValidator
			.Setup(x => x.ValidateAsync(It.IsAny<OrderRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new ValidationResult(validationFailures));

		// Act
		var response = await _function.Run(httpRequest, _mockFunctionContext.Object);

		// Assert
		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		_mockOrchestrator.Verify(x => x.ProcessOrderAsync(
			It.IsAny<OrderRequest>(),
			It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Run_InvalidRequest_LogsWarning()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "",
			Company = "LIDO",
			Licenses = new List<string> { "SKU1" }
		};

		var httpRequest = CreateHttpRequestData(request);

		var validationFailures = new List<ValidationFailure>
		{
			new ValidationFailure("Country", "Country is required")
		};

		_mockValidator
			.Setup(x => x.ValidateAsync(It.IsAny<OrderRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new ValidationResult(validationFailures));

		// Act
		await _function.Run(httpRequest, _mockFunctionContext.Object);

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Warning,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Validation failed")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public async Task Run_MultipleValidationErrors_ReturnsAllErrors()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "",
			Company = "",
			Licenses = new List<string>()
		};

		var httpRequest = CreateHttpRequestData(request);

		var validationFailures = new List<ValidationFailure>
		{
			new ValidationFailure("Country", "Country is required"),
			new ValidationFailure("Company", "Company is required"),
			new ValidationFailure("Licenses", "At least one license is required")
		};

		_mockValidator
			.Setup(x => x.ValidateAsync(It.IsAny<OrderRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new ValidationResult(validationFailures));

		// Act
		var response = await _function.Run(httpRequest, _mockFunctionContext.Object);

		// Assert
		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	[Fact]
	public async Task Run_NullRequestBody_ReturnsValidationError()
	{
		// Arrange
		var httpRequest = CreateHttpRequestData(null as string);

		// Act
		var response = await _function.Run(httpRequest, _mockFunctionContext.Object);

		// Assert
		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		_mockOrchestrator.Verify(x => x.ProcessOrderAsync(
			It.IsAny<OrderRequest>(),
			It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Run_EmptyRequestBody_ReturnsValidationError()
	{
		// Arrange
		var httpRequest = CreateHttpRequestData("");

		// Act
		var response = await _function.Run(httpRequest, _mockFunctionContext.Object);

		// Assert
		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	[Fact]
	public async Task Run_InvalidJson_ReturnsValidationError()
	{
		// Arrange
		var httpRequest = CreateHttpRequestData("{ invalid json }");

		// Act
		var response = await _function.Run(httpRequest, _mockFunctionContext.Object);

		// Assert
		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	[Fact]
	public async Task Run_MalformedJson_ReturnsValidationError()
	{
		// Arrange
		var httpRequest = CreateHttpRequestData("{\"country\":\"Latvia\",\"company\":");

		// Act
		var response = await _function.Run(httpRequest, _mockFunctionContext.Object);

		// Assert
		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	#endregion

	#region Orchestrator Exception Handling Tests

	[Fact]
	public async Task Run_OrchestratorThrowsDomainException_ReturnsBadRequest()
	{
		// Arrange
		var request = CreateValidOrderRequest();
		var httpRequest = CreateHttpRequestData(request);
		SetupValidValidation(request);

		_mockOrchestrator
			.Setup(x => x.ProcessOrderAsync(It.IsAny<OrderRequest>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new DomainException("Company not found"));

		// Act & Assert
		await Assert.ThrowsAsync<DomainException>(async () => await _function.Run(httpRequest, _mockFunctionContext.Object));

	}

	[Fact]
	public async Task Run_OrchestratorThrowsExternalApiException_ReturnsBadGateway()
	{
		// Arrange
		var request = CreateValidOrderRequest();
		var httpRequest = CreateHttpRequestData(request);
		SetupValidValidation(request);

		_mockOrchestrator
			.Setup(x => x.ProcessOrderAsync(It.IsAny<OrderRequest>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new ExternalApiException("External API unavailable"));

		// Act & Assert
		await Assert.ThrowsAsync<ExternalApiException>(async () => await _function.Run(httpRequest, _mockFunctionContext.Object));
	}

	[Fact]
	public async Task Run_OrchestratorThrowsGenericException_ReturnsInternalServerError()
	{
		// Arrange
		var request = CreateValidOrderRequest();
		var httpRequest = CreateHttpRequestData(request);
		SetupValidValidation(request);

		_mockOrchestrator
			.Setup(x => x.ProcessOrderAsync(It.IsAny<OrderRequest>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new Exception("Unexpected error"));

		// Act & Assert
		await Assert.ThrowsAsync<Exception>(async () => await _function.Run(httpRequest, _mockFunctionContext.Object));
	}

	[Fact]
	public async Task Run_OrchestratorThrowsArgumentNullException_ReturnsInternalServerError()
	{
		// Arrange
		var request = CreateValidOrderRequest();
		var httpRequest = CreateHttpRequestData(request);
		SetupValidValidation(request);

		_mockOrchestrator
			.Setup(x => x.ProcessOrderAsync(It.IsAny<OrderRequest>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new ArgumentNullException("request"));

		// Act & Assert
		await Assert.ThrowsAsync<ArgumentNullException>(async () => await _function.Run(httpRequest, _mockFunctionContext.Object));
	}

	[Fact]
	public async Task Run_DomainException_LogsWarning()
	{
		// Arrange
		var request = CreateValidOrderRequest();
		var httpRequest = CreateHttpRequestData(request);
		SetupValidValidation(request);

		_mockOrchestrator
			.Setup(x => x.ProcessOrderAsync(It.IsAny<OrderRequest>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new DomainException("Company not found in Latvia"));

		// Act & Assert
		await Assert.ThrowsAsync<DomainException>(async () => await _function.Run(httpRequest, _mockFunctionContext.Object));
	}
	#endregion

	#region Edge Cases

	[Fact]
	public async Task Run_VeryLargeRequestBody_HandlesCorrectly()
	{
		// Arrange - Request with 100 licenses
		var request = new OrderRequest
		{
			Country = "Latvia",
			Company = "LIDO",
			Licenses = Enumerable.Range(1, 100).Select(i => $"SKU-{i}").ToList()
		};

		var httpRequest = CreateHttpRequestData(request);
		SetupValidValidation(request);
		SetupSuccessfulOrchestrator();

		// Act
		var response = await _function.Run(httpRequest, _mockFunctionContext.Object);

		// Assert
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		_mockOrchestrator.Verify(x => x.ProcessOrderAsync(
			It.Is<OrderRequest>(r => r.Licenses.Count == 100),
			It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Run_SpecialCharactersInCompanyName_ProcessesCorrectly()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "Latvia",
			Company = "LIDO & Co. (Riga)",
			Licenses = new List<string> { "SKU1" }
		};

		var httpRequest = CreateHttpRequestData(request);
		SetupValidValidation(request);
		SetupSuccessfulOrchestrator();

		// Act
		var response = await _function.Run(httpRequest, _mockFunctionContext.Object);

		// Assert
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}

	[Fact]
	public async Task Run_UnicodeCharactersInRequest_ProcessesCorrectly()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "日本", // Japan in Japanese
			Company = "会社", // Company in Japanese
			Licenses = new List<string> { "SKU1" }
		};

		var httpRequest = CreateHttpRequestData(request);
		SetupValidValidation(request);
		SetupSuccessfulOrchestrator();

		// Act
		var response = await _function.Run(httpRequest, _mockFunctionContext.Object);

		// Assert
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}

	[Fact]
	public async Task Run_EmptyLicensesList_ValidatorShouldCatch()
	{
		// Arrange
		var request = new OrderRequest
		{
			Country = "Latvia",
			Company = "LIDO",
			Licenses = new List<string>() // Empty
		};

		var httpRequest = CreateHttpRequestData(request);

		var validationFailures = new List<ValidationFailure>
		{
			new ValidationFailure("Licenses", "At least one license is required")
		};

		_mockValidator
			.Setup(x => x.ValidateAsync(It.IsAny<OrderRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new ValidationResult(validationFailures));

		// Act
		var response = await _function.Run(httpRequest, _mockFunctionContext.Object);

		// Assert
		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	#endregion

	#region Response Format Tests

	[Fact]
	public async Task Run_SuccessResponse_ContainsCorrectContentType()
	{
		// Arrange
		var request = CreateValidOrderRequest();
		var httpRequest = CreateHttpRequestData(request);
		SetupValidValidation(request);
		SetupSuccessfulOrchestrator();

		// Act
		var response = await _function.Run(httpRequest, _mockFunctionContext.Object);

		// Assert
		Assert.True(response.Headers.Contains("Content-Type"));
		Assert.Contains("application/json", response.Headers.GetValues("Content-Type").First());
	}
	#endregion

	#region Helper Methods

	private OrderRequest CreateValidOrderRequest()
	{
		return new OrderRequest
		{
			Country = "Latvia",
			Company = "LIDO",
			Licenses = new List<string> { "TPLV7893-85", "TPLV7884-85" }
		};
	}

	private HttpRequestData CreateHttpRequestData(OrderRequest? request)
	{
		var context = new Mock<FunctionContext>();
		var httpRequest = new Mock<HttpRequestData>(context.Object);

		var json = request != null
			? System.Text.Json.JsonSerializer.Serialize(request)
			: string.Empty;

		var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
		httpRequest.Setup(r => r.Body).Returns(stream);
		httpRequest.Setup(r => r.CreateResponse()).Returns(() =>
		{
			var response = new Mock<HttpResponseData>(context.Object);
			response.SetupProperty(r => r.StatusCode);
			response.SetupProperty(r => r.Headers, new HttpHeadersCollection());
			response.Setup(r => r.Body).Returns(new MemoryStream());
			return response.Object;
		});

		return httpRequest.Object;
	}

	private HttpRequestData CreateHttpRequestData(string? json)
	{
		var context = new Mock<FunctionContext>();
		var httpRequest = new Mock<HttpRequestData>(context.Object);

		var stream = new MemoryStream(Encoding.UTF8.GetBytes(json ?? string.Empty));
		httpRequest.Setup(r => r.Body).Returns(stream);
		httpRequest.Setup(r => r.CreateResponse()).Returns(() =>
		{
			var response = new Mock<HttpResponseData>(context.Object);
			response.SetupProperty(r => r.StatusCode);
			response.SetupProperty(r => r.Headers, new HttpHeadersCollection());
			response.Setup(r => r.Body).Returns(new MemoryStream());
			return response.Object;
		});

		return httpRequest.Object;
	}

	private void SetupValidValidation(OrderRequest request)
	{
		_mockValidator
			.Setup(x => x.ValidateAsync(It.IsAny<OrderRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new ValidationResult());
	}

	private void SetupSuccessfulOrchestrator()
	{
		var response = new SubmitResultResponse
		{
			Data = "{\"status\":\"success\"}"
		};

		_mockOrchestrator
			.Setup(x => x.ProcessOrderAsync(It.IsAny<OrderRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(response);
	}

	#endregion
}