using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/auth");

        auth.MapPost("/register", RegisterUser)
            .AllowAnonymous()
            .WithSummary("Register a user and issue a bearer token");
        auth.MapPost("/login", Login)
            .AllowAnonymous()
            .WithSummary("Authenticate a user and issue a bearer token");

        return auth;
    }

    public static async Task<IResult> RegisterUser(
        RegisterRequest request,
        UserDb db,
        IPasswordHasher<User> passwordHasher,
        JwtTokenService tokenService,
        CancellationToken cancellationToken)
    {
        var username = request.Username?.Trim();
        if (string.IsNullOrWhiteSpace(username) || username.Length is < 3 or > 50 ||
            string.IsNullOrWhiteSpace(request.Password) || request.Password.Length is < 12 or > 128)
        {
            return TypedResults.Problem(
                title: "Invalid registration details",
                detail: "Username must be 3-50 characters and password must be 12-128 characters.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (await db.Users.AnyAsync(user => user.Username == username, cancellationToken))
        {
            return TypedResults.Problem(
                title: "Username already exists",
                statusCode: StatusCodes.Status409Conflict);
        }

        var user = new User { Username = username };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/users/{user.Id}", tokenService.CreateToken(user));
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        UserDb db,
        IPasswordHasher<User> passwordHasher,
        JwtTokenService tokenService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
        {
            return TypedResults.Problem(
                title: "Invalid username or password",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var username = request.Username.Trim();
        var user = await db.Users.FirstOrDefaultAsync(
            existingUser => existingUser.Username == username,
            cancellationToken);
        if (user is null ||
            passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) ==
            PasswordVerificationResult.Failed)
        {
            return TypedResults.Problem(
                title: "Invalid username or password",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return TypedResults.Ok(tokenService.CreateToken(user));
    }
}
