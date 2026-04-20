using System;
using System.Collections.Generic;

namespace CCMW.DTOs
{
    public class MergeRequest
    {
        public Guid PrimaryComplaintId { get; set; }
        public List<Guid> DuplicateComplaintIds { get; set; }
        public int RadiusMeters { get; set; } = 100;
        public Guid MergedByUserId { get; set; }
        public Dictionary<Guid, double> SimilarityScores { get; set; }
    }
}