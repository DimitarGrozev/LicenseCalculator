using FluentValidation;
using LicenseCalculator.Models;

namespace LicenseCalculator.Validators;

public class OrderRequestValidator : AbstractValidator<OrderRequest>
{
	public OrderRequestValidator()
	{
		RuleFor(x => x.Country)
			.NotEmpty().WithMessage("Country is required")
			.MaximumLength(100).WithMessage("Country name too long");

		RuleFor(x => x.CompanyName)
			.NotEmpty().WithMessage("Company name is required")
			.MaximumLength(200).WithMessage("Company name too long");

		RuleFor(x => x.OrderedLicenses)
			.NotEmpty().WithMessage("At least one license must be ordered")
			.Must(licenses => licenses != null && licenses.Any())
			.WithMessage("Ordered licenses cannot be empty");

		RuleForEach(x => x.OrderedLicenses)
			.SetValidator(new OrderLicenseItemValidator());
	}
}
