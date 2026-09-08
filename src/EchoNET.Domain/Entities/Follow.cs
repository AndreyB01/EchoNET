using System;

namespace EchoNET.Domain.Entities;

public class Follow
{
    public Guid Id { get; set; }
    public Guid FollowerId { get; set; }
    public User Follower { get; set; }
    public Guid FollowedId { get; set; }
    public User Followed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}