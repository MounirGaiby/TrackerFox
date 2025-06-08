using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;

namespace PersonalFinanceTracker.Controllers
{
    [Authorize]
    public class SeedController : Controller
    {
        private readonly DataSeedingService _seedingService;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<SeedController> _logger;

        public SeedController(
            DataSeedingService seedingService,
            UserManager<User> userManager,
            ILogger<SeedController> logger)
        {
            _seedingService = seedingService;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> SeedAnalyticsData()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user?.Email == null)
                {
                    TempData["Error"] = "Unable to find user information.";
                    return RedirectToAction("Index", "Analytics");
                }

                await _seedingService.SeedAnalyticsDataAsync(user.Email);
                TempData["Success"] = "Analytics data has been seeded successfully! The dashboard should now show meaningful data from January 2024 onwards.";
                _logger.LogInformation("Analytics data seeded for user {Email}", user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding analytics data");
                TempData["Error"] = "An error occurred while seeding data. Please try again.";
            }

            return RedirectToAction("Index", "Analytics");
        }
    }
}
