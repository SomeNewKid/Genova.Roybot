// This file is part of the Genova project licensed under the GNU General Public License v3.0.
// See the LICENSE file in the project root for more information.

using Genova.Common.Attributes;
using Genova.Conduit.Chats;
using Genova.Conduit.Pipelines;

namespace Genova.Roybot;

/// <summary>
/// Represents a pipeline step that appends the current user and assistant
/// messages to the conversation in the user context and exposes the
/// assistant reply for the caller.
/// </summary>
[CodeQuality(Public = true, Justification = "Intended for use by libraries and applications.")]
public sealed class UpdateConversationStep : IPipelineStep
{
    private readonly string _userContextKey;
    private readonly string _userInputKey;
    private readonly string _replyMessageKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateConversationStep"/> class.
    /// </summary>
    /// <param name="userContextKey">
    /// The key in <see cref="PipelineContext.Items"/> under which the <see cref="UserContext"/> is stored.
    /// </param>
    /// <param name="userInputKey">
    /// The key in <see cref="PipelineContext.Items"/> under which the user input text is stored.
    /// </param>
    /// <param name="replyMessageKey">
    /// The key under which the resulting assistant <see cref="ChatMessage"/> will be stored.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when any key parameter is <c>null</c> or whitespace.
    /// </exception>
    public UpdateConversationStep(
        string userContextKey,
        string userInputKey,
        string replyMessageKey)
    {
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

        _userContextKey = userContextKey;
        _userInputKey = userInputKey;
        _replyMessageKey = replyMessageKey;
    }

    /// <summary>
    /// Executes the step by updating the conversation in the user context and
    /// copying the assistant reply into the pipeline context under the reply key.
    /// </summary>
    /// <param name="context">The shared pipeline context.</param>
    /// <param name="cancellationToken">A token that may be used to observe cancellation.</param>
    public Task ExecuteAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

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

        string assistantMessageKey = CallChatClientStep.GetAssistantMessageKey();
        ChatMessage? assistantMessage = context.GetItem<ChatMessage>(assistantMessageKey);

        if (assistantMessage == null)
        {
            throw new InvalidOperationException(
                "CallChatClientStep did not store an assistant message in the pipeline context.");
        }

        // Append user message.
        ChatMessage userMessage = new ChatMessage
        {
            Role = ChatMessageRole.User,
            Content = userInput,
        };

        userContext.Conversation.Messages.Add(userMessage);

        // Append assistant message (already has Role = Assistant set).
        userContext.Conversation.Messages.Add(assistantMessage);

        // Store the reply for the ChatService to return.
        context.SetItem(_replyMessageKey, assistantMessage);

        return Task.CompletedTask;
    }
}
