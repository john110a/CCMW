using CCMW.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("DuplicateEntries")] // Specify the table name
public class DuplicateEntry
{
    [Key]
    [Column("entry_id")] // Map to database column
    public Guid EntryId { get; set; }

    [Column("cluster_id")]
    public Guid ClusterId { get; set; }

    [Column("complaint_id")]
    public Guid ComplaintId { get; set; }

    [Column("similarity_score")]
    public decimal SimilarityScore { get; set; }

    [Column("similarity_factors")]
    public string SimilarityFactors { get; set; } // JSON details

    [Column("merged_at")]
    public DateTime? MergedAt { get; set; }

    [Column("merged_by_id")]
    public Guid? MergedById { get; set; }

    // Navigation properties
    [ForeignKey("ClusterId")]
    public virtual DuplicateCluster Cluster { get; set; }

    [ForeignKey("ComplaintId")]
    public virtual Complaint Complaint { get; set; }

    [ForeignKey("MergedById")]
    public virtual User MergedBy { get; set; }

    // Remove this duplicate property - it's already defined above as 'Cluster'
    // public DuplicateCluster DuplicateCluster { get; internal set; }
}