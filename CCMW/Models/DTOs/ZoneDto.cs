// DTOs/ZoneDto.cs
using System;
using System.Collections.Generic;

namespace CCMW.Controllers
{
    public class ZoneDto
    {
        public Guid ZoneId { get; set; }
        public int ZoneNumber { get; set; }
        public string ZoneName { get; set; }
        public string ZoneCode { get; set; }
        public string City { get; set; }
        public string Province { get; set; }
        public decimal? TotalAreaSqKm { get; set; }
        public int Population { get; set; }
        public int ActiveComplaintsCount { get; set; }
        public int TotalComplaintsCount { get; set; }
        public string PerformanceRating { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string BoundaryPolygon { get; set; }
        public decimal? CenterLatitude { get; set; }
        public decimal? CenterLongitude { get; set; }
        public string ColorCode { get; set; }
        public bool IsActive { get; set; }

        // Sub-zone properties
        public Guid? ParentZoneId { get; set; }
        public int Level { get; set; }
        public int DisplayOrder { get; set; }
        public string ParentZoneName { get; set; }
        public List<ZoneDto> SubZones { get; set; }
    }
}