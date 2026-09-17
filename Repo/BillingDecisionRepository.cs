using AIbillingRAGBuilder.Dtos;
using AIbillingRAGBuilder.Services.Decision;
using AIbillingRAGBuilder.Services.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;

namespace AIbillingRAGBuilder.Repo
{
    public sealed class BillingDecisionRepository : IBillingDecisionRepository
    {
        private readonly SqlOptions _options;

        public BillingDecisionRepository(IOptions<SqlOptions> options)
        {
            _options = options.Value;
        }

        public async Task<string> CreateDecisionAsync(
            BillingDecisionResponse decision,
            CancellationToken cancellationToken = default)
        {
            ValidateDecision(decision);

            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await InsertDecisionAsync(connection, transaction: null, decision, cancellationToken);

            return decision.DecisionId;
        }

        public async Task<BillingDecisionResponse?> GetDecisionByIdAsync(
            string decisionId,
            CancellationToken cancellationToken = default)
        {
            ValidateDecisionId(decisionId);

            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            return await ReadDecisionByIdAsync(
                connection,
                transaction: null,
                decisionId,
                cancellationToken);
        }

        public async Task<IReadOnlyList<BillingDecisionResponse>> GetDecisionsByOrderIdAsync(
            string orderId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                throw new ArgumentException("Order id is required.", nameof(orderId));
            }

            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            return await ReadDecisionsAsync(
                connection,
                transaction: null,
                orderId,
                cancellationToken);
        }

        public async Task<IReadOnlyList<string>> ListDecisionIdsAsync(
            int skip = 0,
            int take = 100,
            CancellationToken cancellationToken = default)
        {
            if (skip < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(skip));
            }

            if (take <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(take));
            }

            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = """
                select DecisionId
                from dbo.eAiBillingDecisionResponse
                order by CreateAt desc, DecisionId desc
                offset @skip rows fetch next @take rows only;
                """;

            await using var command = new SqlCommand(sql, connection);
            AddParameter(command, "@skip", SqlDbType.Int, skip);
            AddParameter(command, "@take", SqlDbType.Int, take);

            var decisionIds = new List<string>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var decisionId = ReadNullableString(reader, "DecisionId");
                    if (!string.IsNullOrWhiteSpace(decisionId))
                    {
                        decisionIds.Add(decisionId);
                    }
                }
            }

            return decisionIds;
        }

        public async Task<bool> UpdateDecisionAsync(
            BillingDecisionResponse decision,
            CancellationToken cancellationToken = default)
        {
            ValidateDecision(decision);

            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);


            return await UpdateDecisionAsync(connection, transaction: null, decision, cancellationToken);
        }

        public async Task<bool> DeleteDecisionAsync(
            string decisionId,
            CancellationToken cancellationToken = default)
        {
            ValidateDecisionId(decisionId);

            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = """
                delete from dbo.eAiBillingDecisionResponse
                where DecisionId = @decisionId;
                """;

            await using var command = new SqlCommand(sql, connection);
            AddDecisionIdParameter(command, decisionId);

            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            return affected > 0;
        }

        public async Task CreateReasoningAsync(
            string decisionId,
            BillingReasoning reasoning,
            CancellationToken cancellationToken = default)
        {
            ValidateDecisionId(decisionId);
            ArgumentNullException.ThrowIfNull(reasoning);
            ValidateChildDecisionId(decisionId, reasoning.DecisionId);
            reasoning.Id = GetOrCreateRowId(reasoning.Id);
            reasoning.DecisionId = decisionId;

            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await InsertReasoningAsync(connection, transaction: null, decisionId, reasoning, cancellationToken);
        }

        public async Task<BillingReasoning?> GetReasoningAsync(
            string decisionId,
            CancellationToken cancellationToken = default)
        {
            ValidateDecisionId(decisionId);

            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            return await ReadReasoningAsync(connection, transaction: null, decisionId, cancellationToken);
        }

        public async Task DeleteReasoningAsync(
            string decisionId,
            CancellationToken cancellationToken = default)
        {
            ValidateDecisionId(decisionId);
            await DeleteByDecisionIdAsync("dbo.eAiBillingReasoning", decisionId, cancellationToken);
        }

        public async Task CreateContractEvidenceAsync(
            string decisionId,
            ContractEvidence evidence,
            CancellationToken cancellationToken = default)
        {
            ValidateDecisionId(decisionId);
            ArgumentNullException.ThrowIfNull(evidence);
            ValidateChildDecisionId(decisionId, evidence.DecisionId);
            evidence.Id = GetOrCreateRowId(evidence.Id);
            evidence.DecisionId = decisionId;

            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await InsertContractEvidenceAsync(connection, transaction: null, decisionId, evidence, cancellationToken);
        }

        public async Task<IReadOnlyList<ContractEvidence>> ListContractEvidenceAsync(
            string decisionId,
            CancellationToken cancellationToken = default)
        {
            ValidateDecisionId(decisionId);

            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            return await ReadContractEvidenceAsync(connection, transaction: null, decisionId, cancellationToken);
        }

        public async Task DeleteContractEvidenceAsync(
            string decisionId,
            CancellationToken cancellationToken = default)
        {
            ValidateDecisionId(decisionId);
            await DeleteByDecisionIdAsync("dbo.eAiContractEvidence", decisionId, cancellationToken);
        }

        public async Task CreateSpecialRuleEvidenceAsync(
            string decisionId,
            SpecialRuleEvidence evidence,
            CancellationToken cancellationToken = default)
        {
            ValidateDecisionId(decisionId);
            ArgumentNullException.ThrowIfNull(evidence);
            ValidateChildDecisionId(decisionId, evidence.DecisionId);
            evidence.Id = GetOrCreateRowId(evidence.Id);
            evidence.DecisionId = decisionId;

            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await InsertSpecialRuleEvidenceAsync(connection, transaction: null, decisionId, evidence, cancellationToken);
        }

        public async Task<IReadOnlyList<SpecialRuleEvidence>> ListSpecialRuleEvidenceAsync(
            string decisionId,
            CancellationToken cancellationToken = default)
        {
            ValidateDecisionId(decisionId);

            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            return await ReadSpecialRuleEvidenceAsync(connection, transaction: null, decisionId, cancellationToken);
        }

        public async Task DeleteSpecialRuleEvidenceAsync(
            string decisionId,
            CancellationToken cancellationToken = default)
        {
            ValidateDecisionId(decisionId);
            await DeleteByDecisionIdAsync("dbo.eAiSpecialRuleEvidence", decisionId, cancellationToken);
        }

        public async Task<string> CreateHistoryEvidenceAsync(
            string decisionId,
            HistoryEvidence evidence,
            CancellationToken cancellationToken = default)
        {
            ValidateDecisionId(decisionId);
            ArgumentNullException.ThrowIfNull(evidence);
            ValidateChildDecisionId(decisionId, evidence.DecisionId);
            evidence.Id = GetOrCreateRowId(evidence.Id);
            evidence.DecisionId = decisionId;

            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = """
                insert into dbo.eAiHistoryEvidence
                    (Id, DecisionId, DocumentId, WorkOrderId, Billing_Status, Similarity, Reason)
                values
                    (@id, @decisionId, @documentId, @workOrderId, @billingStatus, @similarity, @reason);
            """;

            await using var command = new SqlCommand(sql, connection);
            AddHistoryEvidenceParameters(command, decisionId, evidence);

            await command.ExecuteNonQueryAsync(cancellationToken);
            return evidence.Id;
        }

        public async Task<HistoryEvidence?> GetHistoryEvidenceAsync(
            string id,
            CancellationToken cancellationToken = default)
        {
            ValidateRowId(id);

            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = """
                select Id, DecisionId, DocumentId, WorkOrderId, Billing_Status, Similarity, Reason
                from dbo.eAiHistoryEvidence
                where Id = @id;
                """;

            await using var command = new SqlCommand(sql, connection);
            AddRowIdParameter(command, id);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return ReadHistoryEvidence(reader);
        }

        public async Task<IReadOnlyList<HistoryEvidence>> ListHistoryEvidenceAsync(
            string decisionId,
            CancellationToken cancellationToken = default)
        {
            ValidateDecisionId(decisionId);

            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            return await ReadHistoryEvidenceAsync(
                connection,
                transaction: null,
                decisionId,
                cancellationToken);
        }

        public async Task<bool> UpdateHistoryEvidenceAsync(
            string id,
            HistoryEvidence evidence,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(evidence);
            ValidateDecisionId(evidence.DecisionId);
            ValidateRowId(id);
            if (!string.IsNullOrWhiteSpace(evidence.Id) && evidence.Id != id)
            {
                throw new ArgumentException("Evidence id must match the id being updated.", nameof(evidence));
            }
            evidence.Id = id;

            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = """
                update dbo.eAiHistoryEvidence
                set DocumentId = @documentId,
                    WorkOrderId = @workOrderId,
                    Billing_Status = @billingStatus,
                    Similarity = @similarity,
                    Reason = @reason
                where Id = @id and DecisionId = @decisionId;
                """;

            await using var command = new SqlCommand(sql, connection);
            AddHistoryEvidenceParameters(command, evidence.DecisionId, evidence);

            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            return affected > 0;
        }

        public async Task<bool> DeleteHistoryEvidenceAsync(
            string id,
            CancellationToken cancellationToken = default)
        {
            ValidateRowId(id);

            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = """
                delete from dbo.eAiHistoryEvidence
                where Id = @id;
                """;

            await using var command = new SqlCommand(sql, connection);
            AddRowIdParameter(command, id);

            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            return affected > 0;
        }

        public async Task DeleteHistoryEvidenceByDecisionIdAsync(
            string decisionId,
            CancellationToken cancellationToken = default)
        {
            ValidateDecisionId(decisionId);
            await DeleteByDecisionIdAsync("dbo.eAiHistoryEvidence", decisionId, cancellationToken);
        }

        private static async Task InsertDecisionAsync(
            SqlConnection connection,
            SqlTransaction? transaction,
            BillingDecisionResponse decision,
            CancellationToken cancellationToken)
        {
            const string sql = """
                insert into dbo.eAiBillingDecisionResponse
                    (DecisionId, OrderId, Billing_Status, Confidence, Summary, CreateAt)
                values
                    (@decisionId, @orderId, @billingStatus, @confidence, @summary, @createAt);
                """;
            try
            {
                await using var command = new SqlCommand(sql, connection, transaction);
                AddDecisionParameters(command, decision);
                command.CommandTimeout = 10;

                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch(Exception ex)
            {
                throw;
            }
        }

        private static async Task<bool> UpdateDecisionAsync(
            SqlConnection connection,
            SqlTransaction? transaction,
            BillingDecisionResponse decision,
            CancellationToken cancellationToken)
        {
            const string sql = """
                update dbo.eAiBillingDecisionResponse
                set OrderId = @orderId,
                    Billing_Status = @billingStatus,
                    Confidence = @confidence,
                    Summary = @summary,
                    CreateAt = @createAt
                where DecisionId = @decisionId;
                """;

            await using var command = new SqlCommand(sql, connection, transaction);
            AddDecisionParameters(command, decision);

            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            return affected > 0;
        }

        private static async Task<BillingDecisionResponse?> ReadDecisionByIdAsync(
            SqlConnection connection,
            SqlTransaction? transaction,
            string decisionId,
            CancellationToken cancellationToken)
        {
            const string sql = """
                select DecisionId, OrderId, Billing_Status, Confidence, Summary, CreateAt
                from dbo.eAiBillingDecisionResponse
                where DecisionId = @decisionId;
                """;

            await using var command = new SqlCommand(sql, connection, transaction);
            AddDecisionIdParameter(command, decisionId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return ReadDecision(reader);
        }

        private static async Task<IReadOnlyList<BillingDecisionResponse>> ReadDecisionsAsync(
            SqlConnection connection,
            SqlTransaction? transaction,
            string orderId,
            CancellationToken cancellationToken)
        {
            const string sql = """
                select DecisionId, OrderId, Billing_Status, Confidence, Summary, CreateAt
                from dbo.eAiBillingDecisionResponse
                where OrderId = @orderId
                order by CreateAt desc, DecisionId desc;
                """;

            await using var command = new SqlCommand(sql, connection, transaction);
            AddParameter(command, "@orderId", SqlDbType.NVarChar, orderId, 255);

            var decisions = new List<BillingDecisionResponse>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                decisions.Add(ReadDecision(reader));
            }

            return decisions;
        }

        private static async Task<BillingReasoning?> ReadReasoningAsync(
            SqlConnection connection,
            SqlTransaction? transaction,
            string decisionId,
            CancellationToken cancellationToken)
        {
            const string sql = """
                select top 1 Id, DecisionId, ContractReason, SpecialRuleReason, HistoryReason
                from dbo.eAiBillingReasoning
                where DecisionId = @decisionId
                order by Id;
                """;

            await using var command = new SqlCommand(sql, connection, transaction);
            AddParameter(command, "@decisionId", SqlDbType.NVarChar, decisionId, 255);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new BillingReasoning
            {
                Id = ReadNullableString(reader, "Id") ?? string.Empty,
                DecisionId = ReadNullableString(reader, "DecisionId") ?? string.Empty,
                ContractReason = ReadNullableString(reader, "ContractReason") ?? string.Empty,
                SpecialRuleReason = ReadNullableString(reader, "SpecialRuleReason") ?? string.Empty,
                HistoryReason = ReadNullableString(reader, "HistoryReason") ?? string.Empty
            };
        }

        private static async Task<List<ContractEvidence>> ReadContractEvidenceAsync(
            SqlConnection connection,
            SqlTransaction? transaction,
            string decisionId,
            CancellationToken cancellationToken)
        {
            const string sql = """
                select Id, DecisionId, DocumentId, ContractId, ContractItemId, ProductName, Reason
                from dbo.eAiContractEvidence
                where DecisionId = @decisionId
                order by Id;
                """;

            await using var command = new SqlCommand(sql, connection, transaction);
            AddParameter(command, "@decisionId", SqlDbType.NVarChar, decisionId, 255);

            var items = new List<ContractEvidence>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new ContractEvidence
                {
                    Id = ReadNullableString(reader, "Id") ?? string.Empty,
                    DecisionId = ReadNullableString(reader, "DecisionId") ?? string.Empty,
                    DocumentId = ReadNullableString(reader, "DocumentId") ?? string.Empty,
                    ContractId = ReadNullableString(reader, "ContractId") ?? string.Empty,
                    ContractItemId = ReadNullableString(reader, "ContractItemId"),
                    ProductName = ReadNullableString(reader, "ProductName"),
                    Reason = ReadNullableString(reader, "Reason") ?? string.Empty
                });
            }

            return items;
        }

        private static async Task<List<SpecialRuleEvidence>> ReadSpecialRuleEvidenceAsync(
            SqlConnection connection,
            SqlTransaction? transaction,
            string decisionId,
            CancellationToken cancellationToken)
        {
            const string sql = """
                select Id, DecisionId, DocumentId, RuleName, Reason
                from dbo.eAiSpecialRuleEvidence
                where DecisionId = @decisionId
                order by Id;
                """;

            await using var command = new SqlCommand(sql, connection, transaction);
            AddParameter(command, "@decisionId", SqlDbType.NVarChar, decisionId, 255);

            var items = new List<SpecialRuleEvidence>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new SpecialRuleEvidence
                {
                    Id = ReadNullableString(reader, "Id") ?? string.Empty,
                    DecisionId = ReadNullableString(reader, "DecisionId") ?? string.Empty,
                    DocumentId = ReadNullableString(reader, "DocumentId") ?? string.Empty,
                    RuleName = ReadNullableString(reader, "RuleName"),
                    Reason = ReadNullableString(reader, "Reason") ?? string.Empty
                });
            }

            return items;
        }

        private static async Task<List<HistoryEvidence>> ReadHistoryEvidenceAsync(
            SqlConnection connection,
            SqlTransaction? transaction,
            string decisionId,
            CancellationToken cancellationToken)
        {
            const string sql = """
                select Id, DecisionId, DocumentId, WorkOrderId, Billing_Status, Similarity, Reason
                from dbo.eAiHistoryEvidence
                where DecisionId = @decisionId
                order by Id;
                """;

            await using var command = new SqlCommand(sql, connection, transaction);
            AddParameter(command, "@decisionId", SqlDbType.NVarChar, decisionId, 255);

            var items = new List<HistoryEvidence>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(ReadHistoryEvidence(reader));
            }

            return items;
        }

        private static async Task InsertReasoningAsync(
            SqlConnection connection,
            SqlTransaction? transaction,
            string decisionId,
            BillingReasoning reasoning,
            CancellationToken cancellationToken)
        {
            const string sql = """
                insert into dbo.eAiBillingReasoning
                    (Id, DecisionId, ContractReason, SpecialRuleReason, HistoryReason)
                values
                    (@id, @decisionId, @contractReason, @specialRuleReason, @historyReason);
                """;

            await using var command = new SqlCommand(sql, connection, transaction);
            AddRowIdParameter(command, reasoning.Id);
            AddParameter(command, "@decisionId", SqlDbType.NVarChar, decisionId, 255);
            AddParameter(command, "@contractReason", SqlDbType.NText, reasoning.ContractReason);
            AddParameter(command, "@specialRuleReason", SqlDbType.NText, reasoning.SpecialRuleReason);
            AddParameter(command, "@historyReason", SqlDbType.NText, reasoning.HistoryReason);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static async Task InsertContractEvidenceAsync(
            SqlConnection connection,
            SqlTransaction? transaction,
            string decisionId,
            ContractEvidence evidence,
            CancellationToken cancellationToken)
        {
            const string sql = """
                insert into dbo.eAiContractEvidence
                    (Id, DecisionId, DocumentId, ContractId, ContractItemId, ProductName, Reason)
                values
                    (@id, @decisionId, @documentId, @contractId, @contractItemId, @productName, @reason);
                """;

            try
            {
                await using var command = new SqlCommand(sql, connection, transaction);
                command.CommandTimeout = 10;

                AddRowIdParameter(command, evidence.Id);
                AddParameter(command, "@decisionId", SqlDbType.NVarChar, decisionId, 255);
                AddParameter(command, "@documentId", SqlDbType.NVarChar, evidence.DocumentId, 255);
                AddParameter(command, "@contractId", SqlDbType.NVarChar, evidence.ContractId, 255);
                AddParameter(command, "@contractItemId", SqlDbType.NVarChar, evidence.ContractItemId, 255);
                AddParameter(command, "@productName", SqlDbType.NVarChar, evidence.ProductName, 255);
                AddParameter(command, "@reason", SqlDbType.NText, evidence.Reason);

                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private static async Task InsertSpecialRuleEvidenceAsync(
            SqlConnection connection,
            SqlTransaction? transaction,
            string decisionId,
            SpecialRuleEvidence evidence,
            CancellationToken cancellationToken)
        {
            const string sql = """
                insert into dbo.eAiSpecialRuleEvidence
                    (Id, DecisionId, DocumentId, RuleName, Reason)
                values
                    (@id, @decisionId, @documentId, @ruleName, @reason);
                """;
            try
            {
                await using var command = new SqlCommand(sql, connection, transaction);
                AddRowIdParameter(command, evidence.Id);
                AddParameter(command, "@decisionId", SqlDbType.NVarChar, decisionId, 255);
                AddParameter(command, "@documentId", SqlDbType.NVarChar, evidence.DocumentId, 255);
                AddParameter(command, "@ruleName", SqlDbType.NVarChar, evidence.RuleName, 255);
                AddParameter(command, "@reason", SqlDbType.NText, evidence.Reason);

                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private static async Task InsertHistoryEvidenceAsync(
            SqlConnection connection,
            SqlTransaction? transaction,
            string decisionId,
            HistoryEvidence evidence,
            CancellationToken cancellationToken)
        {
            const string sql = """
                insert into dbo.eAiHistoryEvidence
                    (Id, DecisionId, DocumentId, WorkOrderId, Billing_Status, Similarity, Reason)
                values
                    (@id, @decisionId, @documentId, @workOrderId, @billingStatus, @similarity, @reason);
                """;
            try
            {
                await using var command = new SqlCommand(sql, connection, transaction);
                AddHistoryEvidenceParameters(command, decisionId, evidence);

                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private async Task DeleteByDecisionIdAsync(
            string tableName,
            string decisionId,
            CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = $"delete from {tableName} where DecisionId = @decisionId;";
            await using var command = new SqlCommand(sql, connection);
            AddParameter(command, "@decisionId", SqlDbType.NVarChar, decisionId, 255);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static void AddDecisionParameters(
            SqlCommand command,
            BillingDecisionResponse decision)
        {
            AddDecisionIdParameter(command, decision.DecisionId);
            AddParameter(command, "@orderId", SqlDbType.NVarChar, decision.OrderId, 255);
            AddParameter(command, "@billingStatus", SqlDbType.Int, ToDatabaseStatus(decision.Billing_Status));
            AddParameter(command, "@confidence", SqlDbType.Float, decision.Confidence);
            AddParameter(command, "@summary", SqlDbType.NVarChar, decision.Summary, -1);
            AddParameter(command, "@createAt", SqlDbType.DateTime2, decision.CreateAt);
        }

        private static void AddHistoryEvidenceParameters(
            SqlCommand command,
            string decisionId,
            HistoryEvidence evidence)
        {
            AddRowIdParameter(command, evidence.Id);
            AddDecisionIdParameter(command, decisionId);

            AddParameter(command, "@documentId", SqlDbType.NVarChar, evidence.DocumentId, 255);
            AddParameter(command, "@workOrderId", SqlDbType.NVarChar, evidence.WorkOrderId, 255);
            AddParameter(command, "@billingStatus", SqlDbType.SmallInt, ToDatabaseStatus(evidence.Billing_Status));
            AddParameter(command, "@similarity", SqlDbType.Float, evidence.Similarity);
            AddParameter(command, "@reason", SqlDbType.NText, evidence.Reason);
        }

        private static HistoryEvidence ReadHistoryEvidence(SqlDataReader reader)
        {
            return new HistoryEvidence
            {
                Id = ReadNullableString(reader, "Id") ?? string.Empty,
                DecisionId = ReadNullableString(reader, "DecisionId") ?? string.Empty,
                DocumentId = ReadNullableString(reader, "DocumentId") ?? string.Empty,
                WorkOrderId = ReadNullableString(reader, "WorkOrderId") ?? string.Empty,
                Billing_Status = FromDatabaseStatus(reader.GetInt16(reader.GetOrdinal("Billing_Status"))),
                Similarity = reader.IsDBNull(reader.GetOrdinal("Similarity"))
                    ? null
                    : reader.GetDouble(reader.GetOrdinal("Similarity")),
                Reason = ReadNullableString(reader, "Reason") ?? string.Empty
            };
        }

        private static void AddParameter(
            SqlCommand command,
            string name,
            SqlDbType type,
            object? value,
            int? size = null)
        {
            var parameter = command.Parameters.Add(name, type);
            if (size.HasValue)
            {
                parameter.Size = size.Value;
            }

            parameter.Value = value ?? DBNull.Value;
        }

        private static string? ReadNullableString(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        private static BillingDecisionResponse ReadDecision(SqlDataReader reader)
        {
            return new BillingDecisionResponse
            {
                DecisionId = ReadNullableString(reader, "DecisionId") ?? string.Empty,
                OrderId = ReadNullableString(reader, "OrderId") ?? string.Empty,
                Billing_Status = FromDatabaseStatus(reader.GetInt32(reader.GetOrdinal("Billing_Status"))),
                Confidence = reader.GetFloat(reader.GetOrdinal("Confidence")),
                Summary = ReadNullableString(reader, "Summary") ?? string.Empty,
                CreateAt = ReadDateTimeOrDefault(reader, "CreateAt")
            };
        }

        private static DateTime ReadDateTimeOrDefault(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? DateTime.MinValue : reader.GetDateTime(ordinal);
        }

        private static int ToDatabaseStatus(BillingDecision status)
        {
            return status switch
            {
                BillingDecision.Billable => 1,
                BillingDecision.Free => 2,
                BillingDecision.ReviewRequired => 3,
                _ => 0
            };
        }

        private static BillingDecision FromDatabaseStatus(int status)
        {
            return status switch
            {
                1 => BillingDecision.Billable,
                2 => BillingDecision.Free,
                3 => BillingDecision.ReviewRequired,
                _ => BillingDecision.ReviewRequired
            };
        }

        private static string GetOrCreateRowId(string? id)
        {
            var result = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            ValidateRowId(result);
            return result;
        }

        private static void ValidateRowId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 255)
            {
                throw new ArgumentException("Id must contain 1 to 255 characters.", nameof(id));
            }
        }

        private static void AddRowIdParameter(SqlCommand command, string id)
        {
            ValidateRowId(id);
            AddParameter(command, "@id", SqlDbType.NVarChar, id, 255);
        }

        private static void ValidateChildDecisionId(string decisionId, string? childDecisionId)
        {
            if (!string.IsNullOrWhiteSpace(childDecisionId) &&
                !string.Equals(decisionId, childDecisionId, StringComparison.Ordinal))
            {
                throw new ArgumentException("The child decision id must match the parent decision id.", nameof(childDecisionId));
            }
        }

        private static void ValidateDecisionId(string decisionId)
        {
            if (string.IsNullOrWhiteSpace(decisionId) || decisionId.Length > 255)
            {
                throw new ArgumentException("Decision id must contain 1 to 255 characters.", nameof(decisionId));
            }
        }

        private static void AddDecisionIdParameter(SqlCommand command, string decisionId)
        {
            AddParameter(command, "@decisionId", SqlDbType.NVarChar, decisionId, 255);
        }

        private static void ValidateDecision(BillingDecisionResponse decision)
        {
            ArgumentNullException.ThrowIfNull(decision);
            ValidateDecisionId(decision.DecisionId);

            if (string.IsNullOrWhiteSpace(decision.OrderId))
            {
                throw new ArgumentException("Decision order id is required.", nameof(decision));
            }

            decision.UsedEvidence ??= new UsedEvidence();
            decision.Reasoning ??= new BillingReasoning();
            decision.Summary ??= string.Empty;
        }
    }
}
