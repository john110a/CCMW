using CCMW.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
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
                complaint.IsFake = false;
                complaint.ComplaintPhotos = null;

                // =====================================================
                // ADDED: AUTO-DETECT ZONE BASED ON LOCATION COORDINATES
                // =====================================================
                if (complaint.LocationLatitude != 0 && complaint.LocationLongitude != 0)
                {
                    var detectedZoneId = DetectZoneByLocation(
                        (double)complaint.LocationLatitude,
                        (double)complaint.LocationLongitude
                    );

                    if (detectedZoneId.HasValue)
                    {
                        complaint.ZoneId = detectedZoneId.Value;
                        System.Diagnostics.Debug.WriteLine($"✅ Zone auto-detected: {detectedZoneId.Value}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("⚠️ No zone found for this location");
                    }
                }

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

                // ADDED: Send notification for FILED event
                db.Database.ExecuteSqlCommand(
                    "EXEC sp_NotifyComplaintFlow @ComplaintId, @EventType",
                    new SqlParameter("@ComplaintId", complaint.ComplaintId),
                    new SqlParameter("@EventType", "FILED")
                );

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

                var query = from c in db.Complaints

                            join cat in db.ComplaintCategories
                                on c.CategoryId equals cat.CategoryId into catGroup
                            from cat in catGroup.DefaultIfEmpty()
                            join z in db.Zones
                                on c.ZoneId equals z.ZoneId into zoneGroup
                            from z in zoneGroup.DefaultIfEmpty()
                            join dept in db.Departments
                                on c.DepartmentId equals dept.DepartmentId into deptGroup
                            from dept in deptGroup.DefaultIfEmpty()
                            join citizen in db.Users
                                on c.CitizenId equals citizen.UserId into citizenGroup
                            from citizen in citizenGroup.DefaultIfEmpty()
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
                                c.IsFake,
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

                if (!string.IsNullOrEmpty(status))
                {
                    if (Enum.TryParse<ComplaintStatus>(status, true, out var complaintStatus))
                    {
                        query = query.Where(c => c.CurrentStatus == complaintStatus);
                        System.Diagnostics.Debug.WriteLine($"Filtering by status: {status}");
                    }
                }

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
                    c.IsFake,
                    HasZone = c.ZoneId != null
                }).ToList();

                System.Diagnostics.Debug.WriteLine($"Returning {result.Count} complaints");

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
                var complaints = db.Complaints
                    .Include(c => c.Category)
                    .Include(c => c.Zone)
                    .Include(c => c.Department)
                    .Where(c => c.ZoneId != null)
                    .Where(c => c.LocationLatitude != null)
                    .Where(c => c.LocationLongitude != null)
                    .Where(c => c.CurrentStatus >= ComplaintStatus.Approved)
                    .Where(c => c.IsFake != true)
                    .ToList()
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
                    complaint.IsFake,
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
                    c.UpdatedAt,
                    c.IsFake
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

                // ADDED: Send notification for ASSIGNED event
                db.Database.ExecuteSqlCommand(
                    "EXEC sp_NotifyComplaintFlow @ComplaintId, @EventType",
                    new SqlParameter("@ComplaintId", complaintId),
                    new SqlParameter("@EventType", "ASSIGNED")
                );

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
        // FAKE COMPLAINT MANAGEMENT
        // =====================================================

        /// Mark complaint as fake and apply strike to citizen
        [HttpPost]
        [Route("{complaintId:guid}/mark-fake")]
        public IHttpActionResult MarkAsFake(Guid complaintId, [FromBody] FakeMarkRequest request)
        {
            try
            {
                if (request == null || request.AdminId == Guid.Empty)
                    return BadRequest("Admin ID is required");

                var complaint = db.Complaints.Find(complaintId);
                if (complaint == null)
                    return NotFoundMessage("Complaint not found");

                var citizen = db.Users.Find(complaint.CitizenId);
                if (citizen == null)
                    return NotFoundMessage("Citizen not found");

                if (complaint.IsFake == true)
                    return BadRequest("Complaint already marked as fake");

                // Mark complaint as fake
                complaint.IsFake = true;
                complaint.FakeVerifiedBy = request.AdminId;
                complaint.FakeVerifiedAt = DateTime.Now;
                complaint.CurrentStatus = ComplaintStatus.Rejected;
                complaint.StatusUpdatedAt = DateTime.Now;

                // Increase strike count
                int currentStrikes = citizen.FakeStrikes ?? 0;
                int newStrikes = currentStrikes + 1;
                citizen.FakeStrikes = newStrikes;

                string actionTaken = "Warning";
                DateTime? banUntil = null;
                string message = "";

                // Apply penalty based on strike count
                if (newStrikes >= 3)
                {
                    citizen.IsBanned = true;
                    citizen.IsActive = false;
                    actionTaken = "PermanentBan";
                    message = "Account permanently banned due to 3 fake complaints";
                }
                else if (newStrikes == 2)
                {
                    citizen.IsBanned = true;
                    banUntil = DateTime.Now.AddDays(7);
                    citizen.BanExpiryDate = banUntil;
                    actionTaken = "TempBan";
                    message = "Account banned for 7 days. One more fake complaint = permanent ban";
                }
                else
                {
                    actionTaken = "Warning";
                    message = $"Warning: Fake complaint detected. {3 - newStrikes} more strike(s) = account ban";
                }

                // Log fake complaint
                var log = new FakeComplaintLog
                {
                    LogId = Guid.NewGuid(),
                    ComplaintId = complaintId,
                    CitizenId = citizen.UserId,
                    StrikeNumber = newStrikes,
                    ActionTaken = actionTaken,
                    BannedUntil = banUntil,
                    CreatedAt = DateTime.Now
                };
                db.FakeComplaintLogs.Add(log);

                // Add status history
                var history = new ComplaintStatusHistories
                {
                    HistoryId = Guid.NewGuid(),
                    ComplaintId = complaint.ComplaintId,
                    PreviousStatus = complaint.CurrentStatus.ToString(),
                    NewStatus = "MarkedAsFake",
                    ChangedById = request.AdminId,
                    ChangedAt = DateTime.Now,
                    Notes = $"Marked as fake by admin. Strike #{newStrikes}. {message}"
                };
                db.ComplaintStatusHistories.Add(history);

                db.SaveChanges();

                // ADDED: Send notification for FAKE event
                db.Database.ExecuteSqlCommand(
                    "EXEC sp_NotifyComplaintFlow @ComplaintId, @EventType",
                    new SqlParameter("@ComplaintId", complaintId),
                    new SqlParameter("@EventType", "FAKE")
                );

                return Ok(new
                {
                    success = true,
                    strikes = newStrikes,
                    isBanned = citizen.IsBanned,
                    banExpiryDate = citizen.BanExpiryDate,
                    message = message,
                    actionTaken = actionTaken
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        /// Get citizen's strike info
        [HttpGet]
        [Route("citizen/{citizenId:guid}/strikes")]
        public IHttpActionResult GetCitizenStrikes(Guid citizenId)
        {
            try
            {
                var citizen = db.Users.FirstOrDefault(u => u.UserId == citizenId);
                if (citizen == null)
                    return NotFoundMessage("Citizen not found");

                int strikes = citizen.FakeStrikes ?? 0;
                bool isBanned = citizen.IsBanned ?? false;
                DateTime? banExpiry = citizen.BanExpiryDate;
                string message = "";

                if (isBanned && banExpiry.HasValue && banExpiry.Value > DateTime.Now)
                {
                    message = $"Account banned until {banExpiry.Value.ToLocalTime()}";
                }
                else if (isBanned && (!banExpiry.HasValue || banExpiry.Value <= DateTime.Now))
                {
                    message = "Account permanently banned";
                }
                else if (strikes == 1)
                {
                    message = $"⚠️ Warning: {3 - strikes} more fake complaint(s) = account ban";
                }
                else if (strikes == 2)
                {
                    message = $"⚠️ FINAL WARNING: 1 more fake complaint = permanent account ban!";
                }
                else
                {
                    message = "No strikes. Keep reporting genuine issues!";
                }

                return Ok(new
                {
                    strikes = strikes,
                    isBanned = isBanned,
                    banExpiryDate = banExpiry,
                    message = message
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        /// Get all fake complaints for admin review
        [HttpGet]
        [Route("fake-complaints")]
        public IHttpActionResult GetFakeComplaints([FromUri] int page = 1, [FromUri] int pageSize = 20)
        {
            try
            {
                var fakeComplaints = db.Complaints
                    .Where(c => c.IsFake == true)
                    .OrderByDescending(c => c.FakeVerifiedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new
                    {
                        c.ComplaintId,
                        c.ComplaintNumber,
                        c.Title,
                        c.Description,
                        c.CreatedAt,
                        c.FakeVerifiedAt,
                        CitizenName = c.Citizen.FullName,
                        CitizenEmail = c.Citizen.Email,
                        Strikes = c.Citizen.FakeStrikes,
                        IsBanned = c.Citizen.IsBanned,
                        c.IsFake
                    })
                    .ToList();

                int totalCount = db.Complaints.Count(c => c.IsFake == true);

                return Ok(new
                {
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    FakeComplaints = fakeComplaints
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        /// Get fake complaint log for a citizen
        [HttpGet]
        [Route("citizen/{citizenId:guid}/fake-logs")]
        public IHttpActionResult GetCitizenFakeLogs(Guid citizenId)
        {
            try
            {
                var logs = db.FakeComplaintLogs
                    .Where(l => l.CitizenId == citizenId)
                    .OrderByDescending(l => l.CreatedAt)
                    .Select(l => new
                    {
                        l.LogId,
                        l.ComplaintId,
                        ComplaintNumber = l.Complaint.ComplaintNumber,
                        l.StrikeNumber,
                        l.ActionTaken,
                        l.BannedUntil,
                        l.CreatedAt
                    })
                    .ToList();

                return Ok(logs);
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
                result["Fake_Complaints_Count"] = db.Complaints.Count(c => c.IsFake == true);
                result["Banned_Users_Count"] = db.Users.Count(u => u.IsBanned == true);

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
                        Count = g.Count(),
                        Complaints = g.Select(c => new
                        {
                            c.ComplaintId,
                            c.ComplaintNumber,
                            c.Title,
                            c.CurrentStatus,
                            c.IsFake,
                            HasZone = c.ZoneId != null
                        }).ToList()
                    })
                    .ToList();

                return Ok(new
                {
                    TotalInDatabase = allComplaints.Count,
                    StatusBreakdown = statusBreakdown
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // HELPERS
        // =====================================================

        // =====================================================
        // ADDED: AUTO-DETECT ZONE BASED ON LOCATION COORDINATES
        // =====================================================
        private Guid? DetectZoneByLocation(double lat, double lng)
        {
            try
            {
                // Get all active zones with center coordinates and is_active = true
                var zones = db.Zones
                    .Where(z => z.CenterLatitude.HasValue &&
                               z.CenterLongitude.HasValue &&
                               z.IsActive == true)
                    .ToList();

                if (!zones.Any())
                {
                    System.Diagnostics.Debug.WriteLine("No active zones with center coordinates found");
                    return null;
                }

                Guid? closestZone = null;
                double minDistance = double.MaxValue;

                foreach (var zone in zones)
                {
                    // Calculate distance from user location to zone center
                    double distance = CalculateDistance(
                        lat, lng,
                        (double)zone.CenterLatitude,
                        (double)zone.CenterLongitude
                    );

                    // Default detection radius (you can add a column for custom radius per zone)
                    double detectionRadiusKm = 5.0; // 5km default

                    // If within detection radius and closer than previous matches
                    if (distance <= detectionRadiusKm && distance < minDistance)
                    {
                        minDistance = distance;
                        closestZone = zone.ZoneId;
                    }

                    System.Diagnostics.Debug.WriteLine($"Zone: {zone.ZoneName}, Distance: {distance:F2}km, Within Radius: {distance <= detectionRadiusKm}");
                }

                if (closestZone.HasValue)
                {
                    System.Diagnostics.Debug.WriteLine($"Zone detected: {closestZone.Value} at distance {minDistance:F2}km");
                }

                return closestZone;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Zone detection error: {ex.Message}");
                return null;
            }
        }

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

        private string GetWarningMessage(int strikes)
        {
            if (strikes >= 3) return "Your account has been permanently banned for fake complaints.";
            if (strikes == 2) return "⚠️ FINAL WARNING: Your account is banned for 7 days. One more fake complaint = Permanent Ban.";
            return "⚠️ Warning: Fake complaint detected. 2 more strikes = Account Ban.";
        }

        private IHttpActionResult NotFoundMessage(string message)
        {
            return Content(HttpStatusCode.NotFound, new { error = message });
        }

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
        //========================================
        // extra route
        //=========================================
        // =====================================================
        // SUBMIT COMPLAINT WITH AUTO-MERGE
        // =====================================================
        // =====================================================
        // FIND SIMILAR COMPLAINTS FOR AUTO-MERGE (FIXED)
        // =====================================================


        // =====================================================
        // AUTO-MERGE COMPLAINT (FIXED)
        // =====================================================

        //    public Complaint AutoMergeComplaint(Complaint newComplaint, List<Complaint> similarComplaints, Guid mergedByUserId)
        //    {
        //        // Combine all complaints (new + existing duplicates)
        //        var allComplaints = new List<Complaint> { newComplaint };
        //        allComplaints.AddRange(similarComplaints);

        //        // =====================================================
        //        // CREATE MERGED TITLE
        //        // =====================================================
        //        var allTitles = allComplaints.Select(c => c.Title).ToList();
        //        string mergedTitle = string.Join(" | ", allTitles.Take(3));
        //        if (allTitles.Count > 3)
        //            mergedTitle += $" + {allTitles.Count - 3} more";
        //        if (mergedTitle.Length > 200)
        //            mergedTitle = mergedTitle.Substring(0, 197) + "...";

        //        // =====================================================
        //        // CREATE MERGED DESCRIPTION
        //        // =====================================================
        //        var allDescriptions = new List<string>();
        //        foreach (var complaint in allComplaints)
        //        {
        //            allDescriptions.Add($"=== {complaint.ComplaintNumber} ===\n{complaint.Description}");
        //        }
        //        string mergedDescription = string.Join("\n\n---\n\n", allDescriptions);
        //        if (mergedDescription.Length > 4000)
        //            mergedDescription = mergedDescription.Substring(0, 3997) + "...";

        //        // =====================================================
        //        // CALCULATE TOTALS
        //        // =====================================================
        //        int totalUpvotes = allComplaints.Sum(c => c.UpvoteCount);
        //        int totalViews = allComplaints.Sum(c => c.ViewCount);

        //        // Determine highest priority among all
        //        string highestPriority = "Medium";
        //        if (allComplaints.Any(c => c.Priority == "Critical"))
        //            highestPriority = "Critical";
        //        else if (allComplaints.Any(c => c.Priority == "High"))
        //            highestPriority = "High";
        //        else if (allComplaints.All(c => c.Priority == "Low"))
        //            highestPriority = "Low";

        //        // Get the first complaint's location as the primary location
        //        var primaryLocation = allComplaints.First();

        //        // Get first complaint's CitizenId (or use the new complaint's)
        //        Guid citizenId = newComplaint.CitizenId ?? Guid.Empty;
        //        if (citizenId == Guid.Empty && primaryLocation.CitizenId != null)
        //            citizenId = primaryLocation.CitizenId.Value;

        //        // =====================================================
        //        // CREATE NEW MERGED COMPLAINT
        //        // =====================================================
        //        var mergedComplaint = new Complaint
        //        {
        //            ComplaintId = Guid.NewGuid(),
        //            ComplaintNumber = $"AUTO-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 4)}",
        //            Title = mergedTitle,
        //            Description = mergedDescription,
        //            CategoryId = newComplaint.CategoryId,
        //            DepartmentId = newComplaint.DepartmentId,
        //            ZoneId = newComplaint.ZoneId,
        //            CitizenId = citizenId,
        //            LocationLatitude = (decimal)primaryLocation.LocationLatitude,
        //            LocationLongitude = (decimal)primaryLocation.LocationLongitude,
        //            LocationAddress = primaryLocation.LocationAddress ?? "",
        //            LocationLandmark = primaryLocation.LocationLandmark,
        //            Priority = highestPriority,
        //            UpvoteCount = totalUpvotes,
        //            ViewCount = totalViews,
        //            CurrentStatus = (int)ComplaintStatus.Submitted,
        //            SubmissionStatus = (int)SubmissionStatus.PendingApproval,
        //            CreatedAt = DateTime.Now,
        //            UpdatedAt = DateTime.Now,
        //            IsDuplicate = false,
        //            IsFake = false
        //        };

        //        db.Complaints.Add(mergedComplaint);
        //        db.SaveChanges();

        //        // =====================================================
        //        // COPY PHOTOS FROM ALL COMPLAINTS
        //        // =====================================================
        //        int orderCounter = 1;
        //        foreach (var complaint in allComplaints)
        //        {
        //            var photos = db.ComplaintPhotos.Where(p => p.ComplaintId == complaint.ComplaintId).ToList();
        //            foreach (var photo in photos)
        //            {
        //                db.ComplaintPhotos.Add(new ComplaintPhoto
        //                {
        //                    PhotoId = Guid.NewGuid(),
        //                    ComplaintId = mergedComplaint.ComplaintId,
        //                    PhotoUrl = photo.PhotoUrl,
        //                    PhotoType = photo.PhotoType,
        //                    UploadOrder = orderCounter++,
        //                    UploadedAt = DateTime.Now,
        //                    UploadedById = mergedByUserId
        //                });
        //            }
        //        }

        //        // =====================================================
        //        // CREATE CLUSTER FOR MERGED COMPLAINT
        //        // =====================================================
        //        var cluster = new DuplicateCluster
        //        {
        //            ClusterId = Guid.NewGuid(),
        //            PrimaryComplaintId = mergedComplaint.ComplaintId,
        //            CategoryId = mergedComplaint.CategoryId,
        //            LocationLatitude = mergedComplaint.LocationLatitude,
        //            LocationLongitude = mergedComplaint.LocationLongitude,
        //            ClusterRadiusMeters = 200,
        //            TotalComplaintsMerged = allComplaints.Count,
        //            TotalCombinedUpvotes = totalUpvotes,
        //            CreatedAt = DateTime.Now,
        //            UpdatedAt = DateTime.Now
        //        };

        //        db.DuplicateClusters.Add(cluster);
        //        db.SaveChanges();

        //        // =====================================================
        //        // ADD ENTRIES FOR ALL COMPLAINTS IN CLUSTER
        //        // =====================================================
        //        // Entry for the new merged complaint
        //        db.DuplicateEntries.Add(new DuplicateEntry
        //        {
        //            EntryId = Guid.NewGuid(),
        //            ClusterId = cluster.ClusterId,
        //            ComplaintId = mergedComplaint.ComplaintId,
        //            SimilarityScore = 100,
        //            SimilarityFactors = "{\"type\":\"auto_merged_result\"}",
        //            MergedAt = DateTime.Now,
        //            MergedById = mergedByUserId
        //        });

        //        // Mark all original complaints as merged
        //        foreach (var complaint in allComplaints)
        //        {
        //            complaint.IsDuplicate = true;
        //            complaint.MergedIntoComplaintId = mergedComplaint.ComplaintId;
        //            complaint.UpdatedAt = DateTime.Now;

        //            db.DuplicateEntries.Add(new DuplicateEntry
        //            {
        //                EntryId = Guid.NewGuid(),
        //                ClusterId = cluster.ClusterId,
        //                ComplaintId = complaint.ComplaintId,
        //                SimilarityScore = 100,
        //                SimilarityFactors = "{\"type\":\"auto_merged_original\"}",
        //                MergedAt = DateTime.Now,
        //                MergedById = mergedByUserId
        //            });
        //        }

        //        db.SaveChanges();

        //        // =====================================================
        //        // ADD STATUS HISTORY
        //        // =====================================================
        //        db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
        //        {
        //            HistoryId = Guid.NewGuid(),
        //            ComplaintId = mergedComplaint.ComplaintId,
        //            PreviousStatus = null,
        //            NewStatus = ComplaintStatus.Submitted.ToString(),
        //            ChangedById = mergedByUserId,
        //            ChangedAt = DateTime.Now,
        //            Notes = $"Auto-merged {allComplaints.Count} duplicate complaints"
        //        });

        //        db.SaveChanges();

        //        return mergedComplaint;
        //    }

        //    // Add this method to share DbContext
        //    public void SetDbContext(CCMWDbContext context)
        //    {
        //        this.db = context;
        //    }

        //}// Add this to your ComplaintsController.cs (not DuplicateManagementController)

        // =====================================================
        // SUBMIT COMPLAINT WITH AUTO-MERGE - FIXED VERSION
        // =====================================================
        [HttpPost]
        [Route("submit-with-auto-merge")]
        public IHttpActionResult SubmitComplaintWithAutoMerge([FromBody] ComplaintRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest("Complaint data is required");

                if (request.CitizenId == null || request.CitizenId == Guid.Empty)
                    return BadRequest("CitizenId is required");

                if (request.CategoryId == null || request.CategoryId == Guid.Empty)
                    return BadRequest("CategoryId is required");

                var category = db.ComplaintCategories.Find(request.CategoryId);
                if (category == null)
                    return BadRequest($"Category with ID {request.CategoryId} not found");

                // Check if this is a duplicate before creating the complaint
                var existingSimilar = FindSimilarComplaintsForCheck(
                    request.LocationLatitude ?? 0,
                    request.LocationLongitude ?? 0,
                    request.CategoryId.Value
                );

                if (existingSimilar.Any())
                {
                    // AUTO-MERGE: Create merged complaint directly without creating a separate new complaint first
                    var similarComplaints = existingSimilar.Take(5).ToList();

                    // Create a temporary new complaint object for merging
                    var tempNewComplaint = new Complaint
                    {
                        ComplaintId = Guid.NewGuid(),
                        ComplaintNumber = $"CCMW-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 4)}",
                        Title = request.Title,
                        Description = request.Description,
                        CategoryId = request.CategoryId.Value,
                        DepartmentId = category.DepartmentId,
                        CitizenId = request.CitizenId.Value,
                        LocationLatitude = (decimal)request.LocationLatitude,
                        LocationLongitude = (decimal)request.LocationLongitude,
                        LocationAddress = request.LocationAddress ?? "",
                        LocationLandmark = request.LocationLandmark,
                        Priority = request.Priority ?? "Medium",
                        CurrentStatus = ComplaintStatus.Submitted,
                        SubmissionStatus = SubmissionStatus.PendingApproval,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        UpvoteCount = 0,
                        ViewCount = 0,
                        IsDuplicate = false,
                        IsFake = false
                    };

                    // Create the merged complaint using the duplicate controller
                    var duplicateController = new DuplicateManagementController();
                    duplicateController.SetDbContext(db);

                    var mergedComplaint = duplicateController.AutoMergeComplaint(tempNewComplaint, similarComplaints, request.CitizenId.Value);
                    db.SaveChanges();

                    // Add status history
                    db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
                    {
                        HistoryId = Guid.NewGuid(),
                        ComplaintId = mergedComplaint.ComplaintId,
                        PreviousStatus = null,
                        NewStatus = ComplaintStatus.Submitted.ToString(),
                        ChangedById = request.CitizenId.Value,
                        ChangedAt = DateTime.Now,
                        Notes = $"AUTO-MERGED: Complaint submitted and merged with {similarComplaints.Count} existing complaints"
                    });
                    db.SaveChanges();

                    return Ok(new
                    {
                        success = true,
                        message = $"Complaint submitted and auto-merged with {similarComplaints.Count} existing {(similarComplaints.Count == 1 ? "complaint" : "complaints")}",
                        complaintId = mergedComplaint.ComplaintId,
                        complaintNumber = mergedComplaint.ComplaintNumber,
                        isMerged = true,
                        mergedCount = similarComplaints.Count + 1
                    });
                }

                // No duplicates found - create normal complaint
                var newComplaint = new Complaint
                {
                    ComplaintId = Guid.NewGuid(),
                    ComplaintNumber = $"CCMW-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 4)}",
                    Title = request.Title,
                    Description = request.Description,
                    CategoryId = request.CategoryId.Value,
                    DepartmentId = category.DepartmentId,
                    CitizenId = request.CitizenId.Value,
                    LocationLatitude = (decimal)request.LocationLatitude,
                    LocationLongitude = (decimal)request.LocationLongitude,
                    LocationAddress = request.LocationAddress ?? "",
                    LocationLandmark = request.LocationLandmark,
                    Priority = request.Priority ?? "Medium",
                    CurrentStatus = ComplaintStatus.Submitted,
                    SubmissionStatus = SubmissionStatus.PendingApproval,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    UpvoteCount = 0,
                    ViewCount = 0,
                    IsDuplicate = false,
                    IsFake = false
                };

                // Auto-detect zone
                if (request.LocationLatitude.HasValue && request.LocationLongitude.HasValue)
                {
                    var detectedZoneId = DetectZoneByLocation(request.LocationLatitude.Value, request.LocationLongitude.Value);
                    if (detectedZoneId.HasValue)
                    {
                        newComplaint.ZoneId = detectedZoneId.Value;
                    }
                }

                db.Complaints.Add(newComplaint);
                db.SaveChanges();

                // Add status history
                db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
                {
                    HistoryId = Guid.NewGuid(),
                    ComplaintId = newComplaint.ComplaintId,
                    PreviousStatus = null,
                    NewStatus = ComplaintStatus.Submitted.ToString(),
                    ChangedById = request.CitizenId.Value,
                    ChangedAt = DateTime.Now,
                    Notes = "Complaint submitted"
                });
                db.SaveChanges();

                return Ok(new
                {
                    success = true,
                    message = "Complaint submitted successfully",
                    complaintId = newComplaint.ComplaintId,
                    complaintNumber = newComplaint.ComplaintNumber,
                    isMerged = false,
                    mergedCount = 0
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in SubmitComplaintWithAutoMerge: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return InternalServerError(ex);
            }
        }

        // Add this helper method to find similar complaints
        private List<Complaint> FindSimilarComplaintsForCheck(double lat, double lng, Guid categoryId)
        {
            try
            {
                double searchRadiusKm = 0.2;

                var complaints = db.Complaints
                    .Where(c => c.CategoryId == categoryId)
                    .Where(c => c.LocationLatitude != null && c.LocationLongitude != null)
                    .Where(c => c.CurrentStatus != ComplaintStatus.Resolved &&
                               c.CurrentStatus != ComplaintStatus.Closed &&
                               c.CurrentStatus != ComplaintStatus.Rejected)
                    .Where(c => c.MergedIntoComplaintId == null) // ← only change
                    .ToList();

                var similar = new List<Complaint>();
                foreach (var complaint in complaints)
                {
                    double distance = CalculateDistance(
                        lat, lng,
                        (double)complaint.LocationLatitude,
                        (double)complaint.LocationLongitude
                    );

                    if (distance <= searchRadiusKm)
                    {
                        similar.Add(complaint);
                    }
                }

                return similar;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error finding similar complaints: {ex.Message}");
                return new List<Complaint>();
            }
        }
        public Complaint AutoMergeComplaint(Complaint newComplaint, List<Complaint> similarComplaints, Guid mergedByUserId)
        {
            // Combine all complaints (new + existing duplicates)
            var allComplaints = new List<Complaint> { newComplaint };
            allComplaints.AddRange(similarComplaints);

            // =====================================================
            // CREATE MERGED TITLE
            // =====================================================
            var allTitles = allComplaints.Select(c => c.Title).ToList();
            var mergedTitle = string.Join(" | ", allTitles.Take(3));
            if (allTitles.Count > 3)
                mergedTitle += $" + {allTitles.Count - 3} more";
            if (mergedTitle.Length > 200)
                mergedTitle = mergedTitle.Substring(0, 197) + "...";

            // =====================================================
            // CREATE MERGED DESCRIPTION
            // =====================================================
            var allDescriptions = new List<string>();
            foreach (var complaint in allComplaints)
            {
                allDescriptions.Add($"=== {complaint.ComplaintNumber} ===\n{complaint.Description}");
            }
            var mergedDescription = string.Join("\n\n---\n\n", allDescriptions);
            if (mergedDescription.Length > 4000)
                mergedDescription = mergedDescription.Substring(0, 3997) + "...";

            // =====================================================
            // CALCULATE TOTALS
            // =====================================================
            int totalUpvotes = allComplaints.Sum(c => c.UpvoteCount);
            int totalViews = allComplaints.Sum(c => c.ViewCount);

            // Determine highest priority among all
            string highestPriority = "Medium";
            if (allComplaints.Any(c => c.Priority == "Critical"))
                highestPriority = "Critical";
            else if (allComplaints.Any(c => c.Priority == "High"))
                highestPriority = "High";
            else if (allComplaints.All(c => c.Priority == "Low"))
                highestPriority = "Low";

            // =====================================================
            // CREATE NEW MERGED COMPLAINT
            // =====================================================
            var mergedComplaint = new Complaint
            {
                ComplaintId = Guid.NewGuid(),
                ComplaintNumber = $"AUTO-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 4)}",
                Title = mergedTitle,
                Description = mergedDescription,
                CategoryId = newComplaint.CategoryId,
                DepartmentId = newComplaint.DepartmentId,
                ZoneId = newComplaint.ZoneId,
                CitizenId = newComplaint.CitizenId,
                LocationLatitude = newComplaint.LocationLatitude,
                LocationLongitude = newComplaint.LocationLongitude,
                LocationAddress = newComplaint.LocationAddress ?? "",
                LocationLandmark = newComplaint.LocationLandmark,
                Priority = highestPriority,
                UpvoteCount = totalUpvotes,
                ViewCount = totalViews,
                CurrentStatus = ComplaintStatus.Submitted,
                SubmissionStatus = SubmissionStatus.PendingApproval,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                IsDuplicate = false,
                IsFake = false
            };

            db.Complaints.Add(mergedComplaint);

            // =====================================================
            // COPY PHOTOS FROM ALL COMPLAINTS
            // =====================================================
            int orderCounter = 1;
            foreach (var complaint in allComplaints)
            {
                var photos = db.ComplaintPhotos.Where(p => p.ComplaintId == complaint.ComplaintId).ToList();
                foreach (var photo in photos)
                {
                    db.ComplaintPhotos.Add(new ComplaintPhoto
                    {
                        PhotoId = Guid.NewGuid(),
                        ComplaintId = mergedComplaint.ComplaintId,
                        PhotoUrl = photo.PhotoUrl,
                        PhotoType = photo.PhotoType,
                        UploadOrder = orderCounter++,
                        UploadedAt = DateTime.Now,
                        UploadedById = mergedByUserId
                    });
                }
            }

            // =====================================================
            // CREATE CLUSTER FOR MERGED COMPLAINT
            // =====================================================
            var cluster = new DuplicateCluster
            {
                ClusterId = Guid.NewGuid(),
                PrimaryComplaintId = mergedComplaint.ComplaintId,
                CategoryId = mergedComplaint.CategoryId,
                LocationLatitude = mergedComplaint.LocationLatitude,
                LocationLongitude = mergedComplaint.LocationLongitude,
                ClusterRadiusMeters = 200,
                TotalComplaintsMerged = allComplaints.Count,
                TotalCombinedUpvotes = totalUpvotes,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            db.DuplicateClusters.Add(cluster);

            // =====================================================
            // ADD ENTRIES FOR ALL COMPLAINTS IN CLUSTER
            // =====================================================
            // Entry for the new merged complaint
            db.DuplicateEntries.Add(new DuplicateEntry
            {
                EntryId = Guid.NewGuid(),
                ClusterId = cluster.ClusterId,
                ComplaintId = mergedComplaint.ComplaintId,
                SimilarityScore = 100,
                SimilarityFactors = "{\"type\":\"auto_merged_result\"}",
                MergedAt = DateTime.Now,
                MergedById = mergedByUserId
            });

            // Mark all original complaints as merged
            foreach (var complaint in allComplaints)
            {
                complaint.IsDuplicate = true;
                complaint.MergedIntoComplaintId = mergedComplaint.ComplaintId;
                complaint.UpdatedAt = DateTime.Now;

                db.DuplicateEntries.Add(new DuplicateEntry
                {
                    EntryId = Guid.NewGuid(),
                    ClusterId = cluster.ClusterId,
                    ComplaintId = complaint.ComplaintId,
                    SimilarityScore = 100,
                    SimilarityFactors = "{\"type\":\"auto_merged_original\"}",
                    MergedAt = DateTime.Now,
                    MergedById = mergedByUserId
                });
            }

            // =====================================================
            // ADD STATUS HISTORY
            // =====================================================
            db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
            {
                HistoryId = Guid.NewGuid(),
                ComplaintId = mergedComplaint.ComplaintId,
                PreviousStatus = null,
                NewStatus = ComplaintStatus.Submitted.ToString(),
                ChangedById = mergedByUserId,
                ChangedAt = DateTime.Now,
                Notes = $"Auto-merged {allComplaints.Count} duplicate complaints"
            });

            return mergedComplaint;
        }  // Add this method to DuplicateManagementController.cs


        // =====================================================
        // DTO CLASSES
        // =====================================================
        // Request DTO for complaint submission
        public class ComplaintRequest
        {
            public string Title { get; set; }
            public string Description { get; set; }
            public Guid? CategoryId { get; set; }
            public Guid? CitizenId { get; set; }
            public Guid? DepartmentId { get; set; }
            public Guid? ZoneId { get; set; }
            public double? LocationLatitude { get; set; }
            public double? LocationLongitude { get; set; }
            public string LocationAddress { get; set; }
            public string LocationLandmark { get; set; }
            public string Priority { get; set; }
        }
        public class FakeMarkRequest
        {
            public Guid AdminId { get; set; }
            public string Notes { get; set; }
        }
    }
}