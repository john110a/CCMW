using CCMW.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/feedback")]
    public class FeedbackController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        // POST api/feedback/submit
        [HttpPost]
        [Route("submit")]
        public IHttpActionResult SubmitFeedback([FromBody] ComplaintFeedback feedback)
        {
            try
            {
                if (feedback == null)
                    return BadRequest("Feedback data required");

                // Check if complaint exists
                var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == feedback.ComplaintId);
                if (complaint == null)
                    return Content(HttpStatusCode.NotFound, new { error = "Complaint not found" });

                // Validate rating
                if (feedback.Rating < 1 || feedback.Rating > 5)
                    return BadRequest("Rating must be between 1 and 5");

                feedback.FeedbackId = Guid.NewGuid();
                feedback.CreatedAt = DateTime.Now;

                db.ComplaintFeedback.Add(feedback);
                db.SaveChanges();

                return Ok(new
                {
                    Message = "Feedback submitted successfully",
                    FeedbackId = feedback.FeedbackId
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/feedback/complaint/{complaintId}
        [HttpGet]
        [Route("complaint/{complaintId:guid}")]
        public IHttpActionResult GetFeedbackByComplaint(Guid complaintId)
        {
            try
            {
                var feedback = db.ComplaintFeedback
                    .Where(f => f.ComplaintId == complaintId)
                    .Include(f => f.Citizen)
                    .OrderByDescending(f => f.CreatedAt)
                    .Select(f => new
                    {
                        f.FeedbackId,
                        f.Rating,
                        f.Comments,
                        f.CreatedAt,
                        Citizen = new
                        {
                            f.Citizen.UserId,
                            f.Citizen.FullName
                        }
                    })
                    .ToList();

                return Ok(feedback);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/feedback/stats/{complaintId}
        [HttpGet]
        [Route("stats/{complaintId:guid}")]
        public IHttpActionResult GetFeedbackStats(Guid complaintId)
        {
            try
            {
                var feedback = db.ComplaintFeedback
                    .Where(f => f.ComplaintId == complaintId)
                    .ToList();

                var stats = new
                {
                    TotalCount = feedback.Count,
                    AverageRating = feedback.Any() ? feedback.Average(f => f.Rating) : 0,
                    RatingDistribution = new
                    {
                        OneStar = feedback.Count(f => f.Rating == 1),
                        TwoStar = feedback.Count(f => f.Rating == 2),
                        ThreeStar = feedback.Count(f => f.Rating == 3),
                        FourStar = feedback.Count(f => f.Rating == 4),
                        FiveStar = feedback.Count(f => f.Rating == 5)
                    }
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}