using ERPAPI.Data;
using ERPAPI.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace ERPAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProjectController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Project/GetProcesses
        [HttpGet("GetProjectProcesses")]
        public async Task<ActionResult<IEnumerable<ProjectProcess>>> GetProjectProcesses()
        {
            return await _context.ProjectProcesses.ToListAsync();
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Project>>> GetProject()
        {
            return await _context.Projects.ToListAsync();
        }


        [HttpPost]
        public async Task<ActionResult<Project>> PostProject(Project project)
        {
            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetProject", new { id = project.ProjectId }, project);
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> PutProject(int id, Project project)
        {
            if (id != project.ProjectId)
            {
                return BadRequest();
            }

            _context.Entry(project).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProjectExists(id))
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

        [HttpPost("AddProcessesToProject")]
        public async Task<IActionResult> AddProcessesToProject([FromBody] AddProcessesDto addProcessesDto)
        {
            if (addProcessesDto.ProjectProcesses == null || !addProcessesDto.ProjectProcesses.Any())
            {
                return BadRequest("No processes provided.");
            }

            // Fetch the project to ensure it exists
            var project = await _context.Projects.FindAsync(addProcessesDto.ProjectProcesses.First().ProjectId);
            if (project == null)
            {
                return BadRequest("Project does not exist.");
            }

            // Fetch the processes based on the provided IDs
            var processIds = addProcessesDto.ProjectProcesses.Select(dto => dto.ProcessId).Distinct().ToList();
            var processes = await _context.Processes
                .Where(p => processIds.Contains(p.Id))
                .ToListAsync();

            if (processes.Count != processIds.Count)
            {
                return BadRequest("Some process IDs are invalid.");
            }

            // Calculate the total weightage from the Process table
            var totalWeightage = processes.Sum(p => p.Weightage);

            // Log the total weightage for debugging
            Console.WriteLine($"Total Weightage from Process Table: {totalWeightage}");

            if (totalWeightage == 0)
            {
                return BadRequest("Total weightage from the process table cannot be zero.");
            }

            // Calculate the adjustment factor to normalize the weightage to a total of 100
            var adjustmentFactor = 100.0 / totalWeightage;

            // Log the adjustment factor for debugging
            Console.WriteLine($"Adjustment Factor: {adjustmentFactor}");

            // Prepare ProjectProcess entries
            var projectProcesses = addProcessesDto.ProjectProcesses.Select(dto =>
            {
                var process = processes.FirstOrDefault(p => p.Id == dto.ProcessId);
                if (process == null) return null;

                // Adjust weightage
                var adjustedWeightage = process.Weightage * adjustmentFactor;

                // Log the adjusted weightage for debugging
                Console.WriteLine($"ProcessId: {dto.ProcessId}, Original Weightage: {process.Weightage}, Adjusted Weightage: {adjustedWeightage}");

                return new ProjectProcess
                {
                    Id = dto.Id,
                    ProjectId = dto.ProjectId,
                    ProcessId = dto.ProcessId,
                    Weightage = adjustedWeightage, // Set the adjusted weightage
                    Sequence = dto.Sequence,
                    FeaturesList = dto.FeaturesList,
                 
                };
            }).Where(pp => pp != null).ToList();

            if (projectProcesses.Count != addProcessesDto.ProjectProcesses.Count)
            {
                return BadRequest("Some process entries are invalid.");
            }

            // Save ProjectProcesses
            _context.ProjectProcesses.AddRange(projectProcesses);
            await _context.SaveChangesAsync();

            return Ok(projectProcesses);
        }

        private bool ProjectExists(int id)
        {
            return _context.Projects.Any(e => e.ProjectId == id);
        }
    }

    public class ProjectProcessDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public int ProcessId { get; set; }
        public double Weightage { get; set; }
        public int Sequence { get; set; }
        public List<int> FeaturesList { get; set; }
      
    }

    public class AddProcessesDto
    {
        public List<ProjectProcessDto> ProjectProcesses { get; set; }
    }


}
