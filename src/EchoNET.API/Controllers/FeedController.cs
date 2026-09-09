using EchoNET.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace EchoNET.API.Controllers;

[ApiController]
[Route("api/feed")]
[Authorize]
public class FeedController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;

    public FeedController(ApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> GetFeed([FromQuery] string? cursor = null, [FromQuery] int limit = 20)
    {
        if (limit > 50) limit = 50;

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

        // Кеширование: ключ зависит от userId, cursor и limit (чтобы инвалидировать при новых постах — просто по таймауту)
        var cacheKey = $"feed_{userId}_{cursor}_{limit}";
        if (_cache.TryGetValue(cacheKey, out object cachedResult))
            return Ok(cachedResult);

        // Базовый запрос: посты авторов, на которых подписан пользователь + свои посты
        var query = _context.Posts
            .Include(p => p.Author)
            .Where(p => p.DeletedAt == null &&
                (p.AuthorId == userId ||
                 _context.Follows.Any(f => f.FollowerId == userId && f.FollowedId == p.AuthorId)))
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .AsQueryable();

        // Пагинация курсором
        if (!string.IsNullOrEmpty(cursor))
        {
            var parts = cursor.Split('_');
            if (parts.Length == 2 && DateTime.TryParse(parts[0], out var cursorDate) && Guid.TryParse(parts[1], out var cursorId))
            {
                query = query.Where(p => p.CreatedAt < cursorDate ||
                    (p.CreatedAt == cursorDate && p.Id < cursorId));
            }
        }

        var posts = await query.Take(limit + 1).ToListAsync(); // берем на 1 больше, чтобы узнать, есть ли следующая страница

        var hasNext = posts.Count > limit;
        var items = posts.Take(limit).Select(p => new
        {
            p.Id,
            p.Content,
            p.CreatedAt,
            p.LikesCount,
            p.CommentsCount,
            Author = new { p.Author.Id, p.Author.Username, p.Author.DisplayName }
        }).ToList();

        string? nextCursor = null;
        if (hasNext && items.Any())
        {
            var last = items.Last();
            nextCursor = $"{last.CreatedAt:yyyy-MM-ddTHH:mm:ss.fffffffZ}_{last.Id}";
        }

        var result = new { items, nextCursor };

        // Кешируем на 30 секунд (для демонстрации)
        _cache.Set(cacheKey, result, TimeSpan.FromSeconds(30));

        return Ok(result);
    }
}