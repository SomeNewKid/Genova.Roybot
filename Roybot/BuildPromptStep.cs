// This file is part of the Genova project licensed under the GNU General Public License v3.0.
// See the LICENSE file in the project root for more information.

using System.Text;
using Genova.Common.Attributes;
using Genova.Conduit.Chats;
using Genova.Conduit.Pipelines;

namespace Genova.Roybot;

/// <summary>
/// Represents a pipeline step that builds a <see cref="ChatRequest"/> for the LLM
/// based on the user context, conversation history, metadata, retrieved context
/// chunks, and current user input.
/// </summary>
[CodeQuality(Public = true, Justification = "Intended for use by libraries and applications.")]
public sealed class BuildPromptStep : IPipelineStep
{
    private const string ChatRequestKey = "Roy.ChatRequest";

    private readonly string _userContextKey;
    private readonly string _userInputKey;
    private readonly string _chunksKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildPromptStep"/> class.
    /// </summary>
    /// <param name="userContextKey">
    /// The key in <see cref="PipelineContext.Items"/> under which the <see cref="UserContext"/> is stored.
    /// </param>
    /// <param name="userInputKey">
    /// The key in <see cref="PipelineContext.Items"/> under which the user input text is stored.
    /// </param>
    /// <param name="chunksKey">
    /// The key in <see cref="PipelineContext.Items"/> under which the retrieved context
    /// chunk texts are stored as an <see cref="IList{T}"/> of <see cref="string"/>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when any key parameter is <c>null</c> or whitespace.
    /// </exception>
    public BuildPromptStep(
        string userContextKey,
        string userInputKey,
        string chunksKey)
    {
        if (string.IsNullOrWhiteSpace(userContextKey))
        {
            throw new ArgumentException("User context key must be non-empty.", nameof(userContextKey));
        }

        if (string.IsNullOrWhiteSpace(userInputKey))
        {
            throw new ArgumentException("User input key must be non-empty.", nameof(userInputKey));
        }

        if (string.IsNullOrWhiteSpace(chunksKey))
        {
            throw new ArgumentException("Chunks key must be non-empty.", nameof(chunksKey));
        }

        _userContextKey = userContextKey;
        _userInputKey = userInputKey;
        _chunksKey = chunksKey;
    }

    /// <summary>
    /// Gets the context key under which the <see cref="ChatRequest"/> is stored.
    /// </summary>
    /// <returns>The chat request context key.</returns>
    public static string GetChatRequestKey()
    {
        return ChatRequestKey;
    }

    /// <summary>
    /// Executes the step by constructing a <see cref="ChatRequest"/> and storing it
    /// in the pipeline context for subsequent steps to use.
    /// </summary>
    /// <param name="context">The shared pipeline context.</param>
    /// <param name="cancellationToken">A token that may be used to observe cancellation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ExecuteAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        UserContext? userContext = context.GetItem<UserContext>(_userContextKey);
        if (userContext == null)
        {
            throw new InvalidOperationException(
                $"Pipeline context does not contain a UserContext under key '{_userContextKey}'.");
        }

        string? userInput = context.GetItem<string>(_userInputKey);
        if (string.IsNullOrWhiteSpace(userInput))
        {
            throw new InvalidOperationException(
                $"Pipeline context does not contain user input under key '{_userInputKey}'.");
        }

        IList<string>? chunks = context.GetItem<IList<string>>(_chunksKey);

        ChatRequest request = BuildChatRequest(userContext, userInput, chunks);
        context.SetItem(ChatRequestKey, request);

        return Task.CompletedTask;
    }

    private static ChatRequest BuildChatRequest(
        UserContext userContext,
        string userInput,
        IList<string>? chunks)
    {
        ChatRequest request = new ()
        {
            ModelId = null, // Use default model configured in the chat client (e.g., gpt-4o-mini).
            MaxTokens = 256,
            Temperature = 0.6,
        };

        // System message: persona and safety instructions.
        ChatMessage personaMessage = new ()
        {
            Role = ChatMessageRole.System,
            Content =
                """
                You are Clare, an IT HelpDesk chatbot.
                You are friendly, calm, and concise. You focus on understanding the user’s question and responding in clear, natural language.
                When a question is unclear, ask one brief clarifying question before answering. When it is clear, answer directly and succinctly without unnecessary detail.                
                """,
        };

        request.Messages.Add(personaMessage);

        // System message: environment metadata.
        string metadataText = BuildMetadataSystemText(userContext);
        if (!string.IsNullOrWhiteSpace(metadataText))
        {
            ChatMessage metadataMessage = new ()
            {
                Role = ChatMessageRole.System,
                Content = metadataText,
            };

            request.Messages.Add(metadataMessage);
        }

        // System message: retrieved internal context chunks, if any.
        if (chunks != null && chunks.Count > 0)
        {
            string chunksText = BuildChunksSystemText(chunks);

            if (!string.IsNullOrWhiteSpace(chunksText))
            {
                ChatMessage chunksMessage = new ()
                {
                    Role = ChatMessageRole.System,
                    Content = chunksText,
                };

                request.Messages.Add(chunksMessage);
            }
        }

        // Include a sliding window of recent conversation messages (e.g., last 10).
        IList<ChatMessage> history = userContext.Conversation.Messages;
        int historyCount = history.Count;
        int maxHistory = 10;
        int startIndex = historyCount > maxHistory ? historyCount - maxHistory : 0;

        for (int i = startIndex; i < historyCount; i++)
        {
            ChatMessage message = history[i];
            request.Messages.Add(new ChatMessage
            {
                Role = message.Role,
                Content = message.Content,
            });
        }

        // Current user message.
        ChatMessage currentUserMessage = new ()
        {
            Role = ChatMessageRole.User,
            Content = userInput,
        };

        request.Messages.Add(currentUserMessage);

        return request;
    }

    private static string BuildMetadataSystemText(UserContext userContext)
    {
        if (userContext.Metadata.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new ();
        builder.Append(
            "The following HTTP-style metadata is available about the user and environment " +
            "(you may use this information if helpful, but you do not have to): ");

        bool first = true;

        foreach (KeyValuePair<string, object?> pair in userContext.Metadata)
        {
            if (!first)
            {
                builder.Append("; ");
            }

            builder.Append(pair.Key);
            builder.Append(" = ");
            builder.Append(pair.Value);

            first = false;
        }

        builder.Append('.');
        return builder.ToString();
    }

    private static string BuildChunksSystemText(IList<string> chunks)
    {
        if (chunks.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new ();
        builder.Append(
            "The following internal IT notes may be relevant to the user's question. " +
            "You may use them as additional context when answering, but you do not need to quote them verbatim: ");

        for (int i = 0; i < chunks.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append('[');
            builder.Append(i + 1);
            builder.Append("] ");
            builder.Append(chunks[i]);
        }

        return builder.ToString();
    }
}
