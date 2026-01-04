// This file is part of the Genova project licensed under the GNU General Public License v3.0.
// See the LICENSE file in the project root for more information.

using Genova.Conduit.Chats;

namespace Genova.Roybot;

/// <summary>
/// Represents the public interface for the Roybot chat service, which
/// generates responses given a user context and user input text.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Generates a chat reply for the specified user input and user context.
    /// </summary>
    /// <param name="userContext">The user context containing conversation history and metadata.</param>
    /// <param name="userInput">The user's input text.</param>
    /// <param name="cancellationToken">A token that may be used to observe cancellation.</param>
    /// <returns>A task whose result is the assistant's chat message.</returns>
    Task<ChatMessage> GetReplyAsync(
        UserContext userContext,
        string userInput,
        CancellationToken cancellationToken = default);
}
