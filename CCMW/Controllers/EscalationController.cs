using CCMW.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/escalations")]
    public class EscalationController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        // CHECK FOR OVERDUE COMPLAINTS (48-HOUR RULE)
        [HttpGet]
        [Route("check-overdue")]
        public IHttpActionResult Check48HourOverdueComplaints()
        {
            var overdueThreshold = DateTime.Now.AddHours(-48);

            var overdueComplaints = db.Complaints
                .Where(c => c.CurrentStatus != ComplaintStatus.Resolved &&
                           c.CurrentStatus != ComplaintStatus.Closed &&
                           c.CurrentStatus != ComplaintStatus.Rejected &&
                           c.CreatedAt < overdueThreshold &&
                           c.EscalationLevel < 3) // Max escalation level 3
                .ToList();

            var escalatedCount = 0;
            foreach (var complaint in overdueComplaints)
            {
                // Auto-escalate
                complaint.EscalationLevel++;
                // ✅ REMOVED: Escalation.EscalatedAt = DateTime.Now; - Wrong syntax
                complaint.UpdatedAt = DateTime.Now;

                // Create escalation record
                var escalation = new Escalation
                {
                    EscalationId = Guid.NewGuid(),
                    ComplaintId = complaint.ComplaintId,
                    EscalationLevel = complaint.EscalationLevel,
                    EscalationReason = "Time_Exceeded",
                    HoursElapsed = (decimal)(DateTime.Now - complaint.CreatedAt).TotalHours,
                    EscalatedAt = DateTime.Now,  // ✅ This is correct - using Escalation property
                    EscalatedById = Guid.Empty, // System auto-escalation
                    EscalationNotes = $"Auto-escalated to Level {complaint.EscalationLevel} after 48+ hours"
                };

                db.Escalations.Add(escalation);
                escalatedCount++;

                // Add to status history
                db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
                {
                    HistoryId = Guid.NewGuid(),
                    ComplaintId = complaint.ComplaintId,
                    PreviousStatus = complaint.CurrentStatus.ToString(),
                    NewStatus = complaint.CurrentStatus.ToString(), // Status same, just escalated
                    ChangedById = Guid.Empty, // System
                    ChangedAt = DateTime.Now,
                    Notes = $"Escalated to Level {complaint.EscalationLevel}"
                });
            }

            if (escalatedCount > 0)
            {
                db.SaveChanges();
            }

            return Ok(new
            {
                Message = $"Checked for overdue complaints. Found {overdueComplaints.Count}, escalated {escalatedCount}.",
                TotalChecked = overdueComplaints.Count,
                EscalatedCount = escalatedCount,
                Timestamp = DateTime.Now
            });
        }

        // MANUAL ESCALATION
        [HttpPost]
        [Route("manual")]
        public IHttpActionResult ManualEscalation([FromBody] ManualEscalationRequest request)
        {
            if (request == null)
                return BadRequest("Escalation data required.");

            var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == request.ComplaintId);
            if (complaint == null)
                return NotFound("Complaint not found.");

            if (complaint.EscalationLevel >= 3)
                return BadRequest("Complaint already at maximum escalation level (3).");

            var oldLevel = complaint.EscalationLevel;
            complaint.EscalationLevel++;

            // ✅ REMOVED: complaint.EscalationDate = DateTime.Now; - Property doesn't exist

            complaint.UpdatedAt = DateTime.Now;

            // Create escalation record
            var escalation = new Escalation
            {
                EscalationId = Guid.NewGuid(),
                ComplaintId = complaint.ComplaintId,
                EscalationLevel = complaint.EscalationLevel,
                EscalatedFromId = request.EscalatedFromId,
                EscalatedToId = request.EscalatedToId,
                EscalatedById = request.EscalatedById,
                EscalationReason = request.EscalationReason,
                HoursElapsed = (decimal)(DateTime.Now - complaint.CreatedAt).TotalHours,
                EscalationNotes = request.EscalationNotes,
                EscalatedAt = DateTime.Now  // ✅ This is correct
            };

            db.Escalations.Add(escalation);

            // Add to status history
            db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
            {
                HistoryId = Guid.NewGuid(),
                ComplaintId = complaint.ComplaintId,
                PreviousStatus = complaint.CurrentStatus.ToString(),
                NewStatus = complaint.CurrentStatus.ToString(),
                ChangedById = request.EscalatedById,
                ChangedAt = DateTime.Now,
                Notes = $"Manually escalated from Level {oldLevel} to Level {complaint.EscalationLevel}. Reason: {request.EscalationReason}"
            });

            db.SaveChanges();

            return Ok(new
            {
                Message = $"Complaint escalated to Level {complaint.EscalationLevel}",
                ComplaintId = complaint.ComplaintId,
                NewEscalationLevel = complaint.EscalationLevel,
                EscalationId = escalation.EscalationId
            });
        }

        // GET ALL ESCALATIONS
        [HttpGet]
        [Route("")]
        public IHttpActionResult GetAllEscalations(
            [FromUri] int page = 1,
            [FromUri] int pageSize = 20,
            [FromUri] int? level = null)
        {
            var query = db.Escalations
                .Include(e => e.Complaint)
                .Include(e => e.EscalatedBy)
                .OrderByDescending(e => e.EscalatedAt);

            if (level.HasValue)
            {
                query = query.Where(e => e.EscalationLevel == level.Value).OrderByDescending(e => e.EscalatedAt);
            }

            var total = query.Count();
            var escalations = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList()
                .Select(e => new
                {
                    e.EscalationId,
                    e.ComplaintId,
                    Complaint = new
                    {
                        e.Complaint.ComplaintId,
                        e.Complaint.Title,
                        e.Complaint.CurrentStatus
                    },
                    e.EscalationLevel,
                    e.EscalationReason,
                    e.HoursElapsed,
                    e.EscalationNotes,
                    EscalatedBy = e.EscalatedBy != null ? new { e.EscalatedBy.FullName } : null,
                    e.EscalatedAt,
                    e.Resolved,
                    e.ResolvedAt
                })
                .ToList();

            return Ok(new
            {
                Data = escalations,
                Pagination = new
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)total / pageSize),
                    TotalItems = total
                }
            });
        }

        // GET ESCALATIONS BY COMPLAINT
        [HttpGet]
        [Route("complaint/{complaintId:guid}")]
        public IHttpActionResult GetEscalationsByComplaint(Guid complaintId)
        {
            var escalations = db.Escalations
                .Where(e => e.ComplaintId == complaintId)
                .OrderByDescending(e => e.EscalatedAt)
                .ToList()
                .Select(e => new
                {
                    e.EscalationId,
                    e.EscalationLevel,
                    e.EscalationReason,
                    e.HoursElapsed,
                    e.EscalationNotes,
                    e.EscalatedAt,
                    e.Resolved,
                    e.ResolvedAt
                })
                .ToList();

            return Ok(escalations);
        }

        // MARK ESCALATION AS RESOLVED
        [HttpPost]
        [Route("{escalationId:guid}/resolve")]
        public IHttpActionResult ResolveEscalation(Guid escalationId, [FromUri] Guid resolvedById)
        {
            var escalation = db.Escalations.FirstOrDefault(e => e.EscalationId == escalationId);
            if (escalation == null)
                return NotFound("Escalation not found.");

            if (escalation.Resolved)
                return BadRequest("Escalation already resolved.");

            escalation.Resolved = true;
            escalation.ResolvedAt = DateTime.Now;

            // Also update complaint escalation level if complaint is now resolved
            var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == escalation.ComplaintId);
            if (complaint != null && complaint.CurrentStatus == ComplaintStatus.Resolved)
            {
                complaint.EscalationLevel = 0; // Reset escalation when resolved
            }

            db.SaveChanges();

            return Ok(new
            {
                Message = "Escalation marked as resolved",
                EscalationId = escalationId,
                ResolvedAt = escalation.ResolvedAt
            });
        }

        private IHttpActionResult NotFound(string message)
        {
            return Content(HttpStatusCode.NotFound, new { error = message });
        }

        // GET STATISTICS
        [HttpGet]
        [Route("stats")]
        public IHttpActionResult GetEscalationStats()
        {
            var totalEscalations = db.Escalations.Count();
            var resolvedEscalations = db.Escalations.Count(e => e.Resolved);
            var pendingEscalations = totalEscalations - resolvedEscalations;

            var byLevel = db.Escalations
                .GroupBy(e => e.EscalationLevel)
                .Select(g => new
                {
                    Level = g.Key,
                    Count = g.Count(),
                    Resolved = g.Count(e => e.Resolved)
                })
                .OrderBy(g => g.Level)
                .ToList();

            var byReason = db.Escalations
                .GroupBy(e => e.EscalationReason)
                .Select(g => new
                {
                    Reason = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(g => g.Count)
                .ToList();

            return Ok(new
            {
                Total = totalEscalations,
                Resolved = resolvedEscalations,
                Pending = pendingEscalations,
                ResolutionRate = totalEscalations > 0 ? (double)resolvedEscalations / totalEscalations * 100 : 0,
                ByLevel = byLevel,
                ByReason = byReason
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }

    // DTO for manual escalation request
    public class ManualEscalationRequest
    {
        public Guid ComplaintId { get; set; }
        public Guid EscalatedFromId { get; set; }
        public Guid EscalatedToId { get; set; }
        public Guid EscalatedById { get; set; }
        public string EscalationReason { get; set; }
        public string EscalationNotes { get; set; }
    }
}