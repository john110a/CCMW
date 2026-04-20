// Models/SystemSetting.cs
using System;

namespace CCMW.Models
{
    public class SystemSetting
    {
        public Guid Id { get; set; }
        public bool NotificationsEnabled { get; set; }
        public bool AutoAssignmentEnabled { get; set; }
        public bool MaintenanceMode { get; set; }
        public int EscalationHours { get; set; }
        public string DefaultPriority { get; set; }
        public DateTime CreatedAt { get; set; }  // Added this
        public DateTime UpdatedAt { get; set; }
    }
}