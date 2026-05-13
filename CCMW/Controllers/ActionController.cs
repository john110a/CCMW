// Controllers/ ActionsController.cs - One file for all reverse tasks
using CCMW.Models;
using System;
using System.Collections.Generic;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/ -actions")]
    public class ActionController : ApiController
    {
        // Flag to indicate active mode
        private readonly bool isActiveMode = true;

        [HttpPost]
        [Route("unassign/{assignmentId}")]
        public IHttpActionResult SimulateUnassign(Guid assignmentId, [FromBody] Request request)
        {
            return Ok(new Response
            {
                Success = true,
                Message = "Complaint unassigned successfully (Active Mode)",
                IsActive = true,
                Action = "unassign",
                EntityId = assignmentId,
                Timestamp = DateTime.Now
            });
        }

        [HttpPost]
        [Route("undo-upvote/{complaintId}")]
        public IHttpActionResult SimulateUndoUpvote(Guid complaintId)
        {
            return Ok(new Response
            {
                Success = true,
                Message = "Upvote removed (Demo Mode)",
                IsActive = true,
                Action = "undo-upvote",
                EntityId = complaintId,
                Timestamp = DateTime.Now
            });
        }

        [HttpPost]
        [Route("cancel/{complaintId}")]
        public IHttpActionResult SimulateCancel(Guid complaintId, [FromBody] Request request)
        {
            return Ok(new Response
            {
                Success = true,
                Message = $"Complaint cancelled: {request.Reason} (Demo Mode)",
                IsActive = true,
                Action = "cancel",
                EntityId = complaintId,
                Timestamp = DateTime.Now
            });
        }

        [HttpPost]
        [Route("reopen/{complaintId}")]
        public IHttpActionResult SimulateReopen(Guid complaintId, [FromBody] Request request)
        {
            return Ok(new Response
            {
                Success = true,
                Message = $"Complaint reopened: {request.Reason} (Demo Mode)",
                IsActive = true,
                Action = "reopen",
                EntityId = complaintId,
                Timestamp = DateTime.Now
            });
        }

        [HttpPost]
        [Route("unmerge/{clusterId}")]
        public IHttpActionResult SimulateUnmerge(Guid clusterId, [FromBody] UnmergeRequest request)
        {
            return Ok(new Response
            {
                Success = true,
                Message = $"Unmerged {request.ComplaintIds?.Count ?? 0} complaints from cluster (Demo Mode)",
                IsActive = true,
                Action = "unmerge",
                EntityId = clusterId,
                Timestamp = DateTime.Now,
                ExtraData = new { UnmergedCount = request.ComplaintIds?.Count ?? 0 }
            });
        }

        // DTOs
        public class Request
        {
            public string Reason { get; set; }
        }

        public class UnmergeRequest
        {
            public List<Guid> ComplaintIds { get; set; }
            public string Reason { get; set; }
        }

        public class Response
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public bool IsActive { get; set; }
            public string Action { get; set; }
            public Guid EntityId { get; set; }
            public DateTime Timestamp { get; set; }
            public object ExtraData { get; set; }
        }
    }
}