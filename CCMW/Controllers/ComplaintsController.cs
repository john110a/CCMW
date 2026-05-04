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
        // SUBMIT COMPLAINT WITH AUTO-DUPLICATE DETECTION
        // =====================================================
        [HttpPost]
        [Route("submit")]
        public IHttpActionResult SubmitComplaint([FromBody] Complaint complaint)
        {
            try
            {
                if (complaint == null)
                    return BadRequest("Complaint data is required.");

                if (complaint.CitizenId == null || complaint.CitizenId == Guid.Empty)
                    return BadRequest("CitizenId is required");

                if (complaint.CategoryId == null || complaint.CategoryId == Guid.Empty)
                    return BadRequest("CategoryId is required");

                var category = db.ComplaintCategories.Find(complaint.CategoryId);
                if (category == null)
                    return BadRequest($"Category with ID {complaint.CategoryId} not found");

                complaint.ComplaintId = Guid.NewGuid();
                complaint.DepartmentId = category.DepartmentId;

                if (string.IsNullOrEmpty(complaint.ComplaintNumber))
                {
                    complaint.ComplaintNumber =
                        $"CCMW-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 4)}";
                }

                if (string.IsNullOrEmpty(complaint.Priority))
                    complaint.Priority = "Medium";

                complaint.CurrentStatus = ComplaintStatus.Submitted;
                complaint.SubmissionStatus = SubmissionStatus.PendingApproval;
                complaint.CreatedAt = DateTime.Now;
                complaint.UpdatedAt = DateTime.Now;
                complaint.UpvoteCount = 0;
                complaint.ViewCount = 0;
                complaint.IsDuplicate = false;
                complaint.IsOverdue = false;
                complaint.ComplaintPhotos = null;

                db.Complaints.Add(complaint);

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
        // GET ALL COMPLAINTS - FIXED WITH LEFT JOIN
        // Shows ALL complaints even if Zone/Category/Department
        // is null (e.g. newly submitted complaints from app)
        // =====================================================
        [HttpGet]
        [Route("")]
        public IHttpActionResult GetComplaints(
            [FromUri] int page = 1,
            [FromUri] int pageSize = 100,
            [FromUri] string status = null,
            [FromUri] Guid? zoneId = null,
            [FromUri] Guid? categoryId = null,
            [FromUri] Guid? departmentId = null,
            [FromUri] Guid? citizenId = null,
            [FromUri] bool? isAssigned = null)
        {
            try
            {
                Guid currentUserId = GetCurrentUserIdFromRequest();
                var currentUser = db.Users.FirstOrDefault(u => u.UserId == currentUserId);
                bool isSystemAdmin = currentUser?.UserType == "System_Admin";

                System.Diagnostics.Debug.WriteLine($"User: {currentUser?.Email}, Type: {currentUser?.UserType}, IsAdmin: {isSystemAdmin}");

                // =====================================================
                // LEFT JOIN query - complaints with null Zone/Category/
                // Department will still appear (unlike .Include() which
                // uses INNER JOIN and silently drops them)
                // =====================================================
                var query = from c in db.Complaints
                            join cat in db.ComplaintCategories
                                on c.CategoryId equals cat.CategoryId into catGroup
                            from cat in catGroup.DefaultIfEmpty()        // LEFT JOIN

                            join z in db.Zones
                                on c.ZoneId equals z.ZoneId into zoneGroup
                            from z in zoneGroup.DefaultIfEmpty()         // LEFT JOIN

                            join dept in db.Departments
                                on c.DepartmentId equals dept.DepartmentId into deptGroup
                            from dept in deptGroup.DefaultIfEmpty()      // LEFT JOIN

                            join citizen in db.Users
                                on c.CitizenId equals citizen.UserId into citizenGroup
                            from citizen in citizenGroup.DefaultIfEmpty() // LEFT JOIN

                            select new
                            {
                                c.ComplaintId,
                                c.ComplaintNumber,
                                c.Title,
                                c.Description,
                                c.LocationAddress,
                                c.LocationLatitude,
                                c.LocationLongitude,
                                c.Priority,
                                c.UpvoteCount,
                                c.ViewCount,
                                c.CreatedAt,
                                c.CurrentStatus,
                                c.SubmissionStatus,
                                c.AssignedToId,
                                c.DepartmentId,
                                c.ZoneId,
                                c.CategoryId,
                                c.CitizenId,
                                Category = cat == null ? null : new
                                {
                                    cat.CategoryId,
                                    cat.CategoryName
                                },
                                Zone = z == null ? null : new
                                {
                                    z.ZoneId,
                                    z.ZoneName
                                },
                                Department = dept == null ? null : new
                                {
                                    dept.DepartmentId,
                                    dept.DepartmentName
                                },
                                Citizen = citizen == null ? null : new
                                {
                                    citizen.UserId,
                                    citizen.FullName
                                },
                                IsAssigned = c.AssignedToId != null
                            };

                // =====================================================
                // DEPARTMENT FILTER
                // System Admin sees everything; others see only their dept
                // =====================================================
                if (isSystemAdmin)
                {
                    System.Diagnostics.Debug.WriteLine("System Admin - NO department filter - showing ALL complaints");
                }
                else
                {
                    var staffProfile = db.StaffProfiles.FirstOrDefault(s => s.UserId == currentUserId);
                    Guid? userDepartmentId = staffProfile?.DepartmentId;

                    if (departmentId.HasValue)
                    {
                        query = query.Where(c => c.DepartmentId == departmentId.Value);
                    }
                    else if (userDepartmentId.HasValue)
                    {
                        query = query.Where(c => c.DepartmentId == userDepartmentId.Value);
                    }

                    System.Diagnostics.Debug.WriteLine($"Non-admin - Filtering by department: {userDepartmentId}");
                }

                // =====================================================
                // STATUS FILTER - only applied when status param provided
                // =====================================================
                if (!string.IsNullOrEmpty(status))
                {
                    if (Enum.TryParse<ComplaintStatus>(status, true, out var complaintStatus))
                    {
                        query = query.Where(c => c.CurrentStatus == complaintStatus);
                        System.Diagnostics.Debug.WriteLine($"Filtering by status: {status}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No status filter - returning ALL complaints");
                }

                // =====================================================
                // OTHER FILTERS
                // =====================================================
                if (zoneId.HasValue)
                    query = query.Where(c => c.ZoneId == zoneId.Value);

                if (categoryId.HasValue)
                    query = query.Where(c => c.CategoryId == categoryId.Value);

                if (citizenId.HasValue)
                    query = query.Where(c => c.CitizenId == citizenId.Value);

                if (isAssigned.HasValue)
                {
                    if (isAssigned.Value)
                        query = query.Where(c => c.AssignedToId != null);
                    else
                        query = query.Where(c => c.AssignedToId == null);
                }

                var totalCount = query.Count();
                System.Diagnostics.Debug.WriteLine($"Total complaints found: {totalCount}");

                var complaints = query
                    .OrderByDescending(c => c.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = complaints.Select(c => new
                {
                    c.ComplaintId,
                    c.ComplaintNumber,
                    c.Title,
                    c.Description,
                    c.LocationAddress,
                    c.LocationLatitude,
                    c.LocationLongitude,
                    c.Priority,
                    c.UpvoteCount,
                    c.ViewCount,
                    c.CreatedAt,
                    CurrentStatus = c.CurrentStatus.ToString(),
                    SubmissionStatus = c.SubmissionStatus.ToString(),
                    Category = c.Category,
                    Zone = c.Zone,
                    Department = c.Department,
                    Citizen = c.Citizen,
                    c.IsAssigned,
                    // Flag so Flutter can show warning badge
                    HasZone = c.ZoneId != null
                }).ToList();

                System.Diagnostics.Debug.WriteLine($"Returning {result.Count} complaints");

                var statusBreakdown = result.GroupBy(r => r.CurrentStatus)
                    .Select(g => $"{g.Key}: {g.Count()}");
                System.Diagnostics.Debug.WriteLine($"Status breakdown: {string.Join(", ", statusBreakdown)}");

                return Ok(new
                {
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                    Complaints = result
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // GET MAP COMPLAINTS
        // Strict: only complaints with valid zone + coordinates
        // + approved/assigned/in-progress status
        // This keeps the map clean and accurate
        // =====================================================
        [HttpGet]
        [Route("map")]
        public IHttpActionResult GetMapComplaints(
            [FromUri] double lat,
            [FromUri] double lng,
            [FromUri] double radiusKm = 5,
            [FromUri] Guid? categoryId = null,
            [FromUri] Guid? zoneId = null)
        {
            try
            {
                // Only pull complaints that are ready for the map:
                // - must have a zone
                // - must have GPS coordinates
                // - must be at least Approved (status >= 2), i.e. not just Submitted/Pending
                var complaints = db.Complaints
                    .Include(c => c.Category)
                    .Include(c => c.Zone)
                    .Include(c => c.Department)
                    .Where(c => c.ZoneId != null)
                    .Where(c => c.LocationLatitude != null)
                    .Where(c => c.LocationLongitude != null)
                    .Where(c => c.CurrentStatus >= ComplaintStatus.Approved)
                    .ToList()
                    // Haversine distance filter (done in memory after DB fetch)
                    .Where(c => CalculateDistance(
                        lat, lng,
                        (double)c.LocationLatitude,
                        (double)c.LocationLongitude) <= radiusKm)
                    .ToList();

                if (categoryId.HasValue)
                    complaints = complaints.Where(c => c.CategoryId == categoryId.Value).ToList();

                if (zoneId.HasValue)
                    complaints = complaints.Where(c => c.ZoneId == zoneId.Value).ToList();

                var result = complaints.Select(c => new
                {
                    c.ComplaintId,
                    c.ComplaintNumber,
                    c.Title,
                    c.Description,
                    c.Priority,
                    CurrentStatus = c.CurrentStatus.ToString(),
                    Latitude = c.LocationLatitude,
                    Longitude = c.LocationLongitude,
                    c.LocationAddress,
                    c.UpvoteCount,
                    c.CreatedAt,
                    Category = c.Category != null
                        ? new { c.Category.CategoryId, c.Category.CategoryName }
                        : null,
                    Zone = c.Zone != null
                        ? new { c.Zone.ZoneId, c.Zone.ZoneName }
                        : null,
                    Department = c.Department != null
                        ? new { c.Department.DepartmentId, c.Department.DepartmentName }
                        : null
                }).ToList();

                System.Diagnostics.Debug.WriteLine($"Map complaints returned: {result.Count}");

                return Ok(new
                {
                    TotalCount = result.Count,
                    data = result
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Map error: {ex.Message}");
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // VIEW COMPLAINT (increments view count)
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

        // =====================================================
        // GET COMPLAINTS BY USER
        // =====================================================
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

        // =====================================================
        // ASSIGN COMPLAINT
        // =====================================================
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

        // =====================================================
        // UPDATE COMPLAINT
        // =====================================================
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

        // =====================================================
        // DELETE COMPLAINT
        // =====================================================
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

        // =====================================================
        // UPDATE STATUS
        // =====================================================
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

                complaint.CurrentStatus = statusEnum;
                complaint.StatusUpdatedAt = DateTime.Now;
                complaint.ApprovedById = userId;
                complaint.UpdatedAt = DateTime.Now;

                if (statusEnum == ComplaintStatus.Approved)
                    complaint.SubmissionStatus = SubmissionStatus.Approved;
                else if (statusEnum == ComplaintStatus.Rejected)
                    complaint.SubmissionStatus = SubmissionStatus.Rejected;

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

                return Ok(new
                {
                    Message = "Status updated successfully",
                    CurrentStatus = statusEnum.ToString(),
                    SubmissionStatus = complaint.SubmissionStatus
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // DEBUG ENDPOINTS
        // =====================================================
        [HttpGet]
        [Route("debug/database")]
        public IHttpActionResult DebugDatabase()
        {
            try
            {
                var result = new System.Collections.Generic.Dictionary<string, object>();

                result["Database_Connected"] = db.Database.Connection.State.ToString();
                result["Users_Count"] = db.Users.Count();
                result["Complaints_Count"] = db.Complaints.Count();
                result["Departments_Count"] = db.Departments.Count();
                result["Zones_Count"] = db.Zones.Count();
                result["Complaints_WithCategory"] = db.Complaints.Count(c => c.Category != null);
                result["Complaints_WithZone"] = db.Complaints.Count(c => c.Zone != null);
                result["Complaints_WithDepartment"] = db.Complaints.Count(c => c.Department != null);
                result["Complaints_WithNullZone"] = db.Complaints.Count(c => c.ZoneId == null);
                result["Complaints_Status_Approved"] = db.Complaints.Count(c => c.CurrentStatus == ComplaintStatus.Approved);
                result["Complaints_Status_Submitted"] = db.Complaints.Count(c => c.CurrentStatus == ComplaintStatus.Submitted);
                result["Complaints_Status_Resolved"] = db.Complaints.Count(c => c.CurrentStatus == ComplaintStatus.Resolved);

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

        [HttpGet]
        [Route("debug/all-statuses")]
        public IHttpActionResult DebugAllStatuses()
        {
            try
            {
                var allComplaints = db.Complaints.ToList();

                var statusBreakdown = allComplaints
                    .GroupBy(c => c.CurrentStatus)
                    .Select(g => new
                    {
                        Status = g.Key.ToString(),
                        StatusName = ((ComplaintStatus)g.Key).ToString(),
                        Count = g.Count(),
                        Complaints = g.Select(c => new
                        {
                            c.ComplaintId,
                            c.ComplaintNumber,
                            c.Title,
                            c.CurrentStatus,
                            HasZone = c.ZoneId != null
                        }).ToList()
                    })
                    .ToList();

                return Ok(new
                {
                    TotalInDatabase = allComplaints.Count,
                    StatusBreakdown = statusBreakdown,
                    AllComplaints = allComplaints.Select(c => new
                    {
                        c.ComplaintId,
                        c.ComplaintNumber,
                        c.Title,
                        CurrentStatus = (int)c.CurrentStatus,
                        StatusName = c.CurrentStatus.ToString(),
                        c.CreatedAt,
                        HasZone = c.ZoneId != null
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // BACKGROUND DUPLICATE DETECTION
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

                        // Skip duplicate detection if no coordinates
                        if (newComplaint.LocationLatitude == null || newComplaint.LocationLongitude == null)
                            return;

                        var similar = dbContext.Complaints
                            .Where(c => c.ComplaintId != newComplaintId)
                            .Where(c => c.CategoryId == newComplaint.CategoryId)
                            .Where(c => !c.IsDuplicate && c.MergedIntoComplaintId == null)
                            .Where(c => c.LocationLatitude != null && c.LocationLongitude != null)
                            .ToList()
                            .Where(c => CalculateDistance(
                                (double)c.LocationLatitude, (double)c.LocationLongitude,
                                (double)newComplaint.LocationLatitude, (double)newComplaint.LocationLongitude) < 0.2)
                            .ToList();

                        if (!similar.Any()) return;

                        var existingCluster = dbContext.DuplicateClusters
                            .FirstOrDefault(cl => cl.PrimaryComplaintId == similar.First().ComplaintId);

                        if (existingCluster != null)
                        {
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
                            existingCluster.UpdatedAt = DateTime.Now;

                            newComplaint.IsDuplicate = true;
                            newComplaint.MergedIntoComplaintId = existingCluster.PrimaryComplaintId;
                        }
                        else
                        {
                            var cluster = new DuplicateCluster
                            {
                                ClusterId = Guid.NewGuid(),
                                PrimaryComplaintId = similar.First().ComplaintId,
                                CategoryId = newComplaint.CategoryId,
                                LocationLatitude = newComplaint.LocationLatitude,
                                LocationLongitude = newComplaint.LocationLongitude,
                                ClusterRadiusMeters = 200,
                                TotalComplaintsMerged = similar.Count + 1,
                                CreatedAt = DateTime.Now,
                                UpdatedAt = DateTime.Now
                            };

                            dbContext.DuplicateClusters.Add(cluster);

                            dbContext.DuplicateEntries.Add(new DuplicateEntry
                            {
                                EntryId = Guid.NewGuid(),
                                ClusterId = cluster.ClusterId,
                                ComplaintId = similar.First().ComplaintId,
                                SimilarityScore = 100,
                                SimilarityFactors = "{\"type\":\"primary\"}",
                                MergedAt = DateTime.Now
                            });

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
        // HELPERS
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

        private decimal CalculateSimilarityScore(Complaint c1, Complaint c2)
        {
            decimal score = 0;

            double distance = CalculateDistance(
                (double)c1.LocationLatitude, (double)c1.LocationLongitude,
                (double)c2.LocationLatitude, (double)c2.LocationLongitude);

            if (distance <= 0.1) score += 40;
            else if (distance <= 0.2) score += 30;
            else if (distance <= 0.5) score += 20;

            double daysDiff = Math.Abs((c1.CreatedAt - c2.CreatedAt).TotalDays);
            if (daysDiff <= 1) score += 30;
            else if (daysDiff <= 3) score += 20;
            else if (daysDiff <= 7) score += 10;

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

        private Guid GetCurrentUserIdFromRequest()
        {
            var systemAdmin = db.Users.FirstOrDefault(u => u.Email == "admin@ccmw.gov.pk");
            if (systemAdmin != null)
                return systemAdmin.UserId;

            return Guid.Parse("5b18d046-e0f3-4e90-a36f-d299b563a8e6");
        }

        private Guid GetCurrentUserId()
        {
            if (System.Web.HttpContext.Current != null && System.Web.HttpContext.Current.User != null)
            {
                var identity = System.Web.HttpContext.Current.User.Identity;
                var user = db.Users.FirstOrDefault(u => u.Email == identity.Name);
                if (user != null) return user.UserId;
            }

            return Guid.Parse("5b18d046-e0f3-4e90-a36f-d299b563a8e6");
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