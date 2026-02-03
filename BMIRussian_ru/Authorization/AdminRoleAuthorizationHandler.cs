using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using BMIRussian_ru.Data;
using Sibvic.AuthLib;

namespace BMIRussian_ru.Authorization
{
    public class AdminRoleAuthorizationHandler : AuthorizationHandler<AdminRoleRequirement>
    {
        private readonly ApplicationDbContext _context;

        public AdminRoleAuthorizationHandler(ApplicationDbContext context)
        {
            _context = context;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            AdminRoleRequirement requirement)
        {
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                return;
            }

            var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirstValue("sub");
            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            {
                return;
            }

            var hasAdminRole = await _context.Set<UserRoles>()
                .AnyAsync(r => r.UserId == userId && r.Role == "Admin");

            if (hasAdminRole)
            {
                context.Succeed(requirement);
            }
        }
    }
}
