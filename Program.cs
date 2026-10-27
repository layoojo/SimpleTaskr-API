using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Identity;
using NSwag.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<SimpleTaskrDb>(opt => opt.UseInMemoryDatabase("SimpleTaskrList"));
builder.Services.AddDbContext<UserDb>(opt => opt.UseInMemoryDatabase("UsersList"));
builder.Services.AddOpenApi(options =>
{
    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
    options.AddDocumentTransformer((document, _, _) =>
    {
        var components = document.Components ??= new OpenApiComponents();
        var securitySchemes = components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        securitySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter a valid JWT access token."
        };
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, _) =>
    {
        foreach (var parameter in operation.Parameters ?? [])
        {
            if (parameter.In == ParameterLocation.Path && parameter.Schema is OpenApiSchema schema &&
                schema.Format == "int32")
            {
                schema.Type = JsonSchemaType.Integer;
                schema.Pattern = null;
            }
        }

        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (metadata is not null &&
            metadata.OfType<IAuthorizeData>().Any() &&
            !metadata.OfType<IAllowAnonymous>().Any() &&
            context.Document is { } document)
        {
            var securityRequirements = operation.Security ??= [];
            securityRequirements.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
        }
        return Task.CompletedTask;
    });
});

var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"];
if (string.IsNullOrWhiteSpace(secretKey) || Encoding.UTF8.GetByteCount(secretKey) < 32)
{
    throw new InvalidOperationException("Jwt:SecretKey must be configured with at least 32 UTF-8 bytes.");
}

var issuer = jwtSettings["Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
var audience = jwtSettings["Audience"] ?? throw new InvalidOperationException("Jwt:Audience is not configured.");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = signingKey,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton(new JwtTokenService(issuer, audience, signingKey));


var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUi(settings =>
    {
        settings.Path = "/swagger";
        settings.SwaggerRoutes.Add(new SwaggerUiRoute("SimpleTaskr API", "/openapi/v1.json"));
    });
}

var tasks = app.MapGroup("/tasks");
tasks.RequireAuthorization();

tasks.MapGet("/", GetAllTasks).WithSummary("Get all tasks");
tasks.MapGet("/complete", GetCompleteTasks).WithSummary("Get all completed tasks");
tasks.MapGet("/{id}", GetTask).WithSummary("Get a specific task by ID");
tasks.MapPost("/", CreateTask).WithSummary("Create a new task");
tasks.MapPatch("/{id}", PatchTask).WithSummary("Patch a specific task");
tasks.MapDelete("/{id}", DeleteTask).WithSummary("Delete a specific task");
tasks.MapPut("/{id}/assign", AssignTask).WithSummary("Assign a specific task to a user").WithDescription("Assign a specific task to a user by providing the user ID in the request body.");
tasks.MapGet("/users", GetAllUsers).WithSummary("Get all users");
tasks.MapGet("/users/{id}", GetUser).WithSummary("Get a specific user by ID");
app.MapAuthEndpoints();


// GET api/tasks/ - Get all tasks
static async Task<IResult> GetAllTasks(SimpleTaskrDb db)
{
    return TypedResults.Ok(await db.Tasks.ToArrayAsync());
}

// GET api/tasks/ - Get all users
static async Task<IResult> GetAllUsers(UserDb userDb, SimpleTaskrDb db)
{
    var users = await userDb.Users.ToListAsync();
    var assignments = await db.Tasks
        .Where(task => task.AssignedToID != 0)
        .Select(task => new { task.AssignedToID, task.Id })
        .ToListAsync();

    var tasksByUser = assignments
        .GroupBy(a => a.AssignedToID)
        .ToDictionary(g => g.Key, g => g.Select(a => a.Id).ToList());

    return TypedResults.Ok(users.Select(user => new
    {
        user.Id,
        user.Username,
        Tasks = tasksByUser.GetValueOrDefault(user.Id) ?? []
    }));
}

// GET api/tasks/complete - Get all completed tasks
static async Task<IResult> GetCompleteTasks(SimpleTaskrDb db)
{
    return TypedResults.Ok(await db.Tasks.Where(t => t.IsComplete).ToListAsync());
}

// GET api/tasks/{id} - Get a specific task by ID
static async Task<IResult> GetTask(int id, SimpleTaskrDb db)
{
    var task = await db.Tasks.FindAsync(id);
    return task is not null ? TypedResults.Ok(task) : TypedResults.NotFound();
}

// GET api/users/{id} - Get a specific user by ID
static async Task<IResult> GetUser(int id, UserDb userDb, SimpleTaskrDb db)
{
    var user = await userDb.Users.FindAsync(id);
    if (user is null)
        return TypedResults.NotFound();

    var taskIds = await db.Tasks
        .Where(task => task.AssignedToID == user.Id)
        .Select(task => task.Id)
        .ToListAsync();

    return TypedResults.Ok(new
    {
        User = new { user.Id, user.Username },
        Tasks = taskIds
    });
}

// POST api/tasks - Create a new task
static async Task<IResult> CreateTask(SimpleTaskr task, SimpleTaskrDb db)
{
    db.Tasks.Add(task);
    await db.SaveChangesAsync();

    return TypedResults.Created($"/tasks/{task.Id}", task);
}


// PUT /tasks/{id}/assign - Assign an existing task to a user
static async Task<IResult> AssignTask(int id, AssignTaskRequest request, SimpleTaskrDb db, UserDb userDb)
{
    var task = await db.Tasks.FindAsync(id);
    if (task is null) return TypedResults.NotFound("Task not found");

    var user = await userDb.Users.FindAsync(request.AssignedToID);
    if (user is null) return TypedResults.NotFound("User not found");

    task.AssignedToID = user.Id;
    await db.SaveChangesAsync();

    return TypedResults.NoContent();
}

// DELETE api/tasks/{id} - Delete a specific task
static async Task<IResult> DeleteTask(int id, SimpleTaskrDb db)
{
    if (await db.Tasks.FindAsync(id) is SimpleTaskr task)
    {
        db.Tasks.Remove(task);
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    return TypedResults.NotFound();
}

// PATCH api/tasks/{id} - Partially update a specific task
static async Task<IResult> PatchTask(int id, SimpleTaskrPatchDto inputTask, SimpleTaskrDb db)
{
    var task = await db.Tasks.FindAsync(id);

    if (task is null) return TypedResults.NotFound();

    if (inputTask.Name is not null) task.Name = inputTask.Name;
    if (inputTask.IsComplete is not null) task.IsComplete = inputTask.IsComplete.Value;
    if (inputTask.Details is not null) task.Details = inputTask.Details;

    await db.SaveChangesAsync();

    return TypedResults.NoContent();
}


app.Run();
