using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIbillingRAGBuilder.Dtos
{
    public enum UserType
    {
        Software = 0,//default value
        Hardware = 1,
        SoftwareAndHardware = 2,
    }

    public enum InlineStatus
    {
        Open,
        Closed,
        Suspended,
    }

    public class WorkOrderlineDto
    {
        //PK is workOrderID + LineNumber
        public int WorkOrderId {  get; set; }
        public int LineNumber { get; set; }

        public int ContractId { get; set; }
        public int ContractItemId { get; set; }

        public int TechnicianId { get; set; }
        public UserType TechnicianType { get; set; }
        public string? Technician { get; set; }

        public DateTime? CreatedTime { get; set; }
        public DateTime? DispatchTime { get; set; }
        public DateTime? ArrivalTime { get; set; }
        public DateTime? CompleteTime { get; set; }

        public InlineStatus Status { get; set; }

        public string CombinedContent
        {
            get
            {
                var sb = new StringBuilder();

                string scope = ContractItemId > 0 ? "on Contract" : "Out of the Contract";

                sb.AppendLine($"Line Number: {LineNumber}");
                sb.AppendLine($"Technician: {Technician ?? "Unknown"}");
                sb.AppendLine($"Technician Type: {TechnicianType}");
                sb.AppendLine($"Status: {Status}");
                sb.AppendLine($"Line contract scope: {scope}");

                if (CreatedTime.HasValue)
                    sb.AppendLine($"Created Time: {CreatedTime:yyyy-MM-dd HH:mm:ss}");

                if (DispatchTime.HasValue)
                    sb.AppendLine($"Dispatch Time: {DispatchTime:yyyy-MM-dd HH:mm:ss}");

                if (ArrivalTime.HasValue)
                    sb.AppendLine($"Arrival Time: {ArrivalTime:yyyy-MM-dd HH:mm:ss}");

                if (CompleteTime.HasValue)
                    sb.AppendLine($"Complete Time: {CompleteTime:yyyy-MM-dd HH:mm:ss}");

                return sb.ToString().Trim();
            }
        }
    }
}
