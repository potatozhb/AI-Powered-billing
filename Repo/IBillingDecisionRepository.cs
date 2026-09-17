using AIbillingRAGBuilder.Services.Decision;

namespace AIbillingRAGBuilder.Services.Interfaces
{
    public interface IBillingDecisionRepository
    {
        Task<string> CreateDecisionAsync(
            BillingDecisionResponse decision,
            CancellationToken cancellationToken = default);

        Task<BillingDecisionResponse?> GetDecisionByIdAsync(
            string decisionId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<BillingDecisionResponse>> GetDecisionsByOrderIdAsync(
            string orderId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> ListDecisionIdsAsync(
            int skip = 0,
            int take = 100,
            CancellationToken cancellationToken = default);

        Task<bool> UpdateDecisionAsync(
            BillingDecisionResponse decision,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteDecisionAsync(
            string decisionId,
            CancellationToken cancellationToken = default);

        Task CreateReasoningAsync(
            string decisionId,
            BillingReasoning reasoning,
            CancellationToken cancellationToken = default);

        Task<BillingReasoning?> GetReasoningAsync(
            string decisionId,
            CancellationToken cancellationToken = default);

        Task DeleteReasoningAsync(
            string decisionId,
            CancellationToken cancellationToken = default);

        Task CreateContractEvidenceAsync(
            string decisionId,
            ContractEvidence evidence,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ContractEvidence>> ListContractEvidenceAsync(
            string decisionId,
            CancellationToken cancellationToken = default);

        Task DeleteContractEvidenceAsync(
            string decisionId,
            CancellationToken cancellationToken = default);

        Task CreateSpecialRuleEvidenceAsync(
            string decisionId,
            SpecialRuleEvidence evidence,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SpecialRuleEvidence>> ListSpecialRuleEvidenceAsync(
            string decisionId,
            CancellationToken cancellationToken = default);

        Task DeleteSpecialRuleEvidenceAsync(
            string decisionId,
            CancellationToken cancellationToken = default);

        Task<string> CreateHistoryEvidenceAsync(
            string decisionId,
            HistoryEvidence evidence,
            CancellationToken cancellationToken = default);

        Task<HistoryEvidence?> GetHistoryEvidenceAsync(
            string id,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<HistoryEvidence>> ListHistoryEvidenceAsync(
            string decisionId,
            CancellationToken cancellationToken = default);

        Task<bool> UpdateHistoryEvidenceAsync(
            string id,
            HistoryEvidence evidence,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteHistoryEvidenceAsync(
            string id,
            CancellationToken cancellationToken = default);

        Task DeleteHistoryEvidenceByDecisionIdAsync(
            string decisionId,
            CancellationToken cancellationToken = default);
    }
}
