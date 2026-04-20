// Controllers/ResolutionController.cs
using CCMW.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/resolutions")]
    public class ResolutionController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        // GET api/resolutions/pending
        [HttpGet]
        [Route("pending")]
        public IHttpActionResult GetPendingResolutions()
        {
            try
            {
                // Get complaints that are resolved and need verification
                var resolutions = db.Complaints
                    .Include(c => c.Category)
                    .Include(c => c.AssignedTo.User)
                    .Where(c => c.CurrentStatus == ComplaintStatus.Resolved)
                    .OrderByDescending(c => c.ResolvedAt)
                    .Take(20)
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
                            : "Unknown",
                        SubmittedAt = c.ResolvedAt != null
                            ? ((DateTime)c.ResolvedAt).ToString("MMM dd, yyyy - h:mm tt")
                            : "",
                        Status = "Pending",
                        BeforePhotoUrl = GetBeforePhoto(c.ComplaintId),
                        AfterPhotoUrl = GetAfterPhoto(c.ComplaintId),
                        ResolutionNotes = c.ResolutionNotes ?? "Resolution completed",
                        FlagReason = (string)null // FIXED: Simple null cast for C# 7.3
                    })
                    .ToList();

                return Ok(resolutions);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/resolutions/all
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
                        Status = c.CurrentStatus == ComplaintStatus.Resolved ? "Pending" : "Verified",
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

                // Add to status history
                db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
                {
                    HistoryId = Guid.NewGuid(),
                    ComplaintId = id,
                    PreviousStatus = oldStatus.ToString(),
                    NewStatus = ComplaintStatus.Verified.ToString(),
                    Notes = request != null ? request.Notes : "Resolution verified by admin",
                    ChangedAt = DateTime.Now
                });

                db.SaveChanges();

                return Ok(new
                {
                    Message = "Resolution verified successfully",
                    ComplaintId = id
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
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

                // Send back to InProgress for rework
                complaint.CurrentStatus = ComplaintStatus.InProgress;
                complaint.StatusUpdatedAt = DateTime.Now;

                // Add to status history
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

                return Ok(new
                {
                    Message = "Resolution flagged for rework",
                    ComplaintId = id
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
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
                return InternalServerError(ex);
            }
        }

        // Helper methods
        private string GetBeforePhoto(Guid complaintId)
        {
            var photo = db.ComplaintPhotos
                .FirstOrDefault(p => p.ComplaintId == complaintId && p.PhotoType == "Before");
            return photo != null ? photo.PhotoUrl : null;
        }

        private string GetAfterPhoto(Guid complaintId)
        {
            var photo = db.ComplaintPhotos
                .FirstOrDefault(p => p.ComplaintId == complaintId && p.PhotoType == "After");
            return photo != null ? photo.PhotoUrl : null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }

    // Request DTOs
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