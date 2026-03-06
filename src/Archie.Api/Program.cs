using Archie.Core.Ingestion;
using Archie.Core.Models;
using Archie.Core.Retrieval;
using Archie.Core.Synthesis;
using Archie.Infrastructure.AzureOpenAI;
using Archie.Infrastructure.DocumentParsing;
using Archie.Infrastructure.Ingestion;
using Archie.Infrastructure.Retrieval;
using Archie.Infrastructure.Synthesis;
using Archie.Infrastructure.VectorStore;
using Microsoft.SemanticKernel;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Build AzureOpenAI options — fail fast at startup if any value is missing.
IConfigurationSection azureSection = builder.Configuration.GetSection("AzureOpenAI");
AzureOpenAIOptions azureOptions = new(
    Endpoint: azureSection["Endpoint"]
        ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is required."),
    ApiKey: azureSection["ApiKey"]
        ?? throw new InvalidOperationException("AzureOpenAI:ApiKey is required."),
    ChatDeployment: azureSection["ChatDeployment"]
        ?? throw new InvalidOperationException("AzureOpenAI:ChatDeployment is required."),
    EmbeddingDeployment: azureSection["EmbeddingDeployment"]
        ?? throw new InvalidOperationException("AzureOpenAI:EmbeddingDeployment is required."));

// Semantic Kernel — composition root; also registers IEmbeddingService via AddAzureOpenAIServices.
IKernelBuilder kernelBuilder = builder.Services.AddKernel();
kernelBuilder.AddAzureOpenAIServices(azureOptions);

// Infrastructure implementations.
builder.Services.AddInMemoryVectorStore();
builder.Services.AddDocumentParsing();
builder.Services.AddBasicRetriever();
builder.Services.AddBasicSynthesizer();
builder.Services.AddDocumentIngestionPipeline();

WebApplication app = builder.Build();

app.UseHttpsRedirection();

// POST /api/ingest — Chunk, embed, and store all markdown files in a directory.
app.MapPost("/api/ingest", async (
    IngestRequest request,
    IDocumentIngestionPipeline pipeline,
    CancellationToken ct) =>
{
    if (!Directory.Exists(request.Directory))
        return Results.BadRequest(new { error = $"Directory not found: {request.Directory}" });

    int chunksIngested = await pipeline.IngestDirectoryAsync(request.Directory, ct);
    return Results.Ok(new { chunksIngested });
}).WithName("Ingest");

// POST /api/query — Retrieve relevant chunks and synthesize an answer.
app.MapPost("/api/query", async (
    QueryRequest request,
    IRetriever retriever,
    ISynthesizer synthesizer,
    CancellationToken ct) =>
{
    IReadOnlyList<RetrievedChunk> chunks = await retriever.RetrieveAsync(request.Question, topK: 5, ct);
    QueryResponse response = await synthesizer.SynthesizeAsync(request, chunks, ct);
    return Results.Ok(response);
}).WithName("Query");

app.Run();

// Local request DTO — not part of Core or Infrastructure.
record IngestRequest(string Directory);
