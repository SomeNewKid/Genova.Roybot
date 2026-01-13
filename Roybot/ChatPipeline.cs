// This file is part of the Genova project licensed under the GNU General Public License v3.0.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Genova.Common.Attributes;
using Genova.Conduit.Chats;
using Genova.Conduit.Embeddings;
using Genova.Conduit.Pipelines;
using Genova.Conduit.Storage;

namespace Genova.Roybot;

/// <summary>
/// Represents the main chat pipeline for Roybot. It retrieves relevant context
/// from the vector store, builds the prompt, calls the chat client, and updates
/// the conversation in the user context.
/// </summary>
[CodeQuality(Public = true, Justification = "Intended for use by libraries and applications.")]
[SuppressMessage(
    "Performance",
    "CA1859:Use concrete types when possible for improved performance",
    Justification = "Favor interfaces over concrete types")]
public sealed class ChatPipeline : IPipeline
{
    private readonly IPipelineStep _retrieveContextStep;
    private readonly IPipelineStep _buildPromptStep;
    private readonly IPipelineStep _callChatClientStep;
    private readonly IPipelineStep _updateConversationStep;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatPipeline"/> class.
    /// </summary>
    /// <param name="chatClient">
    /// The chat client used to communicate with the OpenAI Chat Completions API.
    /// </param>
    /// <param name="embeddingClient">
    /// The embedding client used to compute embeddings for user input.
    /// </param>
    /// <param name="vectorStore">
    /// The vector store containing pre-embedded internal text chunks.
    /// </param>
    /// <param name="userContextKey">
    /// The key in <see cref="PipelineContext.Items"/> under which the <see cref="UserContext"/>
    /// instance is stored.
    /// </param>
    /// <param name="userInputKey">
    /// The key in <see cref="PipelineContext.Items"/> under which the user input text is stored.
    /// </param>
    /// <param name="replyMessageKey">
    /// The key under which the resulting assistant ChatMessage will be stored.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="chatClient"/>, <paramref name="embeddingClient"/>, or
    /// <paramref name="vectorStore"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when any key parameter is <c>null</c> or whitespace.
    /// </exception>
    public ChatPipeline(
        IChatClient chatClient,
        IEmbeddingClient embeddingClient,
        IVectorStore vectorStore,
        string userContextKey,
        string userInputKey,
        string replyMessageKey)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(embeddingClient);
        ArgumentNullException.ThrowIfNull(vectorStore);

        if (string.IsNullOrWhiteSpace(userContextKey))
        {
            throw new ArgumentException("User context key must be non-empty.", nameof(userContextKey));
        }

        if (string.IsNullOrWhiteSpace(userInputKey))
        {
            throw new ArgumentException("User input key must be non-empty.", nameof(userInputKey));
        }

        if (string.IsNullOrWhiteSpace(replyMessageKey))
        {
            throw new ArgumentException("Reply message key must be non-empty.", nameof(replyMessageKey));
        }

        const string chunksKey = "Roy.RetrievedChunks";

        _retrieveContextStep = new RetrieveContextStep(
            embeddingClient,
            vectorStore,
            userInputKey,
            chunksKey);

        _buildPromptStep = new BuildPromptStep(
            userContextKey,
            userInputKey,
            chunksKey);

        _callChatClientStep = new CallChatClientStep(
            chatClient);

        _updateConversationStep = new UpdateConversationStep(
            userContextKey,
            userInputKey,
            replyMessageKey);
    }

    /// <summary>
    /// Executes the chat pipeline by retrieving relevant context, building the prompt,
    /// calling the chat client, and updating the conversation in the user context.
    /// </summary>
    /// <param name="context">The shared pipeline context.</param>
    /// <param name="cancellationToken">A token that may be used to observe cancellation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ExecuteAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Retrieve relevant internal context based on user input.
        await _retrieveContextStep.ExecuteAsync(context, cancellationToken)
            .ConfigureAwait(false);

        // Build the prompt and create a ChatRequest in the context.
        await _buildPromptStep.ExecuteAsync(context, cancellationToken)
            .ConfigureAwait(false);

        // Call the chat client and place the assistant message in the context.
        await _callChatClientStep.ExecuteAsync(context, cancellationToken)
            .ConfigureAwait(false);

        // Update the conversation in the user context and copy the reply into the context.
        await _updateConversationStep.ExecuteAsync(context, cancellationToken)
            .ConfigureAwait(false);
    }
}
