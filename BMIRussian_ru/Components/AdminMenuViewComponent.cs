using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using BMIRussian_ru.Data;
using Sibvic.AuthLib;

namespace BMIRussian_ru.Components
{
    public class AdminMenuViewComponent(ApplicationDbContext context) : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync()
        {
            var isAdmin = false;
            if (HttpContext.User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? HttpContext.User.FindFirstValue("id");
                if (!string.IsNullOrEmpty(userIdClaim) && long.TryParse(userIdClaim, out var userId))
                {
                    isAdmin = await context.Set<UserRoles>()
                        .AnyAsync(r => r.UserId == userId && r.Role == "Admin");
                }
            }

            return View(isAdmin);
        }
    }
}
