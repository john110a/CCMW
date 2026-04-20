using CCMW.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/contractors")]
    public class ContractorController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        private IHttpActionResult NotFound(string message)
        {
            return Content(HttpStatusCode.NotFound, new { error = message });
        }

        // =====================================================
        // GET ALL CONTRACTORS - FIXED VERSION
        // =====================================================
        [HttpGet]
        [Route("")]
        public IHttpActionResult GetAllContractors([FromUri] bool? isActive = null)
        {
            try
            {
                var query = db.Contractors.AsQueryable();

                if (isActive.HasValue)
                {
                    query = query.Where(c => c.IsActive == isActive.Value);
                }

                // Get the data first, then calculate in memory
                var contractorsData = query
                    .Select(c => new
                    {
                        c.ContractorId,
                        c.CompanyName,
                        c.CompanyRegistrationNumber,
                        c.ContactPersonName,
                        c.ContactPersonPhone,
                        c.ContactEmail,
                        c.CompanyAddress,
                        c.ContractStart,
                        c.ContractEnd,
                        c.ContractValue,
                        c.PerformanceBond,
                        c.PerformanceScore,
                        c.SLAComplianceRate,
                        c.IsActive,
                        c.CreatedAt,
                        c.UpdatedAt
                    })
                    .OrderBy(c => c.CompanyName)
                    .ToList();

                // Calculate in memory - DateTime is non-nullable, so no HasValue needed
                var result = contractorsData.Select(c => new
                {
                    c.ContractorId,
                    c.CompanyName,
                    c.CompanyRegistrationNumber,
                    c.ContactPersonName,
                    c.ContactPersonPhone,
                    c.ContactEmail,
                    c.CompanyAddress,
                    c.ContractStart,
                    c.ContractEnd,
                    c.ContractValue,
                    c.PerformanceBond,
                    c.PerformanceScore,
                    c.SLAComplianceRate,
                    c.IsActive,
                    c.CreatedAt,
                    c.UpdatedAt,
                    AssignedZones = db.ContractorZoneAssignments.Count(z => z.ContractorId == c.ContractorId && z.IsActive),
                    ContractStatus = (c.ContractEnd < DateTime.Now) ? "Expired" : "Active",
                    DaysRemaining = (c.ContractEnd > DateTime.Now) ? (c.ContractEnd - DateTime.Now).Days : 0
                }).ToList();

                return Ok(new
                {
                    TotalContractors = result.Count,
                    ActiveContractors = result.Count(c => c.IsActive),
                    Contractors = result
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error in GetAllContractors: {ex.Message}");
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // GET CONTRACTOR BY ID
        // =====================================================
        [HttpGet]
        [Route("{contractorId:guid}")]
        public IHttpActionResult GetContractorById(Guid contractorId)
        {
            try
            {
                var contractor = db.Contractors
                    .FirstOrDefault(c => c.ContractorId == contractorId);

                if (contractor == null)
                    return NotFound("Contractor not found");

                var assignedZones = db.ContractorZoneAssignments
                    .Include(z => z.Zone)
                    .Where(z => z.ContractorId == contractorId && z.IsActive)
                    .Select(z => new
                    {
                        z.AssignmentId,
                        z.ZoneId,
                        ZoneName = z.Zone.ZoneName,
                        ZoneNumber = z.Zone.ZoneNumber,
                        z.ServiceType,
                        z.ContractStart,
                        z.ContractEnd,
                        z.ContractValue,
                        z.PerformanceBond,
                        ActiveComplaints = db.Complaints.Count(c => c.ZoneId == z.ZoneId &&
                                                                   c.CurrentStatus != ComplaintStatus.Resolved &&
                                                                   c.CurrentStatus != ComplaintStatus.Closed)
                    })
                    .ToList();

                var performanceHistory = db.ContractorPerformanceHistories
                    .Where(p => p.ContractorId == contractorId)
                    .OrderByDescending(p => p.ReviewPeriodEnd)
                    .Select(p => new
                    {
                        p.HistoryId,
                        p.ReviewPeriodStart,
                        p.ReviewPeriodEnd,
                        p.ComplaintsAssigned,
                        p.ComplaintsResolved,
                        p.ResolvedOnTime,
                        p.SlaComplianceRate,
                        p.CitizenRating,
                        p.PerformanceScore,
                        p.PenaltiesAmount,
                        p.BonusAmount,
                        p.ReviewNotes,
                        p.ReviewedAt
                    })
                    .ToList();

                decimal? avgPerformance = null;
                if (performanceHistory.Any())
                {
                    avgPerformance = performanceHistory.Average(p => p.PerformanceScore);
                }

                return Ok(new
                {
                    Contractor = contractor,
                    AssignedZones = assignedZones,
                    TotalZones = assignedZones.Count,
                    PerformanceHistory = performanceHistory,
                    PerformanceRating = avgPerformance
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error in GetContractorById: {ex.Message}");
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // GET CONTRACTOR ZONES
        // =====================================================
        [HttpGet]
        [Route("{contractorId:guid}/zones")]
        public IHttpActionResult GetContractorZones(Guid contractorId)
        {
            try
            {
                var contractor = db.Contractors.Find(contractorId);
                if (contractor == null)
                    return NotFound("Contractor not found");

                var zones = db.ContractorZoneAssignments
                    .Include(z => z.Zone)
                    .Where(z => z.ContractorId == contractorId && z.IsActive)
                    .Select(z => new
                    {
                        z.AssignmentId,
                        z.ZoneId,
                        ZoneName = z.Zone.ZoneName,
                        ZoneNumber = z.Zone.ZoneNumber,
                        City = z.Zone.City,
                        Province = z.Zone.Province,
                        z.ServiceType,
                        z.ContractStart,
                        z.ContractEnd,
                        z.ContractValue,
                        z.PerformanceBond,
                        z.AssignedDate,
                        ActiveComplaints = db.Complaints.Count(c => c.ZoneId == z.ZoneId &&
                                                                   c.CurrentStatus != ComplaintStatus.Resolved &&
                                                                   c.CurrentStatus != ComplaintStatus.Closed),
                        ResolvedComplaints = db.Complaints.Count(c => c.ZoneId == z.ZoneId &&
                                                                     c.CurrentStatus == ComplaintStatus.Resolved)
                    })
                    .ToList();

                return Ok(new
                {
                    ContractorId = contractorId,
                    CompanyName = contractor.CompanyName,
                    TotalZones = zones.Count,
                    Zones = zones
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error in GetContractorZones: {ex.Message}");
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // CREATE CONTRACTOR
        // =====================================================
        [HttpPost]
        [Route("create")]
        public IHttpActionResult CreateContractor([FromBody] Contractor contractor)
        {
            try
            {
                if (contractor == null)
                    return BadRequest("Contractor data is required.");

                if (string.IsNullOrEmpty(contractor.CompanyName))
                    return BadRequest("Company name is required.");

                if (!string.IsNullOrEmpty(contractor.CompanyRegistrationNumber) &&
                    db.Contractors.Any(c => c.CompanyRegistrationNumber == contractor.CompanyRegistrationNumber))
                {
                    return BadRequest("Company registration number already exists.");
                }

                contractor.ContractorId = Guid.NewGuid();
                contractor.CreatedAt = DateTime.Now;
                contractor.UpdatedAt = DateTime.Now;
                contractor.IsActive = true;

                db.Contractors.Add(contractor);
                db.SaveChanges();

                return Ok(new
                {
                    Message = "Contractor created successfully",
                    ContractorId = contractor.ContractorId
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error in CreateContractor: {ex.Message}");
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // UPDATE CONTRACTOR
        // =====================================================
        [HttpPut]
        [Route("{contractorId:guid}/update")]
        public IHttpActionResult UpdateContractor(Guid contractorId, [FromBody] Contractor updated)
        {
            try
            {
                var contractor = db.Contractors.Find(contractorId);
                if (contractor == null)
                    return NotFound("Contractor not found");

                if (!string.IsNullOrEmpty(updated.CompanyName))
                    contractor.CompanyName = updated.CompanyName;

                if (!string.IsNullOrEmpty(updated.ContactPersonName))
                    contractor.ContactPersonName = updated.ContactPersonName;

                if (!string.IsNullOrEmpty(updated.ContactPersonPhone))
                    contractor.ContactPersonPhone = updated.ContactPersonPhone;

                if (!string.IsNullOrEmpty(updated.ContactEmail))
                    contractor.ContactEmail = updated.ContactEmail;

                if (!string.IsNullOrEmpty(updated.CompanyAddress))
                    contractor.CompanyAddress = updated.CompanyAddress;

                contractor.ContractStart = updated.ContractStart;
                contractor.ContractEnd = updated.ContractEnd;
                contractor.ContractValue = updated.ContractValue;
                contractor.PerformanceBond = updated.PerformanceBond;
                contractor.PerformanceScore = updated.PerformanceScore;
                contractor.SLAComplianceRate = updated.SLAComplianceRate;

                contractor.UpdatedAt = DateTime.Now;

                db.SaveChanges();

                return Ok(new
                {
                    Message = "Contractor updated successfully",
                    ContractorId = contractorId
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error in UpdateContractor: {ex.Message}");
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // DELETE/DEACTIVATE CONTRACTOR
        // =====================================================
        [HttpDelete]
        [Route("{contractorId:guid}")]
        public IHttpActionResult DeleteContractor(Guid contractorId)
        {
            try
            {
                var contractor = db.Contractors.Find(contractorId);
                if (contractor == null)
                    return NotFound("Contractor not found");

                // Soft delete - set IsActive to false
                contractor.IsActive = false;
                contractor.UpdatedAt = DateTime.Now;

                db.SaveChanges();

                return Ok(new
                {
                    Message = "Contractor deactivated successfully",
                    ContractorId = contractorId
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error in DeleteContractor: {ex.Message}");
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // ASSIGN CONTRACTOR TO ZONE
        // =====================================================
        [HttpPost]
        [Route("assign-to-zone")]
        public IHttpActionResult AssignToZone([FromBody] ZoneAssignmentRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest("Assignment data is required.");

                var contractor = db.Contractors.Find(request.ContractorId);
                if (contractor == null)
                    return NotFound("Contractor not found");

                var zone = db.Zones.Find(request.ZoneId);
                if (zone == null)
                    return NotFound("Zone not found");

                var existingActive = db.ContractorZoneAssignments
                    .FirstOrDefault(z => z.ZoneId == request.ZoneId && z.IsActive);

                if (existingActive != null)
                    return BadRequest("Zone already has an active contractor. Please terminate the current assignment first.");

                var assignment = new ContractorZoneAssignment
                {
                    AssignmentId = Guid.NewGuid(),
                    ContractorId = request.ContractorId,
                    ZoneId = request.ZoneId,
                    AssignedBy = request.AssignedBy,
                    AssignedDate = DateTime.Now,
                    ContractStart = request.ContractStart,
                    ContractEnd = request.ContractEnd,
                    ServiceType = request.ServiceType,
                    ContractValue = request.ContractValue,
                    PerformanceBond = request.PerformanceBond,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                db.ContractorZoneAssignments.Add(assignment);
                db.SaveChanges();

                return Ok(new
                {
                    Message = "Contractor assigned to zone successfully",
                    AssignmentId = assignment.AssignmentId,
                    ContractorName = contractor.CompanyName,
                    ZoneName = zone.ZoneName
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error in AssignToZone: {ex.Message}");
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // TERMINATE CONTRACTOR ASSIGNMENT
        // =====================================================
        [HttpPost]
        [Route("assignments/{assignmentId:guid}/terminate")]
        public IHttpActionResult TerminateAssignment(Guid assignmentId, [FromBody] TerminateRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest("Termination data is required.");

                var assignment = db.ContractorZoneAssignments
                    .Include(a => a.Contractor)
                    .Include(a => a.Zone)
                    .FirstOrDefault(a => a.AssignmentId == assignmentId && a.IsActive);

                if (assignment == null)
                    return NotFound("Active assignment not found");

                assignment.IsActive = false;
                assignment.TerminationReason = request.Reason;
                assignment.TerminatedAt = DateTime.Now;
                assignment.TerminatedBy = request.TerminatedBy;
                assignment.UpdatedAt = DateTime.Now;

                db.SaveChanges();

                return Ok(new
                {
                    Message = "Assignment terminated successfully",
                    ContractorName = assignment.Contractor.CompanyName,
                    ZoneName = assignment.Zone.ZoneName,
                    Reason = request.Reason
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error in TerminateAssignment: {ex.Message}");
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // ADD PERFORMANCE RECORD
        // =====================================================
        [HttpPost]
        [Route("{contractorId:guid}/performance")]
        public IHttpActionResult AddPerformanceRecord(Guid contractorId, [FromBody] PerformanceRecordRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest("Performance data is required.");

                var contractor = db.Contractors.Find(contractorId);
                if (contractor == null)
                    return NotFound("Contractor not found");

                var performance = new ContractorPerformanceHistory
                {
                    HistoryId = Guid.NewGuid(),
                    ContractorId = contractorId,
                    ZoneId = request.ZoneId,
                    ReviewPeriodStart = request.ReviewPeriodStart,
                    ReviewPeriodEnd = request.ReviewPeriodEnd,
                    ComplaintsAssigned = request.ComplaintsAssigned,
                    ComplaintsResolved = request.ComplaintsResolved,
                    ResolvedOnTime = request.ResolvedOnTime,
                    SlaComplianceRate = request.SlaComplianceRate,
                    CitizenRating = request.CitizenRating,
                    PerformanceScore = request.PerformanceScore,
                    PenaltiesAmount = request.PenaltiesAmount,
                    BonusAmount = request.BonusAmount,
                    ReviewNotes = request.ReviewNotes,
                    ReviewedBy = request.ReviewedBy,
                    ReviewedAt = DateTime.Now,
                    CreatedAt = DateTime.Now
                };

                db.ContractorPerformanceHistories.Add(performance);

                contractor.PerformanceScore = request.PerformanceScore;
                contractor.SLAComplianceRate = request.SlaComplianceRate;
                contractor.UpdatedAt = DateTime.Now;

                db.SaveChanges();

                return Ok(new
                {
                    Message = "Performance record added successfully",
                    PerformanceId = performance.HistoryId,
                    NewPerformanceScore = contractor.PerformanceScore
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error in AddPerformanceRecord: {ex.Message}");
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // GET CONTRACTOR PERFORMANCE DASHBOARD
        // =====================================================
        [HttpGet]
        [Route("{contractorId:guid}/dashboard")]
        public IHttpActionResult GetContractorDashboard(Guid contractorId)
        {
            try
            {
                var contractor = db.Contractors
                    .FirstOrDefault(c => c.ContractorId == contractorId);

                if (contractor == null)
                    return NotFound("Contractor not found");

                var assignments = db.ContractorZoneAssignments
                    .Where(z => z.ContractorId == contractorId && z.IsActive)
                    .ToList();

                int totalActiveComplaints = 0;
                int totalResolvedComplaints = 0;
                var zonesList = new List<object>();

                foreach (var assignment in assignments)
                {
                    var zone = db.Zones.FirstOrDefault(z => z.ZoneId == assignment.ZoneId);
                    string zoneName = zone?.ZoneName ?? "Unknown Zone";

                    int activeComplaints = db.Complaints.Count(c => c.ZoneId == assignment.ZoneId &&
                                            c.CurrentStatus != ComplaintStatus.Resolved &&
                                            c.CurrentStatus != ComplaintStatus.Closed);

                    int resolvedComplaints = db.Complaints.Count(c => c.ZoneId == assignment.ZoneId &&
                                            c.CurrentStatus == ComplaintStatus.Resolved);

                    totalActiveComplaints += activeComplaints;
                    totalResolvedComplaints += resolvedComplaints;

                    zonesList.Add(new
                    {
                        assignment.ZoneId,
                        ZoneName = zoneName,
                        assignment.ServiceType,
                        assignment.ContractStart,
                        assignment.ContractEnd,
                        ActiveComplaints = activeComplaints,
                        ResolvedComplaints = resolvedComplaints
                    });
                }

                double resolutionRate = 0;
                int totalComplaints = totalActiveComplaints + totalResolvedComplaints;
                if (totalComplaints > 0)
                {
                    resolutionRate = (double)totalResolvedComplaints / totalComplaints * 100;
                }

                int daysRemaining = 0;
                if (contractor.ContractEnd > DateTime.Now)
                {
                    daysRemaining = (contractor.ContractEnd - DateTime.Now).Days;
                }

                var recentPerformance = new List<object>();
                try
                {
                    recentPerformance = db.ContractorPerformanceHistories
                        .Where(p => p.ContractorId == contractorId)
                        .OrderByDescending(p => p.ReviewPeriodEnd)
                        .Take(6)
                        .Select(p => new
                        {
                            p.ReviewPeriodStart,
                            p.ReviewPeriodEnd,
                            p.ComplaintsAssigned,
                            p.ComplaintsResolved,
                            p.SlaComplianceRate,
                            p.PerformanceScore
                        })
                        .ToList<object>();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading performance: {ex.Message}");
                }

                return Ok(new
                {
                    Contractor = new
                    {
                        contractor.ContractorId,
                        contractor.CompanyName,
                        PerformanceScore = contractor.PerformanceScore,
                        SLAComplianceRate = contractor.SLAComplianceRate,
                        ContractStart = contractor.ContractStart,
                        ContractEnd = contractor.ContractEnd,
                        DaysRemaining = daysRemaining
                    },
                    Statistics = new
                    {
                        TotalZones = assignments.Count,
                        TotalActiveComplaints = totalActiveComplaints,
                        TotalResolvedComplaints = totalResolvedComplaints,
                        ResolutionRate = Math.Round(resolutionRate, 2)
                    },
                    Zones = zonesList,
                    RecentPerformance = recentPerformance
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dashboard error: {ex.Message}");
                return InternalServerError(ex);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }

        // =====================================================
        // DTO CLASSES
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
    }
}