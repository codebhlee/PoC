using System.Threading.Channels;
using SmartComponents.LocalEmbeddings;

public class EmbeddingProducer(LocalEmbedder embedder, IMovieRepository repo, int batchSize)
{
    public async Task RunAsync(ChannelWriter<List<VectorRecord>> writer, CancellationToken ct)
    {
        var buffer = new List<MovieRecord>(batchSize);

        await foreach (var movie in repo.StreamAllAsync(ct))
        {
            buffer.Add(movie);

            if (buffer.Count >= batchSize)
            {
                await writer.WriteAsync(Embed(buffer), ct);
                buffer.Clear();
            }
        }

        if (buffer.Count > 0)
        {
            await writer.WriteAsync(Embed(buffer), ct);
        }

        writer.Complete();
    }

    private List<VectorRecord> Embed(List<MovieRecord> batch) =>
        batch.Select(x => new VectorRecord(
            (ulong)x.Id,
            embedder.Embed<EmbeddingF32>(x.Genres.Replace("|", " ")).Values.ToArray(),
            x.Title,
            x.Genres
        )).ToList();
}
