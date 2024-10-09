using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ERPAPI.Data;
using ERPAPI.Model;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProjectProcessFeaturesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProjectProcessFeaturesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/ProjectProcessFeatures
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProjectProcessFeature>>> GetProjectProcessFeatures()
        {
            return await _context.ProjectProcessFeatures.ToListAsync();
        }

        // GET: api/ProjectProcessFeatures/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ProjectProcessFeature>> GetProjectProcessFeature(int id)
        {
            var projectProcessFeature = await _context.ProjectProcessFeatures.FindAsync(id);

            if (projectProcessFeature == null)
            {
                return NotFound();
            }

            return projectProcessFeature;
        }

        // PUT: api/ProjectProcessFeatures/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutProjectProcessFeature(int id, ProjectProcessFeature projectProcessFeature)
        {
            if (id != projectProcessFeature.ProjectProcessFeatureId)
            {
                return BadRequest();
            }

            _context.Entry(projectProcessFeature).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProjectProcessFeatureExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/ProjectProcessFeatures
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<ProjectProcessFeature>> PostProjectProcessFeature(ProjectProcessFeature projectProcessFeature)
        {
            _context.ProjectProcessFeatures.Add(projectProcessFeature);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetProjectProcessFeature", new { id = projectProcessFeature.ProjectProcessFeatureId }, projectProcessFeature);
        }

        // DELETE: api/ProjectProcessFeatures/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProjectProcessFeature(int id)
        {
            var projectProcessFeature = await _context.ProjectProcessFeatures.FindAsync(id);
            if (projectProcessFeature == null)
            {
                return NotFound();
            }

            _context.ProjectProcessFeatures.Remove(projectProcessFeature);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ProjectProcessFeatureExists(int id)
        {
            return _context.ProjectProcessFeatures.Any(e => e.ProjectProcessFeatureId == id);
        }
    }
}
