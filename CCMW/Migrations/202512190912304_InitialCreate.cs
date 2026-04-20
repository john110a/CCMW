namespace CCMW.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.ActivityLogs",
                c => new
                    {
                        LogId = c.Guid(nullable: false),
                        UserId = c.Guid(nullable: false),
                        ActionType = c.String(),
                        EntityType = c.String(),
                        EntityId = c.Guid(),
                        ActionDetails = c.String(),
                        IpAddress = c.String(),
                        UserAgent = c.String(),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.LogId)
                .ForeignKey("dbo.Users", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.Users",
                c => new
                    {
                        UserId = c.Guid(nullable: false),
                        UserType = c.Int(nullable: false),
                        Email = c.String(nullable: false, maxLength: 255),
                        PasswordHash = c.String(nullable: false),
                        FullName = c.String(nullable: false, maxLength: 200),
                        PhoneNumber = c.String(nullable: false, maxLength: 20),
                        Cnic = c.String(maxLength: 20),
                        Address = c.String(),
                        ZoneId = c.Guid(),
                        ProfilePhotoUrl = c.String(),
                        IsActive = c.Boolean(nullable: false),
                        IsVerified = c.Boolean(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        UpdatedAt = c.DateTime(nullable: false),
                        LastLogin = c.DateTime(),
                    })
                .PrimaryKey(t => t.UserId)
                .ForeignKey("dbo.Zones", t => t.ZoneId)
                .Index(t => t.ZoneId);
            
            CreateTable(
                "dbo.ComplaintAssignments",
                c => new
                    {
                        AssignmentId = c.Guid(nullable: false),
                        ComplaintId = c.Guid(nullable: false),
                        AssignedToId = c.Guid(nullable: false),
                        AssignedById = c.Guid(nullable: false),
                        AssignmentType = c.String(),
                        AssignmentNotes = c.String(),
                        ExpectedCompletionDate = c.DateTime(),
                        IsActive = c.Boolean(nullable: false),
                        AcceptedAt = c.DateTime(),
                        StartedAt = c.DateTime(),
                        CompletedAt = c.DateTime(),
                        AssignedAt = c.DateTime(nullable: false),
                        User_UserId = c.Guid(),
                    })
                .PrimaryKey(t => t.AssignmentId)
                .ForeignKey("dbo.Users", t => t.AssignedById, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.AssignedToId, cascadeDelete: true)
                .ForeignKey("dbo.Complaints", t => t.ComplaintId, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.User_UserId)
                .Index(t => t.ComplaintId)
                .Index(t => t.AssignedToId)
                .Index(t => t.AssignedById)
                .Index(t => t.User_UserId);
            
            CreateTable(
                "dbo.Complaints",
                c => new
                    {
                        ComplaintId = c.Guid(nullable: false),
                        ComplaintNumber = c.String(),
                        CitizenId = c.Guid(nullable: false),
                        CategoryId = c.Guid(nullable: false),
                        DepartmentId = c.Guid(nullable: false),
                        ZoneId = c.Guid(nullable: false),
                        Title = c.String(nullable: false),
                        Description = c.String(nullable: false),
                        LocationAddress = c.String(nullable: false),
                        LocationLatitude = c.Decimal(nullable: false, precision: 18, scale: 2),
                        LocationLongitude = c.Decimal(nullable: false, precision: 18, scale: 2),
                        LocationLandmark = c.String(),
                        SubmissionStatus = c.Int(nullable: false),
                        ApprovalStatusUpdatedAt = c.DateTime(),
                        ApprovedById = c.Guid(),
                        RejectionReason = c.String(),
                        CurrentStatus = c.Int(nullable: false),
                        StatusUpdatedAt = c.DateTime(),
                        PriorityLevel = c.Int(nullable: false),
                        EscalationLevel = c.Int(nullable: false),
                        EscalationDate = c.DateTime(),
                        AssignedToId = c.Guid(),
                        AssignedAt = c.DateTime(),
                        ResolutionDescription = c.String(),
                        ResolvedAt = c.DateTime(),
                        ResolvedById = c.Guid(),
                        ResolutionVerified = c.Boolean(nullable: false),
                        VerificationStatus = c.String(),
                        VerifiedById = c.Guid(),
                        VerifiedAt = c.DateTime(),
                        UpvoteCount = c.Int(nullable: false),
                        ViewCount = c.Int(nullable: false),
                        IsDuplicate = c.Boolean(nullable: false),
                        MergedIntoComplaintId = c.Guid(),
                        CreatedAt = c.DateTime(nullable: false),
                        UpdatedAt = c.DateTime(nullable: false),
                        ClosedAt = c.DateTime(),
                        ExpectedResolutionDate = c.DateTime(),
                        IsOverdue = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.ComplaintId)
                .ForeignKey("dbo.ComplaintCategories", t => t.CategoryId, cascadeDelete: true)
                .ForeignKey("dbo.Departments", t => t.DepartmentId, cascadeDelete: true)
                .ForeignKey("dbo.Zones", t => t.ZoneId, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.CitizenId, cascadeDelete: true)
                .Index(t => t.CitizenId)
                .Index(t => t.CategoryId)
                .Index(t => t.DepartmentId)
                .Index(t => t.ZoneId);
            
            CreateTable(
                "dbo.ComplaintCategories",
                c => new
                    {
                        CategoryId = c.Guid(nullable: false),
                        CategoryName = c.String(nullable: false),
                        CategoryCode = c.String(),
                        Description = c.String(),
                        DepartmentId = c.Guid(nullable: false),
                        IconName = c.String(),
                        ColorCode = c.String(),
                        PriorityWeight = c.Int(nullable: false),
                        ExpectedResolutionTimeHours = c.Int(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.CategoryId)
                .ForeignKey("dbo.Departments", t => t.DepartmentId, cascadeDelete: true)
                .Index(t => t.DepartmentId);
            
            CreateTable(
                "dbo.Departments",
                c => new
                    {
                        DepartmentId = c.Guid(nullable: false),
                        DepartmentName = c.String(),
                        DepartmentCode = c.String(),
                        Description = c.String(),
                        PrivatizationStatus = c.Int(nullable: false),
                        ContractorId = c.Guid(),
                        HeadAdminId = c.Guid(),
                        PerformanceScore = c.Decimal(nullable: false, precision: 18, scale: 2),
                        PerformanceRating = c.Int(nullable: false),
                        ActiveComplaintsCount = c.Int(nullable: false),
                        ResolvedComplaintsCount = c.Int(nullable: false),
                        AverageResolutionTimeDays = c.Decimal(nullable: false, precision: 18, scale: 2),
                        IsActive = c.Boolean(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        UpdatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.DepartmentId)
                .ForeignKey("dbo.Contractors", t => t.ContractorId)
                .Index(t => t.ContractorId);
            
            CreateTable(
                "dbo.Contractors",
                c => new
                    {
                        ContractorId = c.Guid(nullable: false),
                        CompanyName = c.String(),
                        CompanyRegistrationNumber = c.String(),
                        ContactPersonName = c.String(),
                        ContactPersonPhone = c.String(),
                        ContactPersonEmail = c.String(),
                        CompanyAddress = c.String(),
                        ContractStartDate = c.DateTime(nullable: false),
                        ContractEndDate = c.DateTime(nullable: false),
                        ContractValue = c.Decimal(nullable: false, precision: 18, scale: 2),
                        PerformanceBond = c.Decimal(nullable: false, precision: 18, scale: 2),
                        PerformanceScore = c.Decimal(nullable: false, precision: 18, scale: 2),
                        SLAComplianceRate = c.Decimal(nullable: false, precision: 18, scale: 2),
                        IsActive = c.Boolean(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        UpdatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.ContractorId);
            
            CreateTable(
                "dbo.ContractPerformances",
                c => new
                    {
                        PerformanceId = c.Guid(nullable: false),
                        ContractorId = c.Guid(nullable: false),
                        DepartmentId = c.Guid(nullable: false),
                        ReviewPeriodStart = c.DateTime(nullable: false),
                        ReviewPeriodEnd = c.DateTime(nullable: false),
                        ComplaintsHandled = c.Int(nullable: false),
                        ComplaintsResolved = c.Int(nullable: false),
                        AverageResolutionTimeDays = c.Decimal(nullable: false, precision: 18, scale: 2),
                        SlaComplianceRate = c.Decimal(nullable: false, precision: 18, scale: 2),
                        CitizenSatisfactionScore = c.Decimal(nullable: false, precision: 18, scale: 2),
                        PerformanceScore = c.Decimal(nullable: false, precision: 18, scale: 2),
                        PenaltiesIncurred = c.Decimal(nullable: false, precision: 18, scale: 2),
                        BonusesEarned = c.Decimal(nullable: false, precision: 18, scale: 2),
                        ReviewNotes = c.String(),
                        ReviewedById = c.Guid(nullable: false),
                        ReviewedAt = c.DateTime(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.PerformanceId)
                .ForeignKey("dbo.Contractors", t => t.ContractorId, cascadeDelete: true)
                .ForeignKey("dbo.Departments", t => t.DepartmentId, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.ReviewedById, cascadeDelete: true)
                .Index(t => t.ContractorId)
                .Index(t => t.DepartmentId)
                .Index(t => t.ReviewedById);
            
            CreateTable(
                "dbo.StaffProfiles",
                c => new
                    {
                        StaffId = c.Guid(nullable: false),
                        UserId = c.Guid(nullable: false),
                        Role = c.String(),
                        DepartmentId = c.Guid(),
                        ZoneId = c.Guid(),
                        EmployeeId = c.String(),
                        HireDate = c.DateTime(nullable: false),
                        TotalAssignments = c.Int(nullable: false),
                        CompletedAssignments = c.Int(nullable: false),
                        PendingAssignments = c.Int(nullable: false),
                        AverageResolutionTime = c.Decimal(nullable: false, precision: 18, scale: 2),
                        PerformanceScore = c.Decimal(nullable: false, precision: 18, scale: 2),
                        IsAvailable = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.StaffId)
                .ForeignKey("dbo.Users", t => t.StaffId)
                .ForeignKey("dbo.Departments", t => t.DepartmentId)
                .ForeignKey("dbo.Zones", t => t.ZoneId)
                .Index(t => t.StaffId)
                .Index(t => t.DepartmentId)
                .Index(t => t.ZoneId);
            
            CreateTable(
                "dbo.ZoneDepartments",
                c => new
                    {
                        ZoneDeptId = c.Guid(nullable: false),
                        ZoneId = c.Guid(nullable: false),
                        DepartmentId = c.Guid(nullable: false),
                        StaffCount = c.Int(nullable: false),
                        ActiveComplaintsCount = c.Int(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.ZoneDeptId)
                .ForeignKey("dbo.Departments", t => t.DepartmentId, cascadeDelete: true)
                .ForeignKey("dbo.Zones", t => t.ZoneId, cascadeDelete: true)
                .Index(t => t.ZoneId)
                .Index(t => t.DepartmentId);
            
            CreateTable(
                "dbo.Zones",
                c => new
                    {
                        ZoneId = c.Guid(nullable: false),
                        ZoneNumber = c.Int(nullable: false),
                        ZoneName = c.String(),
                        ZoneCode = c.String(),
                        BoundaryCoordinates = c.String(),
                        City = c.String(),
                        Province = c.String(),
                        TotalAreaSqKm = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Population = c.Int(nullable: false),
                        ActiveComplaintsCount = c.Int(nullable: false),
                        TotalComplaintsCount = c.Int(nullable: false),
                        PerformanceRating = c.Int(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        UpdatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.ZoneId);
            
            CreateTable(
                "dbo.ComplaintPhotoes",
                c => new
                    {
                        PhotoId = c.Guid(nullable: false),
                        ComplaintId = c.Guid(nullable: false),
                        PhotoType = c.String(),
                        PhotoUrl = c.String(nullable: false),
                        PhotoThumbnailUrl = c.String(),
                        Caption = c.String(),
                        GpsLatitude = c.Decimal(nullable: false, precision: 18, scale: 2),
                        GpsLongitude = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Metadata = c.String(),
                        UploadOrder = c.Int(nullable: false),
                        UploadedById = c.Guid(nullable: false),
                        UploadedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.PhotoId)
                .ForeignKey("dbo.Complaints", t => t.ComplaintId, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.UploadedById, cascadeDelete: true)
                .Index(t => t.ComplaintId)
                .Index(t => t.UploadedById);
            
            CreateTable(
                "dbo.ComplaintStatusHistories",
                c => new
                    {
                        HistoryId = c.Guid(nullable: false),
                        ComplaintId = c.Guid(nullable: false),
                        PreviousStatus = c.String(),
                        NewStatus = c.String(),
                        ChangedById = c.Guid(nullable: false),
                        ChangeReason = c.String(),
                        Notes = c.String(),
                        ChangedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.HistoryId)
                .ForeignKey("dbo.Users", t => t.ChangedById, cascadeDelete: true)
                .ForeignKey("dbo.Complaints", t => t.ComplaintId, cascadeDelete: true)
                .Index(t => t.ComplaintId)
                .Index(t => t.ChangedById);
            
            CreateTable(
                "dbo.Upvotes",
                c => new
                    {
                        UpvoteId = c.Guid(nullable: false),
                        ComplaintId = c.Guid(nullable: false),
                        CitizenId = c.Guid(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.UpvoteId)
                .ForeignKey("dbo.Users", t => t.CitizenId, cascadeDelete: true)
                .ForeignKey("dbo.Complaints", t => t.ComplaintId, cascadeDelete: true)
                .Index(t => t.ComplaintId)
                .Index(t => t.CitizenId);
            
            CreateTable(
                "dbo.CitizenProfiles",
                c => new
                    {
                        CitizenId = c.Guid(nullable: false),
                        UserId = c.Guid(nullable: false),
                        TotalComplaintsFiled = c.Int(nullable: false),
                        ApprovedComplaintsCount = c.Int(nullable: false),
                        ResolvedComplaintsCount = c.Int(nullable: false),
                        RejectedComplaintsCount = c.Int(nullable: false),
                        ContributionScore = c.Int(nullable: false),
                        LeaderboardRank = c.Int(nullable: false),
                        BadgeLevel = c.String(),
                        TotalUpvotesReceived = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.CitizenId)
                .ForeignKey("dbo.Users", t => t.CitizenId)
                .Index(t => t.CitizenId);
            
            CreateTable(
                "dbo.Appeals",
                c => new
                    {
                        AppealId = c.Guid(nullable: false),
                        ComplaintId = c.Guid(nullable: false),
                        CitizenId = c.Guid(nullable: false),
                        AppealReason = c.String(),
                        SupportingDocuments = c.String(),
                        AppealStatus = c.String(),
                        ReviewedById = c.Guid(),
                        ReviewNotes = c.String(),
                        SubmittedAt = c.DateTime(nullable: false),
                        ReviewedAt = c.DateTime(),
                    })
                .PrimaryKey(t => t.AppealId)
                .ForeignKey("dbo.Users", t => t.CitizenId, cascadeDelete: true)
                .ForeignKey("dbo.Complaints", t => t.ComplaintId, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.ReviewedById)
                .Index(t => t.ComplaintId)
                .Index(t => t.CitizenId)
                .Index(t => t.ReviewedById);
            
            CreateTable(
                "dbo.DuplicateClusters",
                c => new
                    {
                        ClusterId = c.Guid(nullable: false),
                        PrimaryComplaintId = c.Guid(nullable: false),
                        CategoryId = c.Guid(nullable: false),
                        LocationLatitude = c.Decimal(nullable: false, precision: 18, scale: 2),
                        LocationLongitude = c.Decimal(nullable: false, precision: 18, scale: 2),
                        ClusterRadiusMeters = c.Int(nullable: false),
                        TotalComplaintsMerged = c.Int(nullable: false),
                        TotalCombinedUpvotes = c.Int(nullable: false),
                        AiSimilarityScore = c.Decimal(nullable: false, precision: 18, scale: 2),
                        CreatedAt = c.DateTime(nullable: false),
                        UpdatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.ClusterId)
                .ForeignKey("dbo.ComplaintCategories", t => t.CategoryId, cascadeDelete: true)
                .ForeignKey("dbo.Complaints", t => t.PrimaryComplaintId, cascadeDelete: true)
                .Index(t => t.PrimaryComplaintId)
                .Index(t => t.CategoryId);
            
            CreateTable(
                "dbo.DuplicateEntries",
                c => new
                    {
                        EntryId = c.Guid(nullable: false),
                        ClusterId = c.Guid(nullable: false),
                        ComplaintId = c.Guid(nullable: false),
                        SimilarityScore = c.Decimal(nullable: false, precision: 18, scale: 2),
                        SimilarityFactors = c.String(),
                        MergedAt = c.DateTime(),
                        MergedById = c.Guid(),
                    })
                .PrimaryKey(t => t.EntryId)
                .ForeignKey("dbo.DuplicateClusters", t => t.ClusterId, cascadeDelete: true)
                .ForeignKey("dbo.Complaints", t => t.ComplaintId, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.MergedById)
                .Index(t => t.ClusterId)
                .Index(t => t.ComplaintId)
                .Index(t => t.MergedById);
            
            CreateTable(
                "dbo.Escalations",
                c => new
                    {
                        EscalationId = c.Guid(nullable: false),
                        ComplaintId = c.Guid(nullable: false),
                        EscalationLevel = c.Int(nullable: false),
                        EscalatedFromId = c.Guid(nullable: false),
                        EscalatedToId = c.Guid(nullable: false),
                        EscalatedById = c.Guid(nullable: false),
                        EscalationReason = c.String(),
                        HoursElapsed = c.Decimal(nullable: false, precision: 18, scale: 2),
                        EscalationNotes = c.String(),
                        Resolved = c.Boolean(nullable: false),
                        ResolvedAt = c.DateTime(),
                        EscalatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.EscalationId)
                .ForeignKey("dbo.Complaints", t => t.ComplaintId, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.EscalatedById, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.EscalatedFromId, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.EscalatedToId, cascadeDelete: true)
                .Index(t => t.ComplaintId)
                .Index(t => t.EscalatedFromId)
                .Index(t => t.EscalatedToId)
                .Index(t => t.EscalatedById);
            
            CreateTable(
                "dbo.Notifications",
                c => new
                    {
                        NotificationId = c.Guid(nullable: false),
                        UserId = c.Guid(nullable: false),
                        NotificationType = c.String(),
                        Title = c.String(),
                        Message = c.String(),
                        ReferenceType = c.String(),
                        ReferenceId = c.Guid(),
                        IsRead = c.Boolean(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        ReadAt = c.DateTime(),
                    })
                .PrimaryKey(t => t.NotificationId)
                .ForeignKey("dbo.Users", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Notifications", "UserId", "dbo.Users");
            DropForeignKey("dbo.Escalations", "EscalatedToId", "dbo.Users");
            DropForeignKey("dbo.Escalations", "EscalatedFromId", "dbo.Users");
            DropForeignKey("dbo.Escalations", "EscalatedById", "dbo.Users");
            DropForeignKey("dbo.Escalations", "ComplaintId", "dbo.Complaints");
            DropForeignKey("dbo.DuplicateClusters", "PrimaryComplaintId", "dbo.Complaints");
            DropForeignKey("dbo.DuplicateEntries", "MergedById", "dbo.Users");
            DropForeignKey("dbo.DuplicateEntries", "ComplaintId", "dbo.Complaints");
            DropForeignKey("dbo.DuplicateEntries", "ClusterId", "dbo.DuplicateClusters");
            DropForeignKey("dbo.DuplicateClusters", "CategoryId", "dbo.ComplaintCategories");
            DropForeignKey("dbo.Appeals", "ReviewedById", "dbo.Users");
            DropForeignKey("dbo.Appeals", "ComplaintId", "dbo.Complaints");
            DropForeignKey("dbo.Appeals", "CitizenId", "dbo.Users");
            DropForeignKey("dbo.CitizenProfiles", "CitizenId", "dbo.Users");
            DropForeignKey("dbo.ComplaintAssignments", "User_UserId", "dbo.Users");
            DropForeignKey("dbo.Upvotes", "ComplaintId", "dbo.Complaints");
            DropForeignKey("dbo.Upvotes", "CitizenId", "dbo.Users");
            DropForeignKey("dbo.ComplaintStatusHistories", "ComplaintId", "dbo.Complaints");
            DropForeignKey("dbo.ComplaintStatusHistories", "ChangedById", "dbo.Users");
            DropForeignKey("dbo.ComplaintPhotoes", "UploadedById", "dbo.Users");
            DropForeignKey("dbo.ComplaintPhotoes", "ComplaintId", "dbo.Complaints");
            DropForeignKey("dbo.Complaints", "CitizenId", "dbo.Users");
            DropForeignKey("dbo.ZoneDepartments", "ZoneId", "dbo.Zones");
            DropForeignKey("dbo.Users", "ZoneId", "dbo.Zones");
            DropForeignKey("dbo.StaffProfiles", "ZoneId", "dbo.Zones");
            DropForeignKey("dbo.Complaints", "ZoneId", "dbo.Zones");
            DropForeignKey("dbo.ZoneDepartments", "DepartmentId", "dbo.Departments");
            DropForeignKey("dbo.StaffProfiles", "DepartmentId", "dbo.Departments");
            DropForeignKey("dbo.StaffProfiles", "StaffId", "dbo.Users");
            DropForeignKey("dbo.Departments", "ContractorId", "dbo.Contractors");
            DropForeignKey("dbo.ContractPerformances", "ReviewedById", "dbo.Users");
            DropForeignKey("dbo.ContractPerformances", "DepartmentId", "dbo.Departments");
            DropForeignKey("dbo.ContractPerformances", "ContractorId", "dbo.Contractors");
            DropForeignKey("dbo.Complaints", "DepartmentId", "dbo.Departments");
            DropForeignKey("dbo.ComplaintCategories", "DepartmentId", "dbo.Departments");
            DropForeignKey("dbo.Complaints", "CategoryId", "dbo.ComplaintCategories");
            DropForeignKey("dbo.ComplaintAssignments", "ComplaintId", "dbo.Complaints");
            DropForeignKey("dbo.ComplaintAssignments", "AssignedToId", "dbo.Users");
            DropForeignKey("dbo.ComplaintAssignments", "AssignedById", "dbo.Users");
            DropForeignKey("dbo.ActivityLogs", "UserId", "dbo.Users");
            DropIndex("dbo.Notifications", new[] { "UserId" });
            DropIndex("dbo.Escalations", new[] { "EscalatedById" });
            DropIndex("dbo.Escalations", new[] { "EscalatedToId" });
            DropIndex("dbo.Escalations", new[] { "EscalatedFromId" });
            DropIndex("dbo.Escalations", new[] { "ComplaintId" });
            DropIndex("dbo.DuplicateEntries", new[] { "MergedById" });
            DropIndex("dbo.DuplicateEntries", new[] { "ComplaintId" });
            DropIndex("dbo.DuplicateEntries", new[] { "ClusterId" });
            DropIndex("dbo.DuplicateClusters", new[] { "CategoryId" });
            DropIndex("dbo.DuplicateClusters", new[] { "PrimaryComplaintId" });
            DropIndex("dbo.Appeals", new[] { "ReviewedById" });
            DropIndex("dbo.Appeals", new[] { "CitizenId" });
            DropIndex("dbo.Appeals", new[] { "ComplaintId" });
            DropIndex("dbo.CitizenProfiles", new[] { "CitizenId" });
            DropIndex("dbo.Upvotes", new[] { "CitizenId" });
            DropIndex("dbo.Upvotes", new[] { "ComplaintId" });
            DropIndex("dbo.ComplaintStatusHistories", new[] { "ChangedById" });
            DropIndex("dbo.ComplaintStatusHistories", new[] { "ComplaintId" });
            DropIndex("dbo.ComplaintPhotoes", new[] { "UploadedById" });
            DropIndex("dbo.ComplaintPhotoes", new[] { "ComplaintId" });
            DropIndex("dbo.ZoneDepartments", new[] { "DepartmentId" });
            DropIndex("dbo.ZoneDepartments", new[] { "ZoneId" });
            DropIndex("dbo.StaffProfiles", new[] { "ZoneId" });
            DropIndex("dbo.StaffProfiles", new[] { "DepartmentId" });
            DropIndex("dbo.StaffProfiles", new[] { "StaffId" });
            DropIndex("dbo.ContractPerformances", new[] { "ReviewedById" });
            DropIndex("dbo.ContractPerformances", new[] { "DepartmentId" });
            DropIndex("dbo.ContractPerformances", new[] { "ContractorId" });
            DropIndex("dbo.Departments", new[] { "ContractorId" });
            DropIndex("dbo.ComplaintCategories", new[] { "DepartmentId" });
            DropIndex("dbo.Complaints", new[] { "ZoneId" });
            DropIndex("dbo.Complaints", new[] { "DepartmentId" });
            DropIndex("dbo.Complaints", new[] { "CategoryId" });
            DropIndex("dbo.Complaints", new[] { "CitizenId" });
            DropIndex("dbo.ComplaintAssignments", new[] { "User_UserId" });
            DropIndex("dbo.ComplaintAssignments", new[] { "AssignedById" });
            DropIndex("dbo.ComplaintAssignments", new[] { "AssignedToId" });
            DropIndex("dbo.ComplaintAssignments", new[] { "ComplaintId" });
            DropIndex("dbo.Users", new[] { "ZoneId" });
            DropIndex("dbo.ActivityLogs", new[] { "UserId" });
            DropTable("dbo.Notifications");
            DropTable("dbo.Escalations");
            DropTable("dbo.DuplicateEntries");
            DropTable("dbo.DuplicateClusters");
            DropTable("dbo.Appeals");
            DropTable("dbo.CitizenProfiles");
            DropTable("dbo.Upvotes");
            DropTable("dbo.ComplaintStatusHistories");
            DropTable("dbo.ComplaintPhotoes");
            DropTable("dbo.Zones");
            DropTable("dbo.ZoneDepartments");
            DropTable("dbo.StaffProfiles");
            DropTable("dbo.ContractPerformances");
            DropTable("dbo.Contractors");
            DropTable("dbo.Departments");
            DropTable("dbo.ComplaintCategories");
            DropTable("dbo.Complaints");
            DropTable("dbo.ComplaintAssignments");
            DropTable("dbo.Users");
            DropTable("dbo.ActivityLogs");
        }
    }
}
