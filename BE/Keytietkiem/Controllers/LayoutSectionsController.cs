/**
 * File: LayoutSectionsController.cs
 * Author: TungNVHE170677
 * Created: 26/10/2025
 * Last Updated: 5/11/2025
 * Purpose: Manage layout sections on the website (CRUD operations).
 *          Each section defines a customizable area of the site layout,
 *          such as header, footer, sidebar, etc.
 * Endpoints:
 *   - GET    /api/layoutsections          : Retrieve all layout sections
 *   - GET    /api/layoutsections/{id}     : Retrieve a layout section by ID
 *   - POST   /api/layoutsections          : Create a new layout section
 *   - PUT    /api/layoutsections/{id}     : Update a layout section
 *   - DELETE /api/layoutsections/{id}     : Delete a layout section
 */

using Keytietkiem.DTOs;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LayoutSectionsController : ControllerBase
    {
        private readonly ILayoutSectionService _service;
        public LayoutSectionsController(ILayoutSectionService service)
        {
            _service = service;
        }
        /**
        * Summary: Retrieve all layout sections.
        * Route: GET /api/layoutsections
        * Params: none
        * Returns: 200 OK with a list of all layout sections.
        */
        [HttpGet]
        public async Task<ActionResult<IEnumerable<LayoutSectionDto>>> GetAll()
        {
            var data = await _service.GetAllAsync();
            return Ok(data);
        }

        /**
         * Summary: Retrieve a specific layout section by its ID.
         * Route: GET /api/layoutsections/{id}
         * Params:
         *   - id (int): The unique identifier of the layout section.
         * Returns:
         *   - 200 OK with layout section data.
         *   - 404 Not Found if the section does not exist.
         */
        [HttpGet("{id}")]
        public async Task<ActionResult<LayoutSectionDto>> Get(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }
        /**
         * Summary: Create a new layout section.
         * Route: POST /api/layoutsections
         * Body: LayoutSectionDto dto
         * Returns:
         *   - 201 Created with the created layout section.
         *   - 400 Bad Request if input is invalid.
         */
        [HttpPost]
        public async Task<ActionResult<LayoutSectionDto>> Create(LayoutSectionDto dto)
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        /**
         * Summary: Update an existing layout section.
         * Route: PUT /api/layoutsections/{id}
         * Params:
         *   - id (int): The ID of the section to update.
         * Body: LayoutSectionDto dto
         * Returns:
         *   - 204 No Content if updated successfully.
         *   - 404 Not Found if section does not exist.
         */
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, LayoutSectionDto dto)
        {
            var updated = await _service.UpdateAsync(id, dto);
            if (updated == null) return NotFound();
            return NoContent();
        }
        /**
        * Summary: Delete a layout section by its ID.
        * Route: DELETE /api/layoutsections/{id}
        * Params:
        *   - id (int): The ID of the section to delete.
        * Returns:
        *   - 204 No Content if deleted successfully.
        *   - 404 Not Found if section does not exist.
        */
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted) return NotFound();
            return NoContent();
        }
    }
}
