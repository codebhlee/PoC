using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Qdrant.Client;
using SmartComponents.LocalEmbeddings;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();

var pgConnStr           = config.GetConnectionString("Postgres")!;
var qdrantHost          = config["Qdrant:Host"]!;
var qdrantPort          = int.Parse(config["Qdrant:Port"]!);
var collectionName      = config["Qdrant:CollectionName"]!;
var embedBatchSize      = int.Parse(config["Pipeline:EmbedBatchSize"]!);
var qdrantBatchSize     = int.Parse(config["Pipeline:QdrantBatchSize"]!);
var geminiModel         = config["Gemini:Model"]!;
var similarityThreshold = float.Parse(config["Gemini:SimilarityThreshold"]!);
var geminiApiKeyEnvVar  = config["Gemini:ApiKey"]!;
var geminiApiKey        = Environment.GetEnvironmentVariable(geminiApiKeyEnvVar)
                          ?? throw new InvalidOperationException($"Environment variable '{geminiApiKeyEnvVar}' is not set.");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("\nCancellation requested...");
    cts.Cancel();
};

var ct = cts.Token;
var sw = Stopwatch.StartNew();

using var embedder = new LocalEmbedder();

IMovieRepository movieRepo = new MovieRepository(pgConnStr);
IVectorRepository vectorRepo = new QdrantVectorRepository(new QdrantClient(qdrantHost, qdrantPort), collectionName);
var advisor = new GeminiMovieAdvisor(geminiApiKey, geminiModel);

try
{
    await vectorRepo.InitCollectionAsync(ct);
    Console.WriteLine($"Collection '{collectionName}' created");

    var channel = Channel.CreateBounded<List<VectorRecord>>(new BoundedChannelOptions(4)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = true
    });

    var producer = new EmbeddingProducer(embedder, movieRepo, embedBatchSize);
    var consumer = new VectorRepositoryConsumer(vectorRepo, qdrantBatchSize);

    await Task.WhenAll(
        Task.Run(() => producer.RunAsync(channel.Writer, ct), ct),
        Task.Run(() => consumer.RunAsync(channel.Reader, sw, ct), ct)
    );

    Console.WriteLine($"\nTotal elapsed: {sw.Elapsed:mm\\:ss\\.ff}");

    // Search + Generation
    var search = new MovieSearchConsole(embedder, vectorRepo, advisor, similarityThreshold);
    await search.RunAsync(ct);
}
catch (OperationCanceledException)
{
    Console.WriteLine($"Cancelled ({sw.Elapsed:mm\\:ss\\.ff})");
}
