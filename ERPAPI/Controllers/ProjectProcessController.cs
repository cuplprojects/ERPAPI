using ERPAPI.Data;
using ERPAPI.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ERPAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectProcessController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProjectProcessController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/ProjectProcess/{userId}/{projectId}
        [HttpGet()]
        public async Task<ActionResult<IEnumerable<object>>> GetProjectProcesses(int userId, int projectId)
        {
            // Check if the project exists
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                return NotFound(new { message = "Project not found." });
            }

            // Get the processes associated with the project and join with Process to get the process name
            var processes = await _context.ProjectProcesses
                .Where(pp => pp.ProjectId == projectId)
                .Join(_context.Processes,
                      pp => pp.ProcessId,
                      p => p.Id,
                      (pp, p) => new
                      {
                          pp.Id,
                          pp.ProjectId,
                          pp.ProcessId,
                          ProcessName = p.Name,   // Assuming Process entity has a "Name" property
                          pp.Weightage,
                          pp.Sequence,
                          pp.FeaturesList,
                          pp.UserId
                      })
                .AsNoTracking()
                .ToListAsync();

            // Filter the processes based on userId using client-side evaluation
            var filteredProcesses = processes
                .Where(pp => pp.UserId == null || pp.UserId.Contains(userId))
                .OrderBy(pp => pp.Sequence)
                .Select(pp => new
                {
                    pp.Id,
                    pp.ProjectId,
                    pp.ProcessId,
                    pp.ProcessName,   // Now including the ProcessName in the result
                    pp.Weightage,
                    pp.Sequence,
                    pp.FeaturesList
                })
                .ToList();

            if (!filteredProcesses.Any())
            {
                return NotFound(new { message = "No processes found for the given user and project." });
            }

            return Ok(filteredProcesses);
        }
    }
}
