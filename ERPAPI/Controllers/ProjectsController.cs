using ERPAPI.Data;
using ERPAPI.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Diagnostics;
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


        [HttpGet("GetProjectProcesses")]
        public async Task<ActionResult<IEnumerable<ProjectProcess>>> GetProjectProcesses()
        {
            var projectProcesses = await _context.ProjectProcesses
                .Select(pp => new
                {
                    pp.Id,
                    pp.ProjectId,
                    pp.ProcessId,
                    pp.Weightage,
                    pp.Sequence,
                    UserIds = pp.UserId // Use the updated UserIds
                })
                .ToListAsync();

            return Ok(projectProcesses);
        }



        [HttpGet]
        public async Task<ActionResult<IEnumerable<Project>>> GetProject()
        {
            return await _context.Projects.ToListAsync();
        }



        [HttpGet("{id}")]
        public async Task<ActionResult<Project>> GetProjectById(int id)
        {
            var projectWithType = await (from m in _context.Projects
                                         join p in _context.Types on m.TypeId equals p.TypeId
                                         where m.ProjectId == id // Filter by the provided id
                                         select new
                                         {
                                             m.ProjectId,
                                             m.TypeId,
                                             m.Status,
                                             m.GroupId,
                                             m.Name,
                                             m.Description,
                                             ProjectType = p.Types
                                         }).FirstOrDefaultAsync(); // Use FirstOrDefault to get a single project

            if (projectWithType == null)
            {
                return NotFound(); // Return 404 if not found
            }

            return Ok(projectWithType); // Return the found project
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







        /* [HttpPost("AddProcessesToProject")]
         public async Task<IActionResult> AddProcessesToProject([FromBody] AddProcessesDto addProcessesDto)
         {
             if (addProcessesDto.ProjectProcesses == null || !addProcessesDto.ProjectProcesses.Any())
             {
                 return BadRequest("No processes provided.");
             }

             var project = await _context.Projects.FindAsync(addProcessesDto.ProjectProcesses.First().ProjectId);
             if (project == null)
             {
                 return BadRequest("Project does not exist.");
             }

             var processIds = addProcessesDto.ProjectProcesses.Select(dto => dto.ProcessId).Distinct().ToList();
             var processes = await _context.Processes.Where(p => processIds.Contains(p.Id)).ToListAsync();

             if (processes.Count != processIds.Count)
             {
                 return BadRequest("Some process IDs are invalid.");
             }

             var totalWeightage = processes.Sum(p => p.Weightage);
             if (totalWeightage == 0)
             {
                 return BadRequest("Total weightage from the process table cannot be zero.");
             }

             var adjustmentFactor = 100.0 / totalWeightage;

             var projectProcesses = addProcessesDto.ProjectProcesses.Select(dto =>
             {
                 var process = processes.FirstOrDefault(p => p.Id == dto.ProcessId);
                 if (process == null) return null;

                 var adjustedWeightage = process.Weightage * adjustmentFactor;

                 return new ProjectProcess
                 {
                     Id = dto.Id,
                     ProjectId = dto.ProjectId,
                     ProcessId = dto.ProcessId,
                     Weightage = adjustedWeightage,
                     Sequence = dto.Sequence,
                     FeaturesList = dto.FeaturesList,
                     UserId = new List<int>() // Initialize UserIds as an empty list
                 };
             }).Where(pp => pp != null).ToList();

             if (projectProcesses.Count != addProcessesDto.ProjectProcesses.Count)
             {
                 return BadRequest("Some process entries are invalid.");
             }

             _context.ProjectProcesses.AddRange(projectProcesses);
             await _context.SaveChangesAsync();

             return Ok(projectProcesses);
         }
         */

        private bool ProjectExists(int id)
        {
            return _context.Projects.Any(e => e.ProjectId == id);
        }

        // GET: api/Project/GetActiveProjects
        [HttpGet("GetActiveProjects")]
        public async Task<ActionResult<IEnumerable<Project>>> GetActiveProjects()
        {
            var activeProjects = await _context.Projects
                .Where(p => p.Status == true) // Filtering projects with Status = true
                .ToListAsync();

            if (activeProjects == null || !activeProjects.Any())
            {
                return NotFound("No active projects found.");
            }

            return Ok(activeProjects);
        }

        [HttpGet("GetDistinctProjectsForUser/{userId}")]
        public async Task<ActionResult<IEnumerable<Project>>> GetDistinctProjectsForUser(int userId)
        {
            // Fetch all project processes that contain the userId in the UserId list
            var projectProcesses = await _context.ProjectProcesses
                .AsNoTracking() // Optional: For read-only operations
                .ToListAsync();

            // Filter for processes where the UserId list contains the userId
            var userAssignedProcesses = projectProcesses
                .Where(pp => pp.UserId.Contains(userId)) // Client-side filtering
                .Select(pp => pp.ProjectId) // Select the project IDs
                .Distinct() // Ensure distinct project IDs
                .ToList();

            // If no project IDs are found, return 404
            if (!userAssignedProcesses.Any())
            {
                return NotFound("No projects found for this user.");
            }

            // Now fetch the project details for the distinct project IDs
            var projects = await _context.Projects
                .Where(p => userAssignedProcesses.Contains(p.ProjectId)) // Filter projects by distinct IDs
                .ToListAsync();

            return Ok(projects);
        }





    }






}


