using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Http;
using CCMW.Models;

namespace CCMW.Controllers
{
    [RoutePrefix("api/contractor-zones")]
    public class ContractorZoneController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        // =====================================================
        // GET: api/contractor-zones/contractor/{contractorId}
        // Get all zones assigned to a contractor
        // =====================================================
        [HttpGet]
        [Route("contractor/{contractorId:guid}")]
        public IHttpActionResult GetContractorZones(Guid contractorId)
        {
            try
            {
                var assignments = db.ContractorZoneAssignments
                    .Include(a => a.Zone)
                    .Where(a => a.ContractorId == contractorId && a.IsActive)
                    .Select(a => new
                    {
                        a.AssignmentId,
                        Zone = new
                        {
                            a.Zone.ZoneId,
                            a.Zone.ZoneName,
                            a.Zone.ZoneNumber,
                            a.Zone.City,
                            a.Zone.Province
                        },
                        a.ServiceType,
                        a.ContractStart,
                        a.ContractEnd,
                        a.ContractValue,
                        a.PerformanceBond,
                        a.AssignedDate,
                        ActiveComplaints = db.Complaints.Count(c =>
                            c.ZoneId == a.ZoneId &&
                            c.CurrentStatus != ComplaintStatus.Resolved && // Use enum
                            c.CurrentStatus != ComplaintStatus.Closed)
                    })
                    .ToList();

                return Ok(new
                {
                    ContractorId = contractorId,
                    TotalZones = assignments.Count,
                    Zones = assignments
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // GET: api/contractor-zones/zone/{zoneId}
        // Get current contractor for a zone
        // =====================================================
        [HttpGet]
        [Route("zone/{zoneId:guid}")]
        public IHttpActionResult GetZoneContractor(Guid zoneId)
        {
            try
            {
                var assignment = db.ContractorZoneAssignments
                    .Include(a => a.Contractor)
                    .Where(a => a.ZoneId == zoneId && a.IsActive)
                    .Select(a => new
                    {
                        a.AssignmentId,
                        Contractor = new
                        {
                            a.Contractor.ContractorId,
                            a.Contractor.CompanyName,
                            a.Contractor.ContactPersonName,
                            a.Contractor.ContactPersonPhone,
                            a.Contractor.ContactEmail,
                            a.Contractor.PerformanceScore
                        },
                        a.ServiceType,
                        a.ContractStart,
                        a.ContractEnd,
                        a.ContractValue,
                        a.PerformanceBond,
                        a.AssignedDate
                    })
                    .FirstOrDefault();

                if (assignment == null)
                {
                    return Ok(new
                    {
                        ZoneId = zoneId,
                        HasContractor = false,
                        Message = "No contractor assigned to this zone"
                    });
                }

                return Ok(new
                {
                    ZoneId = zoneId,
                    HasContractor = true,
                    Assignment = assignment
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // POST: api/contractor-zones/assign
        // Assign contractor to zone
        // =====================================================
        [HttpPost]
        [Route("assign")]
        public IHttpActionResult AssignContractorToZone([FromBody] AssignContractorRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest("Request data is required");

                // Check if zone already has active contractor
                var existing = db.ContractorZoneAssignments
                    .FirstOrDefault(a => a.ZoneId == request.ZoneId && a.IsActive);

                if (existing != null)
                {
                    return Content(HttpStatusCode.BadRequest, new
                    {
                        error = "Zone already has an active contractor. Please terminate current assignment first."
                    });
                }

                // Check if contractor exists
                var contractor = db.Contractors.Find(request.ContractorId);
                if (contractor == null)
                    return Content(HttpStatusCode.NotFound, new { error = "Contractor not found" });

                // Check if zone exists
                var zone = db.Zones.Find(request.ZoneId);
                if (zone == null)
                    return Content(HttpStatusCode.NotFound, new { error = "Zone not found" });

                // Check if admin exists
                var admin = db.Users.Find(request.AssignedBy);
                if (admin == null)
                    return Content(HttpStatusCode.NotFound, new { error = "Admin not found" });

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
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // POST: api/contractor-zones/{assignmentId}/terminate
        // Terminate contractor assignment
        // =====================================================
        [HttpPost]
        [Route("{assignmentId:guid}/terminate")]
        public IHttpActionResult TerminateAssignment(Guid assignmentId, [FromBody] TerminateRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest("Request data is required");

                var assignment = db.ContractorZoneAssignments
                    .Include(a => a.Contractor)
                    .Include(a => a.Zone)
                    .FirstOrDefault(a => a.AssignmentId == assignmentId && a.IsActive);

                if (assignment == null)
                    return Content(HttpStatusCode.NotFound, new { error = "Active assignment not found" });

                assignment.IsActive = false;
                assignment.TerminationReason = request.Reason;
                assignment.TerminatedAt = DateTime.Now;
                assignment.TerminatedBy = request.TerminatedBy;
                assignment.UpdatedAt = DateTime.Now;

                db.SaveChanges();

                return Ok(new
                {
                    Message = "Assignment terminated successfully",
                    ContractorName = assignment.Contractor?.CompanyName,
                    ZoneName = assignment.Zone?.ZoneName,
                    Reason = request.Reason
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // GET: api/contractor-zones/available-zones
        // Get zones available for assignment
        // =====================================================
        [HttpGet]
        [Route("available-zones")]
        public IHttpActionResult GetAvailableZones()
        {
            try
            {
                // Get zones that have no active contractor
                var assignedZoneIds = db.ContractorZoneAssignments
                    .Where(a => a.IsActive)
                    .Select(a => a.ZoneId)
                    .ToList();

                var availableZones = db.Zones
                    .Where(z => !assignedZoneIds.Contains(z.ZoneId))
                    .Select(z => new
                    {
                        z.ZoneId,
                        z.ZoneName,
                        z.ZoneNumber,
                        z.City,
                        z.Province,
                        ActiveComplaints = db.Complaints.Count(c =>
                            c.ZoneId == z.ZoneId &&
                            c.CurrentStatus != ComplaintStatus.Resolved &&
                            c.CurrentStatus != ComplaintStatus.Closed)
                    })
                    .ToList();

                return Ok(availableZones);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // GET: api/contractor-zones/assignment-history/contractor/{contractorId}
        // Get assignment history for a contractor
        // =====================================================
        [HttpGet]
        [Route("assignment-history/contractor/{contractorId:guid}")]
        public IHttpActionResult GetContractorAssignmentHistory(Guid contractorId)
        {
            try
            {
                var history = db.ContractorZoneAssignments
                    .Include(a => a.Zone)
                    .Where(a => a.ContractorId == contractorId)
                    .OrderByDescending(a => a.AssignedDate)
                    .Select(a => new
                    {
                        a.AssignmentId,
                        Zone = new { a.Zone.ZoneName, a.Zone.ZoneNumber },
                        a.AssignedDate,
                        a.ContractStart,
                        a.ContractEnd,
                        a.ServiceType,
                        a.IsActive,
                        a.TerminationReason,
                        a.TerminatedAt
                    })
                    .ToList();

                return Ok(history);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }

    // =====================================================
    // Request DTOs
    // =====================================================
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