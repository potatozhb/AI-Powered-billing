
namespace AIbillingRAGBuilder.Dtos
{
    public enum ContractType
    {
        Software = 0,
        Hardware = 1,
    }


    public class ContractItemDto
    {
        // PK is ContractId + Id
        public int Id { get; set; }

        public int RevisionNumber { get; set; }

        public int ContractId { get; set; }

        public ContractType ItemType { get; set; }

        public int ProductTypeId { get; set; }

        /// <summary>
        /// in DB it is description field
        /// </summary>
        public string? ProductName { get; set; }

        public DateTime? WarrantyStartDate { get; set; }

        public DateTime? WarrantyEndDate { get; set; }

        public DateTime? ContractStartDate { get; set; }

        public DateTime? ContractEndDate { get; set; }

        public int? SoftwareCoverageId { get; set; }

        public int? HardwareCoverageId { get; set; }

        public List<Coverage>? SoftwareCoverages { get; set; } = new List<Coverage>();
        public List<Coverage>? HardwareCoverages { get; set; } = new List<Coverage>();

        public string Content
        {
            get
            {
                return $"""
                Contract Item ID: {Id}
                Product Type ID: {ProductTypeId}
                Product Name: {ProductName}
                """;
            }
        }


        public string Coverages
        {
            get
            {
                var softwareSchedule = "none";

                if (SoftwareCoverages != null && SoftwareCoverages.Count > 0)
                {
                    softwareSchedule = string.Join(
                        Environment.NewLine,
                        SoftwareCoverages
                            .OrderBy(x => x.Day)
                            .Select(x =>
                                $"{x.Day}: {FormatTime(x.StartTime)} - {FormatEndTime(x.EndTime)}"));
                }

                var hardwareSchedule = "none";

                if (HardwareCoverages != null && HardwareCoverages.Count > 0)
                {
                    hardwareSchedule = string.Join(
                        Environment.NewLine,
                        HardwareCoverages
                            .OrderBy(x => x.Day)
                            .Select(x =>
                                $"{x.Day}: {FormatTime(x.StartTime)} - {FormatEndTime(x.EndTime)}"));
                }

                return $"""

                Software Coverage plan:
                Statutory Holiday Coverage: {SoftwareCoverages?.Any(c => c.Day == CoverageDay.Holiday) ?? false}
                Coverage Schedule:
                {softwareSchedule}
                
                Hardware Coverage plan:
                Statutory Holiday Coverage: {HardwareCoverages?.Any(c => c.Day == CoverageDay.Holiday) ?? false}
                Coverage Schedule:
                {hardwareSchedule}
                """;
            }
        }

        private static string FormatTime(TimeOnly? time)
        {
            return time?.ToString("HH:mm") ?? "none";
        }

        private static string FormatEndTime(TimeOnly? time)
        {
            if (time == null)
            {
                return "none";
            }

            return time.Value == TimeOnly.MinValue
                ? "24:00"
                : time.Value.ToString("HH:mm");
        }

    }

    public enum CoverageDay
    {
        Monday,
        Tuesday,
        Wednesday,
        Thursday,
        Friday,
        Saturday,
        Sunday,
        Holiday
    }

    public class Coverage
    {
        public int Id { get; set; }

        public CoverageDay Day { get; set; }

        public TimeOnly? StartTime { get; set; }

        /// <summary>
        /// EndTime is 0, means 24 hours
        /// </summary>
        public TimeOnly? EndTime { get; set; }
    }

    public class ContractRevisionDto
    {
        public int ContractId { get; set; }


        public DateTime? SWWarrantyStartDate { get; set; }

        public DateTime? SWWarrantyEndDate { get; set; }

        public DateTime? SWContractStartDate { get; set; }

        public DateTime? SWContractEndDate { get; set; }

        public DateTime? HWWarrantyStartDate { get; set; }

        public DateTime? HWWarrantyEndDate { get; set; }

        public DateTime? HWContractStartDate { get; set; }

        public DateTime? HWContractEndDate { get; set; }

        public int SoftwareCoverageId { get; set; }

        public int HardwareCoverageId { get; set; }

        public string? BillingSchedule {  get; set; } // unUsed currently
    }
}
