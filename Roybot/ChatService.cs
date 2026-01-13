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
/// Represents the concrete implementation of <see cref="IChatService"/> for Roybot.
/// This service is agnostic to the hosting environment and delegates the work to
/// an internal pipeline.
/// </summary>
[CodeQuality(Public = true, Justification = "Intended for use by libraries and applications.")]
[SuppressMessage(
    "Performance",
    "CA1859:Use concrete types when possible for improved performance",
    Justification = "Favor interfaces over concrete types")]
public sealed class ChatService : IChatService
{
    private const string UserContextKey = "Roy.UserContext";
    private const string UserInputKey = "Roy.UserInput";
    private const string ReplyMessageKey = "Roy.ReplyMessage";

    private readonly IPipeline _chatPipeline;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatService"/> class.
    /// </summary>
    /// <param name="chatClient">
    /// The chat client used by the internal pipeline to communicate with the LLM.
    /// </param>
    /// <param name="embeddingClient">
    /// The embedding client used to compute embeddings for user input.
    /// </param>
    /// <param name="vectorStore">
    /// The vector store containing pre-embedded internal text chunks.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any required dependency is <c>null</c>.
    /// </exception>
    public ChatService(
        IChatClient chatClient,
        IEmbeddingClient embeddingClient,
        IVectorStore vectorStore)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(embeddingClient);
        ArgumentNullException.ThrowIfNull(vectorStore);

        _chatPipeline = new ChatPipeline(
            chatClient,
            embeddingClient,
            vectorStore,
            UserContextKey,
            UserInputKey,
            ReplyMessageKey);
    }

    /// <summary>
    /// Generates a chat reply for the specified user input and user context.
    /// </summary>
    /// <param name="userContext">The user context containing conversation history and metadata.</param>
    /// <param name="userInput">The user's input text.</param>
    /// <param name="cancellationToken">A token that may be used to observe cancellation.</param>
    /// <returns>A task whose result is the assistant's chat message.</returns>
    public async Task<ChatMessage> GetReplyAsync(
        UserContext userContext,
        string userInput,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userContext);

        if (string.IsNullOrWhiteSpace(userInput))
        {
            throw new ArgumentException("User input must be non-empty.", nameof(userInput));
        }

        PipelineContext context = new (ExecutionEnvironment.Application);
        context.SetItem(UserContextKey, userContext);
        context.SetItem(UserInputKey, userInput);

        await _chatPipeline.ExecuteAsync(context, cancellationToken)
            .ConfigureAwait(false);

        ChatMessage? reply = context.GetItem<ChatMessage>(ReplyMessageKey);

        reply ??= new ChatMessage
            {
                Role = ChatMessageRole.Assistant,
                Content = "I encountered an error generating a reply. One of us caused an error. Statistically, it was you.",
            };

        return reply;
    }
}
