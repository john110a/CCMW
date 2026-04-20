using CCMW.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/assignments")]
    public class AssignmentController : ApiController
    {
        private readonly CCMWDbContext db = new CCMWDbContext();

        // Helper method for NotFound with message
        private IHttpActionResult NotFound(string message)
        {
            return Content(HttpStatusCode.NotFound, new { error = message });
        }

        // =====================================================
        // ASSIGN COMPLAINT - FIXED VERSION
        // =====================================================
        [HttpPost]
        [Route("assign")]
        public IHttpActionResult AssignComplaint([FromBody] AssignmentRequest request)
        {
            try
            {
                // Validate request
                if (request == null)
                    return BadRequest("Assignment data is required.");

                if (request.ComplaintId == Guid.Empty)
                    return BadRequest("Complaint ID is required.");

                if (request.AssignedToId == Guid.Empty)
                    return BadRequest("Staff ID is required.");

                // Get complaint
                var complaint = db.Complaints
                    .Include(c => c.Assignments)
                    .FirstOrDefault(c => c.ComplaintId == request.ComplaintId);

                if (complaint == null)
                    return NotFound("Complaint not found.");

                // Get staff
                var staff = db.StaffProfiles
                    .Include(s => s.User)
                    .FirstOrDefault(s => s.StaffId == request.AssignedToId);

                if (staff == null)
                    return NotFound("Staff not found.");

                // Check if staff is available
                if (!staff.IsAvailable)
                    return BadRequest("Staff is not available for assignment.");

                // Check if complaint is already assigned
                if (complaint.AssignedToId != null)
                    return BadRequest("Complaint is already assigned to another staff member.");

                // Check if complaint is approved (can only assign approved complaints)
                if (complaint.CurrentStatus != ComplaintStatus.Approved)
                    return BadRequest($"Complaint status must be 'Approved' to assign. Current status: {complaint.CurrentStatus}");

                // Update complaint
                var oldStatus = complaint.CurrentStatus;
                complaint.AssignedToId = staff.StaffId;
                complaint.AssignedAt = DateTime.Now;
                complaint.CurrentStatus = ComplaintStatus.Assigned;
                complaint.StatusUpdatedAt = DateTime.Now;

                // Create assignment record
                var assignment = new ComplaintAssignment
                {
                    AssignmentId = Guid.NewGuid(),
                    ComplaintId = complaint.ComplaintId,
                    AssignedToId = staff.StaffId,
                    AssignedById = request.AssignedById ?? Guid.Empty,
                    AssignedAt = DateTime.Now,
                    AssignmentType = "Manual",
                    AssignmentNotes = request.AssignmentNotes ?? "Assigned by admin",
                    ExpectedCompletionDate = request.ExpectedCompletionDate ?? DateTime.Now.AddDays(3),
                    IsActive = true
                };

                db.ComplaintAssignments.Add(assignment);

                // Update staff stats
                staff.TotalAssignments += 1;
                staff.PendingAssignments += 1;

                // Add status history
                db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
                {
                    HistoryId = Guid.NewGuid(),
                    ComplaintId = complaint.ComplaintId,
                    PreviousStatus = oldStatus.ToString(),
                    NewStatus = ComplaintStatus.Assigned.ToString(),
                    ChangedById = request.AssignedById ?? Guid.Empty,
                    ChangedAt = DateTime.Now,
                    Notes = $"Assigned to {staff.User?.FullName ?? staff.EmployeeId}"
                });

                db.SaveChanges();

                return Ok(new
                {
                    message = "Complaint assigned successfully",
                    assignmentId = assignment.AssignmentId,
                    complaintId = complaint.ComplaintId,
                    assignedTo = staff.User?.FullName ?? staff.EmployeeId,
                    assignedAt = assignment.AssignedAt
                });
            }
            catch (Exception)
            {
                return InternalServerError();
            }
        }

        // =====================================================
        // REASSIGN COMPLAINT - FIXED VERSION
        // =====================================================
        [HttpPost]
        [Route("reassign")]
        public IHttpActionResult ReassignComplaint([FromBody] AssignmentRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest("Assignment data is required.");

                var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == request.ComplaintId);
                if (complaint == null)
                    return NotFound("Complaint not found.");

                var newStaff = db.StaffProfiles
                    .Include(s => s.User)
                    .FirstOrDefault(s => s.StaffId == request.AssignedToId);

                if (newStaff == null)
                    return NotFound("Staff not found.");

                if (!newStaff.IsAvailable)
                    return BadRequest("Staff is not available for assignment.");

                // Deactivate old assignments
                var oldAssignments = db.ComplaintAssignments
                    .Where(a => a.ComplaintId == complaint.ComplaintId && a.IsActive)
                    .ToList();

                foreach (var old in oldAssignments)
                {
                    old.IsActive = false;

                    // Update old staff's pending count
                    if (old.AssignedToId != null)
                    {
                        var oldStaff = db.StaffProfiles.FirstOrDefault(s => s.StaffId == old.AssignedToId);
                        if (oldStaff != null)
                            oldStaff.PendingAssignments = Math.Max(oldStaff.PendingAssignments - 1, 0);
                    }
                }

                // Update complaint
                var oldStatus = complaint.CurrentStatus;
                complaint.AssignedToId = newStaff.StaffId;
                complaint.AssignedAt = DateTime.Now;
                complaint.CurrentStatus = ComplaintStatus.Assigned;
                complaint.StatusUpdatedAt = DateTime.Now;

                // Create new assignment
                var assignment = new ComplaintAssignment
                {
                    AssignmentId = Guid.NewGuid(),
                    ComplaintId = complaint.ComplaintId,
                    AssignedToId = newStaff.StaffId,
                    AssignedById = request.AssignedById ?? Guid.Empty,
                    AssignedAt = DateTime.Now,
                    AssignmentType = "Reassignment",
                    AssignmentNotes = request.AssignmentNotes ?? "Reassigned by admin",
                    ExpectedCompletionDate = request.ExpectedCompletionDate ?? DateTime.Now.AddDays(3),
                    IsActive = true
                };

                db.ComplaintAssignments.Add(assignment);

                // Update new staff stats
                newStaff.TotalAssignments += 1;
                newStaff.PendingAssignments += 1;

                // Add status history
                db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
                {
                    HistoryId = Guid.NewGuid(),
                    ComplaintId = complaint.ComplaintId,
                    PreviousStatus = oldStatus.ToString(),
                    NewStatus = ComplaintStatus.Assigned.ToString(),
                    ChangedById = request.AssignedById ?? Guid.Empty,
                    ChangedAt = DateTime.Now,
                    Notes = $"Reassigned to {newStaff.User?.FullName ?? newStaff.EmployeeId}"
                });

                db.SaveChanges();

                return Ok(new
                {
                    message = "Complaint reassigned successfully",
                    assignmentId = assignment.AssignmentId,
                    complaintId = complaint.ComplaintId,
                    assignedTo = newStaff.User?.FullName ?? newStaff.EmployeeId
                });
            }
            catch (Exception)
            {
                return InternalServerError();
            }
        }

        // =====================================================
        // GET ASSIGNMENTS BY STAFF
        // =====================================================
        [HttpGet]
        [Route("staff/{staffId}")]
        public IHttpActionResult GetAssignmentsByStaff(Guid staffId)
        {
            try
            {
                var assignments = db.ComplaintAssignments
                    .Include(a => a.Complaint)
                    .Where(a => a.AssignedToId == staffId && a.IsActive)
                    .OrderByDescending(a => a.AssignedAt)
                    .Select(a => new
                    {
                        a.AssignmentId,
                        a.ComplaintId,
                        ComplaintTitle = a.Complaint.Title,
                        ComplaintNumber = a.Complaint.ComplaintNumber,
                        a.AssignmentType,
                        a.AssignmentNotes,
                        a.AssignedAt,
                        a.ExpectedCompletionDate,
                        a.AcceptedAt,
                        a.StartedAt,
                        a.CompletedAt,
                        IsOverdue = a.ExpectedCompletionDate.HasValue && a.ExpectedCompletionDate.Value < DateTime.Now && a.CompletedAt == null,
                        Status = a.CompletedAt != null ? "Completed" :
                                 a.StartedAt != null ? "InProgress" :
                                 a.AcceptedAt != null ? "Accepted" : "Assigned"
                    }).ToList();

                return Ok(assignments);
            }
            catch (Exception)
            {
                return InternalServerError();
            }
        }

        // =====================================================
        // GET COMPLAINT ASSIGNMENT HISTORY
        // =====================================================
        [HttpGet]
        [Route("complaint/{complaintId}/history")]
        public IHttpActionResult GetComplaintAssignmentHistory(Guid complaintId)
        {
            try
            {
                var assignments = db.ComplaintAssignments
                    .Include(a => a.AssignedTo)
                    .Include(a => a.AssignedTo.User)
                    .Where(a => a.ComplaintId == complaintId)
                    .OrderByDescending(a => a.AssignedAt)
                    .Select(a => new
                    {
                        a.AssignmentId,
                        a.AssignedToId,
                        AssignedToName = a.AssignedTo.User.FullName,
                        a.AssignedById,
                        a.AssignmentType,
                        a.AssignmentNotes,
                        a.AssignedAt,
                        a.ExpectedCompletionDate,
                        a.AcceptedAt,
                        a.StartedAt,
                        a.CompletedAt,
                        a.IsActive
                    }).ToList();

                return Ok(assignments);
            }
            catch (Exception)
            {
                return InternalServerError();
            }
        }

        // =====================================================
        // UPDATE ASSIGNMENT STATUS
        // =====================================================
        [HttpPost]
        [Route("update-status")]
        public IHttpActionResult UpdateAssignmentStatus([FromBody] AssignmentStatusUpdate request)
        {
            try
            {
                var assignment = db.ComplaintAssignments
                    .Include(a => a.Complaint)
                    .FirstOrDefault(a => a.AssignmentId == request.AssignmentId && a.IsActive);

                if (assignment == null)
                    return NotFound("Assignment not found.");

                if (request.Status.Equals("Accepted", StringComparison.OrdinalIgnoreCase))
                {
                    if (assignment.AcceptedAt != null)
                        return BadRequest("Assignment already accepted.");
                    assignment.AcceptedAt = DateTime.Now;
                }
                else if (request.Status.Equals("Started", StringComparison.OrdinalIgnoreCase))
                {
                    if (assignment.StartedAt != null)
                        return BadRequest("Work already started.");
                    assignment.StartedAt = DateTime.Now;
                }
                else if (request.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
                {
                    if (assignment.CompletedAt != null)
                        return BadRequest("Assignment already completed.");

                    assignment.CompletedAt = DateTime.Now;
                    assignment.IsActive = false;

                    // Update complaint status
                    var complaint = assignment.Complaint;
                    if (complaint != null)
                    {
                        var oldStatus = complaint.CurrentStatus;
                        complaint.CurrentStatus = ComplaintStatus.Resolved;
                        complaint.ResolvedAt = DateTime.Now;
                        complaint.StatusUpdatedAt = DateTime.Now;

                        // Add status history
                        db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
                        {
                            HistoryId = Guid.NewGuid(),
                            ComplaintId = complaint.ComplaintId,
                            PreviousStatus = oldStatus.ToString(),
                            NewStatus = ComplaintStatus.Resolved.ToString(),
                            ChangedById = assignment.AssignedToId ?? Guid.Empty,
                            ChangedAt = DateTime.Now,
                            Notes = "Complaint resolved by staff"
                        });
                    }

                    // Update staff stats
                    if (assignment.AssignedToId != null)
                    {
                        var staff = db.StaffProfiles.FirstOrDefault(s => s.StaffId == assignment.AssignedToId);
                        if (staff != null)
                        {
                            staff.CompletedAssignments += 1;
                            staff.PendingAssignments = Math.Max(staff.PendingAssignments - 1, 0);
                        }
                    }
                }
                else
                {
                    return BadRequest($"Invalid status: {request.Status}. Valid values: Accepted, Started, Completed");
                }

                db.SaveChanges();
                return Ok(new { message = $"Assignment status updated to {request.Status} successfully" });
            }
            catch (Exception)
            {
                return InternalServerError();
            }
        }

        // =====================================================
        // GET ASSIGNMENT STATS
        // =====================================================
        [HttpGet]
        [Route("stats")]
        public IHttpActionResult GetAssignmentStats()
        {
            try
            {
                var today = DateTime.Today;
                var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
                var startOfMonth = new DateTime(today.Year, today.Month, 1);

                var stats = new
                {
                    TotalAssignments = db.ComplaintAssignments.Count(),
                    PendingAssignments = db.ComplaintAssignments.Count(a => a.CompletedAt == null && a.IsActive),
                    CompletedToday = db.ComplaintAssignments.Count(a =>
                        a.CompletedAt.HasValue && a.CompletedAt.Value.Date == today),
                    CompletedThisWeek = db.ComplaintAssignments.Count(a =>
                        a.CompletedAt.HasValue && a.CompletedAt.Value >= startOfWeek),
                    CompletedThisMonth = db.ComplaintAssignments.Count(a =>
                        a.CompletedAt.HasValue && a.CompletedAt.Value >= startOfMonth),
                    AverageCompletionTime = db.ComplaintAssignments
                        .Where(a => a.CompletedAt.HasValue)
                        .Average(a => (double?)DbFunctions.DiffHours(a.AssignedAt, a.CompletedAt)) ?? 0
                };
                return Ok(stats);
            }
            catch (Exception)
            {
                return Ok(new
                {
                    TotalAssignments = 0,
                    PendingAssignments = 0,
                    CompletedToday = 0,
                    CompletedThisWeek = 0,
                    CompletedThisMonth = 0,
                    AverageCompletionTime = 0
                });
            }
        }

        // =====================================================
        // GET PENDING COMPLAINTS FOR ASSIGNMENT
        // =====================================================
        [HttpGet]
        [Route("pending")]
        public IHttpActionResult GetPendingComplaints()
        {
            try
            {
                var complaints = db.Complaints
                    .Include(c => c.Category)
                    .Include(c => c.Zone)
                    .Where(c => c.CurrentStatus == ComplaintStatus.Approved &&
                               c.AssignedToId == null)
                    .OrderByDescending(c => c.Priority == "High" ? 1 : c.Priority == "Medium" ? 2 : 3)
                    .ThenBy(c => c.CreatedAt)
                    .Take(50)
                    .Select(c => new
                    {
                        c.ComplaintId,
                        c.ComplaintNumber,
                        c.Title,
                        c.Description,
                        c.LocationAddress,
                        c.Priority,
                        c.UpvoteCount,
                        c.CreatedAt,
                        CategoryName = c.Category != null ? c.Category.CategoryName : "General",
                        ZoneName = c.Zone != null ? c.Zone.ZoneName : "Unknown",
                        DepartmentId = c.DepartmentId
                    })
                    .ToList();

                return Ok(complaints);
            }
            catch (Exception)
            {
                return InternalServerError();
            }
        }

        // =====================================================
        // REJECT COMPLAINT
        // =====================================================
        [HttpPost]
        [Route("~/api/complaints/{id}/reject")]
        public IHttpActionResult RejectComplaint(Guid id, [FromBody] RejectRequest request)
        {
            try
            {
                var complaint = db.Complaints.Find(id);
                if (complaint == null)
                    return NotFound("Complaint not found.");

                if (string.IsNullOrEmpty(request?.Reason))
                    return BadRequest("Rejection reason is required.");

                var oldStatus = complaint.CurrentStatus;
                var oldSubmissionStatus = complaint.SubmissionStatus;

                complaint.CurrentStatus = ComplaintStatus.Rejected;
                complaint.SubmissionStatus = SubmissionStatus.Rejected;
                complaint.RejectionReason = request.Reason;
                complaint.StatusUpdatedAt = DateTime.Now;

                db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
                {
                    HistoryId = Guid.NewGuid(),
                    ComplaintId = id,
                    PreviousStatus = oldStatus.ToString(),
                    NewStatus = ComplaintStatus.Rejected.ToString(),
                    ChangeReason = request.Reason,
                    ChangedAt = DateTime.Now
                });

                db.SaveChanges();
                return Ok(new { message = "Complaint rejected successfully" });
            }
            catch (Exception)
            {
                return InternalServerError();
            }
        }

        public class RejectRequest
        {
            public string Reason { get; set; }
        }
    }

    public class AssignmentRequest
    {
        public Guid ComplaintId { get; set; }
        public Guid AssignedToId { get; set; }
        public Guid? AssignedById { get; set; }
        public string AssignmentNotes { get; set; }
        public DateTime? ExpectedCompletionDate { get; set; }
    }

    public class AssignmentStatusUpdate
    {
        public Guid AssignmentId { get; set; }
        public string Status { get; set; }
    }
}