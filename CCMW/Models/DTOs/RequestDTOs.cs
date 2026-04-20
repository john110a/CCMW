using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CCMW.Models
{
    // =====================================================
    // CONTRACTOR DTOs
    // =====================================================

    public class ZoneAssignmentRequest
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

    public class PerformanceRecordRequest
    {
        public Guid? ZoneId { get; set; }
        public DateTime ReviewPeriodStart { get; set; }
        public DateTime ReviewPeriodEnd { get; set; }
        public int ComplaintsAssigned { get; set; }
        public int ComplaintsResolved { get; set; }
        public int ResolvedOnTime { get; set; }
        public decimal SlaComplianceRate { get; set; }
        public decimal CitizenRating { get; set; }
        public decimal PerformanceScore { get; set; }
        public decimal PenaltiesAmount { get; set; }
        public decimal BonusAmount { get; set; }
        public string ReviewNotes { get; set; }
        public Guid ReviewedBy { get; set; }
    }

    // =====================================================
    // STAFF DTOs
    // =====================================================

    public class StaffAssignmentRequest
    {
        public Guid AssignedById { get; set; }
        public string Notes { get; set; }
    }

    public class LocationUpdateRequest
    {
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? Accuracy { get; set; }
    }

    public class ResolutionRequest
    {
        public string ResolutionNotes { get; set; }
        public string AfterPhotoUrl { get; set; }
    }

    // =====================================================
    // DEPARTMENT DTOs
    // =====================================================

    public class AssignDepartmentRequest
    {
        public Guid ZoneId { get; set; }
        public Guid DepartmentId { get; set; }
        public int StaffCount { get; set; }
        public int? ActiveComplaintsCount { get; set; }
        public string BoundaryPolygon { get; set; }
        public string ColorCode { get; set; }
        public decimal? CenterLatitude { get; set; }
        public decimal? CenterLongitude { get; set; }
        public decimal? ServiceAreaSqKm { get; set; }
    }

    public class UpdateStatsRequest
    {
        public int? StaffCount { get; set; }
        public int? ActiveComplaintsCount { get; set; }
    }

    public class UpdateBoundaryRequest
    {
        public string BoundaryPolygon { get; set; }
        public string ColorCode { get; set; }
        public decimal? CenterLatitude { get; set; }
        public decimal? CenterLongitude { get; set; }
        public decimal? ServiceAreaSqKm { get; set; }
    }
}