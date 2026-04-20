using System;

public enum UserType
{
    Citizen,
    FieldStaff,
    DepartmentAdmin,
    SystemAdmin
}





public enum PriorityLevel
{
    Low,
    Medium,
    High,
    Critical
}

public enum PrivatizationStatus
{
    Private,
    Public
  
}


// In enums.cs - Add missing enum values
public enum ComplaintStatus
{
    Submitted = 0,
    UnderReview = 1,
    Approved = 2,
    Assigned = 3,
    InProgress = 4,
    Resolved = 5,
    Verified = 6,
    Rejected = 7,
    Closed = 8,
    Reopened = 9,
    PendingApproval = 10
}

public enum SubmissionStatus
{
    PendingApproval = 0,
    Approved = 1,
    Rejected = 2
}

public enum PerformanceRating
{
    Excellent = 0,
    Good = 1,
    Average = 2,
    BelowAverage = 3,
    Alert = 4
}