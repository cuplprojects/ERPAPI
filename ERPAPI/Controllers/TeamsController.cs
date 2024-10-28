using Microsoft.AspNetCore.Mvc;
using ERPAPI.Data;
using ERPAPI.Model;  
using Microsoft.EntityFrameworkCore;
using ERPAPI.Service;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TeamsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILoggerService _loggerService;

        public TeamsController(AppDbContext context, ILoggerService loggerService)
        {
            _context = context;
            _loggerService = loggerService;
        }

        // GET: api/Teams
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Teams>>> GetTeams()
        {
            try
            {
                var teams = await _context.Teams.ToListAsync();

                // Map user IDs to user details
                var userIds = teams.SelectMany(t => t.UserIds).Distinct().ToList();
                var users = await _context.Users
                    .Where(u => userIds.Contains(u.UserId))
                    .Select(u => new 
                    {
                        u.UserId,
                        FullName = $"{u.FirstName} {(string.IsNullOrEmpty(u.MiddleName) ? "" : u.MiddleName + " ")}{(string.IsNullOrEmpty(u.LastName) ? "" : u.LastName)}".Trim()
                    })
                    .ToListAsync();

                // Create a dictionary for quick lookup
                var userMap = users.ToDictionary(u => u.UserId, u => u.FullName);

                // Update teams with user objects
                var updatedTeams = teams.Select(team => new 
                {
                    team.TeamId,
                    team.TeamName,
                    team.Description,
                    team.CreatedDate,
                    team.Status,
                    Users = team.UserIds.Select(id => new 
                    {
                        Id = id,
                        Name = userMap.ContainsKey(id) ? userMap[id] : "Unknown" // Map user IDs to user objects
                    }).ToList()
                });

                return Ok(updatedTeams);
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Failed to retrieve teams", ex.Message, "TeamsController");
                return StatusCode(500, "Failed to retrieve teams");
            }
        }



        // GET: api/Teams/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Teams>> GetTeam(int id)
        {
            try
            {
                var team = await _context.Teams
                    .FirstOrDefaultAsync(t => t.TeamId == id);

                if (team == null)
                {
                    return NotFound(new { Message = "Team not found" });
                }

                return Ok(team);
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Failed to retrieve team", ex.Message, "TeamsController");
                return StatusCode(500, "Failed to retrieve team");
            }
        }

        // GET: api/Teams/5/Users
        [HttpGet("{id}/Users")]
        public async Task<ActionResult> GetTeamUserNames(int id)
        {
            try
            {
                // Fetch the team by ID
                var team = await _context.Teams
                    .FirstOrDefaultAsync(t => t.TeamId == id);

                if (team == null)
                {
                    return NotFound(new { Message = "Team not found" });
                }

                // Fetch the user names based on UserIds in the team
                var userNames = await _context.Users
                    .Where(u => team.UserIds.Contains(u.UserId))
                    .Select(u => new { u.UserId, u.UserName })
                    .ToListAsync();

                if (!userNames.Any())
                {
                    return NotFound(new { Message = "No users found for this team" });
                }

                return Ok(userNames);
            }
            catch
            {
                return StatusCode(500, "Failed to retrieve user names for team");
            }
        }


        // POST: api/Teams
        [HttpPost]
        public async Task<ActionResult<Teams>> CreateTeam(Teams team)
        {
            try
            {
                // Add the team to the context
                _context.Teams.Add(team);
                await _context.SaveChangesAsync();

                _loggerService.LogEvent("Team created", "Teams", team.TeamId);

                return CreatedAtAction(nameof(GetTeam), new { id = team.TeamId }, team);
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Failed to create team", ex.Message, "TeamsController");
                return StatusCode(500, "Failed to create team");
            }
        }

        // PUT: api/Teams/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTeam(int id, Teams team)
        {
            if (id != team.TeamId)
            {
                return BadRequest();
            }

            try
            {
                var existingTeam = await _context.Teams.FindAsync(id);
                if (existingTeam == null)
                {
                    return NotFound(new { Message = "Team not found" });
                }

                existingTeam.TeamName = team.TeamName;
                existingTeam.Description = team.Description;
                existingTeam.Status = team.Status;
                existingTeam.UserIds = team.UserIds; // Update user IDs

                await _context.SaveChangesAsync();
                _loggerService.LogEvent("Team updated", "Teams", id);

                return Ok(new { Message = "Team updated successfully" });
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Failed to update team", ex.Message, "TeamsController");
                return StatusCode(500, "Failed to update team");
            }
        }

        // DELETE: api/Teams/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTeam(int id)
        {
            try
            {
                var team = await _context.Teams.FindAsync(id);
                if (team == null)
                {
                    return NotFound(new { Message = "Team not found" });
                }

                _context.Teams.Remove(team);
                await _context.SaveChangesAsync();

                _loggerService.LogEvent("Team deleted", "Teams", id);

                return Ok(new { Message = "Team deleted successfully" });
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Failed to delete team", ex.Message, "TeamsController");
                return StatusCode(500, "Failed to delete team");
            }
        }
    }
}