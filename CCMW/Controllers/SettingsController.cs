// Controllers/SettingsController.cs
using CCMW.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/settings")]
    public class SettingsController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        // =====================================================
        // GET SYSTEM SETTINGS
        // =====================================================
        [HttpGet]
        [Route("")]
        public IHttpActionResult GetSystemSettings()
        {
            try
            {
                // Try to get settings from database
                var settings = db.SystemSettings.FirstOrDefault();

                if (settings == null)
                {
                    // Return defaults if no settings exist
                    return Ok(new
                    {
                        notificationsEnabled = true,
                        autoAssignmentEnabled = false,
                        maintenanceMode = false,
                        escalationHours = 48,
                        defaultPriority = "Medium"
                    });
                }

                // Return settings from database
                return Ok(new
                {
                    notificationsEnabled = settings.NotificationsEnabled,
                    autoAssignmentEnabled = settings.AutoAssignmentEnabled,
                    maintenanceMode = settings.MaintenanceMode,
                    escalationHours = settings.EscalationHours,
                    defaultPriority = settings.DefaultPriority
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // UPDATE SYSTEM SETTINGS
        // =====================================================
        [HttpPut]
        [Route("")]
        public IHttpActionResult UpdateSystemSettings([FromBody] SystemSettingsDto settings)
        {
            try
            {
                if (settings == null)
                {
                    return BadRequest("Settings data is required");
                }

                // Validate priority
                var validPriorities = new[] { "Low", "Medium", "High", "Critical" };
                if (!validPriorities.Contains(settings.defaultPriority))
                {
                    return BadRequest("Invalid priority value. Must be: Low, Medium, High, or Critical");
                }

                // Validate escalation hours
                if (settings.escalationHours < 1 || settings.escalationHours > 168)
                {
                    return BadRequest("Escalation hours must be between 1 and 168");
                }

                // Get existing settings or create new
                var dbSettings = db.SystemSettings.FirstOrDefault();
                if (dbSettings == null)
                {
                    dbSettings = new SystemSetting
                    {
                        Id = Guid.NewGuid(),
                        CreatedAt = DateTime.Now
                    };
                    db.SystemSettings.Add(dbSettings);
                }

                // Update settings
                dbSettings.NotificationsEnabled = settings.notificationsEnabled;
                dbSettings.AutoAssignmentEnabled = settings.autoAssignmentEnabled;
                dbSettings.MaintenanceMode = settings.maintenanceMode;
                dbSettings.EscalationHours = settings.escalationHours;
                dbSettings.DefaultPriority = settings.defaultPriority;
                dbSettings.UpdatedAt = DateTime.Now;

                db.SaveChanges();

                // Return the updated settings back to frontend
                return Ok(new
                {
                    Message = "Settings updated successfully",
                    Settings = new
                    {
                        notificationsEnabled = dbSettings.NotificationsEnabled,
                        autoAssignmentEnabled = dbSettings.AutoAssignmentEnabled,
                        maintenanceMode = dbSettings.MaintenanceMode,
                        escalationHours = dbSettings.EscalationHours,
                        defaultPriority = dbSettings.DefaultPriority
                    }
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // RESET SETTINGS TO DEFAULTS
        // =====================================================
        [HttpPost]
        [Route("reset")]
        public IHttpActionResult ResetToDefaults()
        {
            try
            {
                var dbSettings = db.SystemSettings.FirstOrDefault();

                if (dbSettings != null)
                {
                    // Update existing settings to defaults
                    dbSettings.NotificationsEnabled = true;
                    dbSettings.AutoAssignmentEnabled = false;
                    dbSettings.MaintenanceMode = false;
                    dbSettings.EscalationHours = 48;
                    dbSettings.DefaultPriority = "Medium";
                    dbSettings.UpdatedAt = DateTime.Now;
                }
                else
                {
                    // Create new settings with defaults
                    dbSettings = new SystemSetting
                    {
                        Id = Guid.NewGuid(),
                        NotificationsEnabled = true,
                        AutoAssignmentEnabled = false,
                        MaintenanceMode = false,
                        EscalationHours = 48,
                        DefaultPriority = "Medium",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    db.SystemSettings.Add(dbSettings);
                }

                db.SaveChanges();

                return Ok(new
                {
                    Message = "Settings reset to defaults",
                    Settings = new
                    {
                        notificationsEnabled = dbSettings.NotificationsEnabled,
                        autoAssignmentEnabled = dbSettings.AutoAssignmentEnabled,
                        maintenanceMode = dbSettings.MaintenanceMode,
                        escalationHours = dbSettings.EscalationHours,
                        defaultPriority = dbSettings.DefaultPriority
                    }
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // GET SETTINGS HISTORY (Optional - for auditing)
        // =====================================================
        [HttpGet]
        [Route("history")]
        public IHttpActionResult GetSettingsHistory()
        {
            try
            {
                // If you want to track history, you'd need a separate table
                // For now, just return current settings with timestamp
                var settings = db.SystemSettings.FirstOrDefault();

                if (settings == null)
                {
                    return Ok(new
                    {
                        Message = "No settings history available",
                        History = new List<object>()
                    });
                }

                return Ok(new
                {
                    CurrentSettings = new
                    {
                        settings.NotificationsEnabled,
                        settings.AutoAssignmentEnabled,
                        settings.MaintenanceMode,
                        settings.EscalationHours,
                        settings.DefaultPriority,
                        settings.UpdatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // =====================================================
    // DTO for receiving settings from frontend
    // Matches the exact keys used in settings_service.dart
    // =====================================================
    public class SystemSettingsDto
    {
        public bool notificationsEnabled { get; set; }
        public bool autoAssignmentEnabled { get; set; }
        public bool maintenanceMode { get; set; }
        public int escalationHours { get; set; }
        public string defaultPriority { get; set; }
    }
}