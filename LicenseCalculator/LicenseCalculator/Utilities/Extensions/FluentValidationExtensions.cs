using FluentValidation;
using Microsoft.Azure.Functions.Worker.Http;
using System.Text.Json;

namespace LicenseCalculator.Utilities.Extensions;

public static class FluentValidationExtensions
{
	public static async Task<(bool IsValid, string? Error, T? Model)> ValidateRequestBodyWithFluentAsync<T>(
		this HttpRequestData req,
		IValidator<T> validator) where T : class
	{
		// Read and deserialize body
		string body;
		using (var reader = new StreamReader(req.Body))
		{
			body = await reader.ReadToEndAsync();
		}

		if (string.IsNullOrWhiteSpace(body))
		{
			return (false, "Request body is required", default);
		}

		T? model;
		try
		{
			model = JsonSerializer.Deserialize<T>(body, new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			});
		}
		catch (JsonException)
		{
			return (false, "Invalid JSON format", default);
		}

		if (model == null)
		{
			return (false, "Invalid request payload", default);
		}

		// Validate with FluentValidation
		var validationResult = await validator.ValidateAsync(model);

		if (!validationResult.IsValid)
		{
			var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
			return (false, errors, default);
		}

		return (true, null, model);
	}
}