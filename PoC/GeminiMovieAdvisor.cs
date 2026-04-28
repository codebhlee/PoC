using GenerativeAI;

public class GeminiMovieAdvisor
{
    private readonly GenerativeModel _model;

    public GeminiMovieAdvisor(string apiKey, string model)
    {
        var googleAi = new GoogleAi(apiKey);
        _model = googleAi.CreateGenerativeModel(model);
    }

    public async Task<string> RecommendAsync(string query, IReadOnlyList<VectorSearchResult> context, CancellationToken ct = default)
    {
        var contextText = string.Join("\n", context.Select((r, i) =>
            $"{i + 1}. {r.Title} (similarity: {r.Score:F2})"));

        var prompt = $"""
            You are a movie recommendation assistant.
            The user is looking for movies related to: "{query}"

            Here are the most similar movies found:
            {contextText}

            Based on these results, provide a brief recommendation explaining why these movies match the user's interest.
            """;

        var response = await _model.GenerateContentAsync(prompt, cancellationToken: ct);
        return response.Text() ?? string.Empty;
    }
}
