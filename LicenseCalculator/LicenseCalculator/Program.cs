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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Configure Application Insights
builder.Services
	.AddApplicationInsightsTelemetryWorkerService()
	.ConfigureFunctionsApplicationInsights();

// Setup configuration providers
builder.SetupConfigurationProviders();

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


// Register middlewares
builder.UseMiddleware<CorrelationIdMiddleware>();
builder.UseMiddleware<ErrorHandlingMiddleware>();

builder.Build().Run();