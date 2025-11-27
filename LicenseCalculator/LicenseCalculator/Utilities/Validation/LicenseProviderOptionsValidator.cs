using Microsoft.Extensions.Options;
using LicenseCalculator.Utilities.Config;

namespace LicenseCalculator.Utilities.Validation;

/// <summary>
/// Custom validator for LicenseProviderOptions to provide advanced validation logic
/// </summary>
public class LicenseProviderOptionsValidator : IValidateOptions<LicenseProviderOptions>
{
	public ValidateOptionsResult Validate(string? name, LicenseProviderOptions options)
	{
		var failures = new List<string>();

		// Validate BaseUrl
		if (!Uri.IsWellFormedUriString(options.BaseUrl, UriKind.Absolute))
		{
			failures.Add($"LicenseProvider:BaseUrl '{options.BaseUrl}' is not a valid absolute URL");
		}

		// Validate all paths start with '/'
		ValidatePath(nameof(options.GetCompaniesPath), options.GetCompaniesPath, failures);
		ValidatePath(nameof(options.GetCompanyDetailsPath), options.GetCompanyDetailsPath, failures);
		ValidatePath(nameof(options.GetPricePath), options.GetPricePath, failures);
		ValidatePath(nameof(options.SubmitResultPath), options.SubmitResultPath, failures);

		// Validate codes are not empty
		ValidateCode(nameof(options.GetCompaniesCode), options.GetCompaniesCode, failures);
		ValidateCode(nameof(options.GetCompanyDetailsCode), options.GetCompanyDetailsCode, failures);
		ValidateCode(nameof(options.GetPriceCode), options.GetPriceCode, failures);
		ValidateCode(nameof(options.SubmitResultCode), options.SubmitResultCode, failures);

		// Validate timeout range
		if (options.DefaultTimeoutSeconds < 1 || options.DefaultTimeoutSeconds > 300)
		{
			failures.Add($"LicenseProvider:DefaultTimeoutSeconds must be between 1 and 300 seconds, but was {options.DefaultTimeoutSeconds}");
		}

		// Check for duplicate codes
		var codes = new[] { options.GetCompaniesCode, options.GetCompanyDetailsCode, options.GetPriceCode, options.SubmitResultCode };
		var duplicates = codes.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key);
		foreach (var duplicate in duplicates)
		{
			failures.Add($"LicenseProvider: Duplicate code '{duplicate}' found. All codes must be unique.");
		}

		if (failures.Count > 0)
		{
			return ValidateOptionsResult.Fail(failures);
		}

		return ValidateOptionsResult.Success;
	}

	private static void ValidatePath(string propertyName, string path, List<string> failures)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			failures.Add($"LicenseProvider:{propertyName} cannot be empty");
		}
		else if (!path.StartsWith('/'))
		{
			failures.Add($"LicenseProvider:{propertyName} must start with '/', but was '{path}'");
		}
	}

	private static void ValidateCode(string propertyName, string code, List<string> failures)
	{
		if (string.IsNullOrWhiteSpace(code))
		{
			failures.Add($"LicenseProvider:{propertyName} cannot be empty");
		}
		else if (code.Length < 3)
		{
			failures.Add($"LicenseProvider:{propertyName} must be at least 3 characters long, but was '{code}'");
		}
	}
}