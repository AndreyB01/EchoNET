using EchoNET.Domain.Entities;
using EchoNET.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EchoNET.API.Controllers;

[ApiController]
[Route("api/posts/{postId}/like")]
[Authorize]
public class LikesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public LikesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Like(Guid postId)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

        // Проверяем, существует ли пост
        var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == postId);
        if (post == null)
            return NotFound("Post not found.");

        if (post.AuthorId == userId)
            return BadRequest("You cannot like your own post.");

        // Проверяем, не лайкнул ли уже
        var existing = await _context.Likes
            .FirstOrDefaultAsync(l => l.UserId == userId && l.PostId == postId);
        if (existing != null)
            return Conflict("You already liked this post.");

        var like = new Like
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PostId = postId,
            CreatedAt = DateTime.UtcNow
        };
        _context.Likes.Add(like);

        // Увеличиваем счётчик лайков поста
        await _context.Posts
            .Where(p => p.Id == postId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.LikesCount, p => p.LikesCount + 1));

        await _context.SaveChangesAsync();

        return Ok(new { message = "Post liked." });
    }

    [HttpDelete]
    public async Task<IActionResult> Unlike(Guid postId)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

        var like = await _context.Likes
            .FirstOrDefaultAsync(l => l.UserId == userId && l.PostId == postId);
        if (like == null)
            return NotFound("You have not liked this post.");

        _context.Likes.Remove(like);

        await _context.Posts
            .Where(p => p.Id == postId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.LikesCount, p => p.LikesCount - 1));

        await _context.SaveChangesAsync();

        return NoContent();
    }
}