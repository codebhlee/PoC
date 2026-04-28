public interface IMovieRepository
{
    IAsyncEnumerable<MovieRecord> StreamAllAsync(CancellationToken ct = default);
}

public record MovieRecord(int Id, string Title, string Genres);
