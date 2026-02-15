using Microsoft.EntityFrameworkCore;
using ZgjedhjetApi.Data;
using System.Text.Json.Serialization;
using Nest;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<LifeDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("LifeDatabase")));

var esUriString = builder.Configuration["ElasticsearchSettings:Uri"] ?? "http://localhost:9200";
var esDefaultIndex = builder.Configuration["ElasticsearchSettings:DefaultIndex"] ?? "zgjedhjet";

var esSettings = new ConnectionSettings(new Uri(esUriString))
                    .DefaultIndex(esDefaultIndex);

builder.Services.AddSingleton<IElasticClient>(new ElasticClient(esSettings));

var redisConnection = builder.Configuration["RedisSettings:ConnectionString"] ?? "localhost:6379,abortConnect=false";
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnection));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
