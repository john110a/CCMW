using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace CCMW.Models
{
    public class Contractor
    {
        [Key]
        [Column("contractor_id")]
        public Guid ContractorId { get; set; }

        [Column("company_name")]
        public string CompanyName { get; set; }

        [Column("company_registration_number")]
        public string CompanyRegistrationNumber { get; set; }

        [Column("contact_person_name")]
        public string ContactPersonName { get; set; }

        [Column("contact_person_phone")]
        public string ContactPersonPhone { get; set; }

        [Column("contact_email")] // FIXED: Changed from ContactPersonEmail to match database
        public string ContactEmail { get; set; }

        [Column("company_address")]
        public string CompanyAddress { get; set; }

        [Column("contract_start")] // FIXED: Changed from ContractStartDate
        public DateTime ContractStart { get; set; }

        [Column("contract_end")] // FIXED: Changed from ContractEndDate
        public DateTime ContractEnd { get; set; }

        [Column("contract_value")]
        public decimal ContractValue { get; set; }

        [Column("performance_bond")]
        public decimal PerformanceBond { get; set; }

        [Column("performance_score")]
        public decimal PerformanceScore { get; set; }

        [Column("sla_compliance_rate")]
        public decimal SLAComplianceRate { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual ICollection<Department> Departments { get; set; }
        public virtual ICollection<ContractPerformance> ContractPerformances { get; set; } = new HashSet<ContractPerformance>();
        public virtual ICollection<ContractorZoneAssignment> ZoneAssignments { get; set; }
        public virtual ICollection<ContractorPerformanceHistory> PerformanceHistory { get; set; }
    }
}