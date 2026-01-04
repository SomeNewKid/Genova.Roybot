// This file is part of the Genova project licensed under the GNU General Public License v3.0.
// See the LICENSE file in the project root for more information.

using Genova.Common.Attributes;
using Genova.Conduit.Chats;
using Genova.Conduit.Pipelines;

namespace Genova.Roybot;

/// <summary>
/// Represents a pipeline step that invokes the chat client using a
/// <see cref="ChatRequest"/> stored in the pipeline context and stores
/// the resulting assistant <see cref="ChatMessage"/> in the context.
/// </summary>
[CodeQuality(Public = true, Justification = "Intended for use by libraries and applications.")]
public sealed class CallChatClientStep : IPipelineStep
{
    private const string AssistantMessageKey = "Roy.AssistantMessage";

    private readonly IChatClient _chatClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallChatClientStep"/> class.
    /// </summary>
    /// <param name="chatClient">The chat client used to call the LLM.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="chatClient"/> is <c>null</c>.
    /// </exception>
    public CallChatClientStep(IChatClient chatClient)
    {
        if (chatClient == null)
        {
            throw new ArgumentNullException(nameof(chatClient));
        }

        _chatClient = chatClient;
    }

    /// <summary>
    /// Executes the step by invoking the chat client and storing the
    /// resulting assistant message in the pipeline context. If an error
    /// occurs, a fallback error message is generated instead.
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

        string chatRequestKey = BuildPromptStep.GetChatRequestKey();
        ChatRequest? request = context.GetItem<ChatRequest>(chatRequestKey);

        if (request == null)
        {
            throw new InvalidOperationException(
                "No ChatRequest was found in the pipeline context. " +
                "Ensure BuildPromptStep has been executed before CallChatClientStep.");
        }

        ChatMessage assistantMessage;

        try
        {
            ChatResponse response =
                await _chatClient.GenerateAsync(request, cancellationToken)
                    .ConfigureAwait(false);

            if (response.Message == null ||
                string.IsNullOrWhiteSpace(response.Message.Content))
            {
                assistantMessage = new ChatMessage
                {
                    Role = ChatMessageRole.Assistant,
                    Content = "I tried to respond, but an error occurred. One of us caused an error; statistically, it is probably you.",
                };
            }
            else
            {
                assistantMessage = response.Message;
            }
        }
        catch
        {
            assistantMessage = new ChatMessage
            {
                Role = ChatMessageRole.Assistant,
                Content = "I encountered an error talking to my overlords. One of us caused an error. Probably you.",
            };
        }

        context.SetItem(AssistantMessageKey, assistantMessage);
    }

    /// <summary>
    /// Gets the context key under which the assistant <see cref="ChatMessage"/> is stored.
    /// </summary>
    public static string GetAssistantMessageKey()
    {
        return AssistantMessageKey;
    }
}
