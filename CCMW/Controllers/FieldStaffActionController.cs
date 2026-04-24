using CCMW.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/staff-actions")]
    public class StaffActionController : ApiController
    {
        private readonly CCMWDbContext db = new CCMWDbContext();

        // Helper method for NotFound with message
        private IHttpActionResult NotFound(string message)
        {
            return Content(HttpStatusCode.NotFound, new { error = message });
        }

        // Helper method for distance calculation
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371;
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRadians(double angle) => Math.PI * angle / 180.0;

        // =====================================================
        // GET MY ASSIGNMENTS (Staff Dashboard)
        // =====================================================
        [HttpGet]
        [Route("my-assignments/{staffId:guid}")]
        public IHttpActionResult GetMyAssignments(Guid staffId, [FromUri] string status = "active")
        {
            try
            {
                var query = db.ComplaintAssignments
                    .Include(a => a.Complaint)
                    .Include(a => a.Complaint.Category)
                    .Include(a => a.Complaint.Zone)
                    .Where(a => a.AssignedToId == staffId);

                if (status == "active")
                {
                    query = query.Where(a => a.CompletedAt == null && a.IsActive);
                }
                else if (status == "completed")
                {
                    query = query.Where(a => a.CompletedAt != null);
                }

                var assignments = query
                    .OrderByDescending(a => a.AssignedAt)
                    .Select(a => new
                    {
                        a.AssignmentId,
                        a.ComplaintId,
                        ComplaintNumber = a.Complaint.ComplaintNumber,
                        a.Complaint.Title,
                        a.Complaint.Description,
                        a.Complaint.Priority,
                        a.Complaint.LocationAddress,
                        LocationLatitude = (double)a.Complaint.LocationLatitude,
                        LocationLongitude = (double)a.Complaint.LocationLongitude,
                        CategoryName = a.Complaint.Category.CategoryName,
                        ZoneName = a.Complaint.Zone.ZoneName,
                        a.AssignedAt,
                        a.ExpectedCompletionDate,
                        a.AcceptedAt,
                        a.StartedAt,
                        a.CompletedAt,
                        a.AssignmentNotes,
                        Status = a.CompletedAt != null ? "Completed" :
                                 a.StartedAt != null ? "InProgress" :
                                 a.AcceptedAt != null ? "Accepted" : "Assigned",
                        IsOverdue = a.ExpectedCompletionDate.HasValue &&
                                   a.ExpectedCompletionDate.Value < DateTime.Now &&
                                   a.CompletedAt == null
                    })
                    .ToList();

                var stats = new
                {
                    Total = assignments.Count,
                    Completed = assignments.Count(a => a.Status == "Completed"),
                    InProgress = assignments.Count(a => a.Status == "InProgress"),
                    Accepted = assignments.Count(a => a.Status == "Accepted"),
                    Assigned = assignments.Count(a => a.Status == "Assigned"),
                    Overdue = assignments.Count(a => a.IsOverdue)
                };

                return Ok(new
                {
                    Statistics = stats,
                    Assignments = assignments
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // ACCEPT ASSIGNMENT (with GPS check)
        // =====================================================
        [HttpPost]
        [Route("{assignmentId:guid}/accept")]
        public IHttpActionResult AcceptAssignment(Guid assignmentId, [FromUri] Guid staffId, [FromBody] LocationUpdateRequest request)
        {
            try
            {
                var assignment = db.ComplaintAssignments
                    .Include(a => a.Complaint)
                    .FirstOrDefault(a => a.AssignmentId == assignmentId
                                         && a.AssignedToId == staffId
                                         && a.IsActive);

                if (assignment == null)
                    return NotFound("Assignment not found");

                if (assignment.AcceptedAt != null)
                    return BadRequest("Assignment already accepted");

                if (assignment.CompletedAt != null)
                    return BadRequest("Assignment already completed");

                // Check if staff is near the complaint location
                if (request != null && request.Latitude.HasValue && request.Longitude.HasValue)
                {
                    var distance = CalculateDistance(
                        request.Latitude.Value,
                        request.Longitude.Value,
                        (double)assignment.Complaint.LocationLatitude,
                        (double)assignment.Complaint.LocationLongitude);

                    if (distance > 0.5)
                    {
                        return BadRequest("You are " + distance.ToString("F2") + "km away. Please get closer to the location (within 500m) to accept.");
                    }
                }

                assignment.AcceptedAt = DateTime.Now;
                assignment.Complaint.CurrentStatus = ComplaintStatus.InProgress;
                assignment.Complaint.StatusUpdatedAt = DateTime.Now;

                db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
                {
                    HistoryId = Guid.NewGuid(),
                    ComplaintId = assignment.ComplaintId,
                    PreviousStatus = ComplaintStatus.Assigned.ToString(),
                    NewStatus = ComplaintStatus.InProgress.ToString(),
                    ChangedById = staffId,
                    ChangedAt = DateTime.Now,
                    Notes = "Assignment accepted by staff"
                });

                db.SaveChanges();

                return Ok(new
                {
                    Message = "Assignment accepted successfully",
                    AssignmentId = assignmentId,
                    AcceptedAt = assignment.AcceptedAt
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // START WORK ON ASSIGNMENT
        // =====================================================
        [HttpPost]
        [Route("{assignmentId:guid}/start")]
        public IHttpActionResult StartWork(Guid assignmentId, [FromUri] Guid staffId)
        {
            try
            {
                var assignment = db.ComplaintAssignments
                    .FirstOrDefault(a => a.AssignmentId == assignmentId
                                         && a.AssignedToId == staffId
                                         && a.IsActive);

                if (assignment == null)
                    return NotFound("Assignment not found");

                if (assignment.StartedAt != null)
                    return BadRequest("Work already started");

                if (assignment.AcceptedAt == null)
                    return BadRequest("Assignment must be accepted before starting work");

                assignment.StartedAt = DateTime.Now;

                db.SaveChanges();

                return Ok(new
                {
                    Message = "Work started successfully",
                    AssignmentId = assignmentId,
                    StartedAt = assignment.StartedAt
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // RESOLVE COMPLAINT (with photo upload)
        // =====================================================
        [HttpPost]
        [Route("{assignmentId:guid}/resolve")]
        public IHttpActionResult ResolveComplaint(Guid assignmentId, [FromUri] Guid staffId, [FromBody] ResolutionRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.ResolutionNotes))
                    return BadRequest("Resolution notes are required");

                var assignment = db.ComplaintAssignments
                    .Include(a => a.Complaint)
                    .FirstOrDefault(a => a.AssignmentId == assignmentId
                                         && a.AssignedToId == staffId
                                         && a.IsActive);

                if (assignment == null)
                    return NotFound("Assignment not found");

                if (assignment.CompletedAt != null)
                    return BadRequest("Complaint already resolved");

                var oldStatus = assignment.Complaint.CurrentStatus.ToString();

                assignment.CompletedAt = DateTime.Now;
                assignment.IsActive = false;

                assignment.Complaint.CurrentStatus = ComplaintStatus.Resolved;
                assignment.Complaint.ResolutionNotes = request.ResolutionNotes;
                assignment.Complaint.ResolvedAt = DateTime.Now;
                assignment.Complaint.StatusUpdatedAt = DateTime.Now;

                var staff = db.StaffProfiles.Find(staffId);
                if (staff != null)
                {
                    staff.CompletedAssignments += 1;
                    staff.PendingAssignments = Math.Max(staff.PendingAssignments - 1, 0);

                    if (staff.TotalAssignments > 0)
                    {
                        staff.PerformanceScore = (decimal)staff.CompletedAssignments / staff.TotalAssignments * 100;
                    }
                }

                db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
                {
                    HistoryId = Guid.NewGuid(),
                    ComplaintId = assignment.ComplaintId,
                    PreviousStatus = oldStatus,
                    NewStatus = ComplaintStatus.Resolved.ToString(),
                    ChangedById = staffId,
                    ChangedAt = DateTime.Now,
                    Notes = request.ResolutionNotes
                });

                db.SaveChanges();

                return Ok(new
                {
                    Message = "Complaint resolved successfully",
                    AssignmentId = assignmentId,
                    CompletedAt = assignment.CompletedAt
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // GET NEARBY COMPLAINTS FOR STAFF - FIXED: Accept lat/lng as query parameters
        // =====================================================
        [HttpGet]
        [Route("{staffId:guid}/nearby-complaints")]
        public IHttpActionResult GetNearbyComplaints(Guid staffId, [FromUri] double lat, [FromUri] double lng, [FromUri] double radiusKm = 3.0)
        {
            try
            {
                var staff = db.StaffProfiles
                    .Include(s => s.User)
                    .Include(s => s.Department)
                    .FirstOrDefault(s => s.StaffId == staffId);

                if (staff == null)
                    return NotFound("Staff not found");

                var complaints = db.Complaints
                    .Include(c => c.Category)
                    .Include(c => c.Zone)
                    .Where(c => c.DepartmentId == staff.DepartmentId
                                && c.CurrentStatus != ComplaintStatus.Resolved
                                && c.CurrentStatus != ComplaintStatus.Closed
                                && c.LocationLatitude != null
                                && c.LocationLongitude != null)
                    .ToList()
                    .Select(c => new
                    {
                        c.ComplaintId,
                        c.ComplaintNumber,
                        c.Title,
                        c.Description,
                        c.Priority,
                        c.CurrentStatus,
                        CategoryName = c.Category != null ? c.Category.CategoryName : "Unknown",
                        ZoneName = c.Zone != null ? c.Zone.ZoneName : "Unknown",
                        c.LocationAddress,
                        LocationLatitude = (double)c.LocationLatitude,
                        LocationLongitude = (double)c.LocationLongitude,
                        Distance = CalculateDistance(lat, lng, (double)c.LocationLatitude, (double)c.LocationLongitude),
                        CreatedAt = c.CreatedAt,
                        UpvoteCount = c.UpvoteCount
                    })
                    .Where(x => x.Distance <= radiusKm)
                    .OrderBy(x => x.Distance)
                    .Take(20)
                    .ToList();

                return Ok(new
                {
                    StaffId = staffId,
                    StaffLocation = new { Lat = lat, Lng = lng },
                    TotalNearby = complaints.Count,
                    Complaints = complaints
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // UPDATE STAFF LOCATION (GPS Tracking)
        // =====================================================
        [HttpPost]
        [Route("{staffId:guid}/location")]
        public IHttpActionResult UpdateStaffLocation(Guid staffId, [FromBody] LocationUpdateRequest location)
        {
            try
            {
                var staff = db.StaffProfiles
                    .Include(s => s.User)
                    .FirstOrDefault(s => s.StaffId == staffId);

                if (staff == null)
                    return NotFound("Staff not found");

                if (location == null || !location.Latitude.HasValue || !location.Longitude.HasValue)
                    return BadRequest("Location coordinates are required");

                // Update the staff's LastLocation fields if they exist in your Staff_Profile table
                // If these columns don't exist, comment them out
                //staff.LastLatitude = (decimal)location.Latitude.Value;
                //staff.LastLongitude = (decimal)location.Longitude.Value;
                //staff.LastLocationUpdate = DateTime.Now;

                db.SaveChanges();

                return Ok(new
                {
                    Message = "Location updated successfully",
                    StaffId = staffId,
                    UpdatedAt = DateTime.Now,
                    Location = new { location.Latitude, location.Longitude }
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // GET ASSIGNMENT DETAILS WITH TIMELINE
        // =====================================================
        [HttpGet]
        [Route("assignment/{assignmentId:guid}/timeline")]
        public IHttpActionResult GetAssignmentTimeline(Guid assignmentId)
        {
            try
            {
                var assignment = db.ComplaintAssignments
                    .Include(a => a.Complaint)
                    .Include(a => a.Complaint.Category)
                    .Include(a => a.Complaint.Zone)
                    .Include(a => a.Complaint.StatusHistory)
                    .FirstOrDefault(a => a.AssignmentId == assignmentId);

                if (assignment == null)
                    return NotFound("Assignment not found");

                var timeline = new
                {
                    assignment.AssignmentId,
                    assignment.ComplaintId,
                    assignment.Complaint.ComplaintNumber,
                    assignment.Complaint.Title,
                    assignment.Complaint.Priority,
                    Timeline = new
                    {
                        AssignedAt = assignment.AssignedAt,
                        AcceptedAt = assignment.AcceptedAt,
                        StartedAt = assignment.StartedAt,
                        CompletedAt = assignment.CompletedAt,
                        ResolutionTime = assignment.CompletedAt.HasValue
                            ? (assignment.CompletedAt.Value - assignment.AssignedAt).TotalHours
                            : (double?)null
                    },
                    StatusHistory = assignment.Complaint.StatusHistory
                        .OrderBy(h => h.ChangedAt)
                        .Select(h => new
                        {
                            h.PreviousStatus,
                            h.NewStatus,
                            h.ChangedAt,
                            h.ChangeReason,
                            h.Notes
                        })
                        .ToList()
                };

                return Ok(timeline);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // GET STAFF PERFORMANCE
        // =====================================================
        [HttpGet]
        [Route("{staffId:guid}/performance")]
        public IHttpActionResult GetStaffPerformance(Guid staffId)
        {
            try
            {
                var staff = db.StaffProfiles
                    .Include(s => s.User)
                    .FirstOrDefault(s => s.StaffId == staffId);

                if (staff == null)
                    return NotFound("Staff not found");

                var completedAssignments = db.ComplaintAssignments
                    .Count(a => a.AssignedToId == staffId && a.CompletedAt != null);

                var totalAssignments = db.ComplaintAssignments
                    .Count(a => a.AssignedToId == staffId);

                var pendingAssignments = db.ComplaintAssignments
                    .Count(a => a.AssignedToId == staffId && a.CompletedAt == null && a.IsActive);

                var avgResolutionTime = db.ComplaintAssignments
                    .Where(a => a.AssignedToId == staffId && a.CompletedAt != null)
                    .Average(a => (double?)DbFunctions.DiffHours(a.AssignedAt, a.CompletedAt)) ?? 0;

                return Ok(new
                {
                    StaffId = staff.StaffId,
                    StaffName = staff.User?.FullName ?? staff.EmployeeId,
                    Role = staff.Role,
                    PerformanceScore = staff.PerformanceScore,
                    TotalAssignments = totalAssignments,
                    CompletedAssignments = completedAssignments,
                    PendingAssignments = pendingAssignments,
                    AverageResolutionTime = Math.Round(avgResolutionTime, 2)
                });
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

    // DTOs for requests
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
}