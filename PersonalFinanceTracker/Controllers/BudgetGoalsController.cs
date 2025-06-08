using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Data;
using PersonalFinanceTracker.Models;
using System.Security.Claims;

namespace PersonalFinanceTracker.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BudgetGoalsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public BudgetGoalsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private string GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        }

        [HttpGet]
        public async Task<IActionResult> GetBudgetGoals()
        {
            try
            {
                var userId = GetCurrentUserId();
                var currentDate = DateTime.UtcNow;
                var firstDayOfMonth = new DateTime(currentDate.Year, currentDate.Month, 1);
                var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

                var budgetGoals = await _context.BudgetGoals
                    .Include(bg => bg.Category)
                    .Where(bg => bg.UserId == userId)
                    .Select(bg => new
                    {
                        bg.Id,
                        bg.CategoryId,
                        CategoryName = bg.Category.Name,
                        bg.Amount,
                        bg.StartDate,
                        bg.EndDate,
                        bg.Notes,
                        Spent = _context.Transactions
                            .Where(t => t.Account.UserId == userId && 
                                       t.CategoryId == bg.CategoryId && 
                                       t.Date >= firstDayOfMonth && 
                                       t.Date <= lastDayOfMonth)
                            .Sum(t => Math.Abs(t.Amount))
                    })
                    .ToListAsync();

                return Ok(budgetGoals);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateBudgetGoal([FromBody] CreateBudgetGoalRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();

                // Validate category exists
                var category = await _context.Categories.FindAsync(request.CategoryId);
                if (category == null)
                {
                    return BadRequest(new { success = false, message = "Invalid category" });
                }

                var budgetGoal = new BudgetGoalModel
                {
                    UserId = userId,
                    CategoryId = request.CategoryId,
                    Amount = request.Amount,
                    StartDate = DateTime.Parse(request.StartDate).ToUniversalTime(),
                    EndDate = request.EndDate != null ? DateTime.Parse(request.EndDate).ToUniversalTime() : null,
                    Notes = request.Notes,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.BudgetGoals.Add(budgetGoal);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Budget goal created successfully", id = budgetGoal.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBudgetGoal(int id, [FromBody] UpdateBudgetGoalRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var budgetGoal = await _context.BudgetGoals
                    .FirstOrDefaultAsync(bg => bg.Id == id && bg.UserId == userId);

                if (budgetGoal == null)
                {
                    return NotFound(new { success = false, message = "Budget goal not found" });
                }

                budgetGoal.Amount = request.Amount;
                budgetGoal.Notes = request.Notes;
                budgetGoal.UpdatedAt = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(request.EndDate))
                {
                    budgetGoal.EndDate = DateTime.Parse(request.EndDate).ToUniversalTime();
                }

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Budget goal updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBudgetGoal(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var budgetGoal = await _context.BudgetGoals
                    .FirstOrDefaultAsync(bg => bg.Id == id && bg.UserId == userId);

                if (budgetGoal == null)
                {
                    return NotFound(new { success = false, message = "Budget goal not found" });
                }

                _context.BudgetGoals.Remove(budgetGoal);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Budget goal deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }

    public class CreateBudgetGoalRequest
    {
        public int CategoryId { get; set; }
        public decimal Amount { get; set; }
        public string StartDate { get; set; } = string.Empty;
        public string? EndDate { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdateBudgetGoalRequest
    {
        public decimal Amount { get; set; }
        public string? EndDate { get; set; }
        public string? Notes { get; set; }
    }
}
