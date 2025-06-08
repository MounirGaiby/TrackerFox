using Microsoft.AspNetCore.Identity;

namespace PersonalFinanceTracker.Models
{
    public class User : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Country { get; set; } = "Morocco";
        public string Currency { get; set; } = "MAD";
        public string Theme { get; set; } = "system"; // light, dark, system
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public ICollection<Account> Accounts { get; set; } = new List<Account>();
    }
}
