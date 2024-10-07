using Microsoft.AspNetCore.Mvc;
using ERPAPI.Encryption;
using ERPAPI.Services;

using ERPAPI.Service;
using ERPAPI.Data;
using ERPAPI.Model;
using ERPGenericFunctions.Model;

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

        [HttpPost("create")]
        public IActionResult CreateUser(User user)
        {
            try
            {

                _context.Users.Add(user);
                _context.SaveChanges();
                string generatedpassword = Passwordgen.GeneratePassword();
                var hashedPassword = Sha256.ComputeSHA256Hash(generatedpassword);
                var userAuth = new UserAuth
                {
                    UserId = user.UserId,
                    Password = hashedPassword,
                    AutogenPass = true // Assuming auto-generated password at user creation
                };

                _context.UserAuths.Add(userAuth);
                _context.SaveChanges();

                _loggerService.LogEvent("User created", "User", user.UserId);
                return Ok(new { Message = "User created", Password = generatedpassword });
            }
            catch (Exception ex)
            {
                _loggerService.LogError("User creation failed", ex.Message, "UserController");
                return StatusCode(500, "User creation failed");
            }
        }
    }
}
