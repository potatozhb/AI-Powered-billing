using AIbillingRAGBuilder.Services.Decision;

namespace AIbillingRAGBuilder.Services.Interfaces
{
    public interface IBillingDecisionService
    {
        Task WriteDecisionAsync(
            BillingDecisionResponse decision,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<BillingDecisionResponse>> ReadDecisionAsync(
            string orderId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<BillingDecisionResponse>> ReadDecisionsByPageAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);
    }
}
