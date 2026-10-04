using Elastic.Clients.Elasticsearch;
using RealEstateSearch.Indexer;
using RealEstateSearch.Indexer.Elasticsearch;

var builder = Host.CreateApplicationBuilder(args);

// One client for the whole app: it is thread-safe and owns the HTTP connection pool
builder.Services.AddSingleton(_ =>
{
    var url = builder.Configuration["Elasticsearch:Url"]
        ?? throw new InvalidOperationException("Missing configuration: Elasticsearch:Url");

    return new ElasticsearchClient(new ElasticsearchClientSettings(new Uri(url)));
});

builder.Services.AddSingleton<ListingIndexManager>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
