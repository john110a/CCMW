using CCMW.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/complaints")]
    public class ComplaintsController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        // =====================================================
        // UPDATED SUBMIT METHOD WITH AUTO-DUPLICATE DETECTION
        // =====================================================
        [HttpPost]
        [Route("submit")]
        public IHttpActionResult SubmitComplaint([FromBody] Complaint complaint)
        {
            try
            {
                // Basic validation
                if (complaint == null)
                    return BadRequest("Complaint data is required.");

                if (complaint.CitizenId == null || complaint.CitizenId == Guid.Empty)
                    return BadRequest("CitizenId is required");

                if (complaint.CategoryId == null || complaint.CategoryId == Guid.Empty)
                    return BadRequest("CategoryId is required");

                // Verify category exists and get department
                var category = db.ComplaintCategories.Find(complaint.CategoryId);
                if (category == null)
                    return BadRequest($"Category with ID {complaint.CategoryId} not found");

                // Set required fields
                complaint.ComplaintId = Guid.NewGuid();
                complaint.DepartmentId = category.DepartmentId;

                // Generate complaint number
                if (string.IsNullOrEmpty(complaint.ComplaintNumber))
                {
                    complaint.ComplaintNumber =
                        $"CCMW-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 4)}";
                }

                // Set default priority if not provided
                if (string.IsNullOrEmpty(complaint.Priority))
                {
                    complaint.Priority = "Medium";
                }

                // Set status values
                complaint.CurrentStatus = ComplaintStatus.Submitted;
                complaint.SubmissionStatus = SubmissionStatus.PendingApproval;
                complaint.CreatedAt = DateTime.Now;
                complaint.UpdatedAt = DateTime.Now;
                complaint.UpvoteCount = 0;
                complaint.ViewCount = 0;
                complaint.IsDuplicate = false;
                complaint.IsOverdue = false;

                // IMPORTANT: Clear photos - they are uploaded separately
                complaint.ComplaintPhotos = null;

                // Add complaint
                db.Complaints.Add(complaint);

                // Add status history
                db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
                {
                    HistoryId = Guid.NewGuid(),
                    ComplaintId = complaint.ComplaintId,
                    PreviousStatus = null,
                    NewStatus = complaint.CurrentStatus.ToString(),
                    ChangedById = complaint.CitizenId,
                    ChangedAt = DateTime.Now,
                    Notes = "Complaint submitted"
                });

                db.SaveChanges();

                // ===== AUTO-DETECT DUPLICATES IN BACKGROUND =====
                Task.Run(() => CheckForDuplicates(complaint.ComplaintId));

                return Ok(new
                {
                    message = "Complaint submitted successfully",
                    complaintId = complaint.ComplaintId,
                    complaintNumber = complaint.ComplaintNumber
                });
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException ex)
            {
                var errors = ex.EntityValidationErrors
                    .SelectMany(v => v.ValidationErrors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest("Validation errors: " + string.Join(", ", errors));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error: " + ex.Message);
                if (ex.InnerException != null)
                    System.Diagnostics.Debug.WriteLine("Inner: " + ex.InnerException.Message);

                return InternalServerError(ex);
            }
        }

        // =====================================================
        // BACKGROUND TASK FOR DUPLICATE DETECTION
        // =====================================================
        private async Task CheckForDuplicates(Guid newComplaintId)
        {
            await Task.Run(() =>
            {
                try
                {
                    using (var dbContext = new CCMWDbContext())
                    {
                        var newComplaint = dbContext.Complaints
                            .Include(c => c.Category)
                            .FirstOrDefault(c => c.ComplaintId == newComplaintId);

                        if (newComplaint == null) return;

                        // Find similar complaints (last 60 days)
                        var similar = dbContext.Complaints
                            .Where(c => c.ComplaintId != newComplaintId)
                            .Where(c => c.CategoryId == newComplaint.CategoryId)
                            .Where(c => !c.IsDuplicate && c.MergedIntoComplaintId == null)
                            //.Where(c => c.CreatedAt > DateTime.Now.AddDays(-60))
                            .ToList()
                            .Where(c => CalculateDistance(
                                (double)c.LocationLatitude, (double)c.LocationLongitude,
                                (double)newComplaint.LocationLatitude, (double)newComplaint.LocationLongitude) < 0.2)
                            .ToList();

                        if (!similar.Any()) return;

                        // Check if they belong to an existing cluster
                        var existingCluster = dbContext.DuplicateClusters
                            .FirstOrDefault(cl => cl.PrimaryComplaintId == similar.First().ComplaintId);

                        if (existingCluster != null)
                        {
                            // Add to existing cluster
                            dbContext.DuplicateEntries.Add(new DuplicateEntry
                            {
                                EntryId = Guid.NewGuid(),
                                ClusterId = existingCluster.ClusterId,
                                ComplaintId = newComplaint.ComplaintId,
                                SimilarityScore = CalculateSimilarityScore(newComplaint, similar.First()),
                                SimilarityFactors = "{\"auto_detected\":true}",
                                MergedAt = DateTime.Now
                            });

                            existingCluster.TotalComplaintsMerged++;
                           // existingCluster.TotalCombinedUpvotes += newComplaint.UpvoteCount ?? 0;
                            existingCluster.UpdatedAt = DateTime.Now;

                            newComplaint.IsDuplicate = true;
                            newComplaint.MergedIntoComplaintId = existingCluster.PrimaryComplaintId;
                        }
                        else
                        {
                            // Create new cluster
                            var cluster = new DuplicateCluster
                            {
                                ClusterId = Guid.NewGuid(),
                                PrimaryComplaintId = similar.First().ComplaintId,
                                CategoryId = newComplaint.CategoryId,
                                LocationLatitude = newComplaint.LocationLatitude,
                                LocationLongitude = newComplaint.LocationLongitude,
                                ClusterRadiusMeters = 200,
                                TotalComplaintsMerged = similar.Count + 1,
                                //TotalCombinedUpvotes = similar.Sum(s => s.UpvoteCount ?? 0) + (newComplaint.UpvoteCount ?? 0),
                                CreatedAt = DateTime.Now,
                                UpdatedAt = DateTime.Now
                            };

                            dbContext.DuplicateClusters.Add(cluster);

                            // Add primary complaint
                            dbContext.DuplicateEntries.Add(new DuplicateEntry
                            {
                                EntryId = Guid.NewGuid(),
                                ClusterId = cluster.ClusterId,
                                ComplaintId = similar.First().ComplaintId,
                                SimilarityScore = 100,
                                SimilarityFactors = "{\"type\":\"primary\"}",
                                MergedAt = DateTime.Now
                            });

                            // Add other similar complaints
                            foreach (var dup in similar.Skip(1))
                            {
                                dup.IsDuplicate = true;
                                dup.MergedIntoComplaintId = similar.First().ComplaintId;

                                dbContext.DuplicateEntries.Add(new DuplicateEntry
                                {
                                    EntryId = Guid.NewGuid(),
                                    ClusterId = cluster.ClusterId,
                                    ComplaintId = dup.ComplaintId,
                                    SimilarityScore = CalculateSimilarityScore(similar.First(), dup),
                                    SimilarityFactors = "{\"auto_detected\":true}",
                                    MergedAt = DateTime.Now
                                });
                            }

                            // Add new complaint
                            dbContext.DuplicateEntries.Add(new DuplicateEntry
                            {
                                EntryId = Guid.NewGuid(),
                                ClusterId = cluster.ClusterId,
                                ComplaintId = newComplaint.ComplaintId,
                                SimilarityScore = CalculateSimilarityScore(similar.First(), newComplaint),
                                SimilarityFactors = "{\"auto_detected\":true}",
                                MergedAt = DateTime.Now
                            });

                            newComplaint.IsDuplicate = true;
                            newComplaint.MergedIntoComplaintId = similar.First().ComplaintId;
                        }

                        dbContext.SaveChanges();

                        // Notify admins
                        NotifyAdminsOfDuplicates(dbContext, newComplaint.ComplaintId);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Duplicate detection error: {ex.Message}");
                }
            });
        }

        // =====================================================
        // NOTIFICATION HELPER
        // =====================================================
        private void NotifyAdminsOfDuplicates(CCMWDbContext dbContext, Guid complaintId)
        {
            try
            {
                var admins = dbContext.Users
                    .Where(u => u.UserType == "System_Admin" || u.UserType == "Department_Admin")
                    .ToList();

                foreach (var admin in admins)
                {
                    dbContext.Notifications.Add(new Notification
                    {
                        NotificationId = Guid.NewGuid(),
                        UserId = admin.UserId,
                        NotificationType = "Duplicate_Detected",
                        Title = "Duplicate Complaints Found",
                        Message = "New complaint matches existing complaints. Review duplicates.",
                        ReferenceType = "Complaint",
                        ReferenceId = complaintId,
                        CreatedAt = DateTime.Now
                    });
                }

                dbContext.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Notification error: {ex.Message}");
            }
        }

        // =====================================================
        // SIMILARITY CALCULATION HELPER
        // =====================================================
        private decimal CalculateSimilarityScore(Complaint c1, Complaint c2)
        {
            decimal score = 0;

            double distance = CalculateDistance(
                (double)c1.LocationLatitude, (double)c1.LocationLongitude,
                (double)c2.LocationLatitude, (double)c2.LocationLongitude);

            if (distance <= 0.1)
                score += 40;
            else if (distance <= 0.2)
                score += 30;
            else if (distance <= 0.5)
                score += 20;

            double daysDiff = Math.Abs((c1.CreatedAt - c2.CreatedAt).TotalDays);
            if (daysDiff <= 1)
                score += 30;
            else if (daysDiff <= 3)
                score += 20;
            else if (daysDiff <= 7)
                score += 10;

            score += 20;

            if (!string.IsNullOrEmpty(c1.Title) && !string.IsNullOrEmpty(c2.Title))
            {
                var words1 = c1.Title.ToLower().Split(' ');
                var words2 = c2.Title.ToLower().Split(' ');
                var common = words1.Intersect(words2).Count();
                var total = words1.Union(words2).Count();

                if (total > 0)
                    score += (decimal)((double)common / total * 10);
            }

            return Math.Min(score, 100);
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

        // =====================================================
        // EXISTING METHODS (KEPT EXACTLY AS IS)
        // =====================================================

        [HttpGet]
        [Route("{complaintId:guid}/view")]
        public IHttpActionResult ViewComplaint(Guid complaintId)
        {
            try
            {
                var complaint = db.Complaints
                    .FirstOrDefault(c => c.ComplaintId == complaintId);

                if (complaint == null)
                    return NotFound();

                complaint.ViewCount += 1;
                db.SaveChanges();

                var result = new
                {
                    complaint.ComplaintId,
                    complaint.ComplaintNumber,
                    complaint.Title,
                    complaint.Description,
                    CurrentStatus = complaint.CurrentStatus.ToString(),
                    complaint.Priority,
                    complaint.CreatedAt,
                    complaint.LocationAddress,
                    complaint.UpvoteCount,
                    complaint.ViewCount,
                    CitizenId = complaint.CitizenId,
                    DepartmentId = complaint.DepartmentId,
                    ZoneId = complaint.ZoneId,
                    CategoryId = complaint.CategoryId,
                    Photos = complaint.ComplaintPhotos.Select(p => new
                    {
                        p.PhotoId,
                        p.PhotoUrl,
                        p.PhotoType
                    }).ToList()
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpGet]
        [Route("user/{userId:guid}")]
        public IHttpActionResult GetComplaintsByUser(Guid userId)
        {
            try
            {
                var complaints = db.Complaints
                    .Where(c => c.CitizenId == userId || c.AssignedToId == userId)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToList();

                var result = complaints.Select(c => new
                {
                    c.ComplaintId,
                    c.ComplaintNumber,
                    c.Title,
                    c.Description,
                    CurrentStatus = c.CurrentStatus.ToString(),
                    SubmissionStatus = c.SubmissionStatus.ToString(),
                    c.CreatedAt,
                    c.UpdatedAt
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpPost]
        [Route("{complaintId:guid}/assign")]
        public IHttpActionResult AssignComplaint(Guid complaintId, [FromBody] ComplaintAssignment assignment)
        {
            try
            {
                if (assignment == null)
                    return BadRequest("Assignment data is required.");

                var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == complaintId);
                if (complaint == null) return NotFound();

                assignment.AssignmentId = Guid.NewGuid();
                assignment.ComplaintId = complaintId;
                assignment.AssignedAt = DateTime.Now;
                assignment.IsActive = true;

                db.ComplaintAssignments.Add(assignment);

                var oldStatus = complaint.CurrentStatus.ToString();

                complaint.AssignedToId = assignment.AssignedToId;
                complaint.AssignedAt = DateTime.Now;
                complaint.CurrentStatus = ComplaintStatus.Assigned;
                complaint.UpdatedAt = DateTime.Now;

                db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
                {
                    HistoryId = Guid.NewGuid(),
                    ComplaintId = complaintId,
                    PreviousStatus = oldStatus,
                    NewStatus = complaint.CurrentStatus.ToString(),
                    ChangedById = assignment.AssignedById ?? Guid.Empty,
                    ChangedAt = DateTime.Now
                });

                db.SaveChanges();
                return Ok("Complaint assigned successfully");
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpPut]
        [Route("{complaintId:guid}/update")]
        public IHttpActionResult UpdateComplaint(Guid complaintId, [FromBody] Complaint updated)
        {
            try
            {
                if (updated == null)
                    return BadRequest("Updated complaint data is required.");

                var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == complaintId);
                if (complaint == null) return NotFound();

                complaint.Title = updated.Title;
                complaint.Description = updated.Description;
                complaint.Priority = updated.Priority;
                complaint.UpdatedAt = DateTime.Now;

                db.SaveChanges();
                return Ok("Complaint updated successfully");
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpDelete]
        [Route("{complaintId:guid}/delete")]
        public IHttpActionResult DeleteComplaint(Guid complaintId)
        {
            try
            {
                var complaint = db.Complaints
                    .Include(c => c.ComplaintPhotos)
                    .Include(c => c.Assignments)
                    .Include(c => c.StatusHistory)
                    .FirstOrDefault(c => c.ComplaintId == complaintId);

                if (complaint == null) return NotFound();

                db.Complaints.Remove(complaint);
                db.SaveChanges();

                return Ok("Complaint deleted successfully");
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpPut]
        [Route("{complaintId:guid}/status")]
        public IHttpActionResult UpdateStatus(
    Guid complaintId,
    [FromUri] string newStatus,
    [FromUri] Guid userId)
        {
            try
            {
                var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == complaintId);
                if (complaint == null) return NotFound("Complaint not found");

                if (!Enum.TryParse(newStatus, true, out ComplaintStatus statusEnum))
                    return BadRequest("Invalid status value.");

                var oldStatus = complaint.CurrentStatus.ToString();
                var oldSubmissionStatus = complaint.SubmissionStatus;

                complaint.CurrentStatus = statusEnum;
                complaint.StatusUpdatedAt = DateTime.Now;
                complaint.ApprovedById = userId;
                complaint.UpdatedAt = DateTime.Now;

                // ALSO UPDATE SUBMISSION STATUS when approving/rejecting
                if (statusEnum == ComplaintStatus.Approved)
                {
                    complaint.SubmissionStatus = SubmissionStatus.Approved;
                }
                else if (statusEnum == ComplaintStatus.Rejected)
                {
                    complaint.SubmissionStatus = SubmissionStatus.Rejected;
                }

                if (statusEnum == ComplaintStatus.Resolved)
                    complaint.ResolvedAt = DateTime.Now;

                db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
                {
                    HistoryId = Guid.NewGuid(),
                    ComplaintId = complaintId,
                    PreviousStatus = oldStatus,
                    NewStatus = statusEnum.ToString(),
                    ChangedById = userId,
                    ChangedAt = DateTime.Now
                });

                db.SaveChanges();
                return Ok(new { Message = "Status updated successfully", CurrentStatus = statusEnum.ToString(), SubmissionStatus = complaint.SubmissionStatus });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // Add to ComplaintsController.cs

        /// <summary>
        /// Get all complaints with optional filtering
        /// </summary>
        [HttpGet]
        [Route("")]
        public IHttpActionResult GetComplaints(
            [FromUri] int page = 1,
            [FromUri] int pageSize = 20,
            [FromUri] string status = null,
            [FromUri] Guid? zoneId = null,
            [FromUri] Guid? categoryId = null,
            [FromUri] Guid? departmentId = null,
            [FromUri] Guid? citizenId = null,
            [FromUri] bool? isAssigned = null)
        {
            try
            {
                var query = db.Complaints
                    .Include(c => c.Category)
                    .Include(c => c.Zone)
                    .Include(c => c.Department)
                    .Include(c => c.Citizen)
                    .AsQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(status) && Enum.TryParse<ComplaintStatus>(status, true, out var complaintStatus))
                {
                    query = query.Where(c => c.CurrentStatus == complaintStatus);
                }

                if (zoneId.HasValue)
                {
                    query = query.Where(c => c.ZoneId == zoneId.Value);
                }

                if (categoryId.HasValue)
                {
                    query = query.Where(c => c.CategoryId == categoryId.Value);
                }

                if (departmentId.HasValue)
                {
                    query = query.Where(c => c.DepartmentId == departmentId.Value);
                }

                if (citizenId.HasValue)
                {
                    query = query.Where(c => c.CitizenId == citizenId.Value);
                }

                if (isAssigned.HasValue)
                {
                    if (isAssigned.Value)
                        query = query.Where(c => c.AssignedToId != null);
                    else
                        query = query.Where(c => c.AssignedToId == null);
                }

                // Get total count for pagination
                var totalCount = query.Count();

                // Apply pagination
                var complaints = query
                    .OrderByDescending(c => c.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList()
                    .Select(c => new
                    {
                        c.ComplaintId,
                        c.ComplaintNumber,
                        c.Title,
                        c.Description,
                        c.LocationAddress,
                        c.Priority,
                        c.UpvoteCount,
                        c.ViewCount,
                        c.CreatedAt,
                        CurrentStatus = c.CurrentStatus.ToString(),
                        SubmissionStatus = c.SubmissionStatus.ToString(),
                        Category = c.Category != null ? new { c.Category.CategoryId, c.Category.CategoryName } : null,
                        Zone = c.Zone != null ? new { c.Zone.ZoneId, c.Zone.ZoneName } : null,
                        Department = c.Department != null ? new { c.Department.DepartmentId, c.Department.DepartmentName } : null,
                        Citizen = c.Citizen != null ? new { c.Citizen.UserId, c.Citizen.FullName } : null,
                        IsAssigned = c.AssignedToId != null
                    })
                    .ToList();

                return Ok(new
                {
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                    Complaints = complaints
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
        [HttpGet]
        [Route("debug/database")]
        public IHttpActionResult DebugDatabase()
        {
            try
            {
                var result = new System.Collections.Generic.Dictionary<string, object>();

                // 1. Check if we can connect
                result["Database_Connected"] = db.Database.Connection.State.ToString();

                // 2. Count records in main tables
                result["Users_Count"] = db.Users.Count();
                result["Complaints_Count"] = db.Complaints.Count();
                result["Departments_Count"] = db.Departments.Count();
                result["Zones_Count"] = db.Zones.Count();

                // 3. Check complaints with relationships
                result["Complaints_WithCategory"] = db.Complaints.Count(c => c.Category != null);
                result["Complaints_WithZone"] = db.Complaints.Count(c => c.Zone != null);
                result["Complaints_WithDepartment"] = db.Complaints.Count(c => c.Department != null);

                // 4. Check status distribution
                result["Complaints_Status_Approved"] = db.Complaints.Count(c => c.CurrentStatus == ComplaintStatus.Approved);
                result["Complaints_Status_Submitted"] = db.Complaints.Count(c => c.CurrentStatus == ComplaintStatus.Submitted);
                result["Complaints_Status_Resolved"] = db.Complaints.Count(c => c.CurrentStatus == ComplaintStatus.Resolved);

                // 5. Get first complaint as sample
                var firstComplaint = db.Complaints
                    .Include(c => c.Category)
                    .Include(c => c.Zone)
                    .Include(c => c.Department)
                    .FirstOrDefault();

                if (firstComplaint != null)
                {
                    result["Sample_Complaint"] = new
                    {
                        firstComplaint.ComplaintId,
                        firstComplaint.ComplaintNumber,
                        firstComplaint.Title,
                        firstComplaint.CurrentStatus,
                        HasCategory = firstComplaint.Category != null,
                        CategoryName = firstComplaint.Category?.CategoryName,
                        HasZone = firstComplaint.Zone != null,
                        ZoneName = firstComplaint.Zone?.ZoneName,
                        HasDepartment = firstComplaint.Department != null,
                        DepartmentName = firstComplaint.Department?.DepartmentName
                    };
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return Ok(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
        private IHttpActionResult NotFound(string message = null)
        {
            if (string.IsNullOrEmpty(message))
                return NotFound();
            return Content(HttpStatusCode.NotFound, new { error = message });
        }
    }
}