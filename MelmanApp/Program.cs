using MelmanApp;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();


app.MapGet("/healthz", () =>
{
    return new { status = "OK" };
});
app.MapPost("/combat", (GameRequest model) =>
{

    var actions = new List<GameAction>
    {
        new GameAction
        {
            Type = "armor",
            Amount = 5
        },
        new GameAction
        {
            Type = "attack",
            TargetId = 102,
            TroopCount = 20
        },
        new GameAction
        {
            Type = "upgrade"
        }
    };

    return Results.Ok(actions);
});

app.Run();
