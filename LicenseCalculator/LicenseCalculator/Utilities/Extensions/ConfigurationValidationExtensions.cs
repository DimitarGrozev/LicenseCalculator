using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LicenseCalculator.Utilities.Extensions;

/// <summary>
/// Extension methods for configuration validation
/// </summary>
public static class ConfigurationValidationExtensions
{
	/// <summary>
	/// Adds comprehensive validation for options with custom validator
	/// </summary>
	public static OptionsBuilder<T> AddValidatedOptions<T, TValidator>(
		this IServiceCollection services, 
		IConfiguration configuration, 
		string sectionName) 
		where T : class 
		where TValidator : class, IValidateOptions<T>
	{
		services.AddSingleton<IValidateOptions<T>, TValidator>();
		
		return services.AddOptions<T>()
			.Bind(configuration.GetSection(sectionName))
			.ValidateDataAnnotations()
			.ValidateOnStart();
	}
}