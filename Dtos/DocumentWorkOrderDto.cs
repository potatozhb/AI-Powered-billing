using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AIbillingRAGBuilder.Dtos
{
    public class DocumentWorkOrderDto
    {
        /// <summary>
        /// Order ID
        /// </summary>
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("document_type")]
        public DocumentType DocumentType { get; } = DocumentType.Order;

        [JsonPropertyName("document_id")]
        public string DocumentId
        {
            get
            {
                return DocumentType.ToString() + "_" + Id;
            }
        }

        public WorkOrderDto? WorkOrder { get; set; }

        [JsonPropertyName("merged_text")]
        public string SearchContent
        {
            get
            {
                return WorkOrder?.OrderContent ?? "";
            }
        }


        [JsonPropertyName("content_vector")]
        public IReadOnlyList<float> ContentVector { get; set; } = [];
    }
}
