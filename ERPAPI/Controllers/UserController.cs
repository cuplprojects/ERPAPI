using Microsoft.AspNetCore.Mvc;
using ERPAPI.Encryption;
using ERPAPI.Services;
using ERPAPI.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using ERPGenericFunctions.Model;
using System;
using System.IO;
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
        private readonly IWebHostEnvironment _hostingEnvironment;

        public UserController(AppDbContext context, ILoggerService loggerService, IWebHostEnvironment hostingEnvironment)
        {
            _context = context;
            _loggerService = loggerService;
            _hostingEnvironment = hostingEnvironment;
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

        // POST: api/User/uploadProfilePicture/{id}
        [HttpPost("upload/{userId}")]
        public IActionResult UploadImage(int userId)
        {
            try
            {
                // Ensure the user with the provided userId exists
                if (UserExists(userId))
                {
                    var file = Request.Form.Files[0];

                    if (file.Length > 0)
                    {
                        // Extract the file extension from the original filename
                        var fileExtension = Path.GetExtension(file.FileName);

                        // Generate the custom filename based on the user ID and original file extension
                        var customFileName = $"{userId}_profilepic{fileExtension}";

                        var filePath = Path.Combine("wwwroot/Image", customFileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            file.CopyTo(stream);
                        }

                        // Update the user's profile picture path in the database
                        var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
                        if (user != null)
                        {
                            user.ProfilePicturePath = $"images/{customFileName}";
                            _context.SaveChanges();
                        }

                        return Ok(new { message = "Image uploaded successfully", filePath });
                    }
                    else
                    {
                        return BadRequest("No file uploaded");
                    }
                }
                else
                {
                    return BadRequest("Invalid user");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // PUT: api/User/updateProfilePicture/{userId}
        [HttpPut("updateProfilePicture/{userId}")]
        public IActionResult UpdateProfilePicture(int userId)
        {
            try
            {
                // Ensure the user with the provided userId exists
                var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
                if (user == null)
                {
                    return NotFound(new { Message = "User not found" });
                }

                // Delete the existing profile picture if it exists
                if (!string.IsNullOrEmpty(user.ProfilePicturePath))
                {
                    var oldFilePath = Path.Combine("wwwroot", user.ProfilePicturePath);
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                // Proceed to upload the new profile picture
                var file = Request.Form.Files[0];
                if (file.Length > 0)
                {
                    var fileExtension = Path.GetExtension(file.FileName);
                    var customFileName = $"{userId}_profilepic{fileExtension}";
                    var newFilePath = Path.Combine("wwwroot/Image", customFileName);

                    using (var stream = new FileStream(newFilePath, FileMode.Create))
                    {
                        file.CopyTo(stream);
                    }

                    // Update the profile picture path in the database
                    user.ProfilePicturePath = $"images/{customFileName}";
                    _context.SaveChanges();

                    return Ok(new { message = "Profile picture updated successfully", filePath = user.ProfilePicturePath });
                }
                else
                {
                    return BadRequest("No file uploaded");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }


        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.UserId == id);
        }

    }
}
