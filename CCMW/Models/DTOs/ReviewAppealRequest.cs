using System;

namespace CCMW.DTOs
{
    public class ReviewAppealRequest
    {
        public Guid AdminId { get; set; }
        public string Status { get; set; } // Approved or Rejected
        public string ReviewNotes { get; set; }
    }
}