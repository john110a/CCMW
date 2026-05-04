using CCMW.Models;
using System;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Linq;
using System.Net;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/assignments")]
    public class AssignmentController : ApiController
    {
        private readonly CCMWDbContext db = new CCMWDbContext();

        private IHttpActionResult NotFoundMessage(string message)
        {
            return Content(HttpStatusCode.NotFound, new { error = message });
        }

        // =====================================================
        // 1. ASSIGNMENT OPERATIONS
        // =====================================================

        /// Assign complaint to staff
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

                if (request.AssignedById == null || request.AssignedById == Guid.Empty)
                    return BadRequest("AssignedById (admin ID) is required.");

                // Get complaint
                var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == request.ComplaintId);
                if (complaint == null)
                    return NotFoundMessage("Complaint not found.");

                // Get the admin user to check their role
                var adminUser = db.Users.Find(request.AssignedById);
                bool isSystemAdmin = adminUser?.UserType == "System_Admin";

                // AUTO-APPROVE if System Admin is assigning and complaint is not yet approved
                if (isSystemAdmin && (int)complaint.SubmissionStatus != 1)
                {
                    complaint.SubmissionStatus = (SubmissionStatus)1;
                    complaint.CurrentStatus = ComplaintStatus.Approved;
                    complaint.ApprovedById = request.AssignedById;
                    complaint.StatusUpdatedAt = DateTime.Now;

                    // Add approval history
                    var approvalHistory = new ComplaintStatusHistories
                    {
                        HistoryId = Guid.NewGuid(),
                        ComplaintId = complaint.ComplaintId,
                        PreviousStatus = "Submitted",
                        NewStatus = "Approved",
                        ChangedById = request.AssignedById.Value,
                        ChangedAt = DateTime.Now,
                        Notes = "Auto-approved during assignment by System Admin"
                    };
                    db.ComplaintStatusHistories.Add(approvalHistory);
                    db.SaveChanges();
                }

                // Now check if complaint is approved (SubmissionStatus == 1)
                if ((int)complaint.SubmissionStatus != 1)
                {
                    return BadRequest($"Complaint must be approved before assignment. Current submission status: {(int)complaint.SubmissionStatus}");
                }

                // Check if complaint is already assigned
                if (complaint.AssignedToId != null && complaint.AssignedToId != Guid.Empty)
                    return BadRequest("Complaint is already assigned to another staff member.");

                // Get staff
                var staff = db.StaffProfiles.FirstOrDefault(s => s.StaffId == request.AssignedToId);
                if (staff == null)
                    return NotFoundMessage($"Staff not found with ID: {request.AssignedToId}");

                // Check if staff is Field_Staff
                if (staff.Role != "Field_Staff")
                    return BadRequest($"Staff {staff.EmployeeId} is not a Field Staff. Role: {staff.Role}");

                // Check if staff is available
                if (!staff.IsAvailable)
                    return BadRequest($"Staff {staff.EmployeeId} is not available for assignment.");

                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        // Create assignment record
                        var assignment = new ComplaintAssignment
                        {
                            AssignmentId = Guid.NewGuid(),
                            ComplaintId = complaint.ComplaintId,
                            AssignedToId = staff.StaffId,
                            AssignedById = request.AssignedById.Value,
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
                            ChangedById = request.AssignedById.Value,
                            ChangedAt = DateTime.Now,
                            Notes = $"Assigned to {staff.EmployeeId}"
                        };
                        db.ComplaintStatusHistories.Add(history);

                        db.SaveChanges();
                        transaction.Commit();

                        return Ok(new
                        {
                            success = true,
                            message = "Complaint assigned successfully",
                            assignmentId = assignment.AssignmentId,
                            complaintId = complaint.ComplaintId,
                            complaintNumber = complaint.ComplaintNumber,
                            assignedTo = staff.EmployeeId,
                            assignedAt = assignment.AssignedAt,
                            autoApproved = isSystemAdmin && (int)complaint.SubmissionStatus == 1
                        });
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
        }

        /// Reassign complaint to different staff
        [HttpPost]
        [Route("reassign")]
        public IHttpActionResult ReassignComplaint([FromBody] ReassignmentRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest("Reassignment data is required.");

                // Get current assignment
                var currentAssignment = db.ComplaintAssignments
                    .FirstOrDefault(a => a.ComplaintId == request.ComplaintId && a.IsActive);

                if (currentAssignment != null)
                {
                    // Mark current assignment as inactive
                    currentAssignment.IsActive = false;
                }

                // Get the complaint
                var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == request.ComplaintId);
                if (complaint == null)
                    return NotFoundMessage("Complaint not found.");

                // Get new staff
                var newStaff = db.StaffProfiles.FirstOrDefault(s => s.StaffId == request.NewStaffId);
                if (newStaff == null)
                    return NotFoundMessage($"Staff not found with ID: {request.NewStaffId}");

                // Check if new staff is available
                if (!newStaff.IsAvailable)
                    return BadRequest($"Staff {newStaff.EmployeeId} is not available for assignment.");

                // Update staff stats (decrement old, increment new)
                if (currentAssignment != null && currentAssignment.AssignedToId.HasValue)
                {
                    var oldStaff = db.StaffProfiles.Find(currentAssignment.AssignedToId.Value);
                    if (oldStaff != null && oldStaff.PendingAssignments > 0)
                        oldStaff.PendingAssignments -= 1;
                }

                newStaff.PendingAssignments += 1;

                // Create new assignment
                var newAssignment = new ComplaintAssignment
                {
                    AssignmentId = Guid.NewGuid(),
                    ComplaintId = request.ComplaintId,
                    AssignedToId = newStaff.StaffId,
                    AssignedById = request.AssignedById,
                    AssignedAt = DateTime.Now,
                    AssignmentType = "Reassignment",
                    AssignmentNotes = request.Notes ?? "Reassigned by admin",
                    ExpectedCompletionDate = request.ExpectedCompletionDate ?? DateTime.Now.AddDays(3),
                    IsActive = true
                };
                db.ComplaintAssignments.Add(newAssignment);

                // Update complaint
                complaint.AssignedToId = newStaff.StaffId;
                complaint.AssignedAt = DateTime.Now;

                // Add status history
                var history = new ComplaintStatusHistories
                {
                    HistoryId = Guid.NewGuid(),
                    ComplaintId = complaint.ComplaintId,
                    PreviousStatus = "Assigned",
                    NewStatus = "Reassigned",
                    ChangedById = request.AssignedById,
                    ChangedAt = DateTime.Now,
                    Notes = $"Reassigned to {newStaff.EmployeeId}"
                };
                db.ComplaintStatusHistories.Add(history);

                db.SaveChanges();

                return Ok(new
                {
                    success = true,
                    message = "Complaint reassigned successfully",
                    assignmentId = newAssignment.AssignmentId,
                    assignedTo = newStaff.EmployeeId
                });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
        }

        /// Update assignment status (using AcceptedAt, StartedAt, CompletedAt)
        [HttpPost]
        [Route("update-status")]
        public IHttpActionResult UpdateAssignmentStatus([FromBody] AssignmentStatusUpdate request)
        {
            try
            {
                var assignment = db.ComplaintAssignments
                    .FirstOrDefault(a => a.AssignmentId == request.AssignmentId);

                if (assignment == null)
                    return NotFoundMessage("Assignment not found.");

                // Update the appropriate date field based on status
                switch (request.Status?.ToLower())
                {
                    case "accepted":
                        assignment.AcceptedAt = DateTime.Now;
                        break;
                    case "started":
                        assignment.StartedAt = DateTime.Now;
                        break;
                    case "completed":
                        assignment.CompletedAt = DateTime.Now;

                        // Update staff stats
                        if (assignment.AssignedToId.HasValue)
                        {
                            var staff = db.StaffProfiles.Find(assignment.AssignedToId.Value);
                            if (staff != null && staff.PendingAssignments > 0)
                            {
                                staff.PendingAssignments -= 1;
                                staff.CompletedAssignments += 1;
                            }
                        }

                        // Update complaint status
                        var complaint = db.Complaints.Find(assignment.ComplaintId);
                        if (complaint != null)
                        {
                            complaint.CurrentStatus = ComplaintStatus.Resolved;
                            complaint.ResolvedAt = DateTime.Now;
                        }
                        break;
                }

                db.SaveChanges();

                return Ok(new
                {
                    success = true,
                    message = $"Assignment status updated to {request.Status}",
                    assignmentId = assignment.AssignmentId,
                    status = request.Status
                });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
        }

        // =====================================================
        // 2. GET ASSIGNMENTS
        // =====================================================

        /// Get assignments by staff ID
        [HttpGet]
        [Route("staff/{staffId:guid}")]
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
                        ComplaintNumber = a.Complaint.ComplaintNumber,
                        ComplaintTitle = a.Complaint.Title,
                        a.AssignedAt,
                        a.ExpectedCompletionDate,
                        a.CompletedAt,
                        a.AcceptedAt,
                        a.StartedAt,
                        a.AssignmentNotes,
                        ComplaintStatus = (int)a.Complaint.CurrentStatus,
                        Status = a.CompletedAt.HasValue ? "Completed" :
                                 a.StartedAt.HasValue ? "Started" :
                                 a.AcceptedAt.HasValue ? "Accepted" : "Assigned"
                    })
                    .ToList();

                return Ok(assignments);
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
        }

        /// Get complaint assignment history
        [HttpGet]
        [Route("complaint/{complaintId:guid}/history")]
        public IHttpActionResult GetAssignmentHistory(Guid complaintId)
        {
            try
            {
                var assignments = db.ComplaintAssignments
                    .Include(a => a.AssignedTo)
                    .Include(a => a.AssignedBy)
                    .Where(a => a.ComplaintId == complaintId)
                    .OrderByDescending(a => a.AssignedAt)
                    .Select(a => new
                    {
                        a.AssignmentId,
                        AssignedTo = a.AssignedTo != null ? a.AssignedTo.EmployeeId : "Unknown",
                        AssignedBy = a.AssignedBy != null ? a.AssignedBy.FullName : "System",
                        a.AssignedAt,
                        a.ExpectedCompletionDate,
                        a.CompletedAt,
                        a.AcceptedAt,
                        a.StartedAt,
                        a.AssignmentNotes,
                        IsActive = a.IsActive,
                        Status = a.CompletedAt.HasValue ? "Completed" :
                                 a.StartedAt.HasValue ? "Started" :
                                 a.AcceptedAt.HasValue ? "Accepted" : "Assigned"
                    })
                    .ToList();

                return Ok(assignments);
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
        }

        /// Get assignment statistics
        [HttpGet]
        [Route("stats")]
        public IHttpActionResult GetAssignmentStats()
        {
            try
            {
                var totalAssignments = db.ComplaintAssignments.Count();
                var pendingAssignments = db.ComplaintAssignments
                    .Count(a => a.IsActive && !a.AcceptedAt.HasValue);
                var completedToday = db.ComplaintAssignments
                    .Count(a => a.CompletedAt.HasValue && a.CompletedAt.Value.Date == DateTime.Now.Date);
                var completedThisWeek = db.ComplaintAssignments
                    .Count(a => a.CompletedAt.HasValue && a.CompletedAt.Value.Date >= DateTime.Now.AddDays(-7).Date);
                var completedThisMonth = db.ComplaintAssignments
                    .Count(a => a.CompletedAt.HasValue && a.CompletedAt.Value.Month == DateTime.Now.Month);

                double avgCompletionTime = 0;
                var completedAssignments = db.ComplaintAssignments
                    .Where(a => a.CompletedAt.HasValue && a.AssignedAt != null)
                    .ToList();

                if (completedAssignments.Any())
                {
                    avgCompletionTime = completedAssignments
                        .Average(a => (a.CompletedAt.Value - a.AssignedAt).TotalHours);
                }

                var stats = new
                {
                    TotalAssignments = totalAssignments,
                    PendingAssignments = pendingAssignments,
                    CompletedToday = completedToday,
                    CompletedThisWeek = completedThisWeek,
                    CompletedThisMonth = completedThisMonth,
                    AverageCompletionTime = Math.Round(avgCompletionTime, 1)
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
        }

        // =====================================================
        // 3. GET COMPLAINTS (FIXED - WITH DEPARTMENT NAMES)
        // =====================================================

        /// Get pending complaints for approval
        [HttpGet]
        [Route("pending")]
        public IHttpActionResult GetPendingComplaints()
        {
            try
            {
                var complaints = db.Complaints
                    .Where(c => (int)c.SubmissionStatus == 0)
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => new
                    {
                        c.ComplaintId,
                        c.ComplaintNumber,
                        c.Title,
                        c.Description,
                        c.LocationAddress,
                        c.Priority,
                        c.CreatedAt,
                        c.DepartmentId,
                        DepartmentName = c.Department != null ? c.Department.DepartmentName : "Unknown",
                        c.ZoneId,
                        ZoneName = c.Zone != null ? c.Zone.ZoneName : "Unknown",
                        c.CategoryId,
                        CategoryName = c.Category != null ? c.Category.CategoryName : "General",
                        c.UpvoteCount,
                        SubmissionStatus = (int)c.SubmissionStatus,
                        CurrentStatus = (int)c.CurrentStatus,
                        AssignedToId = c.AssignedToId
                    })
                    .ToList();

                return Ok(complaints);
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
        }

        /// Get all complaints ready for assignment (Approved & Unassigned) - FIXED
        [HttpGet]
        [Route("complaints/all")]
        public IHttpActionResult GetAllComplaintsForRouting()
        {
            try
            {
                var complaints = db.Complaints
                    .Where(c => (int)c.SubmissionStatus == 1 && c.AssignedToId == null)
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
                        DepartmentName = c.Department != null ? c.Department.DepartmentName : "Unknown",
                        ZoneName = c.Zone != null ? c.Zone.ZoneName : "Unknown",
                        CategoryName = c.Category != null ? c.Category.CategoryName : "General",
                        SubmissionStatus = (int)c.SubmissionStatus,
                        CurrentStatus = (int)c.CurrentStatus,
                        AssignedToId = c.AssignedToId
                    })
                    .ToList();

                return Ok(complaints);
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
        }

        /// Get complaints by department for routing - FIXED
        [HttpGet]
        [Route("complaints/department/{departmentId:guid}")]
        public IHttpActionResult GetComplaintsByDepartmentForRouting(Guid departmentId)
        {
            try
            {
                var complaints = db.Complaints
                    .Where(c => c.DepartmentId == departmentId && (int)c.SubmissionStatus == 1 && c.AssignedToId == null)
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
                        DepartmentName = c.Department != null ? c.Department.DepartmentName : "Unknown",
                        ZoneName = c.Zone != null ? c.Zone.ZoneName : "Unknown",
                        CategoryName = c.Category != null ? c.Category.CategoryName : "General",
                        SubmissionStatus = (int)c.SubmissionStatus,
                        CurrentStatus = (int)c.CurrentStatus,
                        AssignedToId = c.AssignedToId
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
        // 4. STAFF MANAGEMENT
        // =====================================================

        /// Get available staff for assignment
        [HttpGet]
        [Route("staff/available")]
        public IHttpActionResult GetAvailableStaff([FromUri] string departmentId = null)
        {
            try
            {
                var query = db.StaffProfiles
                    .Where(s => s.Role == "Field_Staff" && s.IsAvailable == true);

                if (!string.IsNullOrEmpty(departmentId) && Guid.TryParse(departmentId, out var deptId))
                {
                    query = query.Where(s => s.DepartmentId == deptId);
                }

                var staffList = query.Select(s => new
                {
                    s.StaffId,
                    s.UserId,
                    FullName = s.EmployeeId,
                    s.DepartmentId,
                    DepartmentName = s.Department != null ? s.Department.DepartmentName : "Unknown",
                    s.Role,
                    s.PendingAssignments,
                    s.PerformanceScore,
                    s.IsAvailable
                }).ToList();

                return Ok(new { TotalAvailable = staffList.Count, Staff = staffList });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
        }

        // =====================================================
        // 5. APPROVE & REJECT
        // =====================================================

        /// Approve a complaint
        [HttpPost]
        [Route("approve/{complaintId:guid}")]
        public IHttpActionResult ApproveComplaint(Guid complaintId, [FromBody] ApproveRequest request)
        {
            try
            {
                var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == complaintId);
                if (complaint == null)
                    return NotFoundMessage("Complaint not found.");

                if ((int)complaint.SubmissionStatus == 1)
                    return BadRequest("Complaint is already approved.");

                complaint.SubmissionStatus = (SubmissionStatus)1;
                complaint.CurrentStatus = ComplaintStatus.Approved;
                complaint.ApprovedById = request.ApprovedById;
                complaint.StatusUpdatedAt = DateTime.Now;

                var history = new ComplaintStatusHistories
                {
                    HistoryId = Guid.NewGuid(),
                    ComplaintId = complaint.ComplaintId,
                    PreviousStatus = "Submitted",
                    NewStatus = "Approved",
                    ChangedById = request.ApprovedById ?? Guid.Empty,
                    ChangedAt = DateTime.Now,
                    Notes = request.Notes ?? "Approved by admin"
                };
                db.ComplaintStatusHistories.Add(history);

                db.SaveChanges();

                return Ok(new
                {
                    success = true,
                    message = "Complaint approved successfully",
                    complaintId = complaint.ComplaintId,
                    status = (int)complaint.SubmissionStatus
                });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
        }

        /// Reject a complaint
        [HttpPost]
        [Route("reject/{complaintId:guid}")]
        public IHttpActionResult RejectComplaint(Guid complaintId, [FromBody] RejectRequest request)
        {
            try
            {
                var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == complaintId);
                if (complaint == null)
                    return NotFoundMessage("Complaint not found.");

                if (string.IsNullOrEmpty(request.Reason))
                    return BadRequest("Rejection reason is required.");

                complaint.SubmissionStatus = (SubmissionStatus)2;
                complaint.CurrentStatus = ComplaintStatus.Rejected;
                complaint.RejectionReason = request.Reason;
                complaint.StatusUpdatedAt = DateTime.Now;

                var history = new ComplaintStatusHistories
                {
                    HistoryId = Guid.NewGuid(),
                    ComplaintId = complaint.ComplaintId,
                    PreviousStatus = "Submitted",
                    NewStatus = "Rejected",
                    ChangedById = request.RejectedById ?? Guid.Empty,
                    ChangedAt = DateTime.Now,
                    Notes = $"Rejected: {request.Reason}"
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

    public class ReassignmentRequest
    {
        public Guid ComplaintId { get; set; }
        public Guid NewStaffId { get; set; }
        public Guid AssignedById { get; set; }
        public string Notes { get; set; }
        public DateTime? ExpectedCompletionDate { get; set; }
    }

    public class AssignmentStatusUpdate
    {
        public Guid AssignmentId { get; set; }
        public string Status { get; set; }
    }

    public class RejectRequest
    {
        public string Reason { get; set; }
        public Guid? RejectedById { get; set; }
    }

    public class ApproveRequest
    {
        public Guid? ApprovedById { get; set; }
        public string Notes { get; set; }
    }
}