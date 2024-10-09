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

                return Ok(new { UserId = user.UserId, Message = "User created", UserName = user.UserName, Password = generatedPassword  });
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

        // GET: api/User/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetUserById(int id)
        {
            try
            {
                // Retrieve the user from the database by ID
                var user = await Task.FromResult(_context.Users.FirstOrDefault(u => u.UserId == id));

                if (user == null)
                {
                    return NotFound(new { Message = "User not found" });
                }

                return Ok(user);
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Failed to retrieve user", ex.Message, "UserController");
                return StatusCode(500, "Failed to retrieve user");
            }
        }

        // POST: api/User/generatePassword/{id}
        [HttpPost("generatePassword/{id}")]
        public IActionResult GeneratePasswordForUser(int id)
        {
            try
            {
                // Find the user by ID
                var user = _context.Users.FirstOrDefault(u => u.UserId == id);
                if (user == null)
                {
                    return NotFound(new { Message = "User not found" });
                }

                // Check if a UserAuth entry already exists for this user
                var userAuth = _context.UserAuths.FirstOrDefault(ua => ua.UserId == user.UserId);
                if (userAuth == null)
                {
                    // If no UserAuth exists, create a new one
                    userAuth = new UserAuth
                    {
                        UserId = user.UserId
                    };
                    _context.UserAuths.Add(userAuth);
                }

                // Generate and hash a new password
                string generatedPassword = Passwordgen.GeneratePassword();
                var hashedPassword = Sha256.ComputeSHA256Hash(generatedPassword);

                // Update UserAuth with the new password and set AutogenPass to true
                userAuth.Password = hashedPassword;
                userAuth.AutogenPass = true;

                // Save changes to the database
                _context.SaveChanges();

                // Log the event
                _loggerService.LogEvent("Password generated for existing user", "User", user.UserId);

                // Return the new password to the client
                return Ok(new { Message = "Password generated", UserName = user.UserName, Password = generatedPassword });
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Failed to generate password for user", ex.Message, "UserController");
                return StatusCode(500, new { Message = "Failed to generate password" });
            }
        }


    }
}
