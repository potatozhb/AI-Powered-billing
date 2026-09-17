using System.Text.Json.Serialization;

namespace AIbillingRAGBuilder.Models
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BillingDecisionType
    {
        CHARGE,
        NO_CHARGE,
        REVIEW_REQUIRED
    }

    public sealed class BillingDecision
    {
        public BillingDecisionType Decision { get; init; }

        public double Confidence { get; init; }

        public string Reason { get; init; } = string.Empty;

        public IReadOnlyList<ContractEvidenceReference> ContractEvidence { get; init; } =
            Array.Empty<ContractEvidenceReference>();

        public IReadOnlyList<HistoricalEvidenceReference> HistoricalEvidence { get; init; } =
            Array.Empty<HistoricalEvidenceReference>();

        public IReadOnlyList<string> MissingInformation { get; init; } =
            Array.Empty<string>();

        public IReadOnlyList<string> Conflicts { get; init; } =
            Array.Empty<string>();

        public IReadOnlyList<RecommendedChargeItem> RecommendedChargeItems { get; init; } =
            Array.Empty<RecommendedChargeItem>();
    }

    public sealed class ContractEvidenceReference
    {
        public string DocumentId { get; init; } = string.Empty;

        public string Clause { get; init; } = string.Empty;

        public string Explanation { get; init; } = string.Empty;
    }

    public sealed class HistoricalEvidenceReference
    {
        public int WorkOrderId { get; init; }

        public string ApprovedDecision { get; init; } = string.Empty;

        public string SimilarityReason { get; init; } = string.Empty;
    }

    public sealed class RecommendedChargeItem
    {
        public string Description { get; init; } = string.Empty;

        public decimal Quantity { get; init; }

        public decimal UnitPrice { get; init; }
    }
}
