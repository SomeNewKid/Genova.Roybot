// This file is part of the Genova project licensed under the GNU General Public License v3.0.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Genova.Common.Attributes;
using Genova.Conduit.Embeddings;
using Genova.Conduit.Pipelines;
using Genova.Conduit.Storage;

namespace Genova.Roybot;

/// <summary>
/// Represents a pipeline step that generates an embedding for the user's input
/// and performs a similarity search against the vector store to find relevant
/// text chunks. The retrieved chunk texts are stored in the pipeline context
/// for later use when building the LLM prompt.
/// </summary>
[CodeQuality(Public = true, Justification = "Intended for use by libraries and applications.")]
public sealed class RetrieveContextStep : IPipelineStep
{
    private readonly IEmbeddingClient _embeddingClient;
    private readonly IVectorStore _vectorStore;
    private readonly string _userInputKey;
    private readonly string _chunksKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetrieveContextStep"/> class.
    /// </summary>
    /// <param name="embeddingClient">The embedding client used to compute embeddings.</param>
    /// <param name="vectorStore">The vector store used to look up similar chunks.</param>
    /// <param name="userInputKey">
    /// The key in <see cref="PipelineContext.Items"/> under which the user input text is stored.
    /// </param>
    /// <param name="chunksKey">
    /// The key under which the retrieved chunk texts will be stored in the context
    /// as an <see cref="IList{T}"/> of <see cref="string"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="embeddingClient"/> or <paramref name="vectorStore"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when any key parameter is <c>null</c> or whitespace.
    /// </exception>
    public RetrieveContextStep(
        IEmbeddingClient embeddingClient,
        IVectorStore vectorStore,
        string userInputKey,
        string chunksKey)
    {
        if (embeddingClient == null)
        {
            throw new ArgumentNullException(nameof(embeddingClient));
        }

        if (vectorStore == null)
        {
            throw new ArgumentNullException(nameof(vectorStore));
        }

        if (string.IsNullOrWhiteSpace(userInputKey))
        {
            throw new ArgumentException("User input key must be non-empty.", nameof(userInputKey));
        }

        if (string.IsNullOrWhiteSpace(chunksKey))
        {
            throw new ArgumentException("Chunks key must be non-empty.", nameof(chunksKey));
        }

        _embeddingClient = embeddingClient;
        _vectorStore = vectorStore;
        _userInputKey = userInputKey;
        _chunksKey = chunksKey;
    }

    /// <summary>
    /// Executes the step by embedding the user input and querying the vector store
    /// for similar chunks. The retrieved chunk texts are stored in the pipeline
    /// context under the configured chunks key.
    /// </summary>
    /// <param name="context">The shared pipeline context.</param>
    /// <param name="cancellationToken">A token that may be used to observe cancellation.</param>
    public async Task ExecuteAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        string? userInput = context.GetItem<string>(_userInputKey);
        if (string.IsNullOrWhiteSpace(userInput))
        {
            // No input, nothing to retrieve.
            context.RemoveItem(_chunksKey);
            return;
        }

        // Compute embedding for the user input.
        EmbeddingRequest request = new EmbeddingRequest
        {
            Inputs = new List<string> { userInput },
            ModelId = null // Use default model (e.g., text-embedding-3-small).
        };

        EmbeddingResponse response =
            await _embeddingClient.GenerateEmbeddingsAsync(request, cancellationToken)
                .ConfigureAwait(false);

        if (response.Embeddings.Count == 0 ||
            response.Embeddings[0].Values == null ||
            response.Embeddings[0].Values.Count == 0)
        {
            context.RemoveItem(_chunksKey);
            return;
        }

        IReadOnlyList<float> queryEmbedding = response.Embeddings[0].Values;

        // Search for relevant chunks using a confidence threshold and a maximum result count.
        IReadOnlyList<VectorSearchResult> results =
            await _vectorStore.SearchAsync(
                    queryEmbedding,
                    minConfidence: 0.2f,
                    maxResults: 5,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

        List<string> chunks = new List<string>();

        for (int i = 0; i < results.Count; i++)
        {
            VectorSearchResult result = results[i];

            if (result.Record.Metadata == null)
            {
                continue;
            }

            if (!result.Record.Metadata.TryGetValue("text", out object? value) ||
                value == null)
            {
                continue;
            }

            string? text = ExtractTextValue(value);
            if (!string.IsNullOrWhiteSpace(text))
            {
                chunks.Add(text.Trim());
            }
        }

        if (chunks.Count == 0)
        {
            context.RemoveItem(_chunksKey);
        }
        else
        {
            context.SetItem<IList<string>>(_chunksKey, chunks);
        }
    }

    private static string? ExtractTextValue(object value)
    {
        if (value is string s)
        {
            return s;
        }

        if (value is JsonElement element &&
            element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        return null;
    }
}
