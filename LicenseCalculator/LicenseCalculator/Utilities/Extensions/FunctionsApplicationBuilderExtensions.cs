using Azure.Identity;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace LicenseCalculator.Utilities.Extensions;

public static class FunctionsApplicationBuilderExtensions
{
	public static FunctionsApplicationBuilder SetupConfigurationProviders(this FunctionsApplicationBuilder builder)
	{
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

		return builder;
	}
}
