namespace EchoNET.API.DTOs;

public class PublicProfileResponse
{
    public string Username { get; set; }
    public string DisplayName { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public int FollowersCount { get; set; }
    public int FollowingCount { get; set; }
    public int PostsCount { get; set; }
}

public class UpdateProfileRequest
{
    public string? DisplayName { get; set; }
    public string? Bio { get; set; }
}