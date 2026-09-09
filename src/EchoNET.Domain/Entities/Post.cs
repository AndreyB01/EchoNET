using System;

namespace EchoNET.Domain.Entities;

public class Post
{
    public Guid Id { get; set; }
    public Guid AuthorId { get; set; }
    public User Author { get; set; }  // навигационное свойство
    public string Content { get; set; } // до 500 символов
    public int LikesCount { get; set; } = 0;
    public int CommentsCount { get; set; } = 0; // для будущего, пока не используем
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; } // soft-delete
}