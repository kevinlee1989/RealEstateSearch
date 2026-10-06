using Elastic.Clients.Elasticsearch;
using Microsoft.EntityFrameworkCore;
using RealEstateSearch.Api.Services;
using RealEstateSearch.Api.SeedData;
using RealEstateSearch.Data;

// Reads appsettings.json and environment variables
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Unhandled errors (e.g. Elasticsearch down) become a JSON ProblemDetails 500
// instead of a stack trace
builder.Services.AddProblemDetails();

// PostgreSQL: only used by the CSV importer below
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<CsvListingImporter>();

// Elasticsearch: one thread-safe client for the whole app, same as in the Indexer
builder.Services.AddSingleton(_ =>
{
    var url = builder.Configuration["Elasticsearch:Url"]
        ?? throw new InvalidOperationException("Missing configuration: Elasticsearch:Url");

    return new ElasticsearchClient(new ElasticsearchClientSettings(new Uri(url)));
});
builder.Services.AddSingleton<ListingSearchService>();

var app = builder.Build();

app.UseExceptionHandler();

// Seed PostgreSQL from the CSV on startup (skipped when data already exists).
// Kept here for the MVP; with several API instances this should become a separate step.
using (var scope = app.Services.CreateScope())
{
    var importer =
        scope.ServiceProvider.GetRequiredService<CsvListingImporter>();

    var csvPath = Path.Combine(
        builder.Environment.ContentRootPath,
        "SeedData",
        "listings.csv"
    );

    await importer.ImportAsync(csvPath);
}


app.MapControllers();

app.Run();