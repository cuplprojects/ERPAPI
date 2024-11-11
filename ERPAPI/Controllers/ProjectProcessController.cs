using ERPAPI.Data;
using ERPAPI.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProjectProcessController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProjectProcessController(AppDbContext context)
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


        [HttpGet("GetProcesses")]
        public async Task<ActionResult<IEnumerable<int>>> GetProcesses(int projectId)
        {
            var processIds = await _context.ProjectProcesses
                .Where(r => r.ProjectId == projectId)
                .Select(r => r.ProcessId)
                .ToListAsync();
            return Ok(processIds);
        }


        [HttpGet("GetProjectProcesses/{projectId}")]
        public async Task<IActionResult> GetProjectProcesses(int projectId)
        {
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                return NotFound("Project does not exist.");
            }

            var projectProcesses = await (from pp in _context.ProjectProcesses
                                          join p in _context.Processes
                                          on pp.ProcessId equals p.Id
                                          where pp.ProjectId == projectId
                                          select new
                                          {
                                              id = pp.ProcessId, // ProjectProcess Id
                                              name = p.Name, // Process name from Process table
                                              installedFeatures = pp.FeaturesList, // Installed features from Process table (array of ints)
                                              status = p.Status, // Status from Process table
                                              weightage = p.Weightage, // Weightage from Process table
                                              userId = pp.UserId,
                                              sequence = pp.Sequence,


                                          })
                                          .ToListAsync();

            return Ok(projectProcesses);
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


        [HttpGet("ByProjectAndSequence/{projectId}/{sequenceId}")]
        public async Task<ActionResult<object>> GetProcessByProjectAndSequence(int projectId, int sequenceId)
        {
            // Check if the project exists
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                return NotFound(new { message = "Project not found." });
            }

            // Get the process associated with the projectId and sequenceId
            var process = await _context.ProjectProcesses
                .Where(pp => pp.ProjectId == projectId && pp.Sequence == sequenceId)
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
                          pp.UserId,
                         ProcessType= p.ProcessType
                      })
                .AsNoTracking()
                .FirstOrDefaultAsync();

            // Check if the process was found
            if (process == null)
            {
                return NotFound(new { message = "Process not found for the given project and sequence." });
            }

            return Ok(process);
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

            // Calculate total weightage for existing processes to find an adjustment factor
            var totalExistingWeightage = existingProcesses.Sum(pp => pp.Weightage);
            var newProcesses = new List<ProjectProcess>();

            foreach (var dto in addProcessesDto.ProjectProcesses)
            {
                var existingProcess = existingProcesses
                    .FirstOrDefault(pp => pp.ProjectId == dto.ProjectId && pp.ProcessId == dto.ProcessId);

                if (existingProcess != null)
                {
                    // Update existing process
                    existingProcess.Weightage = dto.Weightage; // Directly set the weightage from DTO
                    existingProcess.Sequence = dto.Sequence;
                    existingProcess.FeaturesList = dto.FeaturesList;
                    existingProcess.UserId = dto.UserId;
                    _context.ProjectProcesses.Update(existingProcess);
                }
                else
                {
                    // Add new process if it does not exist
                    var newProcess = new ProjectProcess
                    {
                        ProjectId = dto.ProjectId,
                        ProcessId = dto.ProcessId,
                        Weightage = dto.Weightage, // Set weightage from DTO
                        Sequence = dto.Sequence,
                        FeaturesList = dto.FeaturesList,
                        UserId = dto.UserId
                    };
                    newProcesses.Add(newProcess);
                }
            }

            // Add new processes to the context
            if (newProcesses.Any())
            {
                await _context.ProjectProcesses.AddRangeAsync(newProcesses);
            }

            await _context.SaveChangesAsync();
            return Ok("Processes updated successfully!");
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

        [HttpPost("DeleteProcessesFromProject")]
        public async Task<IActionResult> DeleteProcessesFromProject([FromBody] DeleteRequest request)
        {
            if (request == null || request.ProcessIds == null || !request.ProcessIds.Any())
            {
                return BadRequest("Invalid request data.");
            }

            var processesToDelete = await _context.ProjectProcesses
                .Where(pp => request.ProcessIds.Contains(pp.ProcessId) && pp.ProjectId == request.ProjectId)
                .ToListAsync();

            if (!processesToDelete.Any())
            {
                return NotFound("No processes found to delete.");
            }

            _context.ProjectProcesses.RemoveRange(processesToDelete);
            await _context.SaveChangesAsync();

            return NoContent(); // Return 204 No Content
        }
        [HttpPost("UpdateProcessFeatures")]
        public async Task<IActionResult> UpdateProcessFeatures([FromBody] UpdateProcessFeaturesRequest request)
        {
            if (request == null || request.ProjectId <= 0 || request.ProcessId <= 0)
            {
                return BadRequest("Invalid request data.");
            }

            var existingProcess = await _context.ProjectProcesses
                .FirstOrDefaultAsync(pp => pp.ProjectId == request.ProjectId && pp.ProcessId == request.ProcessId);

            if (existingProcess == null)
            {
                return NotFound("Process not found.");
            }

            // Update the installed features
            existingProcess.FeaturesList = request.FeaturesList;

            _context.ProjectProcesses.Update(existingProcess);
            await _context.SaveChangesAsync();

            return Ok("Process features updated successfully!");
        }
        [HttpPost("UpdateProcessSequence")]
        public async Task<IActionResult> UpdateProcessSequence([FromBody] List<UpdateSequenceDto> sequenceUpdates)
        {
            if (sequenceUpdates == null || !sequenceUpdates.Any())
            {
                return BadRequest("Invalid request data.");
            }

            var projectIds = sequenceUpdates.Select(s => s.ProjectId).Distinct().ToList();

            // Ensure that all provided projectIds belong to the same project
            var projects = await _context.Projects
                .Where(p => projectIds.Contains(p.ProjectId))
                .ToListAsync();

            if (projects.Count != projectIds.Count)
            {
                return BadRequest("One or more projects do not exist.");
            }

            // Update the sequence for each process
            foreach (var update in sequenceUpdates)
            {
                var existingProcess = await _context.ProjectProcesses
                    .FirstOrDefaultAsync(pp => pp.ProjectId == update.ProjectId && pp.ProcessId == update.ProcessId);

                if (existingProcess != null)
                {
                    existingProcess.Sequence = update.NewSequence; // Update the sequence
                    _context.ProjectProcesses.Update(existingProcess);
                }
                else
                {
                    return NotFound($"Process with ID {update.ProcessId} not found in project {update.ProjectId}.");
                }
            }

            await _context.SaveChangesAsync();

            return Ok("Process sequences updated successfully!");
        }





        public class DeleteRequest
        {
            public int ProjectId { get; set; }
            public List<int> ProcessIds { get; set; }
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
        public class UpdateProcessFeaturesRequest
        {
            public int ProjectId { get; set; }
            public int ProcessId { get; set; }
            public List<int> FeaturesList { get; set; }
        }
        public class UpdateSequenceDto
        {
            public int ProjectId { get; set; }
            public int ProcessId { get; set; }
            public int NewSequence { get; set; }
        }


    }
}

