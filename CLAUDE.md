# CLAUDE.md — Archie: Agentic RAG Engine

## Project Overview

**Archie** is an agentic RAG (Retrieval Augmented Generation) engine built on .NET 8+ and Semantic Kernel. Unlike simple retrieve-and-generate pipelines, Archie handles complex multi-step questions by decomposing queries, executing multiple retrieval passes, evaluating context sufficiency, and synthesizing answers with source attribution.

This is an exploratory engineering project. Many architectural decisions are intentionally unresolved — the goal is to reason through them, measure outcomes, and document findings.

---

## Tech Stack

- **Runtime:** .NET 8 (or latest stable LTS)
- **AI Orchestration:** Microsoft Semantic Kernel (latest stable NuGet)
- **LLM Provider:** Azure OpenAI Service (GPT-4o for synthesis, GPT-4o-mini for planning/evaluation)
- **Embeddings:** Azure OpenAI `text-embedding-3-small` (1536 dimensions)
- **Vector Store:** Azure AI Search (start here) — evaluate Qdrant or Pinecone later if needed
- **Document Parsing:** PdfPig for PDFs, custom markdown parser for .md files
- **Testing:** xUnit + FluentAssertions
- **Evaluation Harness:** Custom — see `/src/Archie.Eval/`
- **API Surface:** ASP.NET Core Minimal APIs
- **Configuration:** `appsettings.json` + Azure Key Vault for secrets in deployed environments; user-secrets for local dev

---

## Architecture

### Core Components

```
src/
├── Archie.Api/                  # Minimal API host — thin HTTP layer
├── Archie.Core/                 # Domain logic — no infrastructure dependencies
│   ├── Planning/                # Query decomposition and sub-query generation
│   ├── Retrieval/               # Retrieval strategies and orchestration
│   ├── Evaluation/              # Context sufficiency and answer quality scoring
│   ├── Synthesis/               # Final answer assembly with citations
│   └── Models/                  # Domain models and value objects
├── Archie.Infrastructure/       # Azure OpenAI, vector store, document parsing
│   ├── AzureOpenAI/             # LLM and embedding clients
│   ├── VectorStore/             # Azure AI Search implementation
│   └── DocumentParsing/         # PDF, markdown, text ingestion
├── Archie.Eval/                 # Evaluation pipeline — measures retrieval and answer quality
│   ├── Datasets/                # Test question sets with expected answers
│   ├── Metrics/                 # Precision, recall, faithfulness, relevance scorers
│   └── Reports/                 # Output evaluation reports as JSON
└── Archie.Tests/                # Unit and integration tests
    ├── Unit/
    └── Integration/
```

### Agent Loop (Core Pipeline)

This is the central design challenge. The pipeline for handling a query:

1. **Query Planner** — Receives the user's question. Decides whether it can be answered in a single retrieval pass or needs decomposition into sub-queries. Uses an LLM call with structured output.

2. **Retriever** — Executes retrieval for each sub-query. Supports multiple strategies:
   - **Vector similarity** (default) — embed query, find nearest chunks
   - **Hybrid** — combine vector similarity with keyword/BM25 scoring
   - **Graph traversal** (future) — follow relationships between chunks

3. **Context Evaluator** — Examines retrieved chunks and determines:
   - Is there enough relevant context to answer the question?
   - Are there gaps that need another retrieval pass?
   - Should the query be reformulated?
   - This is the hardest unsolved piece — start simple (LLM-as-judge) and iterate.

4. **Synthesis Engine** — Given sufficient context, generates the final answer with:
   - Source attribution (which chunks contributed to which claims)
   - Confidence indication
   - Identification of information gaps

5. **Loop Controller** — Orchestrates the above. Enforces:
   - Maximum retrieval passes (default: 3)
   - Token budget per query
   - Latency budget per query
   - Prevents infinite loops or runaway costs

### Key Design Decisions to Explore

These are open questions. Do not assume an answer — build, measure, decide:

- **Chunking strategy:** Fixed-size with overlap vs. semantic chunking vs. document-structure-aware chunking. Measure retrieval precision for each.
- **When to decompose:** What heuristics or LLM prompts reliably distinguish single-pass vs. multi-pass questions?
- **Re-ranking vs. re-retrieval:** When retrieved context is insufficient, is it better to re-rank existing results with a cross-encoder or execute a new retrieval with a reformulated query?
- **Evaluation without ground truth:** How do you measure answer quality when you don't have labeled datasets? Explore LLM-as-judge, faithfulness scoring (does the answer follow from the context?), and retrieval relevance scoring.

---

## Coding Conventions

### General

- **C# 12+ features** — use file-scoped namespaces, primary constructors, raw string literals, collection expressions where they improve clarity
- **Nullable reference types** enabled project-wide — no suppression without a comment explaining why
- **No `var` for non-obvious types** — use explicit types when the right-hand side doesn't make the type immediately clear
- **Async all the way** — never use `.Result` or `.Wait()`. All I/O-bound operations are `async Task<T>`
- **Cancellation tokens** — pass `CancellationToken` through every async chain. API endpoints should wire up request cancellation.
- **Guard clauses** over deep nesting — fail fast, return early
- **Sealed classes by default** — unseal only when inheritance is explicitly needed
- **Records for value objects and DTOs** — use `record` or `record struct` for immutable data carriers

### Architecture Rules

- **Archie.Core has zero infrastructure dependencies.** It must not reference Azure SDKs, HTTP clients, or database packages. All external integrations are defined as interfaces in Core and implemented in Infrastructure.
- **Dependency injection everywhere.** Register services in the composition root (`Archie.Api/Program.cs`). No `new`-ing up services inside other services.
- **No static state.** Everything flows through DI. This keeps the codebase testable.
- **One public class per file.** Internal helpers can share a file only if they're tightly coupled.

### Semantic Kernel Specifics

- Register Semantic Kernel services via `builder.Services.AddKernel()` in the composition root
- Define AI functions as `KernelFunction` instances with clear `[Description]` attributes
- Use `KernelArguments` for passing state through the pipeline — not ambient/static state
- Prefer Semantic Kernel's built-in `IChatCompletionService` over raw Azure OpenAI SDK calls
- Use `HandleBars` or `Liquid` prompt templates stored as embedded resources in `/Prompts/` — not inline string concatenation

### Naming

- Interfaces: `I{Name}` (e.g., `IQueryPlanner`, `IRetriever`, `IContextEvaluator`)
- Implementations: descriptive name (e.g., `LlmQueryPlanner`, `HybridRetriever`, `LlmContextEvaluator`)
- Async methods: suffix with `Async` (e.g., `PlanQueryAsync`, `RetrieveAsync`)
- Test classes: `{ClassUnderTest}Tests`
- Test methods: `{Method}_When{Condition}_Should{ExpectedResult}`

### Testing

- **Unit tests** for Core logic — mock infrastructure interfaces
- **Integration tests** for Infrastructure — use real Azure services with test resources
- **Eval tests** are separate from unit/integration tests — they measure quality, not correctness
- Use `ITestOutputHelper` for logging in tests
- Arrange-Act-Assert pattern, one assertion per concept (multiple related asserts are fine)

### Error Handling

- Use custom exception types in Core (e.g., `InsufficientContextException`, `TokenBudgetExceededException`)
- Infrastructure layer catches SDK-specific exceptions and wraps them
- API layer maps exceptions to appropriate HTTP status codes via middleware
- Log with structured logging (`ILogger<T>`) — include correlation IDs for tracing a query through the agent loop

---

## Development Phases

### Phase 1: Vertical Slice (Start Here)

Get a single query flowing end-to-end with the simplest possible implementation:

- Markdown document ingestion only
- Fixed-size chunking (512 tokens, 50 token overlap)
- Single vector similarity retrieval (no hybrid, no decomposition)
- Basic synthesis with source attribution
- No evaluation pipeline yet
- Minimal API with one POST endpoint: `POST /api/query`
- In-memory vector store option for local dev without Azure AI Search

**Done when:** You can ingest a set of markdown files and ask a question that returns a sourced answer.

### Phase 2: Agent Loop

Add the intelligence layer:

- Query planner that decomposes complex questions
- Context evaluator that triggers re-retrieval
- Loop controller with budget enforcement
- Structured logging of each agent step for debugging
- Basic evaluation: manually curated question/answer pairs, measure retrieval precision

**Done when:** A multi-hop question (one requiring info from 2+ document sections) gets a correct answer through decomposition that a single-pass retrieval would miss.

### Phase 3: Evaluation Pipeline

Build the measurement infrastructure:

- Curated eval dataset (20-50 question/answer pairs across difficulty levels)
- Automated metrics: retrieval precision@k, answer faithfulness, answer relevance
- LLM-as-judge scoring for answer quality
- JSON report output for tracking quality over time
- Compare chunking strategies quantitatively

**Done when:** You can make an architectural change (e.g., switch chunking strategy) and measure its impact on answer quality with a single command.

### Phase 4: Hybrid Retrieval and Optimization

Expand retrieval capabilities:

- Hybrid vector + keyword search
- Re-ranking with cross-encoder
- PDF document support
- Token and cost tracking per query
- Performance benchmarks (latency percentiles)

### Phase 5: Advanced (Future)

- Graph-based retrieval (chunk relationships)
- Multi-model routing (cheap model for simple queries, powerful model for complex ones)
- Streaming responses
- Caching layer for repeated sub-queries
- Web UI (Blazor or React) — only after the engine is solid

---

## Local Development Setup

### Prerequisites

- .NET 8 SDK (or latest LTS)
- Azure subscription with:
  - Azure OpenAI Service (GPT-4o and text-embedding-3-small deployed)
  - Azure AI Search (Free tier works for development)
- Visual Studio 2022, VS Code + C# Dev Kit, or JetBrains Rider
- Docker (optional — for local vector store alternatives)

### First Run

```bash
# Clone and navigate to repo
cd archie

# Restore dependencies
dotnet restore

# Set up user secrets for local dev
dotnet user-secrets init --project src/Archie.Api
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/" --project src/Archie.Api
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-key" --project src/Archie.Api
dotnet user-secrets set "AzureOpenAI:ChatDeployment" "gpt-4o" --project src/Archie.Api
dotnet user-secrets set "AzureOpenAI:EmbeddingDeployment" "text-embedding-3-small" --project src/Archie.Api
dotnet user-secrets set "AzureAISearch:Endpoint" "https://your-search.search.windows.net" --project src/Archie.Api
dotnet user-secrets set "AzureAISearch:ApiKey" "your-key" --project src/Archie.Api

# Run tests
dotnet test

# Start the API
dotnet run --project src/Archie.Api

# Ingest documents (once API is running)
curl -X POST http://localhost:5000/api/ingest \
  -H "Content-Type: application/json" \
  -d '{"directory": "./docs"}'

# Query
curl -X POST http://localhost:5000/api/query \
  -H "Content-Type: application/json" \
  -d '{"question": "What are the security implications of our auth approach?"}'
```

### Running Evaluations

```bash
dotnet run --project src/Archie.Eval -- --dataset ./eval/datasets/base.json --output ./eval/reports/
```

---

## Key Reminders for AI Agents

- **Do not over-abstract early.** Start with the simplest implementation that works. Refactor when you have tests and measurements that justify the complexity.
- **Every LLM call should have a clear prompt template** stored in a `/Prompts/` directory as an embedded resource. No prompt strings hardcoded in C# methods.
- **Measure before optimizing.** The evaluation pipeline exists for a reason. Do not change chunking, retrieval, or synthesis strategies without measuring the before and after.
- **Keep Archie.Core clean.** If you're tempted to add a NuGet package to Core, stop and define an interface instead. The implementation goes in Infrastructure.
- **Async and cancellation are non-negotiable.** Every I/O operation must be async with CancellationToken support.
- **Structured logging at every agent step.** Each loop iteration should log: step name, input, output, token count, latency. Use a correlation ID to trace a full query lifecycle.
- **When in doubt, write a test first.** Especially for Core logic.
- **Budget enforcement is a safety concern.** The loop controller must enforce max iterations, max tokens, and max latency. Runaway agent loops burn real money.
