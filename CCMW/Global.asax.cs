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
            System.Web.Http.GlobalConfiguration.Configure(WebApiConfig.Register);

            // ===== ADD HANGFIRE CONFIGURATION =====
            ConfigureHangfire();
        }

        private void ConfigureHangfire()
        {
            try
            {
                Hangfire.GlobalConfiguration.Configuration
                    .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                    .UseSimpleAssemblyNameTypeSerializer()
                    .UseRecommendedSerializerSettings()
                    .UseSqlServerStorage("name=CCMWConnectionString");

                _hangfireServer = new BackgroundJobServer(new BackgroundJobServerOptions
                {
                    WorkerCount = 1,
                    Queues = new[] { "default", "escalations" },
                    ServerName = $"CCMW-Server-{Environment.MachineName}"
                });

                // ===== YOUR ORIGINAL ESCALATION JOBS (KEPT EXACTLY) =====
                RecurringJob.AddOrUpdate(
                    "escalation-check",
                    () => RunEscalationCheck(),
                    Cron.Hourly);

                RecurringJob.AddOrUpdate(
                    "escalation-check-30min",
                    () => RunEscalationCheck(),
                    "*/30 * * * *");

                // ===== ADDED: Duplicate check every 5 minutes =====
                RecurringJob.AddOrUpdate(
                    "duplicate-check",
                    () => RunDuplicateCheck(),
                    "*/5 * * * *");

                System.Diagnostics.Debug.WriteLine("✅ Hangfire started successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Hangfire failed to start: {ex.Message}");
            }
        }

        // ===== YOUR ORIGINAL ESCALATION METHOD (KEPT EXACTLY) =====
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
                        complaint.EscalationLevel++;
                        complaint.UpdatedAt = DateTime.Now;

                        var escalation = new Escalation
                        {
                            EscalationId = Guid.NewGuid(),
                            ComplaintId = complaint.ComplaintId,
                            EscalationLevel = complaint.EscalationLevel,
                            EscalationReason = "Time_Exceeded",
                            HoursElapsed = (decimal)(DateTime.Now - complaint.CreatedAt).TotalHours,
                            EscalatedAt = DateTime.Now,
                            EscalatedById = Guid.Empty,
                            EscalationNotes = $"Auto-escalated to Level {complaint.EscalationLevel} after 48+ hours"
                        };

                        db.Escalations.Add(escalation);
                        escalatedCount++;

                        db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
                        {
                            HistoryId = Guid.NewGuid(),
                            ComplaintId = complaint.ComplaintId,
                            PreviousStatus = complaint.CurrentStatus.ToString(),
                            NewStatus = complaint.CurrentStatus.ToString(),
                            ChangedById = Guid.Empty,
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
                throw;
            }
        }

        // ===== ADDED: Background duplicate check method =====
        public static void RunDuplicateCheck()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🔍 Background duplicate check running...");

                using (var db = new CCMWDbContext())
                {
                    var allComplaints = db.Complaints
                        .Where(c => c.MergedIntoComplaintId == null)
                        .Where(c => c.LocationLatitude != null && c.LocationLongitude != null)
                        .Where(c => c.CurrentStatus != ComplaintStatus.Resolved &&
                                   c.CurrentStatus != ComplaintStatus.Closed &&
                                   c.CurrentStatus != ComplaintStatus.Rejected)
                        .OrderBy(c => c.CreatedAt)
                        .ToList();

                    int mergedCount = 0;

                    foreach (var complaint in allComplaints)
                    {
                        if (complaint.MergedIntoComplaintId != null) continue;

                        bool alreadyClustered = db.DuplicateClusters
                            .Any(cl => cl.PrimaryComplaintId == complaint.ComplaintId);
                        if (alreadyClustered) continue;

                        var similar = allComplaints
                            .Where(c => c.ComplaintId != complaint.ComplaintId)
                            .Where(c => c.CategoryId == complaint.CategoryId)
                            .Where(c => c.MergedIntoComplaintId == null)
                            .Where(c => CalculateDistance(
                                (double)complaint.LocationLatitude,
                                (double)complaint.LocationLongitude,
                                (double)c.LocationLatitude,
                                (double)c.LocationLongitude) < 0.2)
                            .ToList();

                        if (!similar.Any()) continue;

                        var cluster = new DuplicateCluster
                        {
                            ClusterId = Guid.NewGuid(),
                            PrimaryComplaintId = complaint.ComplaintId,
                            CategoryId = complaint.CategoryId,
                            LocationLatitude = complaint.LocationLatitude,
                            LocationLongitude = complaint.LocationLongitude,
                            ClusterRadiusMeters = 200,
                            TotalComplaintsMerged = similar.Count + 1,
                            TotalCombinedUpvotes = complaint.UpvoteCount + similar.Sum(s => s.UpvoteCount),
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        };
                        db.DuplicateClusters.Add(cluster);

                        db.DuplicateEntries.Add(new DuplicateEntry
                        {
                            EntryId = Guid.NewGuid(),
                            ClusterId = cluster.ClusterId,
                            ComplaintId = complaint.ComplaintId,
                            SimilarityScore = 100,
                            SimilarityFactors = "{\"type\":\"primary\"}",
                            MergedAt = DateTime.Now
                        });

                        foreach (var dup in similar)
                        {
                            dup.IsDuplicate = true;
                            dup.MergedIntoComplaintId = complaint.ComplaintId;
                            dup.UpdatedAt = DateTime.Now;

                            db.DuplicateEntries.Add(new DuplicateEntry
                            {
                                EntryId = Guid.NewGuid(),
                                ClusterId = cluster.ClusterId,
                                ComplaintId = dup.ComplaintId,
                                SimilarityScore = 100,
                                SimilarityFactors = "{\"type\":\"auto_background\"}",
                                MergedAt = DateTime.Now
                            });
                        }

                        db.SaveChanges();
                        mergedCount += similar.Count + 1;

                        System.Diagnostics.Debug.WriteLine(
                            $"✅ Background merged {similar.Count + 1} complaints " +
                            $"(primary: {complaint.ComplaintNumber})");
                    }

                    System.Diagnostics.Debug.WriteLine(
                        mergedCount > 0
                            ? $"✅ Background check complete — {mergedCount} complaints clustered"
                            : "✅ Background check complete — nothing new to merge");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Background duplicate check error: {ex.Message}");
                throw;
            }
        }

        // ===== ADDED: Distance calculation helper =====
        private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371;
            var dLat = Math.PI * (lat2 - lat1) / 180.0;
            var dLon = Math.PI * (lon2 - lon1) / 180.0;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(Math.PI * lat1 / 180.0) * Math.Cos(Math.PI * lat2 / 180.0) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        // ===== YOUR ORIGINAL APPLICATION END (KEPT EXACTLY) =====
        protected void Application_End()
        {
            _hangfireServer?.Dispose();
        }

        // ===== YOUR ORIGINAL CORS CODE (KEPT EXACTLY) =====
        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            if (HttpContext.Current.Request.HttpMethod == "OPTIONS")
            {
                HttpContext.Current.Response.StatusCode = 200;
                HttpContext.Current.Response.End();
            }
        }
    }
}