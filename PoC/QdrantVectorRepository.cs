using Qdrant.Client;
using Qdrant.Client.Grpc;

public class QdrantVectorRepository(QdrantClient client, string collectionName, int vectorSize = 384) : IVectorRepository
{
    public async Task InitCollectionAsync(CancellationToken ct = default)
    {
        try
        {
            await client.DeleteCollectionAsync(collectionName, cancellationToken: ct);
        }
        catch { }
        await client.CreateCollectionAsync(collectionName, new VectorParams
        {
            Size = (ulong)vectorSize,
            Distance = Distance.Cosine
        }, cancellationToken: ct);
    }

    public async Task UpsertAsync(IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        var points = records.Select(r => new PointStruct
        {
            Id = r.Id,
            Vectors = r.Vector,
            Payload = { ["title"] = r.Title, ["genres"] = r.Genres }
        }).ToList();

        await client.UpsertAsync(collectionName, points, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(float[] vector, int limit, float minScore = 0f, CancellationToken ct = default)
    {
        var results = await client.SearchAsync(collectionName, vector, limit: (ulong)limit,
            scoreThreshold: minScore, cancellationToken: ct);
        return results.Select(r => new VectorSearchResult(
            r.Id.Num,
            r.Score,
            r.Payload["title"].StringValue
        )).ToList();
    }
}
