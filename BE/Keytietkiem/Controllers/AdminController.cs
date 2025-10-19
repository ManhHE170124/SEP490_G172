using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KeytietkiemApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        // GET: api/admin/dashboard
        [HttpGet("dashboard")]
        public IActionResult Dashboard()
        {
            return Ok(new
            {
                message = "Chào mừng bạn đến trang quản trị!",
                time = DateTime.UtcNow
            });
        }

        // GET: api/admin/users
        [HttpGet("users")]
        public IActionResult GetAllUsers()
        {
            return Ok(new
            {
                message = "Đây là danh sách người dùng chỉ Admin được thấy.",
                sample = new[] { "user1@gmail.com", "user2@gmail.com" }
            });
        }
    }
}
