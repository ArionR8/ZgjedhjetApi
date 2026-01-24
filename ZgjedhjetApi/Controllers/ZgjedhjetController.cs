using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using ZgjedhjetApi.Data;
using ZgjedhjetApi.Enums;
using ZgjedhjetApi.Models.DTOs;
using ZgjedhjetApi.Models.Entities;

namespace ZgjedhjetApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ZgjedhjetController : ControllerBase
    {
        private readonly ILogger<ZgjedhjetController> _logger;
        private readonly LifeDbContext _context;

        public ZgjedhjetController(ILogger<ZgjedhjetController> logger, LifeDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        /// <summary>
        /// POST endpoint to import CSV file
        /// </summary>
        [HttpPost("import")]
        public async Task<ActionResult<CsvImportResponse>> MigrateData(IFormFile file)
        {
            var response = new CsvImportResponse();

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                if (file == null || file.Length == 0)
                {
                    response.Success = false;
                    response.Message = "No file uploaded";
                    response.Errors.Add("CSV file is required");
                    return BadRequest(response);
                }

                if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    response.Success = false;
                    response.Message = "Invalid file format";
                    response.Errors.Add("Only CSV files are allowed");
                    return BadRequest(response);
                }

                var records = new List<Zgjedhjet>();
                var lineNumber = 1;

                using var reader = new StreamReader(file.OpenReadStream());

                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Delimiter = ",",
                    HasHeaderRecord = true,
                    TrimOptions = TrimOptions.Trim,
                    BadDataFound = null,
                    MissingFieldFound = null
                };

                using var csv = new CsvReader(reader, config);

                await csv.ReadAsync();
                csv.ReadHeader();
                lineNumber++;

                while (await csv.ReadAsync())
                {
                    lineNumber++;
                    try
                    {
                        var entity = new Zgjedhjet
                        {
                            Kategoria = csv.GetField<Kategoria>("Kategoria"),
                            Komuna = csv.GetField<Komuna>("Komuna"),
                            Qendra_e_votimit = csv.GetField("Qendra_e_votimit")?.Trim() ?? string.Empty,
                            Vendvotimi = csv.GetField("Vendvotimi")?.Trim() ?? string.Empty,
                            Partia111 = csv.GetField<int>("Partia111"),
                            Partia112 = csv.GetField<int>("Partia112"),
                            Partia113 = csv.GetField<int>("Partia113"),
                            Partia114 = csv.GetField<int>("Partia114"),
                            Partia115 = csv.GetField<int>("Partia115"),
                            Partia116 = csv.GetField<int>("Partia116"),
                            Partia117 = csv.GetField<int>("Partia117"),
                            Partia118 = csv.GetField<int>("Partia118"),
                            Partia119 = csv.GetField<int>("Partia119"),
                            Partia120 = csv.GetField<int>("Partia120"),
                            Partia121 = csv.GetField<int>("Partia121"),
                            Partia122 = csv.GetField<int>("Partia122"),
                            Partia123 = csv.GetField<int>("Partia123"),
                            Partia124 = csv.GetField<int>("Partia124"),
                            Partia125 = csv.GetField<int>("Partia125"),
                            Partia126 = csv.GetField<int>("Partia126"),
                            Partia127 = csv.GetField<int>("Partia127"),
                            Partia128 = csv.GetField<int>("Partia128"),
                            Partia129 = csv.GetField<int>("Partia129"),
                            Partia130 = csv.GetField<int>("Partia130"),
                            Partia131 = csv.GetField<int>("Partia131"),
                            Partia132 = csv.GetField<int>("Partia132"),
                            Partia133 = csv.GetField<int>("Partia133"),
                            Partia134 = csv.GetField<int>("Partia134"),
                            Partia135 = csv.GetField<int>("Partia135"),
                            Partia136 = csv.GetField<int>("Partia136"),
                            Partia137 = csv.GetField<int>("Partia137"),
                            Partia138 = csv.GetField<int>("Partia138")
                        };

                        records.Add(entity);
                    }
                    catch (Exception ex)
                    {
                        response.Errors.Add($"Line {lineNumber}: {ex.Message}");
                        _logger.LogWarning($"Error parsing line {lineNumber}: {ex.Message}");
                    }
                }

                if (records.Count == 0)
                {
                    response.Success = false;
                    response.Message = "No valid records to import";
                    return BadRequest(response);
                }

                await _context.Zgjedhjet.ExecuteDeleteAsync();
                await _context.Zgjedhjet.AddRangeAsync(records);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                response.Success = true;
                response.Message = $"Successfully imported {records.Count} records";
                response.RecordsImported = records.Count;

                _logger.LogInformation($"Successfully imported {records.Count} records from CSV");

                return Ok(response);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                _logger.LogError(ex, "Error importing CSV file");
                response.Success = false;
                response.Message = "Error during CSV import";
                response.Errors.Add(ex.Message);

                return StatusCode(500, response);
            }
        }

        /// <summary>
        /// GET endpoint to retrieve and filter electoral data
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ZgjedhjetAggregatedResponse>> GetZgjedhjet(
          [FromQuery] Kategoria? kategoria = null,
          [FromQuery] Komuna? komuna = null,
          [FromQuery] string? qendra_e_votimit = null,
          [FromQuery] string? vendvotimi = null,
          [FromQuery] Partia? partia = null)
        {
            try
            {
                var query = _context.Zgjedhjet.AsQueryable();

                if (kategoria.HasValue && kategoria != Kategoria.TeGjitha)
                {
                    query = query.Where(x => x.Kategoria == kategoria);
                }

                if (komuna.HasValue && komuna != Komuna.TeGjitha)
                {
                    query = query.Where(x => x.Komuna == komuna);
                }

                if (!string.IsNullOrWhiteSpace(qendra_e_votimit))
                {
                    var exists = await _context.Zgjedhjet
                        .AnyAsync(x => x.Qendra_e_votimit == qendra_e_votimit);

                    if (!exists)
                        return NotFound(new { message = "Qendra_e_Votimit nuk ekziston" });

                    query = query.Where(x => x.Qendra_e_votimit == qendra_e_votimit);
                }

                if (!string.IsNullOrWhiteSpace(vendvotimi))
                {
                    var exists = await _context.Zgjedhjet
                        .AnyAsync(x => x.Vendvotimi == vendvotimi);

                    if (!exists)
                        return NotFound(new { message = "Vendvotimi nuk ekziston" });

                    query = query.Where(x => x.Vendvotimi == vendvotimi);
                }

                var data = await query.ToListAsync();
                var response = new ZgjedhjetAggregatedResponse();

                var partiaProperties = typeof(Zgjedhjet)
                    .GetProperties()
                    .Where(p => p.Name.StartsWith("Partia"))
                    .ToList();

                foreach (var prop in partiaProperties)
                {
                    if (partia.HasValue && partia != Partia.TeGjitha && prop.Name != partia.ToString())
                        continue;

                    int totalVotes = data.Sum(x => (int)prop.GetValue(x)!);

                    response.Results.Add(new PartiaVotesResponse
                    {
                        Partia = prop.Name,
                        TotalVota = totalVotes
                    });
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving electoral data");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }
}
