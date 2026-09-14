# Echo.NET — Microblogging Social Network REST API

Backend service for a microblogging social network (a Twitter / Threads analog) built on **.NET 10**,
showcasing skills in designing, developing, testing, and containerizing REST APIs.

> Portfolio pet project: clean architecture, JWT authentication with refresh tokens,
> cursor-based feed pagination, caching, Docker.

## 🛠 Tech Stack

| Component | Technology |
|-----------|------------|
| Language | C# / .NET 10 |
| Web framework | ASP.NET Core Web API |
| ORM | Entity Framework Core |
| Database | PostgreSQL 15 |
| Authentication | JWT (access + refresh, HS256) |
| Password hashing | BCrypt.Net |
| Caching | IMemoryCache |
| Documentation | Swagger / OpenAPI |
| Containerization | Docker, Docker Compose |
| Testing | xUnit *(planned)* |

## ✨ Features

- 🔐 **Authentication**: register, login, refresh (rotating), logout; access token 15 min, refresh — 7 days
- 👤 **Profile**: public profile by username, edit own profile (display_name, bio, avatar)
- 📝 **Posts**: create (with `Idempotency-Key` and a 1 post/sec rate limit), read, soft-delete (author only)
- 👥 **Follows**: follow / unfollow with self-follow check and denormalized counters (atomic `UPDATE`)
- ❤️ **Likes**: like / unlike with a UNIQUE constraint (one like per user)
- 📰 **Feed**: main feed (own posts + posts from followed users), sorted by `created_at DESC`,
  cursor-based pagination (no OFFSET), 30-second cache
- 🐳 **Infrastructure**: fully containerized (API + PostgreSQL), automatic migrations on startup

## 🚀 Quick Start

### Option 1. With Docker (recommended)

Requires Docker Desktop to be installed.

```bash
git clone https://github.com/AndreyB01/EchoNET.git
cd EchoNET
docker-compose up -d
```

The API will be available at **http://localhost:5000/swagger**

Migrations are applied automatically on startup — no manual steps required.

### Option 2. Locally (for development)

Requirements:
- .NET 10 SDK
- PostgreSQL 15 running locally

1. Configure the connection string in `src/EchoNET.API/appsettings.Development.json`:

   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Port=5432;Database=EchoNET;Username=postgres;Password=postgres"
     }
   }
   ```

2. Apply migrations:

   ```bash
   dotnet ef database update --project src/EchoNET.Infrastructure --startup-project src/EchoNET.API
   ```

3. Run the application:

   ```bash
   dotnet run --project src/EchoNET.API
   ```

## 📁 Project Structure

```
EchoNET/
├── src/
│   ├── EchoNET.API/             # Web layer (controllers, Program.cs)
│   ├── EchoNET.Application/     # Business logic, DTOs
│   ├── EchoNET.Domain/          # Entities, business rules
│   └── EchoNET.Infrastructure/  # EF Core, DbContext, migrations
├── Dockerfile
├── docker-compose.yml
└── README.md
```

Dependencies point inward: API → Infrastructure → Application → Domain.

## 🐳 Docker Commands

```bash
# Start everything
docker-compose up -d

# Stop (DB data is preserved)
docker-compose down

# Stop and wipe the database
docker-compose down -v

# Rebuild the image from scratch
docker-compose build --no-cache

# API logs
docker-compose logs -f api

# Container status
docker-compose ps

# Open a psql session
docker exec -it echo-postgres psql -U postgres -d EchoNET
```

## 🌐 Main Endpoints

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/api/Auth/register` | — | Register a new user |
| POST | `/api/Auth/login` | — | Log in, returns access + refresh token |
| POST | `/api/Auth/refresh` | — | Refresh access token (rotating) |
| POST | `/api/Auth/logout` | — | Revoke refresh token |
| GET | `/api/users/{username}` | — | Public profile |
| PATCH | `/api/users/me` | ✅ | Update own profile |
| POST | `/api/users/{username}/follow` | ✅ | Follow a user |
| DELETE | `/api/users/{username}/follow` | ✅ | Unfollow a user |
| POST | `/api/Posts` | ✅ | Create a post |
| GET | `/api/Posts/{id}` | ✅ | Get post by ID |
| DELETE | `/api/Posts/{id}` | ✅ | Soft-delete a post |
| POST | `/api/posts/{postId}/like` | ✅ | Like a post |
| DELETE | `/api/posts/{postId}/like` | ✅ | Unlike a post |
| GET | `/api/feed` | ✅ | Main feed (cursor-paginated) |

To test secured endpoints: get a token via `POST /api/Auth/login`, click **Authorize** in Swagger, paste the token.

## 🔧 Configuration

Key settings are defined in `docker-compose.yml`:

| Variable | Description |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` / `Production` |
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `Jwt__Secret` | JWT signing secret key |

For production, move these values to a `.env` file or a secrets manager (Docker Secrets, Kubernetes Secrets, etc.) and never commit them to the repository.



## 📄 License

MIT