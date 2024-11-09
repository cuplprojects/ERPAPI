
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ERPAPI.Data;
using ERPAPI.Model;
using ERPAPI.Services;
using ERPAPI.Service;


namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CamerasController : ControllerBase
    {

        private readonly AppDbContext _context;
        private readonly ILoggerService _loggerService;

        public CamerasController(AppDbContext context, ILoggerService loggerService)
        {
            _context = context;
            _loggerService = loggerService;
        }

        // GET: api/Cameras
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Camera>>> GetCamera()
        {
            try
            {
                var cameras = await _context.Camera.ToListAsync();
                _loggerService.LogEvent("Fetched all cameras", "Cameras", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                return cameras;
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error fetching cameras", ex.Message, nameof(CamerasController));
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Cameras/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Camera>> GetCamera(int id)
        {
            try
            {
                var camera = await _context.Camera.FindAsync(id);

                if (camera == null)
                {
                    _loggerService.LogEvent($"Camera with ID {id} not found", "Cameras", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                    return NotFound();
                }

                _loggerService.LogEvent($"Fetched camera with ID {id}", "Cameras", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                return camera;
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error fetching camera", ex.Message, nameof(CamerasController));
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/Cameras/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCamera(int id, Camera camera)
        {
            if (id != camera.CameraId)
            {
                return BadRequest();
            }

            _context.Entry(camera).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                _loggerService.LogEvent($"Updated camera with ID {id}", "Cameras", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!CameraExists(id))
                {
                    _loggerService.LogEvent($"Camera with ID {id} not found during update", "Cameras", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                    return NotFound();
                }
                else
                {
                    _loggerService.LogError("Concurrency error during camera update", ex.Message, nameof(CamerasController));
                    throw;
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error updating camera", ex.Message, nameof(CamerasController));
                return StatusCode(500, "Internal server error");
            }

            return NoContent();
        }

        // POST: api/Cameras
        [HttpPost]
        public async Task<ActionResult<Camera>> PostCamera(Camera camera)
        {
            try
            {
                _context.Camera.Add(camera);
                await _context.SaveChangesAsync();
                _loggerService.LogEvent("Created a new camera", "Cameras", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);

                return CreatedAtAction("GetCamera", new { id = camera.CameraId }, camera);
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error creating camera", ex.Message, nameof(CamerasController));
                return StatusCode(500, "Internal server error");
            }
        }

        // DELETE: api/Cameras/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCamera(int id)
        {
            try
            {
                var camera = await _context.Camera.FindAsync(id);
                if (camera == null)
                {
                    _loggerService.LogEvent($"Camera with ID {id} not found during delete", "Cameras", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                    return NotFound();
                }

                _context.Camera.Remove(camera);
                await _context.SaveChangesAsync();
                _loggerService.LogEvent($"Deleted camera with ID {id}", "Cameras", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);

                return NoContent();
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error deleting camera", ex.Message, nameof(CamerasController));
                return StatusCode(500, "Internal server error");
            }
        }

        private bool CameraExists(int id)
        {
            return _context.Camera.Any(e => e.CameraId == id);
        }
    }
}
