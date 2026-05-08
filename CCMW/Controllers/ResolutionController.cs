using CCMW.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Http;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/resolutions")]
    public class ResolutionController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        // Helper method to get full URL
        private string GetFullUrl(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return null;

            var requestUrl = Request.RequestUri;
            var baseUrl = $"{requestUrl.Scheme}://{requestUrl.Authority}";

            var appPath = Request.GetRequestContext().VirtualPathRoot;
            if (appPath == "/")
                appPath = "";

            var cleanPath = relativePath.StartsWith("/") ? relativePath.Substring(1) : relativePath;

            return $"{baseUrl}{appPath}/{cleanPath}";
        }

        // =====================================================
        // EXISTING METHODS
        // =====================================================

        [HttpGet]
        [Route("pending")]
        public IHttpActionResult GetPendingResolutions()
        {
            try
            {
                var resolutions = db.Complaints
                    .Include(c => c.Category)
                    .Include(c => c.AssignedTo.User)
                    .Where(c => c.CurrentStatus == ComplaintStatus.Resolved)
                    .OrderByDescending(c => c.ResolvedAt)
                    .Take(50)
                    .ToList()
                    .Select(c => new
                    {
                        Id = c.ComplaintId,
                        ComplaintId = c.ComplaintId,
                        ComplaintNumber = c.ComplaintNumber ?? "N/A",
                        Title = c.Title ?? "No Title",
                        Location = c.LocationAddress ?? "Unknown Location",
                        Category = c.Category != null ? c.Category.CategoryName : "General",
                        ResolvedBy = c.AssignedTo != null && c.AssignedTo.User != null
                            ? c.AssignedTo.User.FullName
                            : (c.AssignedTo != null ? c.AssignedTo.EmployeeId : "Unknown"),
                        SubmittedAt = c.ResolvedAt != null
                            ? ((DateTime)c.ResolvedAt).ToString("MMM dd, yyyy - h:mm tt")
                            : "",
                        Status = "Pending",
                        BeforePhotoUrl = GetFullUrl(GetBeforePhoto(c.ComplaintId)),
                        AfterPhotoUrl = GetFullUrl(GetAfterPhoto(c.ComplaintId)),
                        ResolutionNotes = c.ResolutionNotes ?? "Resolution completed. Please verify the after photo.",
                        FlagReason = (string)null
                    })
                    .ToList();

                return Ok(resolutions);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpGet]
        [Route("all")]
        public IHttpActionResult GetAllResolutions(
            [FromUri] int page = 1,
            [FromUri] int pageSize = 20,
            [FromUri] string status = null)
        {
            try
            {
                var query = db.Complaints
                    .Include(c => c.Category)
                    .Include(c => c.AssignedTo.User)
                    .Where(c => c.CurrentStatus == ComplaintStatus.Resolved ||
                               c.CurrentStatus == ComplaintStatus.Verified);

                if (!string.IsNullOrEmpty(status))
                {
                    if (status.ToLower() == "pending")
                        query = query.Where(c => c.CurrentStatus == ComplaintStatus.Resolved);
                    else if (status.ToLower() == "verified")
                        query = query.Where(c => c.CurrentStatus == ComplaintStatus.Verified);
                }

                var total = query.Count();
                var resolutions = query
                    .OrderByDescending(c => c.ResolvedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList()
                    .Select(c => new
                    {
                        Id = c.ComplaintId,
                        ComplaintId = c.ComplaintId,
                        ComplaintNumber = c.ComplaintNumber ?? "N/A",
                        Title = c.Title ?? "No Title",
                        Location = c.LocationAddress ?? "Unknown Location",
                        Category = c.Category != null ? c.Category.CategoryName : "General",
                        ResolvedBy = c.AssignedTo != null && c.AssignedTo.User != null
                            ? c.AssignedTo.User.FullName
                            : (c.AssignedTo != null ? c.AssignedTo.EmployeeId : "Unknown"),
                        SubmittedAt = c.ResolvedAt != null
                            ? ((DateTime)c.ResolvedAt).ToString("MMM dd, yyyy - h:mm tt")
                            : "",
                        Status = c.CurrentStatus == ComplaintStatus.Resolved ? "Pending" : "Verified",
                        BeforePhotoUrl = GetFullUrl(GetBeforePhoto(c.ComplaintId)),
                        AfterPhotoUrl = GetFullUrl(GetAfterPhoto(c.ComplaintId)),
                        ResolutionNotes = c.ResolutionNotes ?? "",
                        FlagReason = (string)null,
                        ResolvedAt = c.ResolvedAt,
                        VerifiedAt = c.StatusUpdatedAt
                    })
                    .ToList();

                return Ok(new
                {
                    Total = total,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)total / pageSize),
                    Resolutions = resolutions
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // GET FLAGGED/FAKE COMPLAINTS - FIXED (using only IsFake)
        // =====================================================
        [HttpGet]
        [Route("flagged")]
        public IHttpActionResult GetFlaggedComplaints()
        {
            try
            {
                // Get complaints that are flagged as fake (IsFake == true)
                var flaggedComplaints = db.Complaints
                    .Include(c => c.Category)
                    .Include(c => c.AssignedTo.User)
                    .Include(c => c.ComplaintPhotos)
                    .Where(c => c.IsFake == true)  // Only use IsFake
                    .OrderByDescending(c => c.FakeVerifiedAt ?? c.UpdatedAt)
                    .Take(50)
                    .ToList()
                    .Select(c => new
                    {
                        Id = c.ComplaintId,
                        ComplaintId = c.ComplaintId,
                        ComplaintNumber = c.ComplaintNumber ?? "N/A",
                        Title = c.Title ?? "No Title",
                        Description = c.Description ?? "",
                        Location = c.LocationAddress ?? "Unknown Location",
                        Category = c.Category != null ? c.Category.CategoryName : "General",
                        ResolvedBy = c.AssignedTo != null && c.AssignedTo.User != null
                            ? c.AssignedTo.User.FullName
                            : (c.AssignedTo != null ? c.AssignedTo.EmployeeId : "Unknown"),
                        SubmittedAt = c.ResolvedAt != null
                            ? ((DateTime)c.ResolvedAt).ToString("MMM dd, yyyy - h:mm tt")
                            : c.CreatedAt.ToString("MMM dd, yyyy - h:mm tt"),
                        Status = "Flagged",
                        BeforePhotoUrl = GetFullUrl(GetBeforePhoto(c.ComplaintId)),
                        AfterPhotoUrl = GetFullUrl(GetAfterPhoto(c.ComplaintId)),
                        ResolutionNotes = c.ResolutionNotes ?? "This complaint was flagged as potential fake. Please review the evidence.",
                        FlagReason = c.IsFake == true ? "Fake Complaint" : null,
                        FlaggedAt = c.FakeVerifiedAt,
                        CitizenName = c.Citizen != null ? c.Citizen.FullName : "Unknown",
                        AllPhotos = c.ComplaintPhotos
                            .OrderBy(p => p.UploadOrder)
                            .Select(p => GetFullUrl(p.PhotoUrl))
                            .ToList()
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    totalFlagged = flaggedComplaints.Count,
                    complaints = flaggedComplaints
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting flagged complaints: {ex.Message}");
                return Content(System.Net.HttpStatusCode.InternalServerError, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // =====================================================
        // GET SINGLE FLAGGED COMPLAINT DETAILS - FIXED (using only IsFake)
        // =====================================================
        [HttpGet]
        [Route("flagged/{complaintId:guid}")]
        public IHttpActionResult GetFlaggedComplaintDetails(Guid complaintId)
        {
            try
            {
                var complaint = db.Complaints
                    .Include(c => c.Category)
                    .Include(c => c.AssignedTo.User)
                    .Include(c => c.Citizen)
                    .Include(c => c.ComplaintPhotos)
                    .FirstOrDefault(c => c.ComplaintId == complaintId);

                if (complaint == null)
                    return NotFound();

                var result = new
                {
                    ComplaintId = complaint.ComplaintId,
                    ComplaintNumber = complaint.ComplaintNumber,
                    Title = complaint.Title,
                    Description = complaint.Description,
                    Location = complaint.LocationAddress,
                    Category = complaint.Category?.CategoryName,
                    CitizenName = complaint.Citizen?.FullName,
                    CitizenPhone = complaint.Citizen?.PhoneNumber,
                    ResolvedBy = complaint.AssignedTo?.User?.FullName,
                    ResolvedAt = complaint.ResolvedAt,
                    IsFake = complaint.IsFake,
                    FlaggedAt = complaint.FakeVerifiedAt,
                    ResolutionNotes = complaint.ResolutionNotes,
                    BeforePhotos = complaint.ComplaintPhotos
                        .Where(p => p.PhotoType == "Complaint" || p.PhotoType == "Before" || p.PhotoType == "Initial")
                        .Select(p => GetFullUrl(p.PhotoUrl))
                        .ToList(),
                    AfterPhotos = complaint.ComplaintPhotos
                        .Where(p => p.PhotoType == "Resolution" || p.PhotoType == "After" || p.PhotoType == "Completed")
                        .Select(p => GetFullUrl(p.PhotoUrl))
                        .ToList(),
                    AllPhotos = complaint.ComplaintPhotos
                        .OrderBy(p => p.UploadOrder)
                        .Select(p => GetFullUrl(p.PhotoUrl))
                        .ToList()
                };

                return Ok(new { success = true, complaint = result });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // MARK FLAGGED COMPLAINT AS VERIFIED (NOT FAKE) - FIXED
        // =====================================================
        [HttpPost]
        [Route("flagged/{complaintId:guid}/verify-genuine")]
        public IHttpActionResult VerifyGenuine(Guid complaintId, [FromBody] VerifyGenuineRequest request)
        {
            try
            {
                var complaint = db.Complaints.Find(complaintId);
                if (complaint == null)
                    return NotFound();

                var oldStatus = complaint.CurrentStatus;

                // Clear fake flag - using only IsFake
                complaint.IsFake = false;
                complaint.FakeVerifiedBy = request?.AdminId;
                complaint.FakeVerifiedAt = DateTime.Now;

                // Set as Verified if it was resolved
                if (complaint.CurrentStatus == ComplaintStatus.Resolved)
                {
                    complaint.CurrentStatus = ComplaintStatus.Verified;
                }

                complaint.StatusUpdatedAt = DateTime.Now;

                db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
                {
                    HistoryId = Guid.NewGuid(),
                    ComplaintId = complaintId,
                    PreviousStatus = oldStatus.ToString(),
                    NewStatus = ComplaintStatus.Verified.ToString(),
                    ChangeReason = "Flagged complaint verified as genuine",
                    Notes = request?.Notes ?? "Admin verified this complaint is genuine",
                    ChangedAt = DateTime.Now
                });

                db.SaveChanges();

                // Send notification
                try
                {
                    db.Database.ExecuteSqlCommand(
                        "EXEC sp_NotifyComplaintFlow @ComplaintId, @EventType",
                        new SqlParameter("@ComplaintId", complaintId),
                        new SqlParameter("@EventType", "VERIFIED")
                    );
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Notification error: {ex.Message}");
                }

                return Ok(new
                {
                    success = true,
                    message = "Complaint marked as genuine and verified successfully"
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // MARK FLAGGED COMPLAINT AS FAKE (REJECT) - FIXED
        // =====================================================
        [HttpPost]
        [Route("flagged/{complaintId:guid}/mark-fake")]
        public IHttpActionResult MarkAsFake(Guid complaintId, [FromBody] MarkFakeRequest request)
        {
            try
            {
                var complaint = db.Complaints.Find(complaintId);
                if (complaint == null)
                    return NotFound();

                var oldStatus = complaint.CurrentStatus;

                // If citizen has fake strikes, increment and potentially ban
                if (complaint.CitizenId != null)
                {
                    var citizen = db.Users.Find(complaint.CitizenId);
                    if (citizen != null)
                    {
                        citizen.FakeStrikes = (citizen.FakeStrikes ?? 0) + 1;
                        citizen.LastFakeDate = DateTime.Now;

                        // Ban after 3 strikes
                        if (citizen.FakeStrikes >= 3)
                        {
                            citizen.IsBanned = true;
                            citizen.BanExpiryDate = DateTime.Now.AddDays(30);
                        }
                    }
                }

                // Mark complaint as closed/fake
                complaint.CurrentStatus = ComplaintStatus.Closed;
                complaint.IsFake = true;
                complaint.FakeVerifiedBy = request?.AdminId;
                complaint.FakeVerifiedAt = DateTime.Now;
                complaint.ClosedAt = DateTime.Now;
                complaint.StatusUpdatedAt = DateTime.Now;

                db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
                {
                    HistoryId = Guid.NewGuid(),
                    ComplaintId = complaintId,
                    PreviousStatus = oldStatus.ToString(),
                    NewStatus = "Fake",
                    ChangeReason = request?.Reason ?? "Confirmed as fake",
                    Notes = request?.Notes,
                    ChangedAt = DateTime.Now
                });

                db.SaveChanges();

                return Ok(new
                {
                    success = true,
                    message = "Complaint marked as fake. Citizen has received a strike.",
                    fakeStrikes = complaint.Citizen != null ? complaint.Citizen.FakeStrikes : 0
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // VERIFY RESOLUTION
        // =====================================================
        [HttpPost]
        [Route("{id}/verify")]
        public IHttpActionResult VerifyResolution(Guid id, [FromBody] VerifyRequest request)
        {
            try
            {
                var complaint = db.Complaints.Find(id);
                if (complaint == null)
                    return NotFound();

                var oldStatus = complaint.CurrentStatus;

                complaint.CurrentStatus = ComplaintStatus.Verified;
                complaint.StatusUpdatedAt = DateTime.Now;

                db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
                {
                    HistoryId = Guid.NewGuid(),
                    ComplaintId = id,
                    PreviousStatus = oldStatus.ToString(),
                    NewStatus = ComplaintStatus.Verified.ToString(),
                    Notes = request?.Notes ?? "Resolution verified by admin",
                    ChangedAt = DateTime.Now
                });

                db.SaveChanges();

                try
                {
                    db.Database.ExecuteSqlCommand(
                        "EXEC sp_NotifyComplaintFlow @ComplaintId, @EventType",
                        new SqlParameter("@ComplaintId", id),
                        new SqlParameter("@EventType", "VERIFIED")
                    );
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Notification error: {ex.Message}");
                }

                return Ok(new
                {
                    success = true,
                    Message = "Resolution verified successfully",
                    ComplaintId = id
                });
            }
            catch (Exception ex)
            {
                return Content(System.Net.HttpStatusCode.InternalServerError, new
                {
                    success = false,
                    Message = ex.Message
                });
            }
        }

        // =====================================================
        // FLAG RESOLUTION FOR REWORK
        // =====================================================
        [HttpPost]
        [Route("{id}/flag")]
        public IHttpActionResult FlagResolution(Guid id, [FromBody] FlagRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.Reason))
                    return BadRequest("Flag reason is required");

                var complaint = db.Complaints.Find(id);
                if (complaint == null)
                    return NotFound();

                var oldStatus = complaint.CurrentStatus;

                complaint.CurrentStatus = ComplaintStatus.InProgress;
                complaint.StatusUpdatedAt = DateTime.Now;

                db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
                {
                    HistoryId = Guid.NewGuid(),
                    ComplaintId = id,
                    PreviousStatus = oldStatus.ToString(),
                    NewStatus = ComplaintStatus.InProgress.ToString(),
                    ChangeReason = request.Reason,
                    Notes = request.Notes ?? "Resolution flagged for rework",
                    ChangedAt = DateTime.Now
                });

                db.SaveChanges();

                try
                {
                    db.Database.ExecuteSqlCommand(
                        "EXEC sp_NotifyComplaintFlow @ComplaintId, @EventType",
                        new SqlParameter("@ComplaintId", id),
                        new SqlParameter("@EventType", "REJECTED")
                    );
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Notification error: {ex.Message}");
                }

                return Ok(new
                {
                    success = true,
                    Message = "Resolution flagged for rework",
                    ComplaintId = id
                });
            }
            catch (Exception ex)
            {
                return Content(System.Net.HttpStatusCode.InternalServerError, new
                {
                    success = false,
                    Message = ex.Message
                });
            }
        }

        // =====================================================
        // GET RESOLUTION STATS - FIXED (added Flagged count)
        // =====================================================
        [HttpGet]
        [Route("stats")]
        public IHttpActionResult GetResolutionStats()
        {
            try
            {
                var stats = new
                {
                    PendingResolutions = db.Complaints.Count(c => c.CurrentStatus == ComplaintStatus.Resolved),
                    VerifiedResolutions = db.Complaints.Count(c => c.CurrentStatus == ComplaintStatus.Verified),
                    FlaggedResolutions = db.Complaints.Count(c => c.IsFake == true),  // Using only IsFake
                    TotalResolutions = db.Complaints.Count(c =>
                        c.CurrentStatus == ComplaintStatus.Resolved ||
                        c.CurrentStatus == ComplaintStatus.Verified),
                    ThisMonth = db.Complaints.Count(c =>
                        (c.CurrentStatus == ComplaintStatus.Resolved ||
                         c.CurrentStatus == ComplaintStatus.Verified) &&
                        c.ResolvedAt.HasValue &&
                        c.ResolvedAt.Value.Month == DateTime.Now.Month &&
                        c.ResolvedAt.Value.Year == DateTime.Now.Year)
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    PendingResolutions = 0,
                    VerifiedResolutions = 0,
                    FlaggedResolutions = 0,
                    TotalResolutions = 0,
                    ThisMonth = 0,
                    Error = ex.Message
                });
            }
        }

        // =====================================================
        // HELPER METHODS
        // =====================================================
        private string GetBeforePhoto(Guid complaintId)
        {
            string[] photoTypes = { "Complaint", "Before", "Initial", "Original" };
            var photo = db.ComplaintPhotos
                .FirstOrDefault(p => p.ComplaintId == complaintId && photoTypes.Contains(p.PhotoType));

            if (photo == null)
            {
                photo = db.ComplaintPhotos
                    .FirstOrDefault(p => p.ComplaintId == complaintId && p.UploadOrder == 1);
            }

            if (photo == null)
            {
                photo = db.ComplaintPhotos
                    .FirstOrDefault(p => p.ComplaintId == complaintId);
            }

            return photo?.PhotoUrl;
        }

        private string GetAfterPhoto(Guid complaintId)
        {
            string[] photoTypes = { "Resolution", "After", "Completed", "Resolved" };
            var photo = db.ComplaintPhotos
                .FirstOrDefault(p => p.ComplaintId == complaintId && photoTypes.Contains(p.PhotoType));

            if (photo == null)
            {
                var maxOrder = db.ComplaintPhotos
                    .Where(p => p.ComplaintId == complaintId)
                    .Max(p => (int?)p.UploadOrder);

                if (maxOrder.HasValue)
                {
                    photo = db.ComplaintPhotos
                        .FirstOrDefault(p => p.ComplaintId == complaintId && p.UploadOrder == maxOrder.Value);
                }
            }

            return photo?.PhotoUrl;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }

    public class VerifyRequest
    {
        public string Notes { get; set; }
    }

    public class FlagRequest
    {
        public string Reason { get; set; }
        public string Notes { get; set; }
    }

    public class VerifyGenuineRequest
    {
        public Guid? AdminId { get; set; }
        public string Notes { get; set; }
    }

    public class MarkFakeRequest
    {
        public Guid? AdminId { get; set; }
        public string Reason { get; set; }
        public string Notes { get; set; }
    }
}