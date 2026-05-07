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

        // Helper method to get full URL - FIXED to include virtual directory
        private string GetFullUrl(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return null;

            var requestUrl = Request.RequestUri;
            var baseUrl = $"{requestUrl.Scheme}://{requestUrl.Authority}";

            // Get the application path (e.g., /CCMW)
            var appPath = Request.GetRequestContext().VirtualPathRoot;
            if (appPath == "/")
                appPath = "";

            var cleanPath = relativePath.StartsWith("/") ? relativePath.Substring(1) : relativePath;

            // Combine: baseUrl + appPath + cleanPath
            return $"{baseUrl}{appPath}/{cleanPath}";
        }

        // GET api/resolutions/pending
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

        // GET api/resolutions/all - FIXED to include photos
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
                        // FIXED: Add photo URLs
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

        // POST api/resolutions/{id}/verify
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

                // ADDED: Send notification for VERIFIED event
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

        // POST api/resolutions/{id}/flag
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

                // ADDED: Send notification for REJECTED event
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

        // GET api/resolutions/stats
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
                    TotalResolutions = 0,
                    ThisMonth = 0,
                    Error = ex.Message
                });
            }
        }

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
}