using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Routing;
using Hangfire;
using Hangfire.SqlServer;
using CCMW.Models;
using System.Data.Entity;

namespace CCMW
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        private BackgroundJobServer _hangfireServer;

        protected void Application_Start()
        {
            // ===== YOUR ORIGINAL CODE (KEPT EXACTLY) =====
            // Use FULLY QUALIFIED NAME for WebApi GlobalConfiguration
            System.Web.Http.GlobalConfiguration.Configure(WebApiConfig.Register);

            // ===== ADD HANGFIRE CONFIGURATION =====
            ConfigureHangfire();
        }

        // ===== NEW METHOD: Configure Hangfire =====
        private void ConfigureHangfire()
        {
            try
            {
                // Configure Hangfire to use SQL Server storage
                // Use FULLY QUALIFIED NAME for Hangfire GlobalConfiguration
                Hangfire.GlobalConfiguration.Configuration
                    .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                    .UseSimpleAssemblyNameTypeSerializer()
                    .UseRecommendedSerializerSettings()
                    .UseSqlServerStorage("name=CCMWConnectionString"); // Use your connection string

                // Start Hangfire server
                _hangfireServer = new BackgroundJobServer(new BackgroundJobServerOptions
                {
                    WorkerCount = 1, // Number of parallel jobs
                    Queues = new[] { "default", "escalations" },
                    ServerName = $"CCMW-Server-{Environment.MachineName}"
                });

                // Schedule escalation check to run every hour
                RecurringJob.AddOrUpdate(
                    "escalation-check",
                    () => RunEscalationCheck(),
                    Cron.Hourly); // Runs every hour

                // Also run every 30 minutes for more frequent checks (optional)
                RecurringJob.AddOrUpdate(
                    "escalation-check-30min",
                    () => RunEscalationCheck(),
                    "*/30 * * * *"); // Cron expression for every 30 minutes

                // Log success
                System.Diagnostics.Debug.WriteLine("✅ Hangfire started successfully");
            }
            catch (Exception ex)
            {
                // Log error but don't crash the application
                System.Diagnostics.Debug.WriteLine($"❌ Hangfire failed to start: {ex.Message}");
            }
        }

        // ===== NEW METHOD: Run escalation check (called by Hangfire) =====
        public static void RunEscalationCheck()
        {
            try
            {
                using (var db = new CCMWDbContext())
                {
                    var overdueThreshold = DateTime.Now.AddHours(-48);

                    var overdueComplaints = db.Complaints
                        .Where(c => c.CurrentStatus != ComplaintStatus.Resolved &&
                                   c.CurrentStatus != ComplaintStatus.Closed &&
                                   c.CurrentStatus != ComplaintStatus.Rejected &&
                                   c.CreatedAt < overdueThreshold &&
                                   c.EscalationLevel < 3)
                        .ToList();

                    int escalatedCount = 0;
                    foreach (var complaint in overdueComplaints)
                    {
                        // Auto-escalate
                        complaint.EscalationLevel++;
                        complaint.UpdatedAt = DateTime.Now;

                        // Create escalation record
                        var escalation = new Escalation
                        {
                            EscalationId = Guid.NewGuid(),
                            ComplaintId = complaint.ComplaintId,
                            EscalationLevel = complaint.EscalationLevel,
                            EscalationReason = "Time_Exceeded",
                            HoursElapsed = (decimal)(DateTime.Now - complaint.CreatedAt).TotalHours,
                            EscalatedAt = DateTime.Now,
                            EscalatedById = Guid.Empty, // System auto-escalation
                            EscalationNotes = $"Auto-escalated to Level {complaint.EscalationLevel} after 48+ hours"
                        };

                        db.Escalations.Add(escalation);
                        escalatedCount++;

                        // Add to status history
                        db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
                        {
                            HistoryId = Guid.NewGuid(),
                            ComplaintId = complaint.ComplaintId,
                            PreviousStatus = complaint.CurrentStatus.ToString(),
                            NewStatus = complaint.CurrentStatus.ToString(),
                            ChangedById = Guid.Empty, // System
                            ChangedAt = DateTime.Now,
                            Notes = $"Escalated to Level {complaint.EscalationLevel}"
                        });
                    }

                    if (escalatedCount > 0)
                    {
                        db.SaveChanges();
                        System.Diagnostics.Debug.WriteLine($"✅ Escalated {escalatedCount} complaints");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Escalation check failed: {ex.Message}");
                // Hangfire will automatically retry on failure
                throw; // Re-throw so Hangfire knows it failed
            }
        }

        protected void Application_End()
        {
            // ===== CLEANUP: Dispose Hangfire server =====
            _hangfireServer?.Dispose();
        }

        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            // ===== YOUR ORIGINAL CORS CODE (KEPT EXACTLY) =====
            // Allow CORS preflight requests
            if (HttpContext.Current.Request.HttpMethod == "OPTIONS")
            {
                HttpContext.Current.Response.StatusCode = 200;
                HttpContext.Current.Response.End();
            }
        }
    }
}