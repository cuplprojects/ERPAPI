using Microsoft.AspNetCore.Mvc;
using ERPAPI.Encryption;

using ERPAPI.Model.NonDbModels;
using ERPAPI.Service;
using Microsoft.AspNetCore.Identity.Data;

using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using NuGet.Common;
using ERPAPI.Data;
using ERPAPI.Model;
using ERPGenericFunctions.Model;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        private IConfiguration _configuration;
        private readonly AppDbContext _context;
        private readonly ILoggerService _loggerService;

        public LoginController(AppDbContext context, ILoggerService loggerService, IConfiguration configuration)
        {
            _configuration = configuration;
            _context = context;
            _loggerService = loggerService;
        }

        private string GenerateToken(UserAuth user)
        {
            var securitykey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var credentials = new SigningCredentials(securitykey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserId.ToString()),
                new Claim("AutogenPass", user.AutogenPass.ToString()),
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Issuer"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(120),
                signingCredentials: credentials
                );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        [AllowAnonymous]
        [HttpPost("login")]
        public IActionResult Login([FromBody] Model.NonDbModels.LoginRequest loginRequest)

        {

            var userAuth = (from user in _context.Users
                            join ua in _context.UserAuths on user.UserId equals ua.UserId
                            where user.UserName == loginRequest.UserName
                            select new { ua, user.Status, user.UserName }).FirstOrDefault();

            if (userAuth == null)
            {
                return NotFound("User not found");
            }

            if (!userAuth.Status)
            {
                return Unauthorized("User is inactive");
            }

            string hashedPassword = Sha256.ComputeSHA256Hash(loginRequest.Password);
            Console.WriteLine(hashedPassword);

            if (hashedPassword != userAuth.ua.Password)
            {
                return Unauthorized("Invalid password");
            }


            var token = GenerateToken(userAuth.ua);
            _loggerService.LogEvent($"User Logged-in", "Login", userAuth.ua.UserId);
            return Ok(new { token = token, userAuth.ua.UserId, userAuth.ua.AutogenPass });
        }


        // Change Password API
        [Authorize]
        [HttpPut("Changepassword/{id}")]
        public IActionResult ChangePassword(int id, ChangePasswordRequest cred)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }


            string oldHashPass = Sha256.ComputeSHA256Hash(cred.OldPassword);
            var userauth = _context.UserAuths.FirstOrDefault(i => i.UserId == id);

            if (userauth == null)
            {
                return NotFound("User Authentication Data Not Found");
            }
            if (userauth.Password != oldHashPass)
            {
                return BadRequest("Existing Password Invalid");
            }

            string newPassword = cred.NewPassword;

            string hashedPassword = Sha256.ComputeSHA256Hash(newPassword);

            userauth.Password = hashedPassword;

            userauth.AutogenPass = false;

            _context.SaveChanges();
            var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                // Now you have the user ID
                _loggerService.LogEvent("Password-Changed", "Login", userId);
            }

            return Ok(new { newPassword, hashedPassword });


        }

        [HttpPost("setSecurityAnswers")]
        public IActionResult SetSecurityAnswers(SetSecurityAnswersRequest request)
        {
            var user = _context.UserAuths.FirstOrDefault(x => x.UserId  == request.UserId);
            if (user == null)
            {
                return NotFound("User not found.");
            }


            user.SecurityQuestion1Id = request.SecurityQuestion1Id;
            user.SecurityQuestion2Id = request.SecurityQuestion2Id;
            user.SecurityAnswer1 = request.SecurityAnswer1;
            user.SecurityAnswer2 = request.SecurityAnswer2;
            _context.SaveChanges();

            _loggerService.LogEvent("Security answers set", "User", user.UserId);
            return Ok("Security answers set successfully.");
        }


        // Forgot Password API
        [HttpPost("forgotPassword")]
        public IActionResult ForgotPassword(ERPAPI.Model.NonDbModels.ForgotPasswordRequest request)
        {
            var user = _context.Users.FirstOrDefault(x => x.UserName == request.UserName);  // Use 'request' instead of 'loginRequest'
            if (user == null)
            {
                return NotFound("User not found.");
            }

            var userAuth = _context.UserAuths.FirstOrDefault(u => u.UserId == user.UserId);
            if (userAuth == null ||
                userAuth.SecurityAnswer1 != request.SecurityAnswer1 ||
                userAuth.SecurityAnswer2 != request.SecurityAnswer2)
            {
                return Unauthorized("Security answers do not match.");
            }

            userAuth.Password = Sha256.ComputeSHA256Hash(request.NewPassword);
            _context.SaveChanges();

            _loggerService.LogEvent("Password reset", "User", userAuth.UserId);
            return Ok("Password reset successfully.");
        }

    }
}
