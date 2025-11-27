using Azure.Identity;
using FluentValidation;
using LicenseCalculator.Services;
using LicenseCalculator.Utilities.Config;
using LicenseCalculator.Utilities.Extensions;
using LicenseCalculator.Utilities.Middleware;
using LicenseCalculator.Utilities.Policies;
using LicenseCalculator.Utilities.Validation;
using LicenseCalculator.Validators;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Configure Application Insights
builder.Services
	.AddApplicationInsightsTelemetryWorkerService()
	.ConfigureFunctionsApplicationInsights();

// Add Azure Key Vault configuration in production
if (builder.Environment.IsProduction())
{
	var keyVaultUri = builder.Configuration["KeyVaultUri"];

	if (!string.IsNullOrEmpty(keyVaultUri))
	{
		builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
	}
}

// Add User Secrets for local development
if (builder.Environment.IsDevelopment())
{
	builder.Configuration.AddUserSecrets<Program>(optional: true);
	builder.Configuration.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
}

// Add validated configuration options
builder.Services.AddValidatedOptions<LicenseProviderOptions, LicenseProviderOptionsValidator>(
	builder.Configuration, 
	LicenseProviderOptions.SectionName);

// HttpClient + Polly
builder.Services.AddHttpClient<ILicenseProviderClient, LicenseProviderClient>()
	.AddPolicyHandler(HttpClientPolicies.GetRetryPolicy())
	.AddPolicyHandler(HttpClientPolicies.GetTimeoutPolicy());

// Add FluentValidation validators
builder.Services.AddValidatorsFromAssemblyContaining<OrderRequestValidator>();

// Register business logic services
builder.Services.AddScoped<IOrderOrchestrator, OrderOrchestrator>();


// Add global error handling middleware
builder.UseMiddleware<CorrelationIdMiddleware>();
builder.UseMiddleware<ErrorHandlingMiddleware>();

builder.Build().Run();