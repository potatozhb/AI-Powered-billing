using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AIbillingRAGBuilder.Dtos
{
    public class DocumentContractDto
    {
        /// <summary>
        /// Contract ID
        /// </summary>
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("document_type")]
        public DocumentType DocumentType { get; } = DocumentType.Contract;

        [JsonPropertyName("document_id")]
        public string DocumentId
        {
            get
            {
                return DocumentType.ToString() + "_" + Id;
            }
        }

        public ContractDto? ContractDto { get; set; }

        [JsonPropertyName("merged_text")]
        public string SearchContent
        {
            get
            {
                return ContractDto?.Content ?? "";
            }
        }

        [JsonPropertyName("content_vector")]
        public IReadOnlyList<float> ContentVector { get; set; } = [];
    }
}
