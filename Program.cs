using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpClient();
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse("peaceful-parrot-30114.upstash.io:6379");
    configuration.Password = "AXWiAAIjcDE0MWRmOTliYzEyYjE0YjEyOGIwNTFiNzY4NTY1YmQ4OXAxMA";
    configuration.Ssl = true;
    configuration.AbortOnConnectFail = false;

    return ConnectionMultiplexer.Connect(configuration);
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || true)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
