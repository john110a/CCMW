using System;

namespace CCMW.DTOs
{
    public class AssignmentRequest
    {
        public Guid ComplaintId { get; set; }
        public Guid AssignedToId { get; set; }
        public Guid? AssignedById { get; set; }
        public string AssignmentNotes { get; set; }
        public DateTime? ExpectedCompletionDate { get; set; }
    }

    public class AssignmentStatusUpdate
    {
        public Guid AssignmentId { get; set; }
        public string Status { get; set; } // Accepted, Started, Completed
    }
}