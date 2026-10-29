using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using TexasHoldemServer.Data;
using TexasHoldemServer.Models;

namespace TexasHoldemServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;

        // Create a constructor to inject the AppDbContext
        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        // Register
        [HttpPost("register")]
        public async Task<IActionResult> Register(string username, string password,string q1, string a1, string q2, string a2)
        {
            // Check if the username and password are not empty
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return BadRequest("User name and Password can't be none!");
            }

            // Check if the username already exists
            if (await _context.Users.AnyAsync(u => u.Username == username))
            {
                return BadRequest("This User name already exists");
            }

            // Create a new user
            var user = new User
            {
                Username = username,
                // For simplicity, we use a simple hash function here. In production, consider using a stronger hashing algorithm with salt (e.g., bcrypt).
                PasswordHash = HashPassword(password),
                SecurityQ1 = q1,
                SecurityA1 = a1,
                SecurityQ2 = q2,
                SecurityA2 = a2,
                Chips = 5000, // Registering users start with 5000 chips
                CreatedAt = DateTime.Now
            };

            // Save the user to the database
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Registered Successful！", userId = user.UserID });
        }

        // Login
        [HttpPost("login")]
        public async Task<IActionResult> Login(string username, string password)
        {
            // Find the user by username
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == username);

            // If user not found, return error
            if (user == null)
            {
                return BadRequest("User not found");
            }

            // Verify the password
            if (user.PasswordHash != HashPassword(password))
            {
                return BadRequest("Password is wrong");
            }

            if (TexasHoldemServer.Hubs.SessionManager.IsUserLoggedIn(username))
            {
                return BadRequest("This account has already been logged in elsewhere!");
            }

            TexasHoldemServer.Hubs.SessionManager.LoginUser(username);

            // login successful
            return Ok(new
            {
                message = "Login Successful！",
                userId = user.UserID,
                username = user.Username,
                chips = user.Chips
            });
        }

        // Password hashing function(SHA256)
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(bytes);
            }
        }

        [HttpPost("logout")]
        public IActionResult Logout([FromQuery] string username)
        {
            if (!string.IsNullOrWhiteSpace(username))
            {
                TexasHoldemServer.Hubs.SessionManager.LogoutUser(username);
            }
            return Ok(new { message = "Logged out" });
        }

        // Get security questions for a given username
        [HttpGet("get-questions")]
        public async Task<IActionResult> GetQuestions(string username)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == username);
            if (user == null) return BadRequest("用户名不存在");

            // return the two security questions
            return Ok(new { q1 = user.SecurityQ1, q2 = user.SecurityQ2 });
        }

        // Verify the answers to the security questions
        [HttpPost("verify-answers")]
        public async Task<IActionResult> VerifyAnswers(string username, string a1, string a2)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == username);
            if (user == null) return BadRequest("用户名不存在");

            // Compare the provided answers with the stored answers
            if (user.SecurityA1 == a1 && user.SecurityA2 == a2)
            {
                return Ok(new { message = "验证成功" });
            }

            return BadRequest("安全问题答案错误");
        }

        // Reset password after verifying security questions
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(string username, string newPassword)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == username);
            if (user == null) return BadRequest("用户名不存在");

            // Hash the new password
            string newPasswordHash = HashPassword(newPassword);

            // Compare the new password hash with the old password hash
            if (user.PasswordHash == newPasswordHash)
            {
                return BadRequest("New password cannot be the same as the old password!");
            }

            // if different, update the password hash and save changes
            user.PasswordHash = newPasswordHash;
            await _context.SaveChangesAsync();

            return Ok(new { message = "密码重置成功！" });
        }

        // Receive the username and new chips count from the client
        public class SyncChipsDto
        {
            public string Username { get; set; }
            public int Chips { get; set; }
        }

        // Update the chips count for the given username in the database
        [HttpPost("syncChips")]
        public async Task<IActionResult> SyncChips([FromBody] SyncChipsDto request)
        {
            // find the user by username
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == request.Username);
            if (user == null)
            {
                return BadRequest("User not found");
            }

            // Update the chips count
            user.Chips = request.Chips;

            // save changes to the database
            await _context.SaveChangesAsync();

            return Ok(new { message = "Chips updated successfully!" });
        }

    }
}