using Microsoft.AspNetCore.Mvc;
using ERPAPI.Encryption;
using ERPAPI.Services;
using ERPAPI.Data;
using ERPAPI.Model;
using ERPGenericFunctions.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ERPAPI.Service;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILoggerService _loggerService;

        public UserController(AppDbContext context, ILoggerService loggerService)
        {
            _context = context;
            _loggerService = loggerService;
        }

        // POST: api/User/create
        [HttpPost("create")]
        public IActionResult CreateUser(User user)
        {
            try
            {
                // Check if the username already exists
                var existingUser = _context.Users.FirstOrDefault(u => u.UserName == user.UserName);
                if (existingUser != null)
                {
                    return Conflict(new { Message = "Username already exists" });
                }

                // Add the new user to the database
                _context.Users.Add(user);
                _context.SaveChanges();

                // Generate and hash a password for the user
                string generatedPassword = Passwordgen.GeneratePassword();
                var hashedPassword = Sha256.ComputeSHA256Hash(generatedPassword);

                // Create a UserAuth entry with the auto-generated password
                var userAuth = new UserAuth
                {
                    UserId = user.UserId,
                    Password = hashedPassword,
                    AutogenPass = true
                };

                _context.UserAuths.Add(userAuth);
                _context.SaveChanges();

                // Log the event
                _loggerService.LogEvent("User created", "User", user.UserId);

                return Ok(new { Message = "User created", UserName = user.UserName, Password = generatedPassword });
            }
            catch (Exception ex)
            {
                _loggerService.LogError("User creation failed", ex.Message, "UserController");
                return StatusCode(500, "User creation failed");
            }
        }

        // GET: api/User
        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetAllUsers()
        {
            try
            {
                // Retrieve all users from the database
                var users = await Task.FromResult(_context.Users.ToList());
                return Ok(users);
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Failed to retrieve users", ex.Message, "UserController");
                return StatusCode(500, "Failed to retrieve users");
            }
        }
    }
}
