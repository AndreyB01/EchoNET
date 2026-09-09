using EchoNET.Domain.Entities;
using EchoNET.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EchoNET.API.Controllers;

[ApiController]
[Route("api/users/{username}/follow")]
[Authorize]
public class FollowsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public FollowsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Follow(string username)
    {
        var followerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        var followed = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (followed == null)
            return NotFound("User not found.");

        if (followerId == followed.Id)
            return BadRequest("You cannot follow yourself.");

        // Проверка, не подписан ли уже
        var existing = await _context.Follows
            .FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FollowedId == followed.Id);
        if (existing != null)
            return Conflict("Already following.");

        var follow = new Follow
        {
            Id = Guid.NewGuid(),
            FollowerId = followerId,
            FollowedId = followed.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Follows.Add(follow);

        // Атомарно обновляем счётчики
        await _context.Users
            .Where(u => u.Id == followerId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.FollowingCount, u => u.FollowingCount + 1));

        await _context.Users
            .Where(u => u.Id == followed.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.FollowersCount, u => u.FollowersCount + 1));

        await _context.SaveChangesAsync();

        return Ok(new { message = $"You are now following {username}." });
    }

    [HttpDelete]
    public async Task<IActionResult> Unfollow(string username)
    {
        var followerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        var followed = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (followed == null)
            return NotFound("User not found.");

        var follow = await _context.Follows
            .FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FollowedId == followed.Id);
        if (follow == null)
            return NotFound("You are not following this user.");

        _context.Follows.Remove(follow);

        // Обновляем счётчики (уменьшаем)
        await _context.Users
            .Where(u => u.Id == followerId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.FollowingCount, u => u.FollowingCount - 1));

        await _context.Users
            .Where(u => u.Id == followed.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.FollowersCount, u => u.FollowersCount - 1));

        await _context.SaveChangesAsync();

        return NoContent();
    }
}