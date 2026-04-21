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
        // ASSIGN COMPLAINT - FIXED FOR YOUR ACTUAL MODELS
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
                var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == request.ComplaintId);
                if (complaint == null)
                    return NotFound("Complaint not found.");

                // Get staff with User navigation property
                var staff = db.StaffProfiles
                    .Include(s => s.User)
                    .FirstOrDefault(s => s.StaffId == request.AssignedToId);
                if (staff == null)
                    return NotFound("Staff not found.");

                // Check if complaint is already assigned
                if (complaint.AssignedToId != null && complaint.AssignedToId != Guid.Empty)
                    return BadRequest("Complaint is already assigned to another staff member.");

                // Check if complaint is approved (ComplaintStatus enum)
                if (complaint.CurrentStatus != ComplaintStatus.Approved)
                    return BadRequest($"Complaint status must be 'Approved' to assign. Current status: {complaint.CurrentStatus}");

                // Create assignment record
                var assignment = new ComplaintAssignment
                {
                    AssignmentId = Guid.NewGuid(),
                    ComplaintId = complaint.ComplaintId,
                    AssignedToId = staff.StaffId,
                    AssignedById = request.AssignedById,
                    AssignedAt = DateTime.Now,
                    AssignmentType = "Manual",
                    AssignmentNotes = request.AssignmentNotes ?? "Assigned by admin",
                    ExpectedCompletionDate = request.ExpectedCompletionDate ?? DateTime.Now.AddDays(3),
                    IsActive = true
                };

                db.ComplaintAssignments.Add(assignment);

                // Update complaint
                complaint.AssignedToId = staff.StaffId;
                complaint.AssignedAt = DateTime.Now;
                complaint.CurrentStatus = ComplaintStatus.Assigned;
                complaint.StatusUpdatedAt = DateTime.Now;

                // Update staff stats
                staff.TotalAssignments += 1;
                staff.PendingAssignments += 1;

                // Add status history
                var history = new ComplaintStatusHistories
                {
                    HistoryId = Guid.NewGuid(),
                    ComplaintId = complaint.ComplaintId,
                    PreviousStatus = "Approved",
                    NewStatus = "Assigned",
                    ChangedById = request.AssignedById,
                    ChangedAt = DateTime.Now,
                    Notes = $"Assigned to {staff.User?.FullName ?? staff.EmployeeId}"
                };

                db.ComplaintStatusHistories.Add(history);
                db.SaveChanges();

                // Get the staff name for response
                string staffName = staff.User?.FullName ?? staff.EmployeeId ?? "Staff Member";

                return Ok(new
                {
                    success = true,
                    message = "Complaint assigned successfully",
                    assignmentId = assignment.AssignmentId,
                    complaintId = complaint.ComplaintId,
                    complaintNumber = complaint.ComplaintNumber,
                    assignedTo = staffName,
                    assignedAt = assignment.AssignedAt
                });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace?.ToString()
                });
            }
        }

        // =====================================================
        // REJECT COMPLAINT
        // =====================================================
        [HttpPost]
        [Route("reject")]
        public IHttpActionResult RejectComplaint([FromBody] RejectRequest request)
        {
            try
            {
                if (request == null || request.ComplaintId == Guid.Empty)
                    return BadRequest("Complaint ID is required.");

                if (string.IsNullOrEmpty(request.Reason))
                    return BadRequest("Rejection reason is required.");

                var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == request.ComplaintId);
                if (complaint == null)
                    return NotFound("Complaint not found.");

                // Update complaint
                var oldStatus = complaint.CurrentStatus.ToString();
                complaint.CurrentStatus = ComplaintStatus.Rejected;
                complaint.RejectionReason = request.Reason;
                complaint.StatusUpdatedAt = DateTime.Now;

                // Add status history
                var history = new ComplaintStatusHistories
                {
                    HistoryId = Guid.NewGuid(),
                    ComplaintId = complaint.ComplaintId,
                    PreviousStatus = oldStatus,
                    NewStatus = "Rejected",
                    ChangedById = request.RejectedById,
                    ChangedAt = DateTime.Now,
                    ChangeReason = request.Reason,
                    Notes = $"Rejected by admin: {request.Reason}"
                };

                db.ComplaintStatusHistories.Add(history);
                db.SaveChanges();

                return Ok(new
                {
                    success = true,
                    message = "Complaint rejected successfully",
                    complaintId = complaint.ComplaintId,
                    reason = request.Reason
                });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        // =====================================================
        // GET ALL COMPLAINTS FOR ROUTING (SYSTEM ADMIN)
        // =====================================================
        [HttpGet]
        [Route("complaints/all")]
        public IHttpActionResult GetAllComplaintsForRouting()
        {
            try
            {
                var complaints = db.Complaints
                    .Include(c => c.Category)
                    .Include(c => c.Zone)
                    .Where(c => c.CurrentStatus == ComplaintStatus.Approved && c.AssignedToId == null)
                    .OrderByDescending(c => c.Priority == "High" ? 1 : c.Priority == "Medium" ? 2 : 3)
                    .ThenByDescending(c => c.CreatedAt)
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
                        c.DepartmentId,
                        DepartmentName = "", // Department name comes from Department navigation property if available
                        CurrentStatus = (int)c.CurrentStatus,
                        c.AssignedToId,
                        c.AssignedAt,
                        CategoryName = c.Category != null ? c.Category.CategoryName : "General",
                        ZoneName = c.Zone != null ? c.Zone.ZoneName : "Unknown"
                    })
                    .ToList();

                return Ok(complaints);
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
        }

        // =====================================================
        // GET COMPLAINTS BY DEPARTMENT (DEPARTMENT ADMIN)
        // =====================================================
        [HttpGet]
        [Route("complaints/department/{departmentId}")]
        public IHttpActionResult GetComplaintsByDepartment(Guid departmentId)
        {
            try
            {
                var complaints = db.Complaints
                    .Include(c => c.Category)
                    .Include(c => c.Zone)
                    .Where(c => c.DepartmentId == departmentId && c.CurrentStatus == ComplaintStatus.Approved && c.AssignedToId == null)
                    .OrderByDescending(c => c.Priority == "High" ? 1 : c.Priority == "Medium" ? 2 : 3)
                    .ThenByDescending(c => c.CreatedAt)
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
                        c.DepartmentId,
                        DepartmentName = "",
                        CurrentStatus = (int)c.CurrentStatus,
                        c.AssignedToId,
                        c.AssignedAt,
                        CategoryName = c.Category != null ? c.Category.CategoryName : "General",
                        ZoneName = c.Zone != null ? c.Zone.ZoneName : "Unknown"
                    })
                    .ToList();

                return Ok(complaints);
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
        }

        // =====================================================
        // GET AVAILABLE STAFF
        // =====================================================
        [HttpGet]
        [Route("staff/available")]
        public IHttpActionResult GetAvailableStaff([FromUri] string departmentId = null)
        {
            try
            {
                // Use StaffProfiles with User navigation property
                var query = db.StaffProfiles
                    .Include(s => s.User)
                    .Where(s => s.IsAvailable == true);

                if (!string.IsNullOrEmpty(departmentId) && Guid.TryParse(departmentId, out var deptId))
                {
                    query = query.Where(s => s.DepartmentId == deptId);
                }

                var staff = query
                    .Select(s => new
                    {
                        s.StaffId,
                        s.UserId,
                        FullName = s.User != null ? s.User.FullName : s.EmployeeId,
                        Email = s.User != null ? s.User.Email : "",
                        PhoneNumber = s.User != null ? s.User.PhoneNumber : "",
                        s.DepartmentId,
                        DepartmentName = "", // Department name from Department navigation if available
                        s.Role,
                        s.PendingAssignments,
                        s.PerformanceScore,
                        s.IsAvailable
                    })
                    .ToList();

                return Ok(new { TotalAvailable = staff.Count, Staff = staff });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
        }

        // =====================================================
        // GET ASSIGNMENTS BY STAFF
        // =====================================================
        [HttpGet]
        [Route("staff/{staffId}/assignments")]
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
                        ComplaintTitle = a.Complaint != null ? a.Complaint.Title : "N/A",
                        ComplaintNumber = a.Complaint != null ? a.Complaint.ComplaintNumber : "N/A",
                        a.AssignmentType,
                        a.AssignmentNotes,
                        a.AssignedAt,
                        a.ExpectedCompletionDate,
                        a.AcceptedAt,
                        a.StartedAt,
                        a.CompletedAt,
                        IsOverdue = a.ExpectedCompletionDate.HasValue && a.ExpectedCompletionDate.Value < DateTime.Now && !a.CompletedAt.HasValue,
                        Status = a.CompletedAt.HasValue ? "Completed" :
                                 a.StartedAt.HasValue ? "In Progress" :
                                 a.AcceptedAt.HasValue ? "Accepted" : "Assigned"
                    }).ToList();

                return Ok(assignments);
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { error = ex.Message });
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
                    var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == assignment.ComplaintId);
                    if (complaint != null)
                    {
                        complaint.CurrentStatus = ComplaintStatus.Resolved;
                        complaint.ResolvedAt = DateTime.Now;
                        complaint.StatusUpdatedAt = DateTime.Now;

                        // Add status history
                        var history = new ComplaintStatusHistories
                        {
                            HistoryId = Guid.NewGuid(),
                            ComplaintId = complaint.ComplaintId,
                            PreviousStatus = "Assigned",
                            NewStatus = "Resolved",
                            ChangedById = assignment.AssignedById,
                            ChangedAt = DateTime.Now,
                            Notes = "Complaint resolved by staff"
                        };
                        db.ComplaintStatusHistories.Add(history);
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
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
        }

        // =====================================================
        // GET PENDING COMPLAINTS COUNT
        // =====================================================
        [HttpGet]
        [Route("pending-count")]
        public IHttpActionResult GetPendingComplaintsCount()
        {
            try
            {
                var count = db.Complaints
                    .Count(c => c.CurrentStatus == ComplaintStatus.Approved && c.AssignedToId == null);
                return Ok(new { pendingCount = count });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { error = ex.Message });
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
    // DTO CLASSES
    // =====================================================

    public class AssignmentRequest
    {
        public Guid ComplaintId { get; set; }
        public Guid AssignedToId { get; set; }
        public Guid? AssignedById { get; set; }
        public string AssignmentNotes { get; set; }
        public DateTime? ExpectedCompletionDate { get; set; }
    }

    public class RejectRequest
    {
        public Guid ComplaintId { get; set; }
        public string Reason { get; set; }
        public Guid? RejectedById { get; set; }
    }

    public class AssignmentStatusUpdate
    {
        public Guid AssignmentId { get; set; }
        public string Status { get; set; }
    }
}