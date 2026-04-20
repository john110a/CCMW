using CCMW.Models;
using System;
using System.Linq;
using System.Web.Http;
using System.Data.Entity;

namespace CCMW.Controllers
{
    [RoutePrefix("api/leaderboard")]
    public class LeaderboardController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        // GET TOP CITIZENS
        [HttpGet]
        [Route("citizens")]
        public IHttpActionResult GetCitizenLeaderboard(
            [FromUri] string period = "all", // all, monthly, weekly
            [FromUri] int top = 20)
        {
            IQueryable<CitizenProfile> query = db.CitizenProfiles
                .Include(c => c.User);

            // Filter by period (basic implementation)
            if (period == "monthly")
            {
                var startDate = DateTime.Now.AddMonths(-1);
                // You would need Complaint.CreatedAt in CitizenProfile or join
                // For now, using total count
            }
            else if (period == "weekly")
            {
                var startDate = DateTime.Now.AddDays(-7);
            }

            var leaderboard = query
                .Select(c => new
                {
                    c.UserId,
                    c.User.FullName,
                    c.User.ProfilePhotoUrl,
                    c.User.Zone.ZoneName,
                    ApprovedComplaints = c.ApprovedComplaintsCount,
                    ResolvedComplaints = c.ResolvedComplaintsCount,
                    TotalUpvotes = c.TotalUpvotesReceived,
                    ContributionScore = c.ContributionScore,
                    c.LeaderboardRank,
                    c.BadgeLevel
                })
                .OrderByDescending(c => c.ApprovedComplaints)
                .ThenByDescending(c => c.ContributionScore)
                .Take(top)
                .ToList();

            // Calculate ranks
            for (int i = 0; i < leaderboard.Count; i++)
            {
                // Update rank in database (optional)
                var userId = leaderboard[i].UserId;
                var citizen = db.CitizenProfiles.FirstOrDefault(c => c.UserId == userId);
                if (citizen != null)
                {
                    citizen.LeaderboardRank = i + 1;
                }
            }
            db.SaveChanges();

            return Ok(new
            {
                Period = period,
                UpdatedAt = DateTime.Now,
                Leaderboard = leaderboard.Select((item, index) => new
                {
                    Rank = index + 1,
                    item.UserId,
                    item.FullName,
                    item.ProfilePhotoUrl,
                    item.ZoneName,
                    item.ApprovedComplaints,
                    item.ResolvedComplaints,
                    item.TotalUpvotes,
                    item.ContributionScore,
                    item.BadgeLevel
                })
            });
        }

        // GET USER'S RANK
        [HttpGet]
        [Route("citizens/{userId:guid}/rank")]
        public IHttpActionResult GetUserRank(Guid userId)
        {
            var citizen = db.CitizenProfiles
                .Include(c => c.User)
                .FirstOrDefault(c => c.UserId == userId);

            if (citizen == null)
                return NotFound();

            // Calculate rank
            var rank = db.CitizenProfiles
                .Count(c => c.ApprovedComplaintsCount > citizen.ApprovedComplaintsCount) + 1;

            // Update user's rank
            citizen.LeaderboardRank = rank;
            db.SaveChanges();

            // Determine badge
            var badge = DetermineBadge(citizen.ApprovedComplaintsCount, citizen.ContributionScore);

            return Ok(new
            {
                UserId = userId,
                UserName = citizen.User.FullName,
                ApprovedComplaints = citizen.ApprovedComplaintsCount,
                ResolvedComplaints = citizen.ResolvedComplaintsCount,
                ContributionScore = citizen.ContributionScore,
                TotalUpvotes = citizen.TotalUpvotesReceived,
                Rank = rank,
                Badge = badge,
                TopPercent = Math.Round((double)rank / db.CitizenProfiles.Count() * 100, 1)
            });
        }

        // GET DEPARTMENT PERFORMANCE LEADERBOARD
        [HttpGet]
        [Route("departments")]
        public IHttpActionResult GetDepartmentLeaderboard()
        {
            var departments = db.Departments
                .Select(d => new
                {
                    d.DepartmentId,
                    d.DepartmentName,
                    d.PrivatizationStatus,
                    ActiveComplaints = d.ActiveComplaintsCount,
                    ResolvedComplaints = d.ResolvedComplaintsCount,
                    ResolutionRate = d.TotalComplaintsCount > 0 ?
                                   (double)d.ResolvedComplaintsCount / d.TotalComplaintsCount * 100 : 0,
                    d.AverageResolutionTimeDays,
                    d.PerformanceScore,
                    d.PerformanceRating
                })
                .OrderByDescending(d => d.PerformanceScore)
                .ThenByDescending(d => d.ResolutionRate)
                .ToList();

            return Ok(departments);
        }

        // GET ZONE PERFORMANCE LEADERBOARD
        [HttpGet]
        [Route("zones")]
        public IHttpActionResult GetZoneLeaderboard()
        {
            var zones = db.Zones
                .Select(z => new
                {
                    z.ZoneId,
                    z.ZoneName,
                    z.ZoneCode,
                    z.City,
                    TotalComplaints = z.TotalComplaintsCount,
                    ActiveComplaints = z.ActiveComplaintsCount,
                    ResolutionRate = z.TotalComplaintsCount > 0 ?
                                   (double)(z.TotalComplaintsCount - z.ActiveComplaintsCount) / z.TotalComplaintsCount * 100 : 0,
                    z.PerformanceRating
                })
                .OrderByDescending(z => z.ResolutionRate)
                .ThenBy(z => z.ActiveComplaints)
                .ToList();

            return Ok(zones);
        }

        // GET STAFF PERFORMANCE LEADERBOARD
        [HttpGet]
        [Route("staff")]
        public IHttpActionResult GetStaffLeaderboard([FromUri] Guid? departmentId = null)
        {
            IQueryable<StaffProfile> query = db.StaffProfiles
                .Include(s => s.User)
                .Include(s => s.Department);

            if (departmentId.HasValue)
                query = query.Where(s => s.DepartmentId == departmentId);

            var staff = query
                .Select(s => new
                {
                    s.StaffId,
                    s.User.FullName,
                    s.Department.DepartmentName,
                    s.Role,
                    TotalAssignments = s.TotalAssignments,
                    CompletedAssignments = s.CompletedAssignments,
                    CompletionRate = s.TotalAssignments > 0 ?
                                   (double)s.CompletedAssignments / s.TotalAssignments * 100 : 0,
                    s.AverageResolutionTime,
                    s.PerformanceScore
                })
                .OrderByDescending(s => s.PerformanceScore)
                .ThenByDescending(s => s.CompletedAssignments)
                .Take(20)
                .ToList();

            return Ok(staff);
        }

        // UPDATE CONTRIBUTION SCORE (call this when complaint is approved/resolved)
        [HttpPost]
        [Route("update-score/{userId:guid}")]
        public IHttpActionResult UpdateContributionScore(Guid userId, [FromUri] int points = 10)
        {
            var citizen = db.CitizenProfiles.FirstOrDefault(c => c.UserId == userId);
            if (citizen == null)
                return NotFound();

            citizen.ContributionScore += points;
            citizen.UpdatedAt = DateTime.Now;

            // Update badge based on score
            citizen.BadgeLevel = DetermineBadge(citizen.ApprovedComplaintsCount, citizen.ContributionScore);

            db.SaveChanges();

            return Ok(new
            {
                Message = "Contribution score updated",
                NewScore = citizen.ContributionScore,
                Badge = citizen.BadgeLevel
            });
        }

        // HELPER: Determine badge level
        private string DetermineBadge(int approvedComplaints, int contributionScore)
        {
            if (approvedComplaints >= 50 || contributionScore >= 500)
                return "Gold";
            else if (approvedComplaints >= 20 || contributionScore >= 200)
                return "Silver";
            else if (approvedComplaints >= 5 || contributionScore >= 50)
                return "Bronze";
            else
                return "Newcomer";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}