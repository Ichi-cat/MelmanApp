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
    var actions = new List<GameAction>();
    var money = model.PlayerTower.Resources;

    if(money >= 50 && model.PlayerTower.Level == 1)
    {
        actions.Add(new GameAction
        {
            Type = "upgrade"
        });
        money -= 50;
    }

    if (money >= 88 && model.PlayerTower.Level == 2)
    {
        actions.Add(new GameAction
        {
            Type = "upgrade"
        });

        money -= 88;
    }

    if (money >= 153 && model.PlayerTower.Level == 3)
    {
        actions.Add(new GameAction
        {
            Type = "upgrade"
        });

        money -= 153;
    }

    if (money >= 268 && model.PlayerTower.Level == 4)
    {
        actions.Add(new GameAction
        {
            Type = "upgrade"
        });

        money -= 268;
    }

    if (money >= 469 && model.PlayerTower.Level == 5)
    {
        actions.Add(new GameAction
        {
            Type = "upgrade"
        });

        money -= 469;
    }

    if (money > 0 && model.Turn > 2)
    {
        actions.Add(new GameAction
        {
            Type = "armor",
            Amount = money / 3
        });
    }

    //var actions = new List<GameAction>
    //{
    //    new GameAction
    //    {
    //        Type = "armor",
    //        Amount = 5
    //    },
    //    new GameAction
    //    {
    //        Type = "attack",
    //        TargetId = 102,
    //        TroopCount = 20
    //    }
    //};

    return Results.Ok(actions);
});

app.Run();
