# Genova.Roybot

An IT help desk chatbot library with a console host and a small training tool for generating retrieval embeddings.

> [!WARNING]
> This is an experimental project and should not be considered production-ready. It exists to explore a small AI, ML, agent, or demo idea within the broader Genova ecosystem.

> [!IMPORTANT]
> A fresh public clone of this repository should not be expected to restore or build without additional Genova infrastructure. Many Genova dependencies are distributed through a private authenticated NuGet feed, and the public source does not include feed credentials or a complete public package graph.

## Installation

```bash
dotnet restore
dotnet build
```

Set the required environment variable:

```bash
OPENAI_API_KEY=<your_api_key>
```

## Usage

Run the terminal app:

```bash
dotnet run --project Roybot.Terminal
```

Example library usage:

```csharp
ChatMessage reply = await chatService.GetReplyAsync(userContext, "How do I reset my password?");
```

## Features

* Multi-step chat pipeline for retrieval, prompt building, model call, and conversation update
* Keeps per-user conversation history and request metadata
* Retrieves relevant internal text chunks from a vector store before answering
* Console REPL for local testing
* Training utility to build a vector snapshot from text chunks

## Notes

* Targets .NET 8.0.
* The terminal app loads an embedded `vector-snapshot.json` resource.
* The training project uses local file paths for input and output and may need adjustment before running.

## Thanks

* OpenAI chat and embedding APIs
* ML.NET / ONNX Runtime

## Third-Party Notices

This project has direct runtime dependencies on third-party NuGet packages, including `Microsoft.Extensions.*` packages (MIT), `Microsoft.ML*` packages (MIT). See each package's NuGet license metadata for full license and notice terms.

## License

GNU General Public License v3.0. See the `LICENSE` file for details.
