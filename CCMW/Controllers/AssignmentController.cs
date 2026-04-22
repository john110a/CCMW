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
                var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == request.ComplaintId);
                if (complaint == null)
                    return NotFound("Complaint not found.");

                // Get staff from StaffProfiles table (this matches your database)
                var staff = db.StaffProfiles
                    .Include(s => s.User)
                    .FirstOrDefault(s => s.StaffId == request.AssignedToId);

                if (staff == null)
                {
                    // Try to find by UserId as fallback
                    staff = db.StaffProfiles
                        .Include(s => s.User)
                        .FirstOrDefault(s => s.UserId == request.AssignedToId);
                }

                if (staff == null)
                {
                    // Debug: Log all staff IDs in the database
                    var allStaffIds = db.StaffProfiles.Select(s => s.StaffId).ToList();
                    System.Diagnostics.Debug.WriteLine($"Available Staff IDs in DB: {string.Join(", ", allStaffIds)}");

                    return NotFound($"Staff not found with ID: {request.AssignedToId}");
                }

                // Check if complaint is already assigned
                if (complaint.AssignedToId != null && complaint.AssignedToId != Guid.Empty)
                    return BadRequest("Complaint is already assigned to another staff member.");

                // Check if complaint is approved
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

                // Get staff name for response
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
        // GET AVAILABLE STAFF
        // =====================================================
        [HttpGet]
        [Route("staff/available")]
        public IHttpActionResult GetAvailableStaff([FromUri] string departmentId = null)
        {
            try
            {
                var query = db.StaffProfiles
                    .Include(s => s.User)
                    .Where(s => s.IsAvailable == true);

                if (!string.IsNullOrEmpty(departmentId) && Guid.TryParse(departmentId, out var deptId))
                {
                    query = query.Where(s => s.DepartmentId == deptId);
                }

                var staffList = query.ToList();

                var staff = staffList.Select(s => new
                {
                    s.StaffId,
                    s.UserId,
                    FullName = s.User != null ? s.User.FullName : s.EmployeeId,
                    Email = s.User != null ? s.User.Email : "",
                    PhoneNumber = s.User != null ? s.User.PhoneNumber : "",
                    s.DepartmentId,
                    DepartmentName = GetDepartmentName(s.DepartmentId),
                    s.Role,
                    s.PendingAssignments,
                    s.PerformanceScore,
                    s.IsAvailable
                }).ToList();

                return Ok(new { TotalAvailable = staff.Count, Staff = staff });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
        }

        // Helper method to get department name
        private string GetDepartmentName(Guid? departmentId)
        {
            if (departmentId == null) return "";
            var department = db.Departments.FirstOrDefault(d => d.DepartmentId == departmentId);
            return department != null ? department.DepartmentName : "";
        }

        // =====================================================
        // GET ALL COMPLAINTS FOR ROUTING
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
                    .Include(c => c.Department)
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
                        DepartmentName = c.Department != null ? c.Department.DepartmentName : "",
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
        // GET COMPLAINTS BY DEPARTMENT
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
                    .Include(c => c.Department)
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
                        DepartmentName = c.Department != null ? c.Department.DepartmentName : "",
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

                var oldStatus = complaint.CurrentStatus.ToString();
                complaint.CurrentStatus = ComplaintStatus.Rejected;
                complaint.RejectionReason = request.Reason;
                complaint.StatusUpdatedAt = DateTime.Now;

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
}