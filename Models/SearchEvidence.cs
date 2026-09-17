
namespace AIbillingRAGBuilder.Models
{
    public sealed class ContractEvidence
    {
        public string DocumentId { get; init; } = string.Empty;

        public int CustomerId { get; init; }

        public string ContractId { get; init; } = string.Empty;

        public string ContractVersion { get; init; } = string.Empty;

        public string Section { get; init; } = string.Empty;

        public string Content { get; init; } = string.Empty;

        public double? SearchScore { get; init; }
    }

    public sealed class HistoricalOrderEvidence
    {
        public string DocumentId { get; set; } = string.Empty;

        public string WorkOrderId { get; set; } = string.Empty;

        public string WorkOrderStatus { get; set; } = string.Empty;

        public string Decision { get; set; } = string.Empty;

        public double? SimilarityScore =>
                            RerankerScore.HasValue
                                ? Math.Clamp(RerankerScore.Value / 4.0, 0.0, 1.0)
                                : null;

        public double? RetrievalScore { get; set; }

        public double? RerankerScore { get; set; }

        public string Content { get; set; } = string.Empty;
    }
}
