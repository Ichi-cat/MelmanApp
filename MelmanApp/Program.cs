using MelmanApp;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

// Register AI Bot as Singleton (persists across requests)
builder.Services.AddSingleton<AIGameBot>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

void LogRequest(string endpoint)
{
    Console.WriteLine("[KW-BOT] Mega ogudor");
}

app.MapGet("/healthz", () =>
{
    LogRequest("/healthz");
    return new { status = "OK" };
});

app.MapGet("/info", () =>
{
    LogRequest("/info");
    return new
    {
        name = "Mega ogudor",
        strategy = "AI-trapped-strategy",
        version = "1.0"
    };
});

app.MapPost("/negotiate", (GameRequest model, AIGameBot bot) =>
{
    LogRequest("/negotiate");
    var response = bot.Negotiate(model);
    return Results.Ok(response);
});

app.MapPost("/combat", (GameRequest model, AIGameBot bot) =>
{
    LogRequest("/combat");
    var actions = bot.Combat(model);
    return Results.Ok(actions);
});

// Debug endpoint to view AI learning stats
app.MapGet("/ai/stats", (AIGameBot bot) =>
{
    return Results.Ok(bot.GetStats());
});

// Debug endpoint to reset AI learning
app.MapPost("/ai/reset", (AIGameBot bot) =>
{
    bot.Reset();
    return Results.Ok(new { message = "AI brain reset successful" });
});

app.Run();
