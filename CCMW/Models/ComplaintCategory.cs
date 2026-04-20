using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CCMW.Models
{
    public class ComplaintCategory
    {
        [Key]
        [Column("category_id")]
        public Guid CategoryId { get; set; }

        [Required]
        [Column("category_name")]
        public string CategoryName { get; set; }

        [Column("category_code")]
        public string CategoryCode { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("department_id")]
        public Guid DepartmentId { get; set; }

        [Column("icon_name")]
        public string IconName { get; set; }

        [Column("color_code")]
        public string ColorCode { get; set; }

        [Column("priority_weight")]
        public int PriorityWeight { get; set; }

        [Column("expected_resolution_time_hours")]
        public int ExpectedResolutionTimeHours { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual Department Department { get; set; }

        public virtual ICollection<Complaint> Complaints { get; set; }
    }
}