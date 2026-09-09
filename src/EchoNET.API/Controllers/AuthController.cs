using EchoNET.Domain.Entities;
using EchoNET.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Security.Cryptography;

namespace EchoNET.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (await _context.Users.AnyAsync(u => u.Email == dto.Email || u.Username == dto.Username))
            return Conflict("Email or username already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = dto.Email,
            Username = dto.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            DisplayName = dto.Username,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, new { user.Id, user.Email, user.Username });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.EmailOrUsername || u.Username == dto.EmailOrUsername);
        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Unauthorized("Invalid credentials.");

        var accessToken = GenerateJwtToken(user);
        var refreshToken = GenerateRefreshToken();
        await SaveRefreshToken(user.Id, refreshToken);

        return Ok(new
        {
            access_token = accessToken,
            refresh_token = refreshToken,
            expires_in = 900
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshDto dto)
    {
        var tokenHash = ComputeHash(dto.RefreshToken);
        var storedToken = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow);

        if (storedToken == null)
            return Unauthorized("Invalid or expired refresh token.");

        storedToken.RevokedAt = DateTime.UtcNow;

        var newAccessToken = GenerateJwtToken(storedToken.User);
        var newRefreshToken = GenerateRefreshToken();
        await SaveRefreshToken(storedToken.User.Id, newRefreshToken);

        await _context.SaveChangesAsync();

        return Ok(new { access_token = newAccessToken, refresh_token = newRefreshToken, expires_in = 900 });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutDto dto)
    {
        var tokenHash = ComputeHash(dto.RefreshToken);
        var storedToken = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash && rt.RevokedAt == null);
        if (storedToken != null)
        {
            storedToken.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
        return NoContent();
    }

    private string GenerateJwtToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Secret"]);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()) }),
            Expires = DateTime.UtcNow.AddMinutes(15),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private string ComputeHash(string token)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private async Task SaveRefreshToken(Guid userId, string refreshToken)
    {
        var tokenHash = ComputeHash(refreshToken);
        var expires = DateTime.UtcNow.AddDays(7);
        var rt = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expires,
            CreatedAt = DateTime.UtcNow
        };
        _context.RefreshTokens.Add(rt);
        await _context.SaveChangesAsync();
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();
        return Ok(new { user.Id, user.Email, user.Username });
    }
}

// DTOs (можно вынести в отдельную папку DTOs, но для простоты оставлены здесь)
public class RegisterDto
{
    public string Email { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
}

public class LoginDto
{
    public string EmailOrUsername { get; set; }
    public string Password { get; set; }
}

public class RefreshDto
{
    public string RefreshToken { get; set; }
}

public class LogoutDto
{
    public string RefreshToken { get; set; }
}