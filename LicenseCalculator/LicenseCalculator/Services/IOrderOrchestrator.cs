using LicenseCalculator.Models;

namespace LicenseCalculator.Services;

public interface IOrderOrchestrator
{
	Task<SubmitResultResponse> ProcessOrderAsync(OrderRequest request, CancellationToken ct);
}