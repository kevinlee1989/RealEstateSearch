using Microsoft.EntityFrameworkCore;
// For My AppDbContext
using RealEstateSearch.Api.Data;
// For parsing the data and store to DB
using RealEstateSearch.Api.SeedData;

// building ASP.NET Core app -> reading appsettings, env variables
var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddScoped<CsvListingImporter>();

// db connection 
builder.Services.AddDbContext<AppDbContext>(options =>
    // use PostgreSQL for AppDbContext
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// building temporary DI scope
using (var scope = app.Services.CreateScope())
{
    var importer =
        scope.ServiceProvider.GetRequiredService<CsvListingImporter>();

    var csvPath = Path.Combine(
        builder.Environment.ContentRootPath,
        "Data",
        "listings.csv"
    );

    await importer.ImportAsync(csvPath);
}


app.MapControllers();

app.Run();