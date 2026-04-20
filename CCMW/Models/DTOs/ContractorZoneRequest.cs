using System;

namespace CCMW.DTOs
{
    public class AssignContractorRequest
    {
        public Guid ContractorId { get; set; }
        public Guid ZoneId { get; set; }
        public Guid AssignedBy { get; set; }
        public DateTime ContractStart { get; set; }
        public DateTime ContractEnd { get; set; }
        public string ServiceType { get; set; }
        public decimal ContractValue { get; set; }
        public decimal PerformanceBond { get; set; }
    }

    public class TerminateRequest
    {
        public string Reason { get; set; }
        public Guid TerminatedBy { get; set; }
    }
}