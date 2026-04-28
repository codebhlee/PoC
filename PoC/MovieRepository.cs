using Npgsql;

public class MovieRepository(string connStr) : IMovieRepository
{
    public async IAsyncEnumerable<MovieRecord> StreamAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync(ct);

        using var cmd = new NpgsqlCommand("SELECT movie_id, title, genres FROM movies", conn);
        using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            ct.ThrowIfCancellationRequested();

            var movieId = reader.GetInt32(0);
            var title   = reader.GetString(1);
            var genres  = reader.IsDBNull(2) ? "" : reader.GetString(2);

            yield return new MovieRecord(movieId, title, genres);
        }
    }
}
