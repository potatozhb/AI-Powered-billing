using AIbillingRAGBuilder.Services.Decision;
using AIbillingRAGBuilder.Services.Interfaces;

namespace AIbillingRAGBuilder.Services
{
    public sealed class BillingDecisionService : IBillingDecisionService
    {
        private readonly IBillingDecisionRepository _repository;

        public BillingDecisionService(IBillingDecisionRepository repository)
        {
            _repository = repository;
        }

        public async Task WriteDecisionAsync(
            BillingDecisionResponse decision,
            CancellationToken cancellationToken = default)
        {
            ValidateDecision(decision);

            var savedDecisionId = await _repository.CreateDecisionAsync(decision, cancellationToken);

            await _repository.CreateReasoningAsync(savedDecisionId, decision.Reasoning, cancellationToken);

            foreach (var evidence in decision.UsedEvidence.Contracts)
            {
                await _repository.CreateContractEvidenceAsync(savedDecisionId, evidence, cancellationToken);
            }

            foreach (var evidence in decision.UsedEvidence.SpecialRules)
            {
                await _repository.CreateSpecialRuleEvidenceAsync(savedDecisionId, evidence, cancellationToken);
            }

            foreach (var evidence in decision.UsedEvidence.History)
            {
                await _repository.CreateHistoryEvidenceAsync(savedDecisionId, evidence, cancellationToken);
            }

        }

        public async Task<IReadOnlyList<BillingDecisionResponse>> ReadDecisionAsync(
            string orderId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                throw new ArgumentException("Order id is required.", nameof(orderId));
            }

            var decisions = await _repository.GetDecisionsByOrderIdAsync(orderId, cancellationToken);
            foreach (var decision in decisions)
            {
                await HydrateDecisionAsync(decision.DecisionId, decision, cancellationToken);
            }

            return decisions;
        }

        public async Task<IReadOnlyList<BillingDecisionResponse>> ReadDecisionsByPageAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            if (page <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(page));
            }

            if (pageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize));
            }

            var skip = checked((page - 1) * pageSize);
            var decisionIds = await _repository.ListDecisionIdsAsync(skip, pageSize, cancellationToken);
            var decisions = new List<BillingDecisionResponse>();

            foreach (var decisionId in decisionIds)
            {
                var decision = await _repository.GetDecisionByIdAsync(decisionId, cancellationToken);
                if (decision is null)
                {
                    continue;
                }

                await HydrateDecisionAsync(decisionId, decision, cancellationToken);
                decisions.Add(decision);
            }

            return decisions;
        }

        private async Task HydrateDecisionAsync(
            string decisionId,
            BillingDecisionResponse decision,
            CancellationToken cancellationToken)
        {
            decision.Reasoning =
                await _repository.GetReasoningAsync(decisionId, cancellationToken) ?? new BillingReasoning { DecisionId = decisionId };

            decision.UsedEvidence = new UsedEvidence
            {
                Contracts = [.. await _repository.ListContractEvidenceAsync(decisionId, cancellationToken)],
                SpecialRules = [.. await _repository.ListSpecialRuleEvidenceAsync(decisionId, cancellationToken)],
                History = [.. await _repository.ListHistoryEvidenceAsync(decisionId, cancellationToken)]
            };
        }

        private static void ValidateDecision(BillingDecisionResponse decision)
        {
            ArgumentNullException.ThrowIfNull(decision);

            if (string.IsNullOrWhiteSpace(decision.OrderId))
            {
                throw new ArgumentException("Decision order id is required.", nameof(decision));
            }

            decision.UsedEvidence ??= new UsedEvidence();
            decision.Reasoning ??= new BillingReasoning();
            decision.Summary ??= string.Empty;
            if (string.IsNullOrWhiteSpace(decision.DecisionId))
            {
                decision.DecisionId = Guid.NewGuid().ToString("N");
            }

            decision.Reasoning.DecisionId = decision.DecisionId;
            decision.UsedEvidence.Contracts ??= [];
            decision.UsedEvidence.SpecialRules ??= [];
            decision.UsedEvidence.History ??= [];
            foreach (var evidence in decision.UsedEvidence.Contracts)
            {
                evidence.DecisionId = decision.DecisionId;
            }
            foreach (var evidence in decision.UsedEvidence.SpecialRules)
            {
                evidence.DecisionId = decision.DecisionId;
            }
            foreach (var evidence in decision.UsedEvidence.History)
            {
                evidence.DecisionId = decision.DecisionId;
            }
        }
    }
}
