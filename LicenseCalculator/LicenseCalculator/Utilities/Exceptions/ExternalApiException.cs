namespace LicenseCalculator.Utilities.Exceptions;

public class ExternalApiException : Exception
{
	public ExternalApiException(string message) : base(message) { }

	public ExternalApiException(string message, Exception innerException)
		: base(message, innerException) { }
}
