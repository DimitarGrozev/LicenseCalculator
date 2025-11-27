using Azure.Core;
using LicenseCalculator.Models;
using LicenseCalculator.Utilities.Exceptions;
using LicenseCalculator.Utilities.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Threading;

namespace LicenseCalculator.Utilities.Middleware;

public class ErrorHandlingMiddleware : IFunctionsWorkerMiddleware
{
	private readonly ILogger<ErrorHandlingMiddleware> _logger;

	public ErrorHandlingMiddleware(ILogger<ErrorHandlingMiddleware> logger)
	{
		_logger = logger;
	}

	public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
	{
		try
		{
			await next(context);
		}
		catch (Exception ex)
		{
			var req = await context.GetHttpRequestDataAsync();
			if (req is null)
				throw; // Not an HTTP-trigger


			var requestId = context.Items["CorrelationId"] as string;

			HttpResponseData response = ex switch
			{
				DomainException domainEx =>  await req.CreateErrorResponseAsync(domainEx.Message, HttpStatusCode.BadRequest, requestId),
				ExternalApiException apiEx => await req.CreateErrorResponseAsync("External service error. Please try again later.", HttpStatusCode.BadGateway, requestId),
				OperationCanceledException => await req.CreateErrorResponseAsync("Request was cancelled", HttpStatusCode.RequestTimeout, requestId),
				_ => await req.CreateErrorResponseAsync("An unexpected error occurred. Please contact support.", HttpStatusCode.InternalServerError, requestId)
			};

			LogException(ex, context);

			context.GetInvocationResult().Value = response;
		}
	}

	private void LogException(Exception ex, FunctionContext ctx)
	{
		var functionName = ctx.FunctionDefinition.Name;

		switch (ex)
		{
			case DomainException:
				_logger.LogWarning(ex, "Domain error in {FunctionName}: {Message}", functionName, ex.Message);
				break;

			case ExternalApiException:
				_logger.LogError(ex, "External API failure in {FunctionName}: {Message}", functionName, ex.Message);
				break;

			case OperationCanceledException:
				_logger.LogWarning("Request cancelled in {FunctionName}", functionName);
				break;

			default:
				_logger.LogError(ex, "Unhandled exception in {FunctionName}: {Message}", functionName, ex.Message);
				break;
		}
	}
}
