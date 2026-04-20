using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class CitizenProfile
{
    [Key]
    public Guid CitizenId { get; set; }

    public Guid UserId { get; set; }

    public int TotalComplaintsFiled { get; set; }
    public int ApprovedComplaintsCount { get; set; }
    [Column("resolved_complaints")]
    public int ResolvedComplaintsCount { get; set; }
    [Column("rejected_complaints")]
    public int RejectedComplaintsCount { get; set; }
    public int ContributionScore { get; set; }
    public int LeaderboardRank { get; set; }
    public string BadgeLevel { get; set; } // Bronze, Silver, Gold
    public int TotalUpvotesReceived { get; set; }
    // Add these timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    
    public virtual User User { get; set; }
}
