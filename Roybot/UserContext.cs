// This file is part of the Genova project licensed under the GNU General Public License v3.0.
// See the LICENSE file in the project root for more information.

using Genova.Common.Attributes;
using Genova.Conduit.Chats;
using Genova.Conduit.Storage;

namespace Genova.Roybot;

/// <summary>
/// Represents the context for a single user interacting with the chatbot,
/// including the conversation history and arbitrary metadata such as user agent,
/// IP address, and local date and time.
/// </summary>
[CodeQuality(Public = true, Justification = "Intended for use by libraries and applications.")]
public sealed class UserContext
{
    private Conversation? _conversation;

    /// <summary>
    /// Gets or sets the conversation associated with this user.
    /// A new conversation will be created on first access if one has not
    /// been assigned.
    /// </summary>
    public Conversation Conversation
    {
        get
        {
            _conversation ??= new Conversation
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Messages = [],
                };

            return _conversation;
        }

        set
        {
            _conversation = value;
        }
    }

    /// <summary>
    /// Gets the metadata dictionary for this user context, which can contain
    /// HTTP-related data such as IP address, user agent, and local date and time.
    /// </summary>
    public IDictionary<string, object?> Metadata { get; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}
