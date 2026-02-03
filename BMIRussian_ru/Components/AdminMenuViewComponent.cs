using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using BMIRussian_ru.Data;
using Sibvic.AuthLib;

namespace BMIRussian_ru.Components
{
    public class AdminMenuViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public AdminMenuViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var isAdmin = false;
            if (HttpContext.User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? HttpContext.User.FindFirstValue("sub");
                if (!string.IsNullOrEmpty(userIdClaim) && long.TryParse(userIdClaim, out var userId))
                {
                    isAdmin = await _context.Set<UserRoles>()
                        .AnyAsync(r => r.UserId == userId && r.Role == "Admin");
                }
            }

            return View(isAdmin);
        }
    }
}
