using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nest;
using StackExchange.Redis;
using ZgjedhjetApi.Data;
using ZgjedhjetApi.Models.Entities;
using ZgjedhjetApi.Enums;
using ZgjedhjetApi.Models.DTOs;

namespace ZgjedhjetApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ZgjedhjetElasticSearchController : ControllerBase
    {
        private readonly LifeDbContext _context;
        private readonly IElasticClient _elasticClient;
        private readonly IConnectionMultiplexer _redis;

        public ZgjedhjetElasticSearchController(
            LifeDbContext context,
            IElasticClient elasticClient,
            IConnectionMultiplexer redis)
        {
            _context = context;
            _elasticClient = elasticClient;
            _redis = redis;
        }

        [HttpPost("import")]
        public async Task<IActionResult> Import()
        {
            var exists = await _elasticClient.Indices.ExistsAsync("zgjedhjet");

            if (!exists.Exists)
            {
                var createIndex = await _elasticClient.Indices.CreateAsync("zgjedhjet", c => c
                    .Settings(s => s
                        .Analysis(a => a
                            .Analyzers(an => an
                                .Custom("custom_analyzer", ca => ca
                                    .Tokenizer("standard")
                                    .Filters("lowercase", "asciifolding")
                                )
                            )
                        )
                    )
                    .Map<Zgjedhjet>(m => m
                        .Properties(ps => ps
                            .Text(t => t
                                .Name(n => n.Komuna)
                                .Analyzer("custom_analyzer")
                                .Fields(f => f
                                    .Keyword(k => k.Name("keyword"))
                                )
                            )
                        )
                    )
                );

                if (!createIndex.IsValid)
                    return StatusCode(500, createIndex.ServerError);
            }

            var data = await _context.Zgjedhjet.ToListAsync();

            var response = await _elasticClient.BulkAsync(b => b
                .Index("zgjedhjet")
                .IndexMany(data));

            if (!response.IsValid)
                return StatusCode(500, response.ServerError);

            return Ok(new { message = $"Imported {data.Count} records successfully" });
        }

        [HttpGet("filter")]
        public async Task<IActionResult> Filter(
            [FromQuery] Kategoria? kategoria = null,
            [FromQuery] Komuna? komuna = null,
            [FromQuery] string? qendra_e_votimit = null,
            [FromQuery] string? vendvotimi = null,
            [FromQuery] Partia? partia = null)
        {
            var response = await _elasticClient.SearchAsync<Zgjedhjet>(s => s
                .Index("zgjedhjet")
                .Size(1000)
                .Query(q =>
                    q.Bool(b => b
                        .Must(
                            kategoria.HasValue && kategoria != Kategoria.TeGjitha
                                ? q.Term(t => t.Field(f => f.Kategoria).Value(kategoria))
                                : null,
                            komuna.HasValue && komuna != Komuna.TeGjitha
                                ? q.Term(t => t.Field(f => f.Komuna.Suffix("keyword")).Value(komuna.ToString()))
                                : null,
                            !string.IsNullOrWhiteSpace(qendra_e_votimit)
                                ? q.Term(t => t.Field(f => f.Qendra_e_votimit.Suffix("keyword")).Value(qendra_e_votimit))
                                : null,
                            !string.IsNullOrWhiteSpace(vendvotimi)
                                ? q.Term(t => t.Field(f => f.Vendvotimi.Suffix("keyword")).Value(vendvotimi))
                                : null
                        )
                    )
                )
            );

            if (!response.IsValid)
                return StatusCode(500, response.ServerError);

            var data = response.Documents.ToList();
            var result = new ZgjedhjetAggregatedResponse();

            var partiaProperties = typeof(Zgjedhjet)
                .GetProperties()
                .Where(p => p.Name.StartsWith("Partia"))
                .ToList();

            foreach (var prop in partiaProperties)
            {
                if (partia.HasValue && partia != Partia.TeGjitha && prop.Name != partia.ToString())
                    continue;

                int totalVotes = data.Sum(x => (int)prop.GetValue(x)!);

                result.Results.Add(new PartiaVotesResponse
                {
                    Partia = prop.Name,
                    TotalVota = totalVotes
                });
            }

            return Ok(result);
        }

        [HttpGet("suggest")]
        public async Task<IActionResult> Suggest([FromQuery] string query)
        {
            var response = await _elasticClient.SearchAsync<Zgjedhjet>(s => s
                .Index("zgjedhjet")
                .Size(10)
                .Query(q => q.MatchPhrasePrefix(m => m
                    .Field(f => f.Komuna)
                    .Query(query)))
            );

            if (!response.IsValid)
                return StatusCode(500, response.ServerError);

            var suggestions = response.Documents
                .Select(d => d.Komuna.ToString())
                .Distinct()
                .ToList();

            var db = _redis.GetDatabase();
            foreach (var komuna in suggestions)
                db.HashIncrement("suggestions", komuna);

            return Ok(suggestions);
        }

        [HttpGet("stats")]
        public IActionResult Stats([FromQuery] int top = 5)
        {
            var db = _redis.GetDatabase();
            var entries = db.HashGetAll("suggestions")
                .OrderByDescending(e => (int)e.Value)
                .Take(top)
                .Select(e => new
                {
                    komuna = e.Name.ToString(),
                    nrISugjerimeve = (int)e.Value
                });

            return Ok(entries);
        }
    }
}
