using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text.Json;

namespace LicenseCalculator.Utilities.Extensions;

/// <summary>
/// Extension methods for HttpRequestData to create standardized responses
/// </summary>
public static class HttpResponseExtensions
{
	private static readonly JsonSerializerOptions DefaultJsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false
	};

	/// <summary>
	/// Creates a success response with the specified data
	/// </summary>
	public static async Task<HttpResponseData> CreateSuccessResponseAsync<T>(
		this HttpRequestData req,
		T data,
		string? message = null,
		HttpStatusCode statusCode = HttpStatusCode.OK,
		CancellationToken cancellationToken = default)
	{
		var response = req.CreateResponse(statusCode);
		response.Headers.Add("Content-Type", "application/json");

		var responseBody = new
		{
			Success = true,
			Message = message ?? "Request processed successfully",
			Data = data,
			Timestamp = DateTime.UtcNow
		};

		await response.WriteStringAsync(
			JsonSerializer.Serialize(responseBody, DefaultJsonOptions),
			cancellationToken);

		return response;
	}

	/// <summary>
	/// Creates a success response with custom message only
	/// </summary>
	public static async Task<HttpResponseData> CreateSuccessResponseAsync(
		this HttpRequestData req,
		string message,
		HttpStatusCode statusCode = HttpStatusCode.OK,
		CancellationToken cancellationToken = default)
	{
		var response = req.CreateResponse(statusCode);
		response.Headers.Add("Content-Type", "application/json");

		var responseBody = new
		{
			Success = true,
			Message = message,
			Timestamp = DateTime.UtcNow
		};

		await response.WriteStringAsync(
			JsonSerializer.Serialize(responseBody, DefaultJsonOptions),
			cancellationToken);

		return response;
	}

	/// <summary>
	/// Creates an error response with the specified error message
	/// </summary>
	public static async Task<HttpResponseData> CreateErrorResponseAsync(
		this HttpRequestData req,
		string error,
		HttpStatusCode statusCode = HttpStatusCode.BadRequest,
		string? requestId = null,
		CancellationToken cancellationToken = default)
	{
		var response = req.CreateResponse(statusCode);
		response.Headers.Add("Content-Type", "application/json");

		var responseBody = new
		{
			Success = false,
			Error = error,
			StatusCode = (int)statusCode,
			RequestId = requestId,
			Timestamp = DateTime.UtcNow
		};

		await response.WriteStringAsync(
			JsonSerializer.Serialize(responseBody, DefaultJsonOptions),
			cancellationToken);

		return response;
	}

	/// <summary>
	/// Creates an error response with multiple error messages
	/// </summary>
	public static async Task<HttpResponseData> CreateErrorResponseAsync(
		this HttpRequestData req,
		IEnumerable<string> errors,
		HttpStatusCode statusCode = HttpStatusCode.BadRequest,
		string? requestId = null,
		CancellationToken cancellationToken = default)
	{
		var response = req.CreateResponse(statusCode);
		response.Headers.Add("Content-Type", "application/json");

		var responseBody = new
		{
			Success = false,
			Errors = errors.ToArray(),
			Error = string.Join("; ", errors),
			StatusCode = (int)statusCode,
			RequestId = requestId,
			Timestamp = DateTime.UtcNow
		};

		await response.WriteStringAsync(
			JsonSerializer.Serialize(responseBody, DefaultJsonOptions),
			cancellationToken);

		return response;
	}

	/// <summary>
	/// Creates a validation error response
	/// </summary>
	public static async Task<HttpResponseData> CreateValidationErrorResponseAsync(
		this HttpRequestData req,
		string validationErrors,
		string? requestId = null,
		CancellationToken cancellationToken = default)
	{
		var response = req.CreateResponse(HttpStatusCode.BadRequest);
		response.Headers.Add("Content-Type", "application/json");

		var responseBody = new
		{
			Success = false,
			Error = $"Validation failed: {validationErrors}",
			ValidationErrors = validationErrors,
			StatusCode = 400,
			RequestId = requestId,
			Timestamp = DateTime.UtcNow
		};

		await response.WriteStringAsync(
			JsonSerializer.Serialize(responseBody, DefaultJsonOptions),
			cancellationToken);

		return response;
	}

	/// <summary>
	/// Creates a standardized order processing success response
	/// </summary>
	public static async Task<HttpResponseData> CreateOrderSuccessResponseAsync(
		this HttpRequestData req,
		string resultData,
		string companyName,
		string? requestId = null,
		CancellationToken cancellationToken = default)
	{
		var response = req.CreateResponse(HttpStatusCode.OK);
		response.Headers.Add("Content-Type", "application/json");

		var responseBody = new
		{
			Success = true,
			Message = $"Order processed successfully for {companyName}",
			Data = new
			{
				CompanyName = companyName,
				Result = resultData,
				ProcessedAt = DateTime.UtcNow
			},
			RequestId = requestId,
			Timestamp = DateTime.UtcNow
		};

		await response.WriteStringAsync(
			JsonSerializer.Serialize(responseBody, DefaultJsonOptions),
			cancellationToken);

		return response;
	}

	/// <summary>
	/// Creates a no content response (204)
	/// </summary>
	public static HttpResponseData CreateNoContentResponse(this HttpRequestData req)
	{
		return req.CreateResponse(HttpStatusCode.NoContent);
	}

	/// <summary>
	/// Creates an accepted response (202) for async operations
	/// </summary>
	public static async Task<HttpResponseData> CreateAcceptedResponseAsync(
		this HttpRequestData req,
		string? message = null,
		string? operationId = null,
		CancellationToken cancellationToken = default)
	{
		var response = req.CreateResponse(HttpStatusCode.Accepted);
		response.Headers.Add("Content-Type", "application/json");

		var responseBody = new
		{
			Success = true,
			Message = message ?? "Request accepted for processing",
			OperationId = operationId,
			Timestamp = DateTime.UtcNow
		};

		await response.WriteStringAsync(
			JsonSerializer.Serialize(responseBody, DefaultJsonOptions),
			cancellationToken);

		return response;
	}
}