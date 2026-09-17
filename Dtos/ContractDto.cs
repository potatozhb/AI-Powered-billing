using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIbillingRAGBuilder.Dtos
{
    public enum ContractStatus
    {
        Quote,
        Active
    }

    public class ContractDto
    {
        // retrieve the useful fields from the contract table in the database
        public int Id { get; set; }

        public int StoreId { get; set; }

        public int ChunkId { get; set; } = 0;


        /// <summary>
        /// Just need active contract
        /// </summary>
        public ContractStatus Status { get; set; }

        /// <summary>
        /// Order by this
        /// </summary>
        public DateTime CreateTime { get; set; }

        public ContractRevisionDto ContractTerm { get; set; }

        public List<ContractItemDto>? Items { get; set; }

        public string MetaContent
        {
            get
            {
                var sb = new StringBuilder();

                sb.AppendLine();
                sb.AppendLine($"Contract ID: {Id}");
                sb.AppendLine($"Store ID: {StoreId}");

                sb.AppendLine($"Status: {Status}");

                if (ContractTerm != null)
                {
                    sb.AppendLine();
                    sb.AppendLine($"Software Term:");
                    sb.AppendLine($"Warranty Start Date: {ContractTerm.SWWarrantyStartDate}");
                    sb.AppendLine($"Warranty End Date: {ContractTerm.SWWarrantyEndDate}");
                    sb.AppendLine($"Contract Start Date: {ContractTerm.SWContractStartDate}");
                    sb.AppendLine($"Contract End Date: {ContractTerm.SWContractEndDate}");

                    sb.AppendLine();
                    sb.AppendLine($"Hardware Term:");
                    sb.AppendLine($"Warranty Start Date: {ContractTerm.HWWarrantyStartDate}");
                    sb.AppendLine($"Warranty End Date: {ContractTerm.HWWarrantyEndDate}");
                    sb.AppendLine($"Contract Start Date: {ContractTerm.HWContractStartDate}");
                    sb.AppendLine($"Contract End Date: {ContractTerm.HWContractEndDate}");
                    sb.AppendLine();
                }

                if (Items == null || Items.Count == 0)
                {
                    sb.AppendLine("Contract Items: None");
                    return sb.ToString();
                }

                return sb.ToString().TrimEnd();
            }
        }

        public string Content
        {
            get
            {
                var sb = new StringBuilder();

                sb.AppendLine(MetaContent);

                sb.AppendLine();
                if (Items?.Count > 0)
                    sb.AppendLine(Items[0].Coverages);

                sb.AppendLine();
                sb.AppendLine("Contract Items:");
                sb.AppendLine();

                foreach (var item in Items)
                {
                    sb.AppendLine(item.Content);
                    sb.AppendLine();
                }

                sb.AppendLine();

                return sb.ToString().TrimEnd();
            }
        }
    }
}
