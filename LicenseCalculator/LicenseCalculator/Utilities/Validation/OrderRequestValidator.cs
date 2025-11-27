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

		RuleFor(x => x.Company)
			.NotEmpty().WithMessage("Company name is required")
			.MaximumLength(200).WithMessage("Company name too long");

		RuleFor(x => x.Licenses)
			.NotEmpty().WithMessage("At least one license must be ordered")
			.Must(licenses => licenses != null && licenses.Any())
			.WithMessage("Ordered licenses cannot be empty");

		RuleForEach(x => x.Licenses)
			.Matches(@"^TPLV\d{4}-\d{2}$").WithMessage("SKU must follow the format TPLV####-## (e.g., TPLV7893-85)");
	}
}
