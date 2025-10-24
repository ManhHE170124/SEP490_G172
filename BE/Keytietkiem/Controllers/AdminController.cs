using Keytietkiem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KeytietkiemApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly KeytietkiemDbContext _context;
        public AdminController(KeytietkiemDbContext context) => _context = context;

        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            var users = await _context.Users.CountAsync();
            var accounts = await _context.Accounts.CountAsync();
            var admins = await _context.Users
    .CountAsync(u => u.Roles.Any(r => r.RoleId == "Admin"));


            return Ok(new
            {
                message = "Chào mừng bạn đến trang quản trị!",
                stats = new { users, accounts, admins },
                time = DateTime.UtcNow
            });
        }


    }
}
