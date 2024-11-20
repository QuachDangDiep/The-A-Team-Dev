using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly MyAppContext _context;
        private readonly IConfiguration _config;

        public AuthController(MyAppContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        // Đăng ký
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (await _context.Accounts.AnyAsync(a => a.Email == request.Email))
                return BadRequest("Email đã tồn tại.");

            var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "User");
            if (userRole == null) return StatusCode(500, "Role mặc định không tồn tại.");

            var account = new Account
            {
                Email = request.Email,
                Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
                RoleId = userRole.RoleId
            };

            var customer = new Customer
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                DateOfBirth = request.DateOfBirth,
                Account = account
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            return Ok("Đăng ký thành công.");
        }

        // Đăng nhập
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var account = await _context.Accounts.Include(a => a.Role).FirstOrDefaultAsync(a => a.Email == request.Email);
            if (account == null || !BCrypt.Net.BCrypt.Verify(request.Password, account.Password))
                return Unauthorized("Thông tin đăng nhập không chính xác.");

            var token = GenerateJwtToken(account);
            return Ok(new { Token = token });
        }

        // Quên mật khẩu
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Email == request.Email);
            if (account == null) return NotFound("Email không tồn tại.");

            var resetToken = new PasswordResetToken
            {
                Token = Guid.NewGuid().ToString(),
                ExpiryDate = DateTime.UtcNow.AddHours(1),
                AccountId = account.AccountId
            };

            _context.PasswordResetTokens.Add(resetToken);
            await _context.SaveChangesAsync();

            // Gửi Email
            if (!SendEmail(account.Email, "Password Reset", $"Your reset token: {resetToken.Token}"))
                return StatusCode(500, "Lỗi khi gửi email.");

            return Ok("Mã đặt lại mật khẩu đã được gửi qua email.");
        }

        // Đặt lại mật khẩu
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var tokenEntry = await _context.PasswordResetTokens
                .Include(t => t.Account)
                .FirstOrDefaultAsync(t => t.Token == request.Token && t.ExpiryDate > DateTime.UtcNow);

            if (tokenEntry == null) return BadRequest("Mã không hợp lệ hoặc đã hết hạn.");

            tokenEntry.Account.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            _context.PasswordResetTokens.Remove(tokenEntry);

            await _context.SaveChangesAsync();
            return Ok("Mật khẩu đã được đặt lại.");
        }

        // Tạo JWT Token
        private string GenerateJwtToken(Account account)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, account.Email),
                new Claim(ClaimTypes.Role, account.Role.RoleName),
                new Claim("id", account.AccountId.ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // Gửi Email
        private bool SendEmail(string toEmail, string subject, string body)
        {
            try
            {
                var email = new MimeMessage();
                email.From.Add(new MailboxAddress("MyApp", _config["Email:From"]));
                email.To.Add(new MailboxAddress("", toEmail));
                email.Subject = subject;
                email.Body = new TextPart("plain") { Text = body };

                using var smtp = new SmtpClient();
                smtp.Connect(_config["Email:SmtpServer"], 587, SecureSocketOptions.StartTls); 
                smtp.Authenticate(_config["Email:Username"], _config["Email:Password"]);
                smtp.Send(email);
                smtp.Disconnect(true);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Email sending failed: {ex.Message}");
                return false;
            }
        }
    }
    public class RegisterRequest
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }
    public class LoginRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
    public class ForgotPasswordRequest
    {
        public string Email { get; set; }
    }
    public class ResetPasswordRequest
    {
        public string Token { get; set; }
        public string NewPassword { get; set; }
    }
    public class UpdateCustomerRequest
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime DateOfBirth { get; set; }
    }
}
