public interface IVectorRepository
{
    Task InitCollectionAsync(CancellationToken ct = default);
    Task UpsertAsync(IReadOnlyList<VectorRecord> records, CancellationToken ct = default);
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(float[] vector, int limit, float minScore = 0f, CancellationToken ct = default);
}
