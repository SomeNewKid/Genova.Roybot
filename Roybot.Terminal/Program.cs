// This file is part of the Genova project licensed under the GNU General Public License v3.0.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using Genova.Conduit.Chats;
using Genova.Conduit.Embeddings;
using Genova.Conduit.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Genova.Roybot.Terminal;

/// <summary>
/// Represents the entry point for the Roybot terminal application,
/// which hosts the Roybot chat service in a console-based REPL.
/// </summary>
internal static class Program
{
    private const string ChatApiKeyEnvironmentVariable = "openai-genova-api-key";
    private const string EmbeddingsApiKeyEnvironmentVariable = "openai-genova-api-key";
    private const string SnapshotResourceName = "Genova.Roybot.Data.vector-snapshot.json";

    /// <summary>
    /// The main entry point for the Roybot terminal application.
    /// </summary>
    private static async Task Main()
    {
        IHost host = CreateHostBuilder().Build();

        IChatService chatService =
            host.Services.GetRequiredService<IChatService>();

        UserContext userContext = CreateUserContext();

        Console.WriteLine("=== Genova Roybot Terminal ===");
        Console.WriteLine("Type your IT question or comment. Type 'exit' to quit.");
        Console.WriteLine();

        using CancellationTokenSource cancellationSource = new CancellationTokenSource();

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationSource.Cancel();
        };

        while (!cancellationSource.IsCancellationRequested)
        {
            Console.Write("You: ");
            string? userInput = Console.ReadLine();

            if (userInput == null)
            {
                break;
            }

            userInput = userInput.Trim();

            if (string.Equals(userInput, "exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(userInput))
            {
                continue;
            }

            try
            {
                ChatMessage reply =
                    await chatService.GetReplyAsync(
                            userContext,
                            userInput,
                            cancellationSource.Token)
                        .ConfigureAwait(false);

                Console.WriteLine("Roy: " + reply.Content);
                Console.WriteLine();
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine();
                Console.WriteLine("Operation cancelled.");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine(
                    "Roy: I encountered an unexpected error. One of us caused an error. Probably you. " +
                    $"(Details: {ex.Message})");
                Console.WriteLine();
            }
        }

        Console.WriteLine();
        Console.WriteLine("Exiting Roybot. Try not to break anything while I am gone.");
    }

    /// <summary>
    /// Creates and configures the host builder for the terminal application,
    /// including dependency injection for the chat client, embedding client,
    /// vector store, and chat service.
    /// </summary>
    /// <returns>An <see cref="IHostBuilder"/> instance.</returns>
    private static IHostBuilder CreateHostBuilder()
    {
        IHostBuilder builder = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                // Reduce noise from HTTP client logging.
                logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
            })
            .ConfigureServices(services =>
            {
                // HTTP clients for OpenAI Chat and Embeddings.
                services.AddHttpClient("Genova.Conduit.OpenAI.Chat");
                services.AddHttpClient("Genova.Roybot.OpenAI.Embeddings");

                // Chat client (OpenAI Chat Completions).
                services.AddSingleton<IChatClient>(sp =>
                {
                    IHttpClientFactory factory =
                        sp.GetRequiredService<IHttpClientFactory>();

                    string? apiKey =
                        Environment.GetEnvironmentVariable(ChatApiKeyEnvironmentVariable);

                    if (string.IsNullOrWhiteSpace(apiKey))
                    {
                        throw new InvalidOperationException(
                            $"Environment variable '{ChatApiKeyEnvironmentVariable}' is not set.");
                    }

                    // Assumes an OpenAiChatClient exists that uses the Chat Completions API
                    // and defaults to gpt-4o-mini when ModelId is null.
                    OpenAiChatClient client = new OpenAiChatClient(factory, apiKey);
                    return client;
                });

                // Embedding client (OpenAI Embeddings).
                services.AddSingleton<IEmbeddingClient>(sp =>
                {
                    IHttpClientFactory factory = sp.GetRequiredService<IHttpClientFactory>();

                    string? apiKey =
                        Environment.GetEnvironmentVariable(EmbeddingsApiKeyEnvironmentVariable);

                    if (string.IsNullOrWhiteSpace(apiKey))
                    {
                        throw new InvalidOperationException(
                            $"Environment variable '{EmbeddingsApiKeyEnvironmentVariable}' is not set.");
                    }

                    OpenAiEmbeddingClient client = new OpenAiEmbeddingClient(factory, apiKey);
                    return client;
                });

                // Vector store loaded from embedded snapshot.
                services.AddSingleton<IVectorStore>(sp =>
                {
                    InMemoryVectorStore store = new InMemoryVectorStore();

                    Assembly assembly = typeof(UserContext).Assembly;
                    using Stream? stream =
                        assembly.GetManifestResourceStream(SnapshotResourceName);

                    if (stream == null)
                    {
                        throw new InvalidOperationException(
                            $"Embedded resource '{SnapshotResourceName}' was not found. " +
                            "Ensure the vector-snapshot.json file is embedded with the correct resource name.");
                    }

                    VectorStoreSnapshot snapshot =
                        VectorStoreSnapshotSerializer.ImportAsync(stream, CancellationToken.None)
                            .GetAwaiter()
                            .GetResult();

                    if (snapshot.Records != null && snapshot.Records.Count > 0)
                    {
                        store.UpsertAsync(snapshot.Records, CancellationToken.None)
                            .GetAwaiter()
                            .GetResult();
                    }

                    return store;
                });

                // Chat service.
                services.AddSingleton<IChatService>(sp =>
                {
                    IChatClient chatClient = sp.GetRequiredService<IChatClient>();
                    IEmbeddingClient embeddingClient = sp.GetRequiredService<IEmbeddingClient>();
                    IVectorStore vectorStore = sp.GetRequiredService<IVectorStore>();

                    ChatService service = new ChatService(chatClient, embeddingClient, vectorStore);
                    return service;
                });
            });

        return builder;
    }

    /// <summary>
    /// Creates a new <see cref="UserContext"/> instance with metadata
    /// mimicking HTTP-style information for the terminal environment.
    /// </summary>
    /// <returns>A populated <see cref="UserContext"/> instance.</returns>
    private static UserContext CreateUserContext()
    {
        UserContext context = new UserContext();

        context.Metadata["IpAddress"] = "127.0.0.1";
        context.Metadata["UserAgent"] = "Genova.Roybot.Terminal/1.0";
        context.Metadata["ClientTime"] = DateTimeOffset.Now;
        context.Metadata["OperatingSystem"] = Environment.OSVersion.ToString();
        context.Metadata["MachineName"] = Environment.MachineName;

        return context;
    }
}
