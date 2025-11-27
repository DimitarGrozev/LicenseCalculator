using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace LicenseCalculator.Utilities.Middleware;

public class CorrelationIdMiddleware : IFunctionsWorkerMiddleware
{
	private const string HeaderName = "x-correlation-id";
	private readonly ILogger<CorrelationIdMiddleware> _logger;

	public CorrelationIdMiddleware(ILogger<CorrelationIdMiddleware> logger)
	{
		_logger = logger;
	}

	public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
	{
		// Get the incoming HTTP request (if this is an HTTP-triggered function)
		var httpRequest = await context.GetHttpRequestDataAsync();
		string correlationId = string.Empty;

		if (httpRequest is not null)
		{
			// 1) Try to get correlation id from header
			if (!httpRequest.Headers.TryGetValues(HeaderName, out var values) ||
				string.IsNullOrWhiteSpace(values.FirstOrDefault()))
			{
				// 2) Generate if missing
				correlationId = Guid.NewGuid().ToString();
			}
			else
			{
				correlationId = values.First();
			}
		}
		else
		{
			correlationId = Guid.NewGuid().ToString();
		}

		// 3) Store in FunctionContext.Items so any function or service can read it
		context.Items["CorrelationId"] = correlationId;

		// 4) Use a logging scope so all logs include the RequestId
		using (_logger.BeginScope(new Dictionary<string, object>
		{
			["CorrelationId"] = correlationId
		}))
		{
			await next(context);
		}

		// 5) If this was HTTP, add the header to the response as well
		var response = context.GetHttpResponseData();
		if (response != null && !response.Headers.Contains(HeaderName))
		{
			response.Headers.Add(HeaderName, correlationId);
		}
	}
}
