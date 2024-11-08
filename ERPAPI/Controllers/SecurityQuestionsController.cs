﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
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
    public class SecurityQuestionsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILoggerService _loggerService;

        public SecurityQuestionsController(AppDbContext context, ILoggerService loggerService)
        {
            _context = context;
            _loggerService = loggerService;
        }

        // Helper method to retrieve the UserId from claims
        private int GetUserId()
        {
            // Retrieve the UserId from the authenticated user's claims
            if (int.TryParse(User?.FindFirst("UserId")?.Value, out int userId))
            {
                return userId;
            }
            return 0; // Return 0 or a default ID if not available
        }

        // GET: api/SecurityQuestions
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SecurityQuestion>>> GetSecurityQuestions()
        {
            int userId = GetUserId();
            _loggerService.LogEvent("Retrieve all security questions", "Information", userId);

            var securityQuestions = await _context.SecurityQuestions.ToListAsync();
            return Ok(securityQuestions);
        }

        // GET: api/SecurityQuestions/5
        [HttpGet("{id}")]
        public async Task<ActionResult<SecurityQuestion>> GetSecurityQuestion(int id)
        {
            int userId = GetUserId();
            _loggerService.LogEvent($"Retrieve security question with ID {id}", "Information", userId);

            var securityQuestion = await _context.SecurityQuestions.FindAsync(id);
            if (securityQuestion == null)
            {
                _loggerService.LogEvent($"Security question with ID {id} not found", "Error", userId);
                return NotFound();
            }

            return Ok(securityQuestion);
        }

        // PUT: api/SecurityQuestions/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutSecurityQuestion(int id, SecurityQuestion securityQuestion)
        {
            if (id != securityQuestion.QuestionId)
            {
                return BadRequest();
            }

            int userId = GetUserId();
            _loggerService.LogEvent($"Update security question with ID {id}", "Update", userId);

            _context.Entry(securityQuestion).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SecurityQuestionExists(id))
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

        // POST: api/SecurityQuestions
        [HttpPost]
        public async Task<ActionResult<SecurityQuestion>> PostSecurityQuestion(SecurityQuestion securityQuestion)
        {
            int userId = GetUserId();
            _loggerService.LogEvent("Create new security question", "Create", userId);

            _context.SecurityQuestions.Add(securityQuestion);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetSecurityQuestion", new { id = securityQuestion.QuestionId }, securityQuestion);
        }

        // DELETE: api/SecurityQuestions/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSecurityQuestion(int id)
        {
            int userId = GetUserId();
            _loggerService.LogEvent($"Delete security question with ID {id}", "Delete", userId);

            var securityQuestion = await _context.SecurityQuestions.FindAsync(id);
            if (securityQuestion == null)
            {
                _loggerService.LogEvent($"Security question with ID {id} not found", "Error", userId);
                return NotFound();
            }

            _context.SecurityQuestions.Remove(securityQuestion);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool SecurityQuestionExists(int id)
        {
            return _context.SecurityQuestions.Any(e => e.QuestionId == id);
        }
    }
}
