using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace LicenseCalculator.Utilities.Policies;

public static class HttpClientPolicies
{
	private const int DefaultRetryCount = 3;
	private const int DefaultTimeoutSeconds = 300;
	private const int DefaultCircuitBreakerThreshold = 5;
	private const int DefaultCircuitBreakerDurationSeconds = 30;
	private const int BaseDelayMilliseconds = 100;

	/// <summary>
	/// Retry policy for transient HTTP failures with exponential backoff
	/// </summary>
	/// <param name="logger">Optional logger for retry events</param>
	/// <param name="retryCount">Number of retry attempts (default: 3)</param>
	public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(
		ILogger? logger = null, 
		int retryCount = DefaultRetryCount)
		=> HttpPolicyExtensions
			.HandleTransientHttpError()
			.Or<TimeoutRejectedException>()
			.WaitAndRetryAsync(
				retryCount: retryCount,
				sleepDurationProvider: retryAttempt => 
					TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt) * BaseDelayMilliseconds),
				onRetry: (outcome, timespan, retryAttempt, context) =>
				{
					var errorMessage = outcome.Exception?.Message ?? 
						outcome.Result?.StatusCode.ToString() ?? "Unknown error";
					
					if (logger != null)
					{
						logger.LogWarning(
							"HTTP retry attempt {RetryAttempt} after {DelayMs}ms due to: {Error}",
							retryAttempt, 
							timespan.TotalMilliseconds, 
							errorMessage
						);
					}
				}
			);

	/// <summary>
	/// Timeout policy for individual requests
	/// </summary>
	/// <param name="timeoutSeconds">Request timeout in seconds (default: 30)</param>
	public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(int timeoutSeconds = DefaultTimeoutSeconds)
		=> Policy.TimeoutAsync<HttpResponseMessage>(
			timeout: TimeSpan.FromSeconds(timeoutSeconds),
			timeoutStrategy: TimeoutStrategy.Optimistic
		);

	/// <summary>
	/// Circuit breaker policy to prevent cascading failures
	/// </summary>
	/// <param name="threshold">Number of failures before opening circuit (default: 5)</param>
	/// <param name="durationSeconds">Duration to keep circuit open in seconds (default: 30)</param>
	/// <param name="logger">Optional logger for circuit breaker events</param>
	public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(
		int threshold = DefaultCircuitBreakerThreshold,
		int durationSeconds = DefaultCircuitBreakerDurationSeconds,
		ILogger? logger = null)
		=> HttpPolicyExtensions
			.HandleTransientHttpError()
			.CircuitBreakerAsync(
				handledEventsAllowedBeforeBreaking: threshold,
				durationOfBreak: TimeSpan.FromSeconds(durationSeconds),
				onBreak: (exception, duration) =>
				{
					logger?.LogWarning(
						"Circuit breaker opened for {DurationMs}ms due to: {Error}",
						duration.TotalMilliseconds,
						exception.Exception?.Message ?? exception.Result?.StatusCode.ToString());
				},
				onReset: () =>
				{
					logger?.LogInformation("Circuit breaker reset - requests will be allowed");
				}
			);
}