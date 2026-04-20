using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace CCMW.Models
{
    public class Department
    {
        [Key]
        [Column("department_id")]
        public Guid DepartmentId { get; set; }

        [Column("department_name")]
        public string DepartmentName { get; set; }

        [Column("department_code")]
        public string DepartmentCode { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("privatization_status")]
        public string PrivatizationStatus { get; set; }

        [Column("contractor_id")]
        public Guid? ContractorId { get; set; }

        [Column("head_admin_id")]
        public Guid? HeadAdminId { get; set; }

        [Column("performance_score")]
        public decimal PerformanceScore { get; set; }

        [Column("performance_rating")]
        public PerformanceRating PerformanceRating { get; set; } = PerformanceRating.Average;

        [Column("active_complaints_count")]
        public int ActiveComplaintsCount { get; set; }

        [Column("resolved_complaints_count")]
        public int ResolvedComplaintsCount { get; set; }

        [Column("total_complaints_count")]
        public int TotalComplaintsCount { get; set; }

        // FIXED: Added default value
        [Column("average_resolution_time_days")]
        public decimal AverageResolutionTimeDays { get; set; } = 0;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("ContractorId")]
        public virtual Contractor Contractor { get; set; }

        [ForeignKey("HeadAdminId")]
        public virtual User HeadAdmin { get; set; }

        public virtual ICollection<StaffProfile> Staffs { get; set; }
        public virtual ICollection<Complaint> Complaints { get; set; }
        public virtual ICollection<ComplaintCategory> ComplaintCategories { get; set; }
        public virtual ICollection<ZoneDepartment> ZoneDepartments { get; set; } = new HashSet<ZoneDepartment>();
    }
}