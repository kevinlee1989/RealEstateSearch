using Elastic.Clients.Elasticsearch;
using Microsoft.EntityFrameworkCore;
using RealEstateSearch.Data;
using RealEstateSearch.Indexer;
using RealEstateSearch.Indexer.Elasticsearch;
using RealEstateSearch.Indexer.Sync;

var builder = Host.CreateApplicationBuilder(args);

// Scoped: ListingSyncer opens a short-lived scope per batch to get one
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// One client for the whole app: it is thread-safe and owns the HTTP connection pool
builder.Services.AddSingleton(_ =>
{
    var url = builder.Configuration["Elasticsearch:Url"]
        ?? throw new InvalidOperationException("Missing configuration: Elasticsearch:Url");

    return new ElasticsearchClient(new ElasticsearchClientSettings(new Uri(url)));
});

builder.Services.Configure<SyncOptions>(builder.Configuration.GetSection("Sync"));

builder.Services.AddSingleton<ListingIndexManager>();
builder.Services.AddSingleton<ListingSyncer>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
