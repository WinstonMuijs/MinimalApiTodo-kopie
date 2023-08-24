

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration["ConnectionStrings:DefaultConnection"];
builder.Services.AddDbContext<ApiDbContext>(options =>
    options.UseSqlServer(connectionString));

var securityScheme = new OpenApiSecurityScheme()
{
    Name =  "Authorisation",
    Type = SecuritySchemeType.Http,
    Scheme = "bearer",
    BearerFormat = "JWT",
    In = ParameterLocation.Header,
    Description = "JWT Authentication for MinimalApi"
};

var securityRequirements = new OpenApiSecurityRequirement()
{
    {
        new OpenApiSecurityScheme
        {
           Reference = new OpenApiReference
           {
                Type = ReferenceType.SecurityScheme,
                Id = "bearer"
           }
        },
        new string[] {}
    }
};

var contactInfo = new OpenApiContact()
{
    Name = "Winston",
    Email = "whmuijs@gmail.com",
    Url = new Uri("https://databrein.online")
};

var license = new OpenApiLicense()
{
    Name = "Free License",
};

var info = new OpenApiInfo()
{
    Version = "v1",
    Title = "ToDo list with JWT Authentication",
    Description = "ToDo list with JWT Authentication",
    Contact = contactInfo,
    License = license,

};


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", info);
    options.AddSecurityDefinition("bearer", securityScheme);
    options.AddSecurityRequirement(securityRequirements);
 });

// jwt Authentication

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{ // validatie
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateAudience = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
        ValidateLifetime = false, // if not demo: true
        ValidateIssuerSigningKey = true, 
    };
});

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

var app = builder.Build();



// Configure the HTTP request 



app.MapGet("/items", [Authorize] async (ApiDbContext db) =>
{
    var items = await db.Items.ToListAsync();
    return items;
});

//app.MapPost("/items", [Authorize] async (ApiDbContext db, Item item) =>
//{
//    if (await db.Items.FirstOrDefaultAsync(x => x.Id == item.Id) != null)
//    {
//        return Results.BadRequest();
//    }

//    db.Items.Add(item);
//    await db.SaveChangesAsync();
//    return Results.Created($"/Items/{item.Id}", item);

//});

app.MapPost("/items", [Authorize] async (ApiDbContext db, Item item) =>
{
    // Controleer of er al een item bestaat met dezelfde Id
    var existingItem = await db.Items.FirstOrDefaultAsync(x => x.Id == item.Id);
    if (existingItem != null)
    {
        return Results.BadRequest("Item with the same Id already exists.");
    }

    db.Items.Add(item);
    await db.SaveChangesAsync();
    return Results.Created($"/Items/{item.Id}", item);
});


app.MapGet("/items/{id}",  async (ApiDbContext db, int id) =>
{
    var item = await db.Items.FirstOrDefaultAsync(x => x.Id == id);

    return item == null ? Results.NotFound() : Results.Ok(item);
});


app.MapPut("/items/{id}", async (ApiDbContext db, int id, Item item) =>
{
    var existItem = await db.Items.FirstOrDefaultAsync(x => x.Id == id);
    if ( existItem == null)
    {
        return Results.BadRequest();
    }

    existItem.Title = item.Title;
    existItem.IsCompleted = item.IsCompleted;

    await db.SaveChangesAsync();
    return Results.Ok(item);
});

app.MapDelete("/items/{id}", [Authorize] async (ApiDbContext db, int id) =>
{
    var existItem = await db.Items.FirstOrDefaultAsync(x => x.Id == id);
    if (existItem == null)
    {
        return Results.BadRequest();
    }

    db.Items.Remove(existItem);
    await db.SaveChangesAsync();
    return Results.NoContent();

});

// Controller voor het afhandelen van de token, checken tegen de Db.
// Komt overeen dan token, anders niet.

app.MapPost("/accounts/login", [AllowAnonymous] (UserDto user) => {
    if(user.username == "Winston" && user.password == "123")
    {
        // genereer token
        var secureKey = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]);

        var issuer = builder.Configuration["Jwt:Issuer"];
        var audience = builder.Configuration["Jwt:Audience"];
        var securityKey = new SymmetricSecurityKey(secureKey);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha512);

        var jwtTokenHandler = new JwtSecurityTokenHandler();

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new [] {
                new Claim("Id","1"),
                new Claim(JwtRegisteredClaimNames.Sub, user.username),
                new Claim(JwtRegisteredClaimNames.Email, user.username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            }),
            Expires = DateTime.Now.AddMinutes(5),
            Audience = audience,
            Issuer = issuer,
            SigningCredentials = credentials
        };

        var token = jwtTokenHandler.CreateToken(tokenDescriptor);
        var jwtToken = jwtTokenHandler.WriteToken(token);
        return Results.Ok(jwtToken);
        
    }
    return Results.Unauthorized();
});

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello World from Minimal API!");

app.Run();


//record Item(int id, string title, bool IsCompleted);

record UserDto(string username, string password);

class Item
{
    public int Id { get; set; }
    public string Title { get; set; }
    public bool IsCompleted { get; set; }
}

//class ItemRespository
//{
//    private Dictionary<int, Item> items = new Dictionary<int, Item>();

//    public ItemRespository()
//    {
//        var item1 = new Item(1, "Go to the Gym", false);
//        var item2 = new Item(2, "Drink water", true);
//        var item3 = new Item(3, "Watch tv", false);

//        items.Add(item1.id, item1);
//        items.Add(item2.id, item2);
//        items.Add(item3.id, item3);
//    }

//    public IEnumerable<Item> GetAll() => items.Values;

//    public Item GetById(int id) {
//        if (items.ContainsKey(id))
//        {
//            return items[id];
//        }
//        return null;
//    }

//    public void Add(Item item) => items.Add(item.id, item);

//    public void Update(Item item) => items[item.id] = item;

//    public void Delete(int id) => items.Remove(id);
//}

class ApiDbContext : DbContext
{
    public DbSet<Item> Items { get; set; }

    public ApiDbContext(DbContextOptions<ApiDbContext> options) : base(options) { }

}