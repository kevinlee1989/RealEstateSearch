using System.Reflection;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using RealEstateSearch.Data.Search;

namespace RealEstateSearch.Indexer.Elasticsearch;

// Makes sure the "listings" alias points to an index built from listings-index.json.
// Safe to run on every startup: it only creates what is missing.
public class ListingIndexManager(ElasticsearchClient client, ILogger<ListingIndexManager> logger)
{
    private const string AliasName = ListingDocument.IndexAlias;

    private const string InitialIndexName = "listings_v1";
    private const string MappingResourceName =
        "RealEstateSearch.Indexer.Elasticsearch.listings-index.json";

    // wait for maximum 2 seconds
    private static readonly TimeSpan MaxWaitForElasticsearch = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(16);

    public async Task EnsureIndexAsync(CancellationToken cancellationToken)
    {
        // wait for ES to activate 
        await WaitForElasticsearchAsync(cancellationToken);

        if (await AliasExistsAsync(cancellationToken))
        {
            logger.LogInformation("Alias {Alias} already exists, nothing to create", AliasName);
            return;
        }

        // Index without alias = an earlier run stopped between the two steps; finish the job
        if (await IndexExistsAsync(InitialIndexName, cancellationToken))
        {
            logger.LogInformation("Index {Index} already exists, reusing it", InitialIndexName);
        }
        else
        {
            await CreateIndexAsync(InitialIndexName, cancellationToken);
        }

        await AttachAliasAsync(InitialIndexName, cancellationToken);
    }

    // ES takes 20-40s to boot and the worker may start first, so retry with exponential backoff
    private async Task WaitForElasticsearchAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + MaxWaitForElasticsearch;
        var delay = TimeSpan.FromSeconds(1);

        while (true)
        {
            var ping = await client.PingAsync(cancellationToken);
            if (ping.IsValidResponse)
            {
                logger.LogInformation("Connected to Elasticsearch");
                return;
            }

            if (DateTime.UtcNow + delay > deadline)
            {
                throw new InvalidOperationException(
                    $"Elasticsearch did not respond within {MaxWaitForElasticsearch.TotalSeconds}s");
            }

            logger.LogWarning(
                "Elasticsearch is not ready, retrying in {Delay}s", delay.TotalSeconds);
            await Task.Delay(delay, cancellationToken);

            delay = TimeSpan.FromSeconds(
                Math.Min(delay.TotalSeconds * 2, MaxRetryDelay.TotalSeconds));
        }
    }

    // Does the "listings" alias exist in ES?
    private async Task<bool> AliasExistsAsync(CancellationToken cancellationToken)
    {
        var response = await client.Indices.ExistsAliasAsync(AliasName, cancellationToken);
        return ToExists(response, $"check alias {AliasName}");
    }

    private async Task<bool> IndexExistsAsync(string indexName, CancellationToken cancellationToken)
    {
        var response = await client.Indices.ExistsAsync(indexName, cancellationToken);
        return ToExists(response, $"check index {indexName}");
    }

    // Sends the JSON file as-is so the code creates exactly what was tested in Kibana
    private async Task CreateIndexAsync(string indexName, CancellationToken cancellationToken)
    {
        // Get listings-index.json
        var body = await ReadMappingAsync();

        // PUT/ listings_v1
        var response = await client.Transport.RequestAsync<StringResponse>(
            Elastic.Transport.HttpMethod.PUT,
            $"/{indexName}",
            PostData.String(body),
            cancellationToken);

        if (!response.ApiCallDetails.HasSuccessfulStatusCode)
        {
            throw new InvalidOperationException(
                $"Failed to create index {indexName}: {response.Body}");
        }

        logger.LogInformation("Created index {Index}", indexName);
    }

    private async Task AttachAliasAsync(string indexName, CancellationToken cancellationToken)
    {
        var response = await client.Indices.PutAliasAsync(indexName, AliasName, cancellationToken);

        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException(
                $"Failed to attach alias {AliasName} to {indexName}: {response.DebugInformation}");
        }

        logger.LogInformation("Attached alias {Alias} to {Index}", AliasName, indexName);
    }

    // Reads listings-index.json, which is embedded in the DLL at build time,
    // so it works the same wherever the worker runs
    private static async Task<string> ReadMappingAsync()
    {
        await using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(MappingResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource {MappingResourceName} not found");

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    // Exists APIs answer 200 or 404; anything else (e.g. connection error) is a real failure
    private static bool ToExists(TransportResponse response, string action)
    {
        return response.ApiCallDetails.HttpStatusCode switch
        {
            200 => true,
            404 => false,
            _ => throw new InvalidOperationException(
                $"Failed to {action}: {response.ApiCallDetails.DebugInformation}")
        };
    }
}
