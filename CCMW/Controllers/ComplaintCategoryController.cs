// Controllers/ComplaintCategoryController.cs
using CCMW.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/complaint-categories")]
    public class ComplaintCategoryController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        // GET: api/complaint-categories
        [HttpGet]
        [Route("")]
        public IHttpActionResult GetAllCategories()
        {
            try
            {
                var categories = db.ComplaintCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.PriorityWeight)
                    .Select(c => new
                    {
                        c.CategoryId,
                        c.CategoryName,
                        c.CategoryCode,
                        c.Description,
                        c.IconName,
                        c.ColorCode,
                        c.PriorityWeight,
                        c.DepartmentId
                    })
                    .ToList();

                return Ok(categories);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET: api/complaint-categories/{id}
        [HttpGet]
        [Route("{id:guid}")]
        public IHttpActionResult GetCategoryById(Guid id)
        {
            try
            {
                var category = db.ComplaintCategories
                    .Where(c => c.CategoryId == id)
                    .Select(c => new
                    {
                        c.CategoryId,
                        c.CategoryName,
                        c.CategoryCode,
                        c.Description,
                        c.IconName,
                        c.ColorCode,
                        c.PriorityWeight,
                        c.ExpectedResolutionTimeHours,
                        c.DepartmentId,
                        c.IsActive
                    })
                    .FirstOrDefault();

                if (category == null)
                    return NotFound();

                return Ok(category);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET: api/complaint-categories/department/{departmentId}
        [HttpGet]
        [Route("department/{departmentId:guid}")]
        public IHttpActionResult GetCategoriesByDepartment(Guid departmentId)
        {
            try
            {
                var categories = db.ComplaintCategories
                    .Where(c => c.DepartmentId == departmentId && c.IsActive)
                    .OrderBy(c => c.PriorityWeight)
                    .Select(c => new
                    {
                        c.CategoryId,
                        c.CategoryName,
                        c.CategoryCode,
                        c.Description,
                        c.IconName,
                        c.ColorCode,
                        c.PriorityWeight
                    })
                    .ToList();

                return Ok(categories);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST: api/complaint-categories (Admin only)
        [HttpPost]
        [Route("")]
        public IHttpActionResult CreateCategory([FromBody] ComplaintCategory category)
        {
            try
            {
                if (category == null)
                    return BadRequest("Category data is required.");

                category.CategoryId = Guid.NewGuid();
                category.CreatedAt = DateTime.Now;
                category.IsActive = true;

                db.ComplaintCategories.Add(category);
                db.SaveChanges();

                return Ok(new
                {
                    Message = "Category created successfully",
                    CategoryId = category.CategoryId
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // PUT: api/complaint-categories/{id} (Admin only)
        [HttpPut]
        [Route("{id:guid}")]
        public IHttpActionResult UpdateCategory(Guid id, [FromBody] ComplaintCategory updatedCategory)
        {
            try
            {
                var category = db.ComplaintCategories.Find(id);
                if (category == null)
                    return NotFound();

                category.CategoryName = updatedCategory.CategoryName ?? category.CategoryName;
                category.Description = updatedCategory.Description ?? category.Description;
                category.IconName = updatedCategory.IconName ?? category.IconName;
                category.ColorCode = updatedCategory.ColorCode ?? category.ColorCode;
                category.PriorityWeight = updatedCategory.PriorityWeight;
                category.ExpectedResolutionTimeHours = updatedCategory.ExpectedResolutionTimeHours;
                category.DepartmentId = updatedCategory.DepartmentId;
                category.IsActive = updatedCategory.IsActive;

                db.SaveChanges();

                return Ok(new { Message = "Category updated successfully" });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // DELETE: api/complaint-categories/{id} (Admin only)
        [HttpDelete]
        [Route("{id:guid}")]
        public IHttpActionResult DeleteCategory(Guid id)
        {
            try
            {
                var category = db.ComplaintCategories.Find(id);
                if (category == null)
                    return NotFound();

                // Soft delete
                category.IsActive = false;
                db.SaveChanges();

                return Ok(new { Message = "Category deactivated successfully" });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}