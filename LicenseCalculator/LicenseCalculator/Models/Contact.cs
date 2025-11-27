namespace LicenseCalculator.Models;

public sealed class Contact
{
	public string Name { get; set; } = default!;
	public string Surname { get; set; } = default!;
	public Address Address { get; set; } = default!;
}
