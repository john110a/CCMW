using CCMW.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/staff-actions")]
    public class StaffActionController : ApiController
    {
        private readonly CCMWDbContext db = new CCMWDbContext();

        private IHttpActionResult NotFound(string message)
        {
            return Content(HttpStatusCode.NotFound, new { error = message });
        }

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

        private string GetRootCause(Exception ex)
        {
            Exception innermost = ex;
            while (innermost.InnerException != null)
                innermost = innermost.InnerException;
            return innermost.Message;
        }

        // =====================================================
        // TEST ENDPOINT
        // =====================================================
        [HttpGet]
        [Route("test")]
        public IHttpActionResult Test()
        {
            try
            {
                var dbConnected = db.Database.Exists();
                var assignmentCount = db.ComplaintAssignments.Count();
                return Ok(new
                {
                    success = true,
                    status = "API is working",
                    databaseConnected = dbConnected,
                    assignmentCount = assignmentCount,
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = ex.Message,
                    rootCause = GetRootCause(ex)
                });
            }
        }

        // =====================================================
        // GET MY ASSIGNMENTS
        // =====================================================
        // =====================================================
        // GET MY ASSIGNMENTS - FIXED with Pending and correct Completed
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

                // For status filter - but we need ALL assignments for stats
                var filteredQuery = query;
                if (status == "active")
                    filteredQuery = filteredQuery.Where(a => a.CompletedAt == null && a.IsActive);
                else if (status == "completed")
                    filteredQuery = filteredQuery.Where(a => a.CompletedAt != null);

                var assignments = filteredQuery
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

                // Calculate stats from ALL assignments (not just filtered)
                var allAssignments = query
                    .Select(a => new
                    {
                        a.CompletedAt,
                        a.StartedAt,
                        a.AcceptedAt,
                        a.IsActive
                    })
                    .ToList();

                var totalCount = allAssignments.Count;
                var completedCount = allAssignments.Count(a => a.CompletedAt != null);
                var inProgressCount = allAssignments.Count(a => a.StartedAt != null && a.CompletedAt == null);
                var acceptedCount = allAssignments.Count(a => a.AcceptedAt != null && a.StartedAt == null && a.CompletedAt == null);
                var assignedCount = allAssignments.Count(a => a.AcceptedAt == null && a.StartedAt == null && a.CompletedAt == null && a.IsActive);

                // Pending = tasks that are Assigned or Accepted (need action)
                var pendingCount = assignedCount + acceptedCount;

                var stats = new
                {
                    Total = totalCount,
                    Completed = completedCount,
                    InProgress = inProgressCount,
                    Accepted = acceptedCount,
                    Assigned = assignedCount,
                    Pending = pendingCount,      // ADDED - for frontend dashboard
                    Overdue = assignments.Count(a => a.IsOverdue)
                };

                System.Diagnostics.Debug.WriteLine($"📊 Stats - Total: {totalCount}, Completed: {completedCount}, Pending: {pendingCount}, InProgress: {inProgressCount}");

                return Ok(new { Statistics = stats, Assignments = assignments });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new
                {
                    success = false,
                    message = "Failed to load assignments",
                    error = ex.Message,
                    rootCause = GetRootCause(ex)
                });
            }
        }

        // =====================================================
        // ACCEPT ASSIGNMENT
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
                    return Content(HttpStatusCode.NotFound, new { success = false, message = "Assignment not found" });

                if (assignment.AcceptedAt != null)
                    return Content(HttpStatusCode.BadRequest, new { success = false, message = "Assignment already accepted" });

                if (assignment.CompletedAt != null)
                    return Content(HttpStatusCode.BadRequest, new { success = false, message = "Assignment already completed" });

                if (assignment.Complaint == null)
                    return Content(HttpStatusCode.BadRequest, new { success = false, message = "Complaint not found" });

                if (request != null && request.Latitude.HasValue && request.Longitude.HasValue)
                {
                    if (assignment.Complaint.LocationLatitude != 0m && assignment.Complaint.LocationLongitude != 0m)
                    {
                        var distance = CalculateDistance(
                            request.Latitude.Value, request.Longitude.Value,
                            (double)assignment.Complaint.LocationLatitude,
                            (double)assignment.Complaint.LocationLongitude);

                        if (distance > 5.0)
                        {
                            return Content(HttpStatusCode.BadRequest, new
                            {
                                success = false,
                                message = $"You are {distance:F2}km away. Please get within 5km to accept.",
                                distance = distance
                            });
                        }
                    }
                }

                var oldStatus = assignment.Complaint.CurrentStatus.ToString();
                assignment.AcceptedAt = DateTime.Now;
                assignment.Complaint.CurrentStatus = ComplaintStatus.InProgress;
                assignment.Complaint.StatusUpdatedAt = DateTime.Now;

                db.SaveChanges();

                try
                {
                    db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
                    {
                        HistoryId = Guid.NewGuid(),
                        ComplaintId = assignment.ComplaintId,
                        PreviousStatus = oldStatus,
                        NewStatus = ComplaintStatus.InProgress.ToString(),
                        ChangedById = staffId,
                        ChangedAt = DateTime.Now,
                        ChangeReason = "Staff accepted assignment",
                        Notes = $"Accepted at {DateTime.Now}"
                    });
                    db.SaveChanges();
                }
                catch (Exception histEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Status history warning: {GetRootCause(histEx)}");
                }

                return Ok(new
                {
                    success = true,
                    message = "Assignment accepted successfully",
                    assignmentId = assignmentId,
                    acceptedAt = assignment.AcceptedAt,
                    complaintId = assignment.ComplaintId,
                    newStatus = "InProgress"
                });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new
                {
                    success = false,
                    message = "An error occurred while accepting the assignment",
                    error = ex.Message,
                    innerError = ex.InnerException?.Message,
                    rootCause = GetRootCause(ex)
                });
            }
        }

        // =====================================================
        // START WORK
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
                    success = true,
                    message = "Work started successfully",
                    assignmentId = assignmentId,
                    startedAt = assignment.StartedAt
                });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new
                {
                    success = false,
                    message = "Failed to start work",
                    error = ex.Message,
                    rootCause = GetRootCause(ex)
                });
            }
        }

        // =====================================================
        // RESOLVE COMPLAINT
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
                        staff.PerformanceScore = (decimal)staff.CompletedAssignments / staff.TotalAssignments * 100;
                }

                db.SaveChanges();

                try
                {
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
                }
                catch (Exception histEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Status history warning: {GetRootCause(histEx)}");
                }

                try
                {
                    db.Database.ExecuteSqlCommand(
                        "EXEC sp_NotifyComplaintFlow @ComplaintId, @EventType",
                        new SqlParameter("@ComplaintId", assignment.ComplaintId),
                        new SqlParameter("@EventType", "COMPLETED")
                    );
                }
                catch (Exception notifEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Notification warning: {GetRootCause(notifEx)}");
                }

                return Ok(new
                {
                    success = true,
                    message = "Complaint resolved successfully",
                    assignmentId = assignmentId,
                    completedAt = assignment.CompletedAt
                });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new
                {
                    success = false,
                    message = "Failed to resolve complaint",
                    error = ex.Message,
                    rootCause = GetRootCause(ex)
                });
            }
        }

        // =====================================================
        // GET NEARBY COMPLAINTS - FIXED (using IsFake)
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
                             && c.LocationLatitude != 0m
                             && c.LocationLongitude != 0m)
                    .ToList()
                    .Select(c => new
                    {
                        c.ComplaintId,
                        c.ComplaintNumber,
                        c.Title,
                        c.Description,
                        c.Priority,
                        CurrentStatus = c.CurrentStatus.ToString(),
                        CategoryName = c.Category != null ? c.Category.CategoryName : "Unknown",
                        ZoneName = c.Zone != null ? c.Zone.ZoneName : "Unknown",
                        c.LocationAddress,
                        LocationLatitude = (double)c.LocationLatitude,
                        LocationLongitude = (double)c.LocationLongitude,
                        Distance = CalculateDistance(lat, lng, (double)c.LocationLatitude, (double)c.LocationLongitude),
                        c.CreatedAt,
                        c.UpvoteCount,
                        IsFlagged = c.IsFake == true,
                        FlagReason = c.IsFake == true ? "Flagged as potential fake complaint" : null
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
                return Content(HttpStatusCode.InternalServerError, new
                {
                    success = false,
                    message = "Failed to fetch nearby complaints",
                    error = ex.Message,
                    rootCause = GetRootCause(ex)
                });
            }
        }

        // =====================================================
        // GET FLAGGED COMPLAINTS FOR STAFF - FIXED (using IsFake)
        // =====================================================
        // =====================================================
        // GET FLAGGED COMPLAINTS FOR STAFF - FIXED (matching types)
        // =====================================================
        [HttpGet]
        [Route("{staffId:guid}/flagged-complaints")]
        public IHttpActionResult GetFlaggedComplaints(Guid staffId)
        {
            try
            {
                var staff = db.StaffProfiles
                    .Include(s => s.Department)
                    .FirstOrDefault(s => s.StaffId == staffId);

                if (staff == null)
                    return NotFound("Staff not found");

                // DEBUG: Log staff info
                System.Diagnostics.Debug.WriteLine($"=== DEBUG: Staff ID: {staffId}, Department: {staff.DepartmentId} ===");

                // DEBUG: Check if any complaints have IsFake = true in the database
                var totalFakeComplaints = db.Complaints.Count(c => c.IsFake == true);
                System.Diagnostics.Debug.WriteLine($"DEBUG: Total fake complaints in system: {totalFakeComplaints}");

                // DEBUG: Check complaints assigned to this staff
                var staffAssignments = db.ComplaintAssignments
                    .Where(a => a.AssignedToId == staffId)
                    .Select(a => new { a.ComplaintId, a.Complaint.IsFake, a.CompletedAt })
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"DEBUG: Staff has {staffAssignments.Count} total assignments");
                foreach (var assign in staffAssignments)
                {
                    System.Diagnostics.Debug.WriteLine($"DEBUG: Complaint {assign.ComplaintId} - IsFake: {assign.IsFake}, CompletedAt: {assign.CompletedAt}");
                }

                // Now get flagged complaints (remove ALL filters except IsFake and AssignedToId)
                var flaggedComplaints = db.ComplaintAssignments
                    .Include(a => a.Complaint)
                    .Include(a => a.Complaint.Category)
                    .Include(a => a.Complaint.Zone)
                    .Where(a => a.AssignedToId == staffId
                             && a.Complaint != null
                             && a.Complaint.IsFake == true)  // ONLY filter on IsFake and AssignedToId
                    .Select(a => new
                    {
                        a.ComplaintId,
                        a.Complaint.ComplaintNumber,
                        a.Complaint.Title,
                        a.Complaint.Description,
                        a.Complaint.Priority,
                        CurrentStatus = a.Complaint.CurrentStatus.ToString(),
                        FlagStatus = a.Complaint.IsFake == true ? "Fake" : null,
                        FlagReason = a.Complaint.IsFake == true ? "Flagged as potential fake complaint" : null,
                        FlaggedAt = a.Complaint.FakeVerifiedAt,
                        CategoryName = a.Complaint.Category != null ? a.Complaint.Category.CategoryName : "Unknown",
                        ZoneName = a.Complaint.Zone != null ? a.Complaint.Zone.ZoneName : "Unknown",
                        a.Complaint.LocationAddress,
                        LocationLatitude = (double)a.Complaint.LocationLatitude,
                        LocationLongitude = (double)a.Complaint.LocationLongitude,
                        a.Complaint.CreatedAt,
                        AssignmentId = (Guid?)a.AssignmentId,
                        a.CompletedAt,
                        a.IsActive
                    })
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"DEBUG: Found {flaggedComplaints.Count} flagged complaints from assignments");

                var deptFlaggedComplaints = db.Complaints
                    .Include(c => c.Category)
                    .Include(c => c.Zone)
                    .Where(c => c.DepartmentId == staff.DepartmentId
                             && c.IsFake == true
                             && !db.ComplaintAssignments.Any(a => a.ComplaintId == c.ComplaintId && a.AssignedToId == staffId))
                    .Select(c => new
                    {
                        c.ComplaintId,
                        c.ComplaintNumber,
                        c.Title,
                        c.Description,
                        c.Priority,
                        CurrentStatus = c.CurrentStatus.ToString(),
                        FlagStatus = c.IsFake == true ? "Fake" : null,
                        FlagReason = c.IsFake == true ? "Flagged as potential fake complaint" : null,
                        FlaggedAt = c.FakeVerifiedAt,
                        CategoryName = c.Category != null ? c.Category.CategoryName : "Unknown",
                        ZoneName = c.Zone != null ? c.Zone.ZoneName : "Unknown",
                        c.LocationAddress,
                        LocationLatitude = (double)c.LocationLatitude,
                        LocationLongitude = (double)c.LocationLongitude,
                        c.CreatedAt,
                        AssignmentId = (Guid?)null,
                        CompletedAt = (DateTime?)null,
                        IsActive = false
                    })
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"DEBUG: Found {deptFlaggedComplaints.Count} unassigned flagged complaints in department");

                var allFlagged = flaggedComplaints
                    .Concat(deptFlaggedComplaints)
                    .OrderByDescending(c => c.FlaggedAt)
                    .ToList();

                return Ok(new
                {
                    success = true,
                    totalFlagged = allFlagged.Count,
                    complaints = allFlagged
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"STACK: {ex.StackTrace}");

                return Content(HttpStatusCode.InternalServerError, new
                {
                    success = false,
                    message = "Failed to fetch flagged complaints",
                    error = ex.Message,
                    rootCause = GetRootCause(ex)
                });
            }
        }

        // =====================================================
        // RE-COMPLETE FLAGGED COMPLAINT - FIXED (using IsFake)
        // =====================================================
        [HttpPost]
        [Route("{complaintId:guid}/recomplete")]
        public IHttpActionResult ReCompleteFlaggedComplaint(Guid complaintId, [FromBody] RecompleteRequest request)
        {
            try
            {
                if (request == null || request.StaffId == Guid.Empty)
                    return BadRequest("Staff ID is required");

                var complaint = db.Complaints
                    .FirstOrDefault(c => c.ComplaintId == complaintId);

                if (complaint == null)
                    return NotFound("Complaint not found");

                // Verify this complaint is assigned to this staff member
                var assignment = db.ComplaintAssignments
                    .FirstOrDefault(a => a.ComplaintId == complaintId
                                      && a.AssignedToId == request.StaffId);

                if (assignment == null)
                    return Content(HttpStatusCode.Forbidden, new
                    {
                        success = false,
                        message = "You are not authorized to re-complete this complaint"
                    });

                var oldStatus = complaint.CurrentStatus.ToString();
                var wasCompleted = assignment.CompletedAt != null;

                // Reset the complaint status for re-completion
                complaint.CurrentStatus = ComplaintStatus.InProgress;
                complaint.StatusUpdatedAt = DateTime.Now;

                // Append notes to existing resolution notes
                var existingNotes = complaint.ResolutionNotes ?? "";
                complaint.ResolutionNotes = existingNotes + $"\n\n[RE-COMPLETED at {DateTime.Now} by Staff {request.StaffId}]:\n{request.Notes}";

                // Clear fake flag when staff is re-completing with evidence
                if (!string.IsNullOrWhiteSpace(request.Notes) && request.Notes.Length > 10)
                {
                    complaint.IsFake = false;
                    complaint.FakeVerifiedBy = null;
                    complaint.FakeVerifiedAt = null;
                }

                // Reset assignment if it was completed
                if (assignment.CompletedAt != null)
                {
                    assignment.CompletedAt = null;
                    assignment.IsActive = true;
                }

                var recompletedAt = DateTime.Now;
                db.SaveChanges();

                // Add status history
                try
                {
                    db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
                    {
                        HistoryId = Guid.NewGuid(),
                        ComplaintId = complaintId,
                        PreviousStatus = oldStatus,
                        NewStatus = ComplaintStatus.InProgress.ToString(),
                        ChangedById = request.StaffId,
                        ChangedAt = recompletedAt,
                        ChangeReason = wasCompleted ? "Completed flagged complaint re-opened" : "Flagged complaint re-completed",
                        Notes = $"Re-completed by staff with notes: {request.Notes}"
                    });
                    db.SaveChanges();
                }
                catch (Exception histEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Status history warning: {GetRootCause(histEx)}");
                }

                // Update staff performance (decrement completed count if it was completed)
                var staff = db.StaffProfiles.Find(request.StaffId);
                if (staff != null && wasCompleted)
                {
                    staff.CompletedAssignments = Math.Max(staff.CompletedAssignments - 1, 0);
                    if (staff.TotalAssignments > 0)
                        staff.PerformanceScore = (decimal)staff.CompletedAssignments / staff.TotalAssignments * 100;
                    db.SaveChanges();
                }

                // Send notification
                try
                {
                    db.Database.ExecuteSqlCommand(
                        "EXEC sp_NotifyComplaintFlow @ComplaintId, @EventType",
                        new SqlParameter("@ComplaintId", complaintId),
                        new SqlParameter("@EventType", "REOPENED")
                    );
                }
                catch (Exception notifEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Notification warning: {GetRootCause(notifEx)}");
                }

                return Ok(new
                {
                    success = true,
                    message = wasCompleted ? "Completed complaint has been re-opened for re-completion" : "Complaint has been re-opened for re-completion",
                    complaintId = complaintId,
                    recompletedAt = recompletedAt,
                    newStatus = "InProgress"
                });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new
                {
                    success = false,
                    message = "Failed to re-complete complaint",
                    error = ex.Message,
                    rootCause = GetRootCause(ex)
                });
            }
        }

        // =====================================================
        // SUBMIT EVIDENCE FOR FLAGGED COMPLAINT - FIXED
        // =====================================================
        [HttpPost]
        [Route("{complaintId:guid}/submit-evidence")]
        public IHttpActionResult SubmitFlaggedEvidence(Guid complaintId, [FromBody] EvidenceRequest request)
        {
            try
            {
                if (request == null || request.StaffId == Guid.Empty)
                    return BadRequest("Staff ID is required");

                if (string.IsNullOrWhiteSpace(request.Evidence))
                    return BadRequest("Evidence details are required");

                var complaint = db.Complaints
                    .FirstOrDefault(c => c.ComplaintId == complaintId);

                if (complaint == null)
                    return NotFound("Complaint not found");

                // Verify authorization
                var assignment = db.ComplaintAssignments
                    .FirstOrDefault(a => a.ComplaintId == complaintId
                                      && a.AssignedToId == request.StaffId);

                if (assignment == null)
                    return Content(HttpStatusCode.Forbidden, new
                    {
                        success = false,
                        message = "You are not authorized to submit evidence for this complaint"
                    });

                // Add evidence to complaint
                var existingNotes = complaint.ResolutionNotes ?? "";
                complaint.ResolutionNotes = existingNotes + $"\n\n[EVIDENCE SUBMITTED at {DateTime.Now} by Staff]:\n{request.Evidence}";
                complaint.StatusUpdatedAt = DateTime.Now;

                db.SaveChanges();

                return Ok(new
                {
                    success = true,
                    message = "Evidence submitted successfully. Complaint is now pending review.",
                    complaintId = complaintId,
                    submittedAt = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new
                {
                    success = false,
                    message = "Failed to submit evidence",
                    error = ex.Message,
                    rootCause = GetRootCause(ex)
                });
            }
        }

        // =====================================================
        // UPDATE STAFF LOCATION
        // =====================================================
        [HttpPost]
        [Route("{staffId:guid}/location")]
        public IHttpActionResult UpdateStaffLocation(Guid staffId, [FromBody] LocationUpdateRequest location)
        {
            try
            {
                var staff = db.StaffProfiles
                    .FirstOrDefault(s => s.StaffId == staffId);

                if (staff == null)
                    return NotFound("Staff not found");

                if (location == null || !location.Latitude.HasValue || !location.Longitude.HasValue)
                    return BadRequest("Location coordinates are required");

                db.SaveChanges();

                return Ok(new
                {
                    success = true,
                    message = "Location updated successfully",
                    staffId = staffId,
                    updatedAt = DateTime.Now,
                    location = new { location.Latitude, location.Longitude }
                });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new
                {
                    success = false,
                    message = "Failed to update location",
                    error = ex.Message,
                    rootCause = GetRootCause(ex)
                });
            }
        }

        // =====================================================
        // GET ASSIGNMENT TIMELINE
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
                        assignment.AssignedAt,
                        assignment.AcceptedAt,
                        assignment.StartedAt,
                        assignment.CompletedAt,
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
                return Content(HttpStatusCode.InternalServerError, new
                {
                    success = false,
                    message = "Failed to load timeline",
                    error = ex.Message,
                    rootCause = GetRootCause(ex)
                });
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
                return Content(HttpStatusCode.InternalServerError, new
                {
                    success = false,
                    message = "Failed to load performance",
                    error = ex.Message,
                    rootCause = GetRootCause(ex)
                });
            }
        }

        // =====================================================
        // DEBUG ASSIGNMENT
        // =====================================================
        [HttpGet]
        [Route("debug/assignment/{assignmentId:guid}")]
        public IHttpActionResult DebugAssignment(Guid assignmentId)
        {
            try
            {
                var assignment = db.ComplaintAssignments
                    .Include(a => a.Complaint)
                    .FirstOrDefault(a => a.AssignmentId == assignmentId);

                if (assignment == null)
                    return NotFound("Assignment not found");

                return Ok(new
                {
                    Assignment = new
                    {
                        assignment.AssignmentId,
                        assignment.AssignedToId,
                        assignment.ComplaintId,
                        assignment.AssignedAt,
                        assignment.AcceptedAt,
                        assignment.IsActive
                    },
                    Complaint = assignment.Complaint != null ? new object[]
                    {
                        new {
                            assignment.Complaint.ComplaintId,
                            assignment.Complaint.ComplaintNumber,
                            assignment.Complaint.Title,
                            assignment.Complaint.CurrentStatus,
                            LocationLatitude = assignment.Complaint.LocationLatitude,
                            LocationLongitude = assignment.Complaint.LocationLongitude,
                            HasValidLocation = assignment.Complaint.LocationLatitude != 0m &&
                                               assignment.Complaint.LocationLongitude != 0m
                        }
                    } : null
                });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new
                {
                    success = false,
                    message = "Debug failed",
                    error = ex.Message,
                    rootCause = GetRootCause(ex)
                });
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
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

    public class RecompleteRequest
    {
        public Guid StaffId { get; set; }
        public string Notes { get; set; }
    }

    public class EvidenceRequest
    {
        public Guid StaffId { get; set; }
        public string Evidence { get; set; }
        public List<string> PhotoUrls { get; set; }
    }
}