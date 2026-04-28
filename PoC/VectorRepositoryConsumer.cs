using System.Diagnostics;
using System.Threading.Channels;

public class VectorRepositoryConsumer(IVectorRepository repo, int batchSize)
{
    public async Task RunAsync(ChannelReader<List<VectorRecord>> reader, Stopwatch sw, CancellationToken ct)
    {
        var buffer = new List<VectorRecord>(batchSize);
        int total = 0;

        await foreach (var records in reader.ReadAllAsync(ct))
        {
            buffer.AddRange(records);

            if (buffer.Count >= batchSize)
            {
                await repo.UpsertAsync(buffer, ct);
                total += buffer.Count;
                Console.WriteLine($"  {total} uploaded ({sw.Elapsed:mm\\:ss})");
                buffer.Clear();
            }
        }

        if (buffer.Count > 0)
        {
            await repo.UpsertAsync(buffer, ct);
            total += buffer.Count;
        }

        Console.WriteLine($"Upload complete: {total} total ({sw.Elapsed:mm\\:ss\\.ff})");
    }
}
