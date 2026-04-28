using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

/// <summary>
/// Local embedder based on DirectML(GPU) using bge-micro-v2 ONNX model.
/// </summary>
public sealed class DirectMLEmbedder : IDisposable
{
    private readonly InferenceSession _session;
    private readonly BertTokenizer _tokenizer;
    private const int MaxLength = 512;

    public DirectMLEmbedder(string modelPath, string vocabPath)
    {
        var options = new SessionOptions();
        options.AppendExecutionProvider_CPU();

        _session = new InferenceSession(modelPath, options);
        _tokenizer = BertTokenizer.Create(vocabPath);
    }

    public float[] Embed(string text) => EmbedBatch([text])[0];

    /// <summary>
    /// Embeds multiple texts in a single GPU batch.
    /// </summary>
    public float[][] EmbedBatch(IReadOnlyList<string> texts)
    {
        int batchSize = texts.Count;
        var inputIds     = new long[batchSize * MaxLength];
        var attentionMask = new long[batchSize * MaxLength];
        var tokenTypeIds  = new long[batchSize * MaxLength];
        var seqLens = new int[batchSize];

        for (int b = 0; b < batchSize; b++)
        {
            var encoded = _tokenizer.EncodeToIds(texts[b]);
            int len = Math.Min(encoded.Count, MaxLength);
            seqLens[b] = len;
            for (int i = 0; i < len; i++)
            {
                inputIds[b * MaxLength + i]      = encoded[i];
                attentionMask[b * MaxLength + i] = 1;
            }
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids",
                new DenseTensor<long>(inputIds, [batchSize, MaxLength])),
            NamedOnnxValue.CreateFromTensor("attention_mask",
                new DenseTensor<long>(attentionMask, [batchSize, MaxLength])),
            NamedOnnxValue.CreateFromTensor("token_type_ids",
                new DenseTensor<long>(tokenTypeIds, [batchSize, MaxLength]))
        };

        using var results = _session.Run(inputs);
        var output = results.First().AsTensor<float>();
        int hiddenSize = (int)(output.Length / batchSize / MaxLength);

        var vectors = new float[batchSize][];
        for (int b = 0; b < batchSize; b++)
        {
            var vector = new float[hiddenSize];
            int len = seqLens[b];

            // Mean pooling
            for (int h = 0; h < hiddenSize; h++)
            {
                float sum = 0;
                for (int s = 0; s < len; s++)
                {
                    sum += output[b, s, h];
                }
                vector[h] = sum / len;
            }

            // L2 normalization
            float norm = MathF.Sqrt(vector.Sum(v => v * v));
            if (norm > 0)
            {
                for (int i = 0; i < hiddenSize; i++)
                {
                    vector[i] /= norm;
                }
            }

            vectors[b] = vector;
        }

        return vectors;
    }

    public void Dispose() => _session.Dispose();
}
