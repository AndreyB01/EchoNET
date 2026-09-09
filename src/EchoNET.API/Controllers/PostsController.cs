using EchoNET.Domain.Entities;
using EchoNET.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace EchoNET.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // все методы требуют авторизации
public class PostsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache; // для идемпотентности (можно заменить на таблицу)
    private static readonly ConcurrentDictionary<string, DateTime> _postRateLimits = new();

    public PostsController(ApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    [HttpPost]
    public async Task<IActionResult> CreatePost([FromBody] CreatePostDto dto, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey)
    {
        // Проверка идемпотентности (упрощённо: храним в кеше 24 часа)
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            if (_cache.TryGetValue($"idempotent_{idempotencyKey}", out object result))
                return Ok(result);
        }

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

        // Ограничение: 1 пост в секунду
        var key = $"post_rate_{userId}";
        if (_postRateLimits.TryGetValue(key, out var lastPostTime))
        {
            if ((DateTime.UtcNow - lastPostTime).TotalSeconds < 1)
                return StatusCode(429, "Too many posts. Please wait.");
        }
        _postRateLimits[key] = DateTime.UtcNow;

        var post = new Post
        {
            Id = Guid.NewGuid(),
            AuthorId = userId,
            Content = dto.Content,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        // Обновить счётчик постов пользователя
        await _context.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.PostsCount, u => u.PostsCount + 1));

        var response = new { post.Id, post.Content, post.CreatedAt };

        // Сохранить идемпотентность
        if (!string.IsNullOrEmpty(idempotencyKey))
            _cache.Set($"idempotent_{idempotencyKey}", response, TimeSpan.FromHours(24));

        return CreatedAtAction(nameof(GetPost), new { id = post.Id }, response);
    }

    [HttpGet("{id}")]
    [AllowAnonymous] // можно смотреть посты без авторизации (но для простоты оставим Authorize)
    public async Task<IActionResult> GetPost(Guid id)
    {
        var post = await _context.Posts
            .Include(p => p.Author)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (post == null)
            return NotFound();

        return Ok(new
        {
            post.Id,
            post.Content,
            post.CreatedAt,
            Author = new { post.Author.Id, post.Author.Username, post.Author.DisplayName },
            post.LikesCount,
            post.CommentsCount
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePost(Guid id)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == id && p.AuthorId == userId);

        if (post == null)
            return NotFound();

        // Soft delete
        post.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

public class CreatePostDto
{
    public string Content { get; set; }
}