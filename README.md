# MovieLens RAG PoC

A Retrieval-Augmented Generation (RAG) implementation using the MovieLens 32M dataset. Given a genre keyword query, the system retrieves semantically similar movies from a vector database and generates a recommendation using Google Gemini.

> **This is a PoC.** Only the minimum required components are implemented to validate the RAG pipeline end-to-end. Error handling, authentication, logging, and production-grade concerns are intentionally out of scope.

## Architecture

```
PostgreSQL (movies) → Embedding (bge-micro-v2) → Qdrant (vector DB) → Gemini (generation)
```

### RAG Pipeline

1. **Indexing**: Movie genres are embedded using `SmartComponents.LocalEmbeddings` (bge-micro-v2, CPU) and stored in Qdrant
2. **Retrieval**: User query is embedded and searched against Qdrant with a similarity threshold (default: 0.7)
3. **Generation**: Retrieved movies are passed as context to Google Gemini, which generates a natural language recommendation

## Tech Stack

- **.NET 8** — runtime
- **PostgreSQL 16** — source data store (MovieLens 32M)
- **Qdrant** — vector database
- **SmartComponents.LocalEmbeddings** — local CPU embedding (bge-micro-v2, 384-dim)
- **Google Gemini API** — LLM generation
- **System.Threading.Channels** — producer/consumer pipeline for streaming ingestion

## Project Structure

```
PoC/
├── Program.cs                   # Entry point, pipeline wiring
├── appsettings.json             # Configuration
├── MovieRepository.cs           # IMovieRepository — streams movies from PostgreSQL
├── EmbeddingProducer.cs         # Reads from DB, generates embeddings, writes to channel
├── VectorRepositoryConsumer.cs  # Reads from channel, upserts to Qdrant
├── QdrantVectorRepository.cs    # IVectorRepository — Qdrant implementation
├── GeminiMovieAdvisor.cs        # Calls Gemini API with retrieved context
├── MovieSearchConsole.cs        # Interactive search REPL
├── DirectMLEmbedder.cs          # GPU-based embedder (DirectML/CUDA) — reference impl
├── Interfaces/
│   ├── IMovieRepository.cs
│   └── IVectorRepository.cs
└── Data/
    ├── init.sql                 # Schema + CSV import for PostgreSQL
    └── ml-32m/                  # MovieLens 32M dataset (not committed)
```

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [Google Gemini API Key](https://aistudio.google.com/app/apikey)
- MovieLens 32M dataset (see below)

## Dataset

Download the MovieLens 32M dataset from GroupLens:

> F. Maxwell Harper and Joseph A. Konstan. 2015. The MovieLens Datasets: History and Context. ACM Transactions on Interactive Intelligent Systems (TiiS) 5, 4: 25:1–25:19. https://doi.org/10.1145/2827872

**Download**: [https://grouplens.org/datasets/movielens/32m/](https://grouplens.org/datasets/movielens/32m/)

Extract and place the CSV files in `PoC/Data/ml-32m/`:

```
PoC/Data/ml-32m/
├── movies.csv
├── ratings.csv
├── tags.csv
└── links.csv
```

## Setup

### 1. Set environment variable

```powershell
# Windows (PowerShell)
[System.Environment]::SetEnvironmentVariable("GEMINI_API_KEY", "your-api-key", "User")
```

### 2. Start infrastructure

```bash
docker compose up -d
```

PostgreSQL will automatically create the schema and import CSV data on first start via `init.sql`.

### 3. Download embedding model

```powershell
Invoke-WebRequest -Uri "https://huggingface.co/TaylorAI/bge-micro-v2/resolve/main/onnx/model_quantized.onnx" -OutFile "PoC/model.onnx"
Invoke-WebRequest -Uri "https://huggingface.co/TaylorAI/bge-micro-v2/resolve/main/vocab.txt" -OutFile "PoC/vocab.txt"
```

### 4. Run

```bash
dotnet run --project PoC/PoC.csproj
```

The first run will embed all 87,585 movies and upload them to Qdrant (~1 min on CPU).

## Usage

```
> Animation Comedy, 10       # search with limit
> Action Thriller             # search with default limit (5)
>                             # empty input to quit
```

Results below the similarity threshold (0.7) are filtered out before being sent to Gemini.

## Configuration

`appsettings.json`:

```json
{
  "Qdrant": { "Host": "localhost", "Port": 6334, "CollectionName": "movies" },
  "Pipeline": { "EmbedBatchSize": 1000, "QdrantBatchSize": 250 },
  "Gemini": { "ApiKey": "GEMINI_API_KEY", "Model": "gemini-2.0-flash-lite", "SimilarityThreshold": 0.7 }
}
```

`Gemini.ApiKey` is the name of the environment variable that holds the actual API key.

## Design Notes

- **CPU over GPU for small models**: bge-micro-v2 is a 22MB model with minimal compute per inference. GPU overhead (memory transfer, kernel launch) exceeded the actual computation time, making CPU faster. GPU acceleration becomes beneficial with larger models (>100MB). Parallel processing via `Task.WhenAll` also showed no improvement for the same reason — the bottleneck was embedding throughput, not I/O wait.
- **Backpressure via Channel**: `System.Threading.Channels` with `BoundedChannelOptions(capacity: 4)` automatically applies backpressure — the producer blocks when the consumer falls behind, preventing unbounded memory growth without any manual coordination.
- **O(1) memory via lazy evaluation**: `IAsyncEnumerable` streams rows one at a time from PostgreSQL. Combined with a fixed-size batch buffer that is cleared after each Qdrant upsert, memory usage stays constant regardless of dataset size (87k or 87M rows).
- **SOLID**: `IMovieRepository` and `IVectorRepository` interfaces allow swapping PostgreSQL or Qdrant for other implementations
- **Similarity cutoff**: Results below 0.7 cosine similarity are excluded from the LLM context to reduce noise
