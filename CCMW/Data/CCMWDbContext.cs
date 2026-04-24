using CCMW.Models;
using System.Data.Entity;

public class CCMWDbContext : DbContext
{
    public CCMWDbContext() : base("name=CCMWConnectionString")
    {
        this.Configuration.LazyLoadingEnabled = false;
        this.Configuration.ProxyCreationEnabled = false;
    }

    // ----------------------
    // Main Entities
    // ----------------------
    public DbSet<User> Users { get; set; }
    public DbSet<CitizenProfile> CitizenProfiles { get; set; }
    public DbSet<StaffProfile> StaffProfiles { get; set; }
    public DbSet<Zone> Zones { get; set; }
    public DbSet<Department> Departments { get; set; }
    public DbSet<Contractor> Contractors { get; set; }
    public DbSet<ComplaintCategory> ComplaintCategories { get; set; }
    public DbSet<Complaint> Complaints { get; set; }
    public DbSet<ComplaintPhoto> ComplaintPhotos { get; set; }
    public DbSet<ComplaintStatusHistories> ComplaintStatusHistories { get; set; }
    public DbSet<ComplaintAssignment> ComplaintAssignments { get; set; }
    public DbSet<ComplaintUpvote> Upvotes { get; set; }
    public DbSet<Escalation> Escalations { get; set; }
    public DbSet<DuplicateCluster> DuplicateClusters { get; set; }
    public DbSet<DuplicateEntry> DuplicateEntries { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<ZoneDepartment> ZoneDepartments { get; set; }
    public DbSet<ContractPerformance> ContractPerformances { get; set; }
    public DbSet<Appeal> Appeals { get; set; }
    public DbSet<ActivityLog> ActivityLogs { get; set; }
    public DbSet<ComplaintFeedback> ComplaintFeedback { get; set; }
    public DbSet<ContractorZoneAssignment> ContractorZoneAssignments { get; set; }
    public DbSet<ContractorPerformanceHistory> ContractorPerformanceHistories { get; set; }
    public DbSet<SystemSetting> SystemSettings { get; set; }

    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // =====================================================
        // TABLE MAPPINGS
        // =====================================================
        modelBuilder.Entity<User>().ToTable("Users");
        modelBuilder.Entity<CitizenProfile>().ToTable("Citizen_Profile");
        modelBuilder.Entity<StaffProfile>().ToTable("Staff_Profile");
        modelBuilder.Entity<Zone>().ToTable("Zones");
        modelBuilder.Entity<Department>().ToTable("Departments");
        modelBuilder.Entity<Contractor>().ToTable("Contractors");
        modelBuilder.Entity<ComplaintCategory>().ToTable("Complaint_Categories");
        modelBuilder.Entity<Complaint>().ToTable("Complaints");
        modelBuilder.Entity<ComplaintPhoto>().ToTable("Complaint_Photos");
        modelBuilder.Entity<ComplaintStatusHistories>().ToTable("ComplaintStatusHistories");
        modelBuilder.Entity<ComplaintAssignment>().ToTable("ComplaintAssignments");
        modelBuilder.Entity<ComplaintUpvote>().ToTable("Complaint_Upvotes");
        modelBuilder.Entity<Escalation>().ToTable("Complaint_Escalations");
        modelBuilder.Entity<DuplicateCluster>().ToTable("DuplicateClusters");
        modelBuilder.Entity<DuplicateEntry>().ToTable("DuplicateEntries");
        modelBuilder.Entity<Notification>().ToTable("Notifications");
        modelBuilder.Entity<ZoneDepartment>().ToTable("ZoneDepartments");
        modelBuilder.Entity<ContractPerformance>().ToTable("ContractPerformances");
        modelBuilder.Entity<Appeal>().ToTable("Complaint_Appeals");
        modelBuilder.Entity<ActivityLog>().ToTable("ActivityLogs");
        modelBuilder.Entity<ComplaintFeedback>().ToTable("Complaint_Feedback");
        modelBuilder.Entity<ContractorZoneAssignment>().ToTable("ContractorZoneAssignments");
        modelBuilder.Entity<ContractorPerformanceHistory>().ToTable("ContractorPerformanceHistories");

        // =====================================================
        // COLUMN MAPPINGS
        // =====================================================

        // User Entity
        modelBuilder.Entity<User>()
            .Property(u => u.UserId).HasColumnName("user_id");
        modelBuilder.Entity<User>()
            .Property(u => u.UserType).HasColumnName("user_type");
        modelBuilder.Entity<User>()
            .Property(u => u.FullName).HasColumnName("full_name");
        modelBuilder.Entity<User>()
            .Property(u => u.Email).HasColumnName("email");
        modelBuilder.Entity<User>()
            .Property(u => u.PasswordHash).HasColumnName("password_hash");
        modelBuilder.Entity<User>()
            .Property(u => u.PhoneNumber).HasColumnName("PhoneNumber");
        modelBuilder.Entity<User>()
            .Property(u => u.ZoneId).HasColumnName("zone_id");
        modelBuilder.Entity<User>()
            .Property(u => u.IsActive).HasColumnName("is_active");
        modelBuilder.Entity<User>()
            .Property(u => u.CreatedAt).HasColumnName("created_at");
        modelBuilder.Entity<User>()
            .Property(u => u.CNIC).HasColumnName("CNIC");
        modelBuilder.Entity<User>()
            .Property(u => u.Address).HasColumnName("Address");
        modelBuilder.Entity<User>()
            .Property(u => u.ProfilePhotoUrl).HasColumnName("ProfilePhotoUrl");
        modelBuilder.Entity<User>()
            .Property(u => u.IsVerified).HasColumnName("IsVerified");
        modelBuilder.Entity<User>()
            .Property(u => u.UpdatedAt).HasColumnName("UpdatedAt");
        modelBuilder.Entity<User>()
            .Property(u => u.LastLogin).HasColumnName("LastLogin");

        // CitizenProfile Entity
        modelBuilder.Entity<CitizenProfile>()
            .Property(c => c.CitizenId).HasColumnName("citizen_id");
        modelBuilder.Entity<CitizenProfile>()
            .Property(c => c.UserId).HasColumnName("user_id");
        modelBuilder.Entity<CitizenProfile>()
            .Property(c => c.TotalComplaintsFiled).HasColumnName("total_complaints");
        modelBuilder.Entity<CitizenProfile>()
            .Property(c => c.ApprovedComplaintsCount).HasColumnName("approved_complaints");
        modelBuilder.Entity<CitizenProfile>()
            .Property(c => c.ContributionScore).HasColumnName("contribution_score");
        modelBuilder.Entity<CitizenProfile>()
            .Property(c => c.LeaderboardRank).HasColumnName("leaderboard_rank");
        modelBuilder.Entity<CitizenProfile>()
            .Property(c => c.BadgeLevel).HasColumnName("badge_level");
        modelBuilder.Entity<CitizenProfile>()
            .Property(c => c.TotalUpvotesReceived).HasColumnName("total_upvotes");
        modelBuilder.Entity<CitizenProfile>()
            .Property(c => c.CreatedAt).HasColumnName("created_at");
        modelBuilder.Entity<CitizenProfile>()
            .Property(c => c.UpdatedAt).HasColumnName("updated_at");

        // StaffProfile Entity
        modelBuilder.Entity<StaffProfile>()
            .Property(s => s.StaffId).HasColumnName("staff_id");
        modelBuilder.Entity<StaffProfile>()
            .Property(s => s.UserId).HasColumnName("user_id");
        modelBuilder.Entity<StaffProfile>()
            .Property(s => s.DepartmentId).HasColumnName("department_id");
        modelBuilder.Entity<StaffProfile>()
            .Property(s => s.ZoneId).HasColumnName("zone_id");
        modelBuilder.Entity<StaffProfile>()
            .Property(s => s.Role).HasColumnName("role");
        modelBuilder.Entity<StaffProfile>()
            .Property(s => s.EmployeeId).HasColumnName("employee_id");
        modelBuilder.Entity<StaffProfile>()
            .Property(s => s.HireDate).HasColumnName("hire_date");
        modelBuilder.Entity<StaffProfile>()
            .Property(s => s.TotalAssignments).HasColumnName("total_assignments");
        modelBuilder.Entity<StaffProfile>()
            .Property(s => s.CompletedAssignments).HasColumnName("completed_assignments");
        modelBuilder.Entity<StaffProfile>()
            .Property(s => s.PendingAssignments).HasColumnName("pending_assignments");
        modelBuilder.Entity<StaffProfile>()
            .Property(s => s.AverageResolutionTime).HasColumnName("average_resolution_time");
        modelBuilder.Entity<StaffProfile>()
            .Property(s => s.PerformanceScore).HasColumnName("performance_score");
        modelBuilder.Entity<StaffProfile>()
            .Property(s => s.IsAvailable).HasColumnName("is_available");

        // Department Entity
        modelBuilder.Entity<Department>()
            .Property(d => d.DepartmentId).HasColumnName("department_id");
        modelBuilder.Entity<Department>()
            .Property(d => d.DepartmentName).HasColumnName("department_name");
        modelBuilder.Entity<Department>()
            .Property(d => d.PrivatizationStatus).HasColumnName("privatization_status");
        modelBuilder.Entity<Department>()
            .Property(d => d.ContractorId).HasColumnName("contractor_id");
        modelBuilder.Entity<Department>()
            .Property(d => d.PerformanceScore).HasColumnName("performance_score");
        modelBuilder.Entity<Department>()
            .Property(d => d.DepartmentCode).HasColumnName("department_code");
        modelBuilder.Entity<Department>()
            .Property(d => d.Description).HasColumnName("description");
        modelBuilder.Entity<Department>()
            .Property(d => d.HeadAdminId).HasColumnName("head_admin_id");
        modelBuilder.Entity<Department>()
            .Property(d => d.PerformanceRating).HasColumnName("performance_rating");
        modelBuilder.Entity<Department>()
            .Property(d => d.ActiveComplaintsCount).HasColumnName("active_complaints_count");
        modelBuilder.Entity<Department>()
            .Property(d => d.ResolvedComplaintsCount).HasColumnName("resolved_complaints_count");
        modelBuilder.Entity<Department>()
            .Property(d => d.TotalComplaintsCount).HasColumnName("total_complaints_count");
        modelBuilder.Entity<Department>()
            .Property(d => d.AverageResolutionTimeDays).HasColumnName("average_resolution_time_days");
        modelBuilder.Entity<Department>()
            .Property(d => d.IsActive).HasColumnName("is_active");
        modelBuilder.Entity<Department>()
            .Property(d => d.CreatedAt).HasColumnName("created_at");
        modelBuilder.Entity<Department>()
            .Property(d => d.UpdatedAt).HasColumnName("updated_at");

        // Complaint Entity
        modelBuilder.Entity<Complaint>()
            .Property(c => c.ComplaintId).HasColumnName("complaint_id");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.CitizenId).HasColumnName("citizen_id");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.CategoryId).HasColumnName("category_id");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.DepartmentId).HasColumnName("department_id");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.ZoneId).HasColumnName("zone_id");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.Title).HasColumnName("title");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.Description).HasColumnName("description");
        // ✅ FIXED: Legacy Status string maps to "status" column
        modelBuilder.Entity<Complaint>()
            .Property(c => c.Status).HasColumnName("status");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.Priority).HasColumnName("priority");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.EscalationLevel).HasColumnName("escalation_level");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.CreatedAt).HasColumnName("created_at");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.ResolvedAt).HasColumnName("resolved_at");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.LocationAddress).HasColumnName("location_address");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.LocationLatitude).HasColumnName("location_latitude");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.LocationLongitude).HasColumnName("location_longitude");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.LocationLandmark).HasColumnName("location_landmark");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.ComplaintNumber).HasColumnName("ComplaintNumber");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.ApprovedById).HasColumnName("ApprovedById");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.RejectionReason).HasColumnName("RejectionReason");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.StatusUpdatedAt).HasColumnName("StatusUpdatedAt");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.UpvoteCount).HasColumnName("UpvoteCount");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.ViewCount).HasColumnName("ViewCount");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.IsDuplicate).HasColumnName("IsDuplicate");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.MergedIntoComplaintId).HasColumnName("MergedIntoComplaintId");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.UpdatedAt).HasColumnName("UpdatedAt");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.ClosedAt).HasColumnName("ClosedAt");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.ExpectedResolutionDate).HasColumnName("ExpectedResolutionDate");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.IsOverdue).HasColumnName("IsOverdue");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.AssignedToId).HasColumnName("assigned_to_id");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.AssignedAt).HasColumnName("assigned_at");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.SubmissionStatus).HasColumnName("SubmissionStatus");
        // ✅ FIXED: Only ONE mapping for CurrentStatus - removed duplicate "status" mapping
        modelBuilder.Entity<Complaint>()
            .Property(c => c.CurrentStatus).HasColumnName("CurrentStatus");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.ResolutionNotes).HasColumnName("ResolutionNotes");
        modelBuilder.Entity<Complaint>()
            .Property(c => c.ReopenedAt).HasColumnName("ReopenedAt");

        // ComplaintPhoto Entity
        modelBuilder.Entity<ComplaintPhoto>()
            .Property(p => p.PhotoId).HasColumnName("photo_id");
        modelBuilder.Entity<ComplaintPhoto>()
            .Property(p => p.ComplaintId).HasColumnName("complaint_id");
        modelBuilder.Entity<ComplaintPhoto>()
            .Property(p => p.PhotoUrl).HasColumnName("photo_url");
        modelBuilder.Entity<ComplaintPhoto>()
            .Property(p => p.UploadedAt).HasColumnName("uploaded_at");
        modelBuilder.Entity<ComplaintPhoto>()
            .Property(p => p.UploadedById).HasColumnName("UploadedById");
        modelBuilder.Entity<ComplaintPhoto>()
            .Property(p => p.PhotoType).HasColumnName("PhotoType");
        modelBuilder.Entity<ComplaintPhoto>()
            .Property(p => p.PhotoThumbnailUrl).HasColumnName("PhotoThumbnailUrl");
        modelBuilder.Entity<ComplaintPhoto>()
            .Property(p => p.Caption).HasColumnName("Caption");
        modelBuilder.Entity<ComplaintPhoto>()
            .Property(p => p.GpsLatitude).HasColumnName("GpsLatitude");
        modelBuilder.Entity<ComplaintPhoto>()
            .Property(p => p.GpsLongitude).HasColumnName("GpsLongitude");
        modelBuilder.Entity<ComplaintPhoto>()
            .Property(p => p.Metadata).HasColumnName("Metadata");
        modelBuilder.Entity<ComplaintPhoto>()
            .Property(p => p.UploadOrder).HasColumnName("UploadOrder");

        // ComplaintAssignment Entity
        modelBuilder.Entity<ComplaintAssignment>()
            .Property(a => a.AssignmentId).HasColumnName("assignment_id");
        modelBuilder.Entity<ComplaintAssignment>()
            .Property(a => a.ComplaintId).HasColumnName("complaint_id");
        modelBuilder.Entity<ComplaintAssignment>()
            .Property(a => a.AssignedToId).HasColumnName("staff_id");
        modelBuilder.Entity<ComplaintAssignment>()
            .Property(a => a.AssignedById).HasColumnName("AssignedById");
        modelBuilder.Entity<ComplaintAssignment>()
            .Property(a => a.AssignedAt).HasColumnName("assigned_at");
        modelBuilder.Entity<ComplaintAssignment>()
            .Property(a => a.AssignmentType).HasColumnName("AssignmentType");
        modelBuilder.Entity<ComplaintAssignment>()
            .Property(a => a.AssignmentNotes).HasColumnName("AssignmentNotes");
        modelBuilder.Entity<ComplaintAssignment>()
            .Property(a => a.ExpectedCompletionDate).HasColumnName("ExpectedCompletionDate");
        modelBuilder.Entity<ComplaintAssignment>()
            .Property(a => a.IsActive).HasColumnName("IsActive");
        modelBuilder.Entity<ComplaintAssignment>()
            .Property(a => a.AcceptedAt).HasColumnName("AcceptedAt");
        modelBuilder.Entity<ComplaintAssignment>()
            .Property(a => a.StartedAt).HasColumnName("StartedAt");
        modelBuilder.Entity<ComplaintAssignment>()
            .Property(a => a.CompletedAt).HasColumnName("CompletedAt");

        // Appeal Entity
        modelBuilder.Entity<Appeal>()
            .Property(a => a.AppealId).HasColumnName("appeal_id");
        modelBuilder.Entity<Appeal>()
            .Property(a => a.ComplaintId).HasColumnName("complaint_id");
        modelBuilder.Entity<Appeal>()
            .Property(a => a.CitizenId).HasColumnName("citizen_id");
        modelBuilder.Entity<Appeal>()
            .Property(a => a.AppealReason).HasColumnName("appeal_reason");
        modelBuilder.Entity<Appeal>()
            .Property(a => a.AppealStatus).HasColumnName("appeal_status");
        modelBuilder.Entity<Appeal>()
            .Property(a => a.SubmittedAt).HasColumnName("submitted_at");
        modelBuilder.Entity<Appeal>()
            .Property(a => a.ReviewedAt).HasColumnName("reviewed_at");
        modelBuilder.Entity<Appeal>()
            .Property(a => a.ReviewedById).HasColumnName("reviewed_by_id");
        modelBuilder.Entity<Appeal>()
            .Property(a => a.ReviewNotes).HasColumnName("review_notes");
        modelBuilder.Entity<Appeal>()
            .Property(a => a.SupportingDocuments).HasColumnName("supporting_documents");

        // Escalation Entity
        modelBuilder.Entity<Escalation>()
            .Property(e => e.EscalationId).HasColumnName("escalation_id");
        modelBuilder.Entity<Escalation>()
            .Property(e => e.ComplaintId).HasColumnName("complaint_id");
        modelBuilder.Entity<Escalation>()
            .Property(e => e.EscalationLevel).HasColumnName("escalation_level");
        modelBuilder.Entity<Escalation>()
            .Property(e => e.EscalationReason).HasColumnName("reason");
        modelBuilder.Entity<Escalation>()
            .Property(e => e.EscalatedAt).HasColumnName("escalated_at");
        modelBuilder.Entity<Escalation>()
            .Property(e => e.EscalatedFromId).HasColumnName("escalated_from_id");
        modelBuilder.Entity<Escalation>()
            .Property(e => e.EscalatedToId).HasColumnName("escalated_to_id");
        modelBuilder.Entity<Escalation>()
            .Property(e => e.EscalatedById).HasColumnName("escalated_by_id");
        modelBuilder.Entity<Escalation>()
            .Property(e => e.HoursElapsed).HasColumnName("hours_elapsed");
        modelBuilder.Entity<Escalation>()
            .Property(e => e.EscalationNotes).HasColumnName("escalation_notes");
        modelBuilder.Entity<Escalation>()
            .Property(e => e.Resolved).HasColumnName("resolved");
        modelBuilder.Entity<Escalation>()
            .Property(e => e.ResolvedAt).HasColumnName("resolved_at");

        // Zone Entity
        modelBuilder.Entity<Zone>()
            .Property(z => z.ZoneId).HasColumnName("zone_id");
        modelBuilder.Entity<Zone>()
            .Property(z => z.ZoneNumber).HasColumnName("zone_number");
        modelBuilder.Entity<Zone>()
            .Property(z => z.ZoneName).HasColumnName("zone_name");
        modelBuilder.Entity<Zone>()
            .Property(z => z.BoundaryCoordinates).HasColumnName("boundary_coordinates");
        modelBuilder.Entity<Zone>()
            .Property(z => z.City).HasColumnName("city");
        modelBuilder.Entity<Zone>()
            .Property(z => z.Province).HasColumnName("province");
        modelBuilder.Entity<Zone>()
            .Property(z => z.Population).HasColumnName("population");
        modelBuilder.Entity<Zone>()
            .Property(z => z.PerformanceRating).HasColumnName("performance_rating");
        modelBuilder.Entity<Zone>()
            .Property(z => z.CreatedAt).HasColumnName("created_at");
        modelBuilder.Entity<Zone>()
            .Property(z => z.ZoneCode).HasColumnName("zone_code");
        modelBuilder.Entity<Zone>()
            .Property(z => z.TotalAreaSqKm).HasColumnName("total_area_sq_km");
        modelBuilder.Entity<Zone>()
            .Property(z => z.ActiveComplaintsCount).HasColumnName("active_complaints_count");
        modelBuilder.Entity<Zone>()
            .Property(z => z.TotalComplaintsCount).HasColumnName("total_complaints_count");
        modelBuilder.Entity<Zone>()
            .Property(z => z.UpdatedAt).HasColumnName("updated_at");
        modelBuilder.Entity<Zone>()
            .Property(z => z.BoundaryPolygon).HasColumnName("boundary_polygon");
        modelBuilder.Entity<Zone>()
            .Property(z => z.CenterLatitude).HasColumnName("center_latitude");
        modelBuilder.Entity<Zone>()
            .Property(z => z.CenterLongitude).HasColumnName("center_longitude");
        modelBuilder.Entity<Zone>()
            .Property(z => z.ColorCode).HasColumnName("color_code");

        // ComplaintCategory Entity
        modelBuilder.Entity<ComplaintCategory>()
            .Property(c => c.CategoryId).HasColumnName("category_id");
        modelBuilder.Entity<ComplaintCategory>()
            .Property(c => c.CategoryName).HasColumnName("category_name");
        modelBuilder.Entity<ComplaintCategory>()
            .Property(c => c.DepartmentId).HasColumnName("department_id");
        modelBuilder.Entity<ComplaintCategory>()
            .Property(c => c.ExpectedResolutionTimeHours).HasColumnName("expected_resolution_hours");
        modelBuilder.Entity<ComplaintCategory>()
            .Property(c => c.CategoryCode).HasColumnName("category_code");
        modelBuilder.Entity<ComplaintCategory>()
            .Property(c => c.Description).HasColumnName("description");
        modelBuilder.Entity<ComplaintCategory>()
            .Property(c => c.IconName).HasColumnName("icon_name");
        modelBuilder.Entity<ComplaintCategory>()
            .Property(c => c.ColorCode).HasColumnName("color_code");
        modelBuilder.Entity<ComplaintCategory>()
            .Property(c => c.PriorityWeight).HasColumnName("priority_weight");
        modelBuilder.Entity<ComplaintCategory>()
            .Property(c => c.IsActive).HasColumnName("is_active");
        modelBuilder.Entity<ComplaintCategory>()
            .Property(c => c.CreatedAt).HasColumnName("created_at");

        // Contractor Entity
        modelBuilder.Entity<Contractor>()
            .Property(c => c.ContractorId).HasColumnName("contractor_id");
        modelBuilder.Entity<Contractor>()
            .Property(c => c.CompanyName).HasColumnName("company_name");
        modelBuilder.Entity<Contractor>()
            .Property(c => c.CompanyRegistrationNumber).HasColumnName("company_registration_number");
        modelBuilder.Entity<Contractor>()
            .Property(c => c.ContactPersonName).HasColumnName("contact_person_name");
        modelBuilder.Entity<Contractor>()
            .Property(c => c.ContactPersonPhone).HasColumnName("contact_person_phone");
        modelBuilder.Entity<Contractor>()
            .Property(c => c.ContactEmail).HasColumnName("contact_email");
        modelBuilder.Entity<Contractor>()
            .Property(c => c.CompanyAddress).HasColumnName("company_address");
        modelBuilder.Entity<Contractor>()
            .Property(c => c.ContractStart).HasColumnName("contract_start");
        modelBuilder.Entity<Contractor>()
            .Property(c => c.ContractEnd).HasColumnName("contract_end");
        modelBuilder.Entity<Contractor>()
            .Property(c => c.ContractValue).HasColumnName("contract_value");
        modelBuilder.Entity<Contractor>()
            .Property(c => c.PerformanceBond).HasColumnName("performance_bond");
        modelBuilder.Entity<Contractor>()
            .Property(c => c.PerformanceScore).HasColumnName("performance_score");
        modelBuilder.Entity<Contractor>()
            .Property(c => c.SLAComplianceRate).HasColumnName("sla_compliance_rate");
        modelBuilder.Entity<Contractor>()
            .Property(c => c.IsActive).HasColumnName("is_active");
        modelBuilder.Entity<Contractor>()
            .Property(c => c.CreatedAt).HasColumnName("created_at");
        modelBuilder.Entity<Contractor>()
            .Property(c => c.UpdatedAt).HasColumnName("updated_at");

        // ComplaintUpvote Entity
        modelBuilder.Entity<ComplaintUpvote>()
            .Property(u => u.UpvoteId).HasColumnName("upvote_id");
        modelBuilder.Entity<ComplaintUpvote>()
            .Property(u => u.ComplaintId).HasColumnName("complaint_id");
        modelBuilder.Entity<ComplaintUpvote>()
            .Property(u => u.CitizenId).HasColumnName("citizen_id");
        modelBuilder.Entity<ComplaintUpvote>()
            .Property(u => u.CreatedAt).HasColumnName("upvoted_at");
        modelBuilder.Entity<ComplaintUpvote>()
            .Property(u => u.Notes).HasColumnName("Notes");

        // ComplaintStatusHistories Entity
        modelBuilder.Entity<ComplaintStatusHistories>()
            .Property(h => h.HistoryId).HasColumnName("history_id");
        modelBuilder.Entity<ComplaintStatusHistories>()
            .Property(h => h.ComplaintId).HasColumnName("complaint_id");
        modelBuilder.Entity<ComplaintStatusHistories>()
            .Property(h => h.PreviousStatus).HasColumnName("old_status");
        modelBuilder.Entity<ComplaintStatusHistories>()
            .Property(h => h.NewStatus).HasColumnName("new_status");
        modelBuilder.Entity<ComplaintStatusHistories>()
            .Property(h => h.ChangedById).HasColumnName("changed_by");
        modelBuilder.Entity<ComplaintStatusHistories>()
            .Property(h => h.ChangedAt).HasColumnName("changed_at");
        modelBuilder.Entity<ComplaintStatusHistories>()
            .Property(h => h.ChangeReason).HasColumnName("ChangeReason");
        modelBuilder.Entity<ComplaintStatusHistories>()
            .Property(h => h.Notes).HasColumnName("Notes");

        // =====================================================
        // RELATIONSHIPS
        // =====================================================

        // User - CitizenProfile (1:1)
        modelBuilder.Entity<CitizenProfile>()
            .HasRequired(c => c.User)
            .WithOptional(u => u.CitizenProfile);

        // User - StaffProfile (1:1)
        modelBuilder.Entity<StaffProfile>()
            .HasRequired(s => s.User)
            .WithOptional(u => u.StaffProfile)
            .WillCascadeOnDelete(false);

        // User - Complaints via CitizenId
        modelBuilder.Entity<Complaint>()
            .HasRequired(c => c.Citizen)
            .WithMany(u => u.Complaints)
            .HasForeignKey(c => c.CitizenId)
            .WillCascadeOnDelete(false);

        // User - ApprovedBy on Complaint
        modelBuilder.Entity<Complaint>()
            .HasOptional(c => c.ApprovedBy)
            .WithMany()
            .HasForeignKey(c => c.ApprovedById)
            .WillCascadeOnDelete(false);

        // User - Uploaded Photos
        modelBuilder.Entity<ComplaintPhoto>()
            .HasOptional(p => p.UploadedBy)
            .WithMany(u => u.UploadedPhotos)
            .HasForeignKey(p => p.UploadedById)
            .WillCascadeOnDelete(false);

        // User - Upvotes
        modelBuilder.Entity<ComplaintUpvote>()
            .HasRequired(u => u.Citizen)
            .WithMany(c => c.Upvotes)
            .HasForeignKey(u => u.CitizenId)
            .WillCascadeOnDelete(false);

        // User - Status History
        modelBuilder.Entity<ComplaintStatusHistories>()
            .HasOptional(h => h.ChangedBy)
            .WithMany(u => u.ComplaintStatusHistories)
            .HasForeignKey(h => h.ChangedById)
            .WillCascadeOnDelete(false);

        // User - Appeals (as Citizen)
        modelBuilder.Entity<Appeal>()
            .HasRequired(a => a.Citizen)
            .WithMany()
            .HasForeignKey(a => a.CitizenId)
            .WillCascadeOnDelete(false);

        // User - Appeals (as Reviewer)
        modelBuilder.Entity<Appeal>()
            .HasOptional(a => a.ReviewedBy)
            .WithMany()
            .HasForeignKey(a => a.ReviewedById)
            .WillCascadeOnDelete(false);

        // User - ActivityLogs
        modelBuilder.Entity<ActivityLog>()
            .HasRequired(a => a.User)
            .WithMany(u => u.ActivityLogs)
            .HasForeignKey(a => a.UserId)
            .WillCascadeOnDelete(false);

        // User - Zone
        modelBuilder.Entity<User>()
            .HasOptional(u => u.Zone)
            .WithMany(z => z.Users)
            .HasForeignKey(u => u.ZoneId)
            .WillCascadeOnDelete(false);

        // User - AssignedAssignments
        modelBuilder.Entity<ComplaintAssignment>()
            .HasOptional(a => a.AssignedBy)
            .WithMany(u => u.AssignedAssignments)
            .HasForeignKey(a => a.AssignedById)
            .WillCascadeOnDelete(false);

        // =====================================================
        // COMPLAINT RELATIONSHIPS
        // =====================================================

        // Complaint - Category
        modelBuilder.Entity<Complaint>()
            .HasRequired(c => c.Category)
            .WithMany(cat => cat.Complaints)
            .HasForeignKey(c => c.CategoryId)
            .WillCascadeOnDelete(false);

        // Complaint - Department
        modelBuilder.Entity<Complaint>()
            .HasRequired(c => c.Department)
            .WithMany(d => d.Complaints)
            .HasForeignKey(c => c.DepartmentId)
            .WillCascadeOnDelete(false);

        // Complaint - Zone
        modelBuilder.Entity<Complaint>()
            .HasRequired(c => c.Zone)
            .WithMany(z => z.Complaints)
            .HasForeignKey(c => c.ZoneId)
            .WillCascadeOnDelete(false);

        // Complaint - AssignedTo (Staff)
        modelBuilder.Entity<Complaint>()
            .HasOptional(c => c.AssignedTo)
            .WithMany(s => s.AssignedComplaints)
            .HasForeignKey(c => c.AssignedToId)
            .WillCascadeOnDelete(false);

        // Complaint - MergedIntoComplaint
        modelBuilder.Entity<Complaint>()
            .HasOptional(c => c.MergedIntoComplaint)
            .WithMany()
            .HasForeignKey(c => c.MergedIntoComplaintId)
            .WillCascadeOnDelete(false);

        // =====================================================
        // COMPLAINT ASSIGNMENT RELATIONSHIPS
        // =====================================================

        // ComplaintAssignment - Complaint
        modelBuilder.Entity<ComplaintAssignment>()
            .HasRequired(a => a.Complaint)
            .WithMany(c => c.Assignments)
            .HasForeignKey(a => a.ComplaintId)
            .WillCascadeOnDelete(false);

        // ComplaintAssignment - AssignedTo (Staff)
        modelBuilder.Entity<ComplaintAssignment>()
            .HasOptional(a => a.AssignedTo)
            .WithMany(s => s.Assignments)
            .HasForeignKey(a => a.AssignedToId)
            .WillCascadeOnDelete(false);

        // =====================================================
        // COMPLAINT PHOTO RELATIONSHIPS
        // =====================================================

        modelBuilder.Entity<ComplaintPhoto>()
            .HasRequired(p => p.Complaint)
            .WithMany(c => c.ComplaintPhotos)
            .HasForeignKey(p => p.ComplaintId)
            .WillCascadeOnDelete(false);

        // =====================================================
        // COMPLAINT STATUS HISTORY RELATIONSHIPS
        // =====================================================

        modelBuilder.Entity<ComplaintStatusHistories>()
            .HasRequired(h => h.Complaint)
            .WithMany(c => c.StatusHistory)
            .HasForeignKey(h => h.ComplaintId)
            .WillCascadeOnDelete(false);

        // =====================================================
        // COMPLAINT UPVOTE RELATIONSHIPS
        // =====================================================

        modelBuilder.Entity<ComplaintUpvote>()
            .HasRequired(u => u.Complaint)
            .WithMany(c => c.Upvotes)
            .HasForeignKey(u => u.ComplaintId)
            .WillCascadeOnDelete(false);

        // =====================================================
        // ESCALATION RELATIONSHIPS
        // =====================================================

        modelBuilder.Entity<Escalation>()
            .HasRequired(e => e.Complaint)
            .WithMany(c => c.Escalations)
            .HasForeignKey(e => e.ComplaintId)
            .WillCascadeOnDelete(false);

        modelBuilder.Entity<Escalation>()
            .HasRequired(e => e.EscalatedFrom)
            .WithMany()
            .HasForeignKey(e => e.EscalatedFromId)
            .WillCascadeOnDelete(false);

        modelBuilder.Entity<Escalation>()
            .HasRequired(e => e.EscalatedTo)
            .WithMany()
            .HasForeignKey(e => e.EscalatedToId)
            .WillCascadeOnDelete(false);

        modelBuilder.Entity<Escalation>()
            .HasRequired(e => e.EscalatedBy)
            .WithMany()
            .HasForeignKey(e => e.EscalatedById)
            .WillCascadeOnDelete(false);

        // =====================================================
        // APPEAL RELATIONSHIPS
        // =====================================================

        modelBuilder.Entity<Appeal>()
            .HasRequired(a => a.Complaint)
            .WithMany()
            .HasForeignKey(a => a.ComplaintId)
            .WillCascadeOnDelete(false);

        // =====================================================
        // COMPLAINT FEEDBACK RELATIONSHIPS
        // =====================================================

        modelBuilder.Entity<ComplaintFeedback>()
            .HasRequired(f => f.Complaint)
            .WithMany(c => c.Feedback)
            .HasForeignKey(f => f.ComplaintId)
            .WillCascadeOnDelete(false);

        modelBuilder.Entity<ComplaintFeedback>()
            .HasRequired(f => f.Citizen)
            .WithMany()
            .HasForeignKey(f => f.CitizenId)
            .WillCascadeOnDelete(false);

        // =====================================================
        // DEPARTMENT RELATIONSHIPS
        // =====================================================

        modelBuilder.Entity<Department>()
            .HasOptional(d => d.Contractor)
            .WithMany(c => c.Departments)
            .HasForeignKey(d => d.ContractorId)
            .WillCascadeOnDelete(false);

        modelBuilder.Entity<Department>()
            .HasOptional(d => d.HeadAdmin)
            .WithMany()
            .HasForeignKey(d => d.HeadAdminId)
            .WillCascadeOnDelete(false);

        // =====================================================
        // COMPLAINT CATEGORY RELATIONSHIPS
        // =====================================================

        modelBuilder.Entity<ComplaintCategory>()
            .HasRequired(cat => cat.Department)
            .WithMany(d => d.ComplaintCategories)
            .HasForeignKey(cat => cat.DepartmentId)
            .WillCascadeOnDelete(false);

        // =====================================================
        // STAFF PROFILE RELATIONSHIPS
        // =====================================================

        modelBuilder.Entity<StaffProfile>()
            .HasOptional(s => s.Department)
            .WithMany(d => d.Staffs)
            .HasForeignKey(s => s.DepartmentId)
            .WillCascadeOnDelete(false);

        modelBuilder.Entity<StaffProfile>()
            .HasOptional(s => s.Zone)
            .WithMany(z => z.Staffs)
            .HasForeignKey(s => s.ZoneId)
            .WillCascadeOnDelete(false);

        // =====================================================
        // DUPLICATE CLUSTER RELATIONSHIPS
        // =====================================================

        modelBuilder.Entity<DuplicateCluster>()
            .HasRequired(d => d.PrimaryComplaint)
            .WithMany()
            .HasForeignKey(d => d.PrimaryComplaintId)
            .WillCascadeOnDelete(false);

        modelBuilder.Entity<DuplicateCluster>()
            .HasRequired(d => d.Category)
            .WithMany()
            .HasForeignKey(d => d.CategoryId)
            .WillCascadeOnDelete(false);

        modelBuilder.Entity<DuplicateEntry>()
            .HasRequired(d => d.Cluster)
            .WithMany(c => c.DuplicateEntries)
            .HasForeignKey(d => d.ClusterId)
            .WillCascadeOnDelete(false);

        modelBuilder.Entity<DuplicateEntry>()
            .HasRequired(d => d.Complaint)
            .WithMany()
            .HasForeignKey(d => d.ComplaintId)
            .WillCascadeOnDelete(false);

        modelBuilder.Entity<DuplicateEntry>()
            .HasOptional(d => d.MergedBy)
            .WithMany()
            .HasForeignKey(d => d.MergedById)
            .WillCascadeOnDelete(false);

        // =====================================================
        // CONTRACTOR RELATIONSHIPS
        // =====================================================

        modelBuilder.Entity<ContractPerformance>()
            .HasRequired(cp => cp.Contractor)
            .WithMany(c => c.ContractPerformances)
            .HasForeignKey(cp => cp.ContractorId)
            .WillCascadeOnDelete(false);

        modelBuilder.Entity<ContractPerformance>()
            .HasRequired(cp => cp.Department)
            .WithMany()
            .HasForeignKey(cp => cp.DepartmentId)
            .WillCascadeOnDelete(false);

        modelBuilder.Entity<ContractPerformance>()
            .HasRequired(cp => cp.ReviewedBy)
            .WithMany()
            .HasForeignKey(cp => cp.ReviewedById)
            .WillCascadeOnDelete(false);

        modelBuilder.Entity<ContractorZoneAssignment>()
            .HasRequired(cza => cza.Contractor)
            .WithMany(c => c.ZoneAssignments)
            .HasForeignKey(cza => cza.ContractorId)
            .WillCascadeOnDelete(false);

        modelBuilder.Entity<ContractorZoneAssignment>()
            .HasRequired(cza => cza.Zone)
            .WithMany(z => z.ContractorAssignments)
            .HasForeignKey(cza => cza.ZoneId)
            .WillCascadeOnDelete(false);

        modelBuilder.Entity<ContractorZoneAssignment>()
            .HasRequired(cza => cza.AssignedByAdmin)
            .WithMany()
            .HasForeignKey(cza => cza.AssignedBy)
            .WillCascadeOnDelete(false);

        modelBuilder.Entity<ContractorZoneAssignment>()
            .HasOptional(cza => cza.TerminatedByAdmin)
            .WithMany()
            .HasForeignKey(cza => cza.TerminatedBy)
            .WillCascadeOnDelete(false);

        modelBuilder.Entity<ContractorPerformanceHistory>()
            .HasRequired(cph => cph.Contractor)
            .WithMany(c => c.PerformanceHistory)
            .HasForeignKey(cph => cph.ContractorId)
            .WillCascadeOnDelete(false);

        modelBuilder.Entity<ContractorPerformanceHistory>()
            .HasOptional(cph => cph.Zone)
            .WithMany()
            .HasForeignKey(cph => cph.ZoneId)
            .WillCascadeOnDelete(false);

        modelBuilder.Entity<ContractorPerformanceHistory>()
            .HasOptional(cph => cph.Reviewer)
            .WithMany()
            .HasForeignKey(cph => cph.ReviewedBy)
            .WillCascadeOnDelete(false);

        // =====================================================
        // ZONE DEPARTMENT RELATIONSHIPS
        // =====================================================

        modelBuilder.Entity<ZoneDepartment>()
            .HasRequired(zd => zd.Zone)
            .WithMany(z => z.ZoneDepartments)
            .HasForeignKey(zd => zd.ZoneId)
            .WillCascadeOnDelete(false);

        modelBuilder.Entity<ZoneDepartment>()
            .HasRequired(zd => zd.Department)
            .WithMany(d => d.ZoneDepartments)
            .HasForeignKey(zd => zd.DepartmentId)
            .WillCascadeOnDelete(false);
    }
}