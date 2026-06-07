// This file is part of the Genova project licensed under the GNU General Public License v3.0.
// See the LICENSE file in the project root for more information.

using System.Text;
using Genova.Conduit.Embeddings;
using Genova.Conduit.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Genova.Roybot.Training;

internal static class Program
{
    /// <summary>
    /// The entry point for the training application that generates vector embeddings
    /// and writes them to a JSON snapshot file.
    /// </summary>
    private static async Task Main()
    {
        string solutionFolder = FindSolutionFolder();
        string inputPath = Path.Combine(solutionFolder, "Roybot.Training", "Input", "chunks.txt");
        string outputPath = Path.Combine(solutionFolder, "Roybot", "Data", "vector-snapshot.json");

        IHost host = CreateHostBuilder().Build();

        IEmbeddingClient embeddingClient =
            host.Services.GetRequiredService<IEmbeddingClient>();

        Console.WriteLine("=== Genova.Roybot Training: Embedding Chunks ===");
        Console.WriteLine("Reading input chunks from:");
        Console.WriteLine(inputPath);
        Console.WriteLine();

        if (!File.Exists(inputPath))
        {
            Console.WriteLine("ERROR: Input file does not exist.");
            return;
        }

        IList<string> chunks = await ReadChunksAsync(inputPath);

        if (chunks.Count == 0)
        {
            Console.WriteLine("No chunks found to embed.");
            return;
        }

        Console.WriteLine("Generating embeddings...");
        EmbeddingRequest request = new()
        {
            Inputs = chunks,
            ModelId = null // Use default model ("text-embedding-3-small")
        };

        EmbeddingResponse response =
            await embeddingClient.GenerateEmbeddingsAsync(request, CancellationToken.None);

        if (response.Embeddings.Count != chunks.Count)
        {
            Console.WriteLine("ERROR: Mismatch between chunks and embeddings.");
            return;
        }

        Console.WriteLine("Creating snapshot...");
        VectorStoreSnapshot snapshot = BuildSnapshot(chunks, response);

        Console.WriteLine("Writing snapshot to:");
        Console.WriteLine(outputPath);
        Console.WriteLine();

        await WriteSnapshotAsync(snapshot, outputPath);

        Console.WriteLine("Completed successfully.");
    }

    private static string FindSolutionFolder()
    {
        const string solutionFileName = "Genova.Roybot.sln";
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, solutionFileName)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Solution folder containing '{solutionFileName}' could not be found.");
    }

    /// <summary>
    /// Creates and configures the application host, including DI for the embedding client.
    /// </summary>
    private static IHostBuilder CreateHostBuilder()
    {
        IHostBuilder builder = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddHttpClient("Genova.Roybot.OpenAI.Embeddings");

                services.AddSingleton<IEmbeddingClient>(sp =>
                {
                    IHttpClientFactory factory = sp.GetRequiredService<IHttpClientFactory>();

                    string? apiKey =
                        Environment.GetEnvironmentVariable("OPENAI_API_KEY");

                    if (string.IsNullOrWhiteSpace(apiKey))
                    {
                        throw new InvalidOperationException(
                            "Environment variable 'OPENAI_API_KEY' is not set.");
                    }

                    return new OpenAiEmbeddingClient(factory, apiKey);
                });
            });

        return builder;
    }

    /// <summary>
    /// Reads all non-empty lines from the specified text file, preserving order.
    /// Each line is treated as its own chunk.
    /// </summary>
    private static async Task<IList<string>> ReadChunksAsync(string path)
    {
        List<string> chunks = [];

        using FileStream fs = File.OpenRead(path);
        using StreamReader reader = new(fs, Encoding.UTF8);

        while (!reader.EndOfStream)
        {
            string? line = await reader.ReadLineAsync();

            if (!string.IsNullOrWhiteSpace(line))
            {
                chunks.Add(line.Trim());
            }
        }

        return chunks;
    }

    /// <summary>
    /// Creates a <see cref="VectorStoreSnapshot"/> from the given chunks and embedding response.
    /// Each vector record contains an Id, an embedding, and a "text" metadata field.
    /// </summary>
    private static VectorStoreSnapshot BuildSnapshot(
        IList<string> chunks,
        EmbeddingResponse response)
    {
        List<VectorRecord> records = new(chunks.Count);

        for (int i = 0; i < chunks.Count; i++)
        {
            string id = "chunk-" + i.ToString();

            Embedding embedding = response.Embeddings[i];

            VectorRecord record = new()
            {
                Id = id,
                Embedding = embedding.Values,
                Metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["text"] = chunks[i]
                }
            };

            records.Add(record);
        }

        VectorStoreSnapshot snapshot = new()
        {
            ModelId = "text-embedding-3-small",
            CreatedAt = DateTimeOffset.UtcNow,
            Records = records
        };

        return snapshot;
    }

    /// <summary>
    /// Writes the snapshot JSON to the specified file path.
    /// Ensures that the target directory exists.
    /// </summary>
    private static async Task WriteSnapshotAsync(VectorStoreSnapshot snapshot, string path)
    {
        string? directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory) &&
            !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using FileStream fs = File.Create(path);
        await VectorStoreSnapshotSerializer.ExportAsync(snapshot, fs, CancellationToken.None);
    }
}
