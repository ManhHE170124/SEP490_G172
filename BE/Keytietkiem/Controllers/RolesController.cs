/// <summary>
/// File: RolesController.cs
/// Layer: Web API (ASP.NET Core)
/// Purpose:
///   Expose role lookup for UI. Returns only non-admin roles (case-insensitive),
///   to prevent accidental assignment of privileged roles via the UI.
/// </summary>

using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RolesController : ControllerBase
    {
        private readonly KeytietkiemDbContext _db;
        public RolesController(KeytietkiemDbContext db) => _db = db;

        /// <summary>
        /// Get a list of roles excluding any role whose name contains "admin" (case-insensitive).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var roles = await _db.Roles
                .AsNoTracking()
                .Where(r => !EF.Functions.Like(r.Name.ToLower(), "%admin%"))
                .Select(r => new { r.RoleId, r.Name })
                .ToListAsync();

            return Ok(roles);
        }
    }
}
