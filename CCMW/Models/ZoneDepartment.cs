using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CCMW.Models
{
    [Table("ZoneDepartments")]
    public class ZoneDepartment
    {
        [Key]
        [Column("zonedept_id")]
        public Guid ZoneDeptId { get; set; }

        [Column("zone_id")]
        public Guid ZoneId { get; set; }

        [Column("department_id")]
        public Guid DepartmentId { get; set; }

        [Column("staff_count")]
        public int StaffCount { get; set; }

        [Column("active_complaints_count")]
        public int ActiveComplaintsCount { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ========== NEW POLYGON SUPPORT PROPERTIES ==========

        /// <summary>
        /// Department-specific sub-polygon within the zone (GeoJSON format)
        /// Stores the boundary coordinates for this department's service area
        /// </summary>
        [Column("boundary_polygon")]
        public string BoundaryPolygon { get; set; }

        /// <summary>
        /// Color code for this department in this zone (hex format)
        /// Example: "#2196F3" for WASA blue, "#4CAF50" for RWMC green
        /// </summary>
        [Column("color_code")]
        public string ColorCode { get; set; }

        /// <summary>
        /// Center latitude of the department's service area
        /// Used for map centering and markers
        /// </summary>
        [Column("center_latitude")]
        public decimal? CenterLatitude { get; set; }

        /// <summary>
        /// Center longitude of the department's service area
        /// Used for map centering and markers
        /// </summary>
        [Column("center_longitude")]
        public decimal? CenterLongitude { get; set; }

        /// <summary>
        /// Service area in square kilometers
        /// Calculated from the polygon
        /// </summary>
        [Column("service_area_sq_km")]
        public decimal? ServiceAreaSqKm { get; set; }

        /// <summary>
        /// Last updated timestamp for the polygon
        /// </summary>
        [Column("polygon_updated_at")]
        public DateTime? PolygonUpdatedAt { get; set; }

        // Navigation Properties
        
        public virtual Zone Zone { get; set; }

        public virtual Department Department { get; set; }
    }
}