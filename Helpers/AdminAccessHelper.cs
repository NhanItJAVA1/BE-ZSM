using BE_ZSM.Contexts;
using BE_ZSM.Enums;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BE_ZSM.Helpers
{
    public class AdminAccessHelper
    {
        private readonly AppDbContext _context;

        public AdminAccessHelper(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> IsCurrentUserAdminAsync(ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdClaim, out var userId))
            {
                return false;
            }

            var roleName = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.Role.Name)
                .FirstOrDefaultAsync();

            return roleName == UserRole.Admin;
        }
    }

}
