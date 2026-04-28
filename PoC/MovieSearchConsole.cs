using SmartComponents.LocalEmbeddings;

public class MovieSearchConsole(
    LocalEmbedder embedder,
    IVectorRepository vectorRepo,
    GeminiMovieAdvisor advisor,
    float similarityThreshold)
{
    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine("\nEnter search query (format: \"keywords, limit\" or just \"keywords\"). Empty to quit:");

        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                break;
            }

            var (keyword, limit) = ParseInput(input);
            var queryVec = embedder.Embed<EmbeddingF32>(keyword).Values.ToArray();

            // Search with similarity cutoff
            var results = await vectorRepo.SearchAsync(queryVec, limit, similarityThreshold, ct);

            if (results.Count == 0)
            {
                Console.WriteLine("  No results above similarity threshold.");
                Console.WriteLine();
                continue;
            }

            Console.WriteLine("  Similar movies:");
            foreach (var r in results)
            {
                Console.WriteLine($"    [{r.Score:F3}] {r.Title}");
            }

            // Generate recommendation
            Console.WriteLine("\n  Gemini recommendation:");
            try
            {
                var recommendation = await advisor.RecommendAsync(keyword, results, ct);
                Console.WriteLine($"  {recommendation}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Gemini error: {ex.Message}");
            }

            Console.WriteLine();
        }
    }

    private static (string Keyword, int Limit) ParseInput(string input)
    {
        var parts = input.Split(',', 2);
        var keyword = parts[0].Trim();
        var limit = parts.Length > 1 && int.TryParse(parts[1].Trim(), out var n) ? n : 5;
        return (keyword, limit);
    }
}
