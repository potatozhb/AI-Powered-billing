using System.Text;

namespace AIbillingRAGBuilder.Dtos
{
    public enum OrderStatus
    {
        Canceled,//default
        Billed,
        Open,
        Closed,
        Pending,
    }

    public enum BillingStatus
    {
        Billable,
        Free,
    }

    public class WorkOrderDto
    {
        // retrieve the useful fields from the order table in the database
        // PK
        public int WorkOrderId { get; set;  }

        public int StoreId { get; set; }

        public string? StoreName { get; set; }

        public bool IsMajorAccount { get; set; }

        public string? CountryString { get; set; } = null;

        public DateTime? CreateDateTime { get; set; }

        public string MajorAccountName { get; set; } = string.Empty;

        public int InitialSeverity { get; set; }

        public int CurrentSeverity { get; set; }

        public int ProjectId { get; set; }

        public OrderStatus Status { get; set; }

        public BillingStatus BillingStatus { get; set; }

        public int? FirstRemarkId{ get; set; }
        public int? LastRemarkId { get; set; }

        public List<WorkOrderlineDto>? WorkOrderLines { get; set; }

        public List<WorkOrderRemarkDto>? Remarks { get; set; }

        public List<ContractDto>? Contracts { get; set; }

        public string WorkOrderInlinesCombined
        {
            get
            {
                if(WorkOrderLines == null || !WorkOrderLines.Any()) return string.Empty;
                var lines = "Order Lines: \r\n\r\n" + string.Join("\r\n\r\n" , WorkOrderLines.Select((x,i) => $"Order Line: #{i + 1}\r\n\r\n" + x.CombinedContent));
                return lines;
            }
        }

        public string ContractsCombined
        {
            get
            {
                if(Contracts == null || !Contracts.Any()) return string.Empty;
                var lines = "Contracts: \r\n\r\n" + string.Join("\r\n\r\n" , Contracts.Select((x,i) => $"Contract Item: #{i + 1}\r\n\r\n" + x.Content));
                return lines;
            }
        }

        public string RemarksCombined
        {
            get
            {
                if (Remarks == null || !Remarks.Any()) return string.Empty;
                var lines = "Order Remarks: \r\n\r\n" + string.Join("\r\n\r\n", Remarks.Select((x, i) => $"Order Remark: #{i + 1}\r\n\r\n" + x.Content));
                return lines;
            }
        }

        /// <summary>
        /// For library creation, no contract included
        /// </summary>
        public string OrderSearchContent
        {
            get
            {
                var sb = new StringBuilder();

                sb.AppendLine();
                sb.AppendLine("Work Order");
                sb.AppendLine("----------------------------------------");
                sb.AppendLine($"Work Order ID: {WorkOrderId}");
                sb.AppendLine($"Store ID: {StoreId}");
                sb.AppendLine($"Store Name: {StoreName}");
                sb.AppendLine($"Country: {CountryString}");
                sb.AppendLine($"Is Major Customer: {IsMajorAccount}");
                sb.AppendLine($"Major Customer Name: {MajorAccountName}");
                sb.AppendLine($"Initial Severity: {InitialSeverity}");
                sb.AppendLine($"Current Severity: {CurrentSeverity}");
                sb.AppendLine($"Project ID: {ProjectId}");

                sb.AppendLine();

                if (!string.IsNullOrWhiteSpace(WorkOrderInlinesCombined))
                {
                    sb.AppendLine(WorkOrderInlinesCombined);
                    sb.AppendLine();
                }

                if(!string.IsNullOrWhiteSpace(RemarksCombined))
                {
                    sb.AppendLine(RemarksCombined);
                    sb.AppendLine();
                }

                sb.AppendLine();

                if (!string.IsNullOrWhiteSpace(ContractsCombined))
                {
                    sb.AppendLine(ContractsCombined);
                }

                sb.AppendLine();

                return sb.ToString().Trim();
            }
        }

        public string OrderMetaContent
        {
            get
            {
                var sb = new StringBuilder();

                sb.AppendLine();
                sb.AppendLine("Work Order");
                sb.AppendLine("----------------------------------------");
                sb.AppendLine($"Work Order ID: {WorkOrderId}");
                sb.AppendLine($"Billing Status: {BillingStatus}");
                sb.AppendLine($"Store ID: {StoreId}");
                sb.AppendLine($"Store Name: {StoreName}");
                sb.AppendLine($"Country: {CountryString}");
                sb.AppendLine($"Project ID: {ProjectId}");
                sb.AppendLine($"Is Major Customer: {IsMajorAccount}");
                sb.AppendLine($"Major Customer Name: {MajorAccountName}");
                sb.AppendLine($"Initial Severity: {InitialSeverity}");
                sb.AppendLine($"Current Severity: {CurrentSeverity}");

                sb.AppendLine();

                return sb.ToString().Trim();
            }
        }

        /// <summary>
        /// For library creation, no contract included
        /// </summary>
        public string OrderContentWithoutContract
        {
            get
            {
                var sb = new StringBuilder();

                sb.AppendLine(OrderMetaContent);

                if (!string.IsNullOrWhiteSpace(WorkOrderInlinesCombined))
                {
                    sb.AppendLine(WorkOrderInlinesCombined);
                    sb.AppendLine();
                }

                if (!string.IsNullOrWhiteSpace(RemarksCombined))
                {
                    sb.AppendLine(RemarksCombined);
                    sb.AppendLine();
                }

                sb.AppendLine();

                return sb.ToString().Trim();
            }
        }

        /// <summary>
        /// For search target, with its contract information
        /// </summary>
        public string OrderContent
        {
            get
            {
                var sb = new StringBuilder();

                if (!string.IsNullOrWhiteSpace(OrderContentWithoutContract))
                {
                    sb.AppendLine(OrderContentWithoutContract);
                    sb.AppendLine();
                }

                if (!string.IsNullOrWhiteSpace(ContractsCombined))
                {
                    sb.AppendLine(ContractsCombined);
                }

                sb.AppendLine();

                return sb.ToString().Trim();
            }
        }
    }
}
