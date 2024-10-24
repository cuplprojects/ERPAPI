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

        // GET: api/Project/GetProcesses
        /*    [HttpGet("GetProjectProcesses")]
            public async Task<ActionResult<IEnumerable<ProjectProcess>>> GetProjectProcesses()
            {
                return await _context.ProjectProcesses.ToListAsync();
            }
    */
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


        [HttpGet("GetProcesses")]
        public async Task<ActionResult<IEnumerable<int>>> GetProcesses(int projectId)
        {
            var processIds = await _context.ProjectProcesses
                .Where(r => r.ProjectId == projectId)
                .Select(r => r.ProcessId)
                .ToListAsync();
            return Ok(processIds);
        }



        [HttpGet]
        public async Task<ActionResult<IEnumerable<Project>>> GetProject()
        {
            return await _context.Projects.ToListAsync();
        }
        // GET: api/Project/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Project>> GetProjectById(int id)
        {
            var project = await _context.Projects.FindAsync(id);

            if (project == null)
            {
                return NotFound();
            }

            return project;
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

            var projectId = addProcessesDto.ProjectProcesses.First().ProjectId;
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                return BadRequest("Project does not exist.");
            }

            var existingProcesses = await _context.ProjectProcesses
                .Where(pp => pp.ProjectId == projectId)
                .ToListAsync();

            foreach (var dto in addProcessesDto.ProjectProcesses)
            {
                var existingProcess = existingProcesses.FirstOrDefault(pp => pp.ProcessId == dto.ProcessId);

                if (existingProcess != null)
                {
                    // Update existing process
                    existingProcess.Weightage = dto.Weightage;
                    existingProcess.Sequence = dto.Sequence;
                    existingProcess.FeaturesList = dto.FeaturesList;
                    existingProcess.UserId = dto.UserId; // Update user IDs directly
                    _context.ProjectProcesses.Update(existingProcess);
                }
                else
                {
                    // Add new process if it does not exist
                    var newProcess = new ProjectProcess
                    {
                        ProjectId = dto.ProjectId,
                        ProcessId = dto.ProcessId,
                        Weightage = dto.Weightage,
                        Sequence = dto.Sequence,
                        FeaturesList = dto.FeaturesList,
                        UserId = dto.UserId // Set user IDs
                    };
                    await _context.ProjectProcesses.AddAsync(newProcess);
                }
            }

            await _context.SaveChangesAsync();
            return Ok("Processes updated successfully!");
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
        private bool ProjectProcessExists(int id)
        {
            return _context.ProjectProcesses.Any(e => e.Id == id);
        }

        [HttpGet("GetProcessesWithUsers/{projectId}")]
        public async Task<ActionResult<object>> GetProcessesWithUsers(int projectId)
        {
            // Check if the project exists
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                return NotFound("Project does not exist.");
            }

            // Use LINQ to fetch project processes and associated users
            var processesWithUsers = await (from pp in _context.ProjectProcesses
                                            where pp.ProjectId == projectId
                                            join p in _context.Processes on pp.ProcessId equals p.Id into processGroup
                                            from p in processGroup.DefaultIfEmpty() // Left join to handle potential null
                                            select new
                                            {
                                                pp.Id,
                                                pp.ProcessId,
                                                ProcessName = p != null ? p.Name : "Unknown", // Handle potential null
                                                pp.Weightage,
                                                pp.Sequence,
                                                UserId = pp.UserId == null ? new List<int>() : pp.UserId // Handle potential null
                                            }).ToListAsync();

            var users = await _context.Users.Select(u => new
            {
                u.UserId,
                u.UserName
            }).ToListAsync();

            return new
            {
                Processes = processesWithUsers,
                Users = users
            };
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

        [HttpGet("GetActiveProjectsForUser/{userId}")]
        public async Task<ActionResult<IEnumerable<Project>>> GetActiveProjectsForUser(int userId)
        {
            // Fetch all active projects and their related processes
            var activeProjectsWithProcesses = await _context.Projects
                .Where(p => p.Status == true)  // Filter for active projects
                .Join(
                    _context.ProjectProcesses,   // Join with ProjectProcesses to check assignment
                    p => p.ProjectId,
                    pp => pp.ProjectId,
                    (p, pp) => new { Project = p, Process = pp }
                )
                .AsNoTracking()
                .ToListAsync();  // Fetch all active projects and their processes

            // Now apply client-side filtering based on the user's assignment to processes
            var activeProjectsForUser = activeProjectsWithProcesses
                .Where(pp => pp.Process.UserId.Contains(userId))  // Client-side evaluation for user assignment
                .Select(pp => pp.Project)  // Select the project information
                .Distinct()  // Ensure no duplicates in case the user is assigned to multiple processes
                .ToList();

            // Check if any active projects were found for the user
            if (!activeProjectsForUser.Any())
            {
                return NotFound("No active projects found for this user.");
            }

            return Ok(activeProjectsForUser);
        }




        [HttpPost("UpdateProcessUsers/{projectId}")]
        public async Task<IActionResult> UpdateProcessUsers(int projectId, [FromBody] Dictionary<int, List<int>> userIdsByProcessId)
        {
            // Check if the project exists
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                return NotFound("Project does not exist.");
            }

            // Get the project processes
            var projectProcesses = await _context.ProjectProcesses
                .Where(pp => pp.ProjectId == projectId && userIdsByProcessId.Keys.Contains(pp.ProcessId))
                .ToListAsync();

            if (!projectProcesses.Any())
            {
                return NotFound("No processes found for this project.");
            }

            // Update the UserId list for each project process based on the processId
            foreach (var projectProcess in projectProcesses)
            {
                if (userIdsByProcessId.TryGetValue(projectProcess.ProcessId, out var userIds))
                {
                    projectProcess.UserId = userIds; // Set user IDs for this process
                }
            }

            // Save changes to the database
            await _context.SaveChangesAsync();

            return Ok(projectProcesses.Select(pp => new
            {
                pp.Id,
                pp.ProcessId,
                pp.Weightage,
                pp.Sequence,
                UserId = pp.UserId // Return updated UserId list
            }));
        }



        [HttpPut("UpdateProcesses")]
        public async Task<IActionResult> UpdateProcesses([FromBody] UpdateProcessRequest request)
        {
            if (request == null || request.ProjectProcesses == null || !request.ProjectProcesses.Any())
            {
                return BadRequest("Invalid request data.");
            }

            foreach (var process in request.ProjectProcesses)
            {
                // Check if the process exists for the given projectId
                var existingProcess = await _context.ProjectProcesses
                    .FirstOrDefaultAsync(p => p.ProjectId == process.ProjectId && p.ProcessId == process.ProcessId);

                if (existingProcess != null)
                {
                    // Update existing process features
                    existingProcess.FeaturesList = process.FeaturesList;
                    _context.ProjectProcesses.Update(existingProcess);
                }
                else
                {
                    // Add new process if it does not exist
                    var newProcess = new ProjectProcess
                    {
                        ProjectId = process.ProjectId,
                        ProcessId = process.ProcessId,
                        FeaturesList = process.FeaturesList,
                        Weightage = process.Weightage,
                        Sequence = process.Sequence,
                        UserId = process.UserId // Assuming UserId is part of the process
                    };
                    await _context.ProjectProcesses.AddAsync(newProcess);
                }
            }

            await _context.SaveChangesAsync();
            return Ok("Processes updated successfully!");
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
        public List<int> UserId { get; set; } = new List<int>();

    }

    public class AddProcessesDto
    {
        public List<ProjectProcessDto> ProjectProcesses { get; set; }
    }

    public class UpdateProcessRequest
    {
        public List<ProjectProcessDto> ProjectProcesses { get; set; }
    }

}


