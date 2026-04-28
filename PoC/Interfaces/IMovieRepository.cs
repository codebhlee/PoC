public interface IMovieRepository
{
    IAsyncEnumerable<MovieRecord> StreamAllAsync(CancellationToken ct = default);
}
