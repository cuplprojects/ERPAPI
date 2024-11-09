using System;
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
    public class FeaturesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILoggerService _loggerService;

        public FeaturesController(AppDbContext context, ILoggerService loggerService)
        {
            _context = context;
            _loggerService = loggerService;
        }

        // GET: api/Features
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Feature>>> GetFeatures()
        {
            try
            {
                var features = await _context.Features.ToListAsync();
                _loggerService.LogEvent("Fetched all features", "Features", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                return features;
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error fetching features", ex.Message, nameof(FeaturesController));
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Features/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Feature>> GetFeature(int id)
        {
            try
            {
                var feature = await _context.Features.FindAsync(id);

                if (feature == null)
                {
                    _loggerService.LogEvent($"Feature with ID {id} not found", "Features", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                    return NotFound();
                }

                _loggerService.LogEvent($"Fetched feature with ID {id}", "Features", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                return feature;
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error fetching feature", ex.Message, nameof(FeaturesController));
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/Features/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutFeature(int id, Feature feature)
        {
            if (id != feature.FeatureId)
            {
                return BadRequest();
            }

            _context.Entry(feature).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                _loggerService.LogEvent($"Updated feature with ID {id}", "Features", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!FeatureExists(id))
                {
                    _loggerService.LogEvent($"Feature with ID {id} not found during update", "Features", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                    return NotFound();
                }
                else
                {
                    _loggerService.LogError("Concurrency error during feature update", ex.Message, nameof(FeaturesController));
                    throw;
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error updating feature", ex.Message, nameof(FeaturesController));
                return StatusCode(500, "Internal server error");
            }

            return NoContent();
        }

        // POST: api/Features
        [HttpPost]
        public async Task<ActionResult<Feature>> PostFeature(Feature feature)
        {
            try
            {
                _context.Features.Add(feature);
                await _context.SaveChangesAsync();
                _loggerService.LogEvent("Created a new feature", "Features", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);

                return CreatedAtAction("GetFeature", new { id = feature.FeatureId }, feature);
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error creating feature", ex.Message, nameof(FeaturesController));
                return StatusCode(500, "Internal server error");
            }
        }

        // DELETE: api/Features/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFeature(int id)
        {
            try
            {
                var feature = await _context.Features.FindAsync(id);
                if (feature == null)
                {
                    _loggerService.LogEvent($"Feature with ID {id} not found during delete", "Features", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                    return NotFound();
                }

                _context.Features.Remove(feature);
                await _context.SaveChangesAsync();
                _loggerService.LogEvent($"Deleted feature with ID {id}", "Features", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);

                return NoContent();
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error deleting feature", ex.Message, nameof(FeaturesController));
                return StatusCode(500, "Internal server error");
            }
        }

        private bool FeatureExists(int id)
        {
            return _context.Features.Any(e => e.FeatureId == id);
        }
    }
}
