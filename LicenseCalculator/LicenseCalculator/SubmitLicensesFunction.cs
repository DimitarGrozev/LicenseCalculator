using FluentValidation;
using LicenseCalculator.Models;
using LicenseCalculator.Services;
using LicenseCalculator.Utilities.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace LicenseCalculator;

public class SubmitLicensesFunction
{
	private readonly IOrderOrchestrator _orchestrator;
	private readonly ILogger<SubmitLicensesFunction> _logger;
	private readonly IValidator<OrderRequest> _validator;

	public SubmitLicensesFunction(
		IOrderOrchestrator orchestrator,
		ILogger<SubmitLicensesFunction> logger,
		IValidator<OrderRequest> validator)
	{
		_orchestrator = orchestrator;
		_logger = logger;
		_validator = validator;
	}

	[Function("SubmitLicenses")]
	public async Task<HttpResponseData> Run(
		[HttpTrigger(AuthorizationLevel.Function, "post", Route = "submit-licenses")]
		HttpRequestData req,
		FunctionContext executionContext,
		CancellationToken cancellationToken = default)
	{
		_logger.LogInformation("Request started");

		// Validate request body using FluentValidation
		var (isValid, error, orderRequest) = await req.ValidateRequestBodyWithFluentAsync(_validator);

		if (!isValid)
		{
			_logger.LogWarning("Request: Validation failed - {Error}", error);
			return await req.CreateValidationErrorResponseAsync(error!, cancellationToken);
		}

		_logger.LogInformation("Processing request for company: {CompanyName}", orderRequest!.Company);

		// Process the order
		var result = await _orchestrator.ProcessOrderAsync(orderRequest, cancellationToken);

		_logger.LogInformation("Request completed successfully for company: {CompanyName}", orderRequest.Company);

		// Return success response using extension method
		return await req.CreateOrderSuccessResponseAsync(result.Data, orderRequest.Company, cancellationToken);
	}
}