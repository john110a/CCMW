using CCMW.Models;
using System;
using System.Linq;
using System.Net;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/notifications")]
    public class NotificationController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        // GET: api/notifications/{userId}
        [HttpGet]
        [Route("{userId:guid}")]
        public IHttpActionResult GetUserNotifications(Guid userId)
        {
            try
            {
                // Check if user exists
                var user = db.Users.FirstOrDefault(u => u.UserId == userId);
                if (user == null)
                {
                    return Content(HttpStatusCode.NotFound, new { error = "User not found" });
                }

                // Get notifications for the user
                var notifications = db.Notifications
                    .Where(n => n.UserId == userId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Select(n => new
                    {
                        n.NotificationId,
                        n.UserId,
                        n.NotificationType,
                        n.Title,
                        n.Message,
                        n.ReferenceType,
                        n.ReferenceId,
                        n.IsRead,
                        n.CreatedAt,
                        n.ReadAt,
                        TimeAgo = GetTimeAgo(n.CreatedAt)
                    })
                    .ToList();

                return Ok(notifications);
            }
            catch (Exception ex)
            {
                // Log the error
                System.Diagnostics.Debug.WriteLine($"Error in GetUserNotifications: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                return Content(HttpStatusCode.InternalServerError, new
                {
                    error = ex.Message,
                    details = ex.InnerException?.Message
                });
            }
        }

        // GET: api/notifications/{userId}/unread-count
        [HttpGet]
        [Route("{userId:guid}/unread-count")]
        public IHttpActionResult GetUnreadCount(Guid userId)
        {
            try
            {
                var unreadCount = db.Notifications
                    .Count(n => n.UserId == userId && !n.IsRead);

                return Ok(new { unreadCount = unreadCount });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
        }

        // PUT: api/notifications/mark-read/{notificationId}
        [HttpPut]
        [Route("mark-read/{notificationId:guid}")]
        public IHttpActionResult MarkAsRead(Guid notificationId)
        {
            try
            {
                var notification = db.Notifications.FirstOrDefault(n => n.NotificationId == notificationId);
                if (notification == null)
                {
                    return NotFound();
                }

                notification.IsRead = true;
                notification.ReadAt = DateTime.Now;
                db.SaveChanges();

                return Ok(new { message = "Notification marked as read." });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
        }

        // PUT: api/notifications/mark-all-read/{userId}
        [HttpPut]
        [Route("mark-all-read/{userId:guid}")]
        public IHttpActionResult MarkAllAsRead(Guid userId)
        {
            try
            {
                var notifications = db.Notifications
                    .Where(n => n.UserId == userId && !n.IsRead)
                    .ToList();

                foreach (var notification in notifications)
                {
                    notification.IsRead = true;
                    notification.ReadAt = DateTime.Now;
                }

                db.SaveChanges();

                return Ok(new { message = $"{notifications.Count} notifications marked as read." });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
        }

        // DELETE: api/notifications/{notificationId}
        [HttpDelete]
        [Route("{notificationId:guid}")]
        public IHttpActionResult DeleteNotification(Guid notificationId)
        {
            try
            {
                var notification = db.Notifications.FirstOrDefault(n => n.NotificationId == notificationId);
                if (notification == null)
                {
                    return NotFound();
                }

                db.Notifications.Remove(notification);
                db.SaveChanges();

                return Ok(new { message = "Notification deleted successfully." });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
        }

        // POST: api/notifications/send
        [HttpPost]
        [Route("send")]
        public IHttpActionResult SendNotification([FromBody] Notification notification)
        {
            try
            {
                if (notification == null)
                {
                    return BadRequest("Notification data required.");
                }

                if (string.IsNullOrEmpty(notification.Title))
                {
                    return BadRequest("Notification title is required.");
                }

                notification.NotificationId = Guid.NewGuid();
                notification.CreatedAt = DateTime.Now;
                notification.IsRead = false;

                db.Notifications.Add(notification);
                db.SaveChanges();

                return Ok(new
                {
                    message = "Notification sent successfully.",
                    notificationId = notification.NotificationId
                });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
        }

        // Helper method to calculate time ago
        private string GetTimeAgo(DateTime dateTime)
        {
            var timeSpan = DateTime.Now - dateTime;

            if (timeSpan.TotalSeconds < 60)
                return "Just now";
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes} minute{(timeSpan.TotalMinutes >= 2 ? "s" : "")} ago";
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours} hour{(timeSpan.TotalHours >= 2 ? "s" : "")} ago";
            if (timeSpan.TotalDays < 7)
                return $"{(int)timeSpan.TotalDays} day{(timeSpan.TotalDays >= 2 ? "s" : "")} ago";
            if (timeSpan.TotalDays < 30)
                return $"{(int)(timeSpan.TotalDays / 7)} week{(timeSpan.TotalDays / 7 >= 2 ? "s" : "")} ago";
            if (timeSpan.TotalDays < 365)
                return $"{(int)(timeSpan.TotalDays / 30)} month{(timeSpan.TotalDays / 30 >= 2 ? "s" : "")} ago";

            return $"{(int)(timeSpan.TotalDays / 365)} year{(timeSpan.TotalDays / 365 >= 2 ? "s" : "")} ago";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}