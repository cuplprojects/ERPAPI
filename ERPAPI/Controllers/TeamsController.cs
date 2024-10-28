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

        // Populate UserNames based on UserIds
        foreach (var team in teams)
        {
            team.UserNames = GetUserNamesByIds(team.UserIds);
        }

        return Ok(teams);
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

        // Populate UserNames based on UserIds
        team.UserNames = GetUserNamesByIds(team.UserIds);

        return Ok(team);
    }
    catch (Exception ex)
    {
        _loggerService.LogError("Failed to retrieve team", ex.Message, "TeamsController");
        return StatusCode(500, "Failed to retrieve team");
    }
}


        // Helper method to get usernames by user IDs
       private string GetUserNamesByIds(List<int> userIds)
{
    var userNames = new List<string>();
    var users = _context.Users.Where(u => userIds.Contains(u.UserId)).ToList(); // Fetch users in one query
    foreach (var user in users)
    {
        // Construct the full name
        var fullName = user.FirstName;
        if (!string.IsNullOrEmpty(user.MiddleName))
        {
            fullName += " " + user.MiddleName;
        }
        if (!string.IsNullOrEmpty(user.LastName))
        {
            fullName += " " + user.LastName;
        }
        userNames.Add(fullName.Trim()); // Add the full name to the list
    }
    return string.Join(", ", userNames); // Return as a comma-separated string
}

        // POST: api/Teams
        [HttpPost]
public async Task<ActionResult<Teams>> CreateTeam(Teams team)
{
    try
    {
        // Populate UserNames based on UserIds
        team.UserNames = GetUserNamesByIds(team.UserIds);

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