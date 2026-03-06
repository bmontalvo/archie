using Microsoft.SemanticKernel;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Semantic Kernel — composition root for all AI services
builder.Services.AddKernel();

// TODO: Register Archie.Core services (IQueryPlanner, IRetriever, IContextEvaluator, ISynthesisEngine)
// TODO: Register Archie.Infrastructure implementations (Azure OpenAI, Azure AI Search)

WebApplication app = builder.Build();

app.UseHttpsRedirection();

// POST /api/query — Phase 1 vertical slice endpoint (not yet implemented)
app.MapPost("/api/query", () => Results.StatusCode(501))
    .WithName("Query");

// POST /api/ingest — Document ingestion endpoint (not yet implemented)
app.MapPost("/api/ingest", () => Results.StatusCode(501))
    .WithName("Ingest");

app.Run();
