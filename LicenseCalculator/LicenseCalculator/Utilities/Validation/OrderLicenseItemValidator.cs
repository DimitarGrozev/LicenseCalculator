using FluentValidation;
using LicenseCalculator.Models;

namespace LicenseCalculator.Validators;

public class OrderLicenseItemValidator : AbstractValidator<OrderLicenseItem>
{
	public OrderLicenseItemValidator()
	{
		RuleFor(x => x.Sku)
			.NotEmpty().WithMessage("SKU is required")
			.Matches(@"^[A-Z0-9\-]+$").WithMessage("SKU must contain only uppercase letters, numbers, and hyphens");

		RuleFor(x => x.Count)
			.GreaterThan(0).WithMessage("Count must be greater than 0")
			.LessThanOrEqualTo(10000).WithMessage("Count cannot exceed 10,000");
	}
}