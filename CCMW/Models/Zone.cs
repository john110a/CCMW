using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CCMW.Models
{
    public class Zone
    {
        [Key]
        [Column("zone_id")]
        public Guid ZoneId { get; set; }

        [Column("zone_number")]
        public int ZoneNumber { get; set; }

        [Column("zone_name")]
        public string ZoneName { get; set; }

        [Column("zone_code")]
        public string ZoneCode { get; set; }

        [Column("boundary_coordinates")]
        public string BoundaryCoordinates { get; set; }

        [Column("city")]
        public string City { get; set; }

        [Column("province")]
        public string Province { get; set; }

        [Column("total_area_sq_km")]
        public decimal? TotalAreaSqKm { get; set; }

        [Column("population")]
        public int Population { get; set; }

        [Column("active_complaints_count")]
        public int ActiveComplaintsCount { get; set; } = 0;

        [Column("total_complaints_count")]
        public int TotalComplaintsCount { get; set; } = 0;

        [Column("performance_rating")]
        public string PerformanceRating { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [Column("boundary_polygon")]
        public string BoundaryPolygon { get; set; } // GeoJSON format

        [Column("center_latitude")]
        public decimal? CenterLatitude { get; set; }

        [Column("center_longitude")]
        public decimal? CenterLongitude { get; set; }

        [Column("color_code")]
        public string ColorCode { get; set; } // Hex color for zone

        // ADD THIS PROPERTY - IsActive flag for soft delete
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        // Department-specific polygons (for sub-zones)
        [NotMapped]
        public Dictionary<Guid, string> DepartmentPolygons { get; set; }

        public virtual ICollection<User> Users { get; set; }
        public virtual ICollection<StaffProfile> Staffs { get; set; }
        public virtual ICollection<Complaint> Complaints { get; set; }
        public virtual ICollection<ZoneDepartment> ZoneDepartments { get; set; } = new HashSet<ZoneDepartment>();
        public virtual ICollection<ContractorZoneAssignment> ContractorAssignments { get; set; }
    }
}