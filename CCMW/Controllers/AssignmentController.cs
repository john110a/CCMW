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
        // ASSIGN COMPLAINT
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

                // ✅ FIX: Validate AssignedById is not null
                if (request.AssignedById == null || request.AssignedById == Guid.Empty)
                    return BadRequest("AssignedById (admin ID) is required.");

                // Get complaint
                var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == request.ComplaintId);
                if (complaint == null)
                    return NotFoundMessage("Complaint not found.");

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

                // Check if complaint is already assigned
                if (complaint.AssignedToId != null && complaint.AssignedToId != Guid.Empty)
                    return BadRequest("Complaint is already assigned to another staff member.");

                // Check if complaint is approved (2 = Approved)
                if ((int)complaint.CurrentStatus != 2)
                    return BadRequest($"Complaint status must be 'Approved' to assign. Current status: {complaint.CurrentStatus}");

                // Begin transaction
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
                            AssignedById = request.AssignedById.Value, // ✅ Safe now, validated above
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
                        complaint.CurrentStatus = (ComplaintStatus)3; // 3 = Assigned
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
                            ChangedById = request.AssignedById.Value, // ✅ Safe now, validated above
                            ChangedAt = DateTime.Now,
                            Notes = $"Assigned to {staff.EmployeeId}"
                        };

                        db.ComplaintStatusHistories.Add(history);

                        // Save all changes
                        int result = db.SaveChanges();

                        transaction.Commit();

                        return Ok(new
                        {
                            success = true,
                            message = "Complaint assigned successfully",
                            assignmentId = assignment.AssignmentId,
                            complaintId = complaint.ComplaintId,
                            complaintNumber = complaint.ComplaintNumber,
                            assignedTo = staff.EmployeeId,
                            assignedAt = assignment.AssignedAt
                        });
                    }
                    catch (DbEntityValidationException ex)
                    {
                        transaction.Rollback();

                        // ✅ Collect all EF validation errors
                        var validationErrors = ex.EntityValidationErrors
                            .SelectMany(e => e.ValidationErrors)
                            .Select(e => $"{e.PropertyName}: {e.ErrorMessage}");

                        return Content(HttpStatusCode.InternalServerError, new
                        {
                            success = false,
                            error = "Validation failed",
                            validationErrors = validationErrors,
                        });
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();

                        // ✅ Drill down to the deepest/real SQL error
                        var innermost = ex;
                        while (innermost.InnerException != null)
                            innermost = innermost.InnerException;

                        return Content(HttpStatusCode.InternalServerError, new
                        {
                            success = false,
                            error = ex.Message,
                            innerError = ex.InnerException?.Message,
                            deepestError = innermost.Message, // ← Real SQL error will appear here
                            stackTrace = ex.StackTrace?.ToString()
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                // ✅ Drill down to the deepest/real SQL error
                var innermost = ex;
                while (innermost.InnerException != null)
                    innermost = innermost.InnerException;

                return Content(HttpStatusCode.InternalServerError, new
                {
                    success = false,
                    error = ex.Message,
                    innerError = ex.InnerException?.Message,
                    deepestError = innermost.Message, // ← Real SQL error will appear here
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
                    .Where(s => s.Role == "Field_Staff" && s.IsAvailable == true);

                if (!string.IsNullOrEmpty(departmentId) && Guid.TryParse(departmentId, out var deptId))
                {
                    query = query.Where(s => s.DepartmentId == deptId);
                }

                var staffList = query.ToList();

                var staff = staffList.Select(s => new
                {
                    s.StaffId,
                    s.UserId,
                    FullName = s.EmployeeId,
                    Email = "",
                    PhoneNumber = "",
                    s.DepartmentId,
                    DepartmentName = "",
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
                    .Where(c => (int)c.CurrentStatus == 2 && c.AssignedToId == null)
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
                        c.AssignedAt
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
                    .Where(c => c.DepartmentId == departmentId && (int)c.CurrentStatus == 2 && c.AssignedToId == null)
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
                        c.AssignedAt
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
                    return NotFoundMessage("Complaint not found.");

                complaint.CurrentStatus = (ComplaintStatus)7; // 7 = Rejected
                complaint.RejectionReason = request.Reason;
                complaint.StatusUpdatedAt = DateTime.Now;

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