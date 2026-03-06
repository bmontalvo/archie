# Archie

An agentic RAG (Retrieval-Augmented Generation) engine built on .NET 10 and Microsoft Semantic Kernel.

Unlike simple retrieve-and-generate pipelines, Archie handles complex multi-step questions by decomposing queries, executing multiple retrieval passes, evaluating whether the retrieved context is sufficient, and synthesizing answers with source attribution. The agent loop runs until it has enough context to answer confidently — or until it hits a configurable budget.

---

## How it works

A query flows through a loop of four cooperating components:

```
User question
     │
     ▼
┌─────────────────┐
│  Query Planner  │  Decides: single-pass or decompose into sub-queries?
└────────┬────────┘
         │ sub-queries
         ▼
┌─────────────────┐
│    Retriever    │  Vector similarity (+ hybrid BM25) search over indexed chunks
└────────┬────────┘
         │ chunks
         ▼
┌─────────────────────┐
│  Context Evaluator  │  Is this enough? If not, reformulate and retrieve again.
└────────┬────────────┘
         │ sufficient context
         ▼
┌──────────────────┐
│ Synthesis Engine │  Generates answer with source attribution + confidence
└──────────────────┘
         │
         ▼
    Sourced answer
```

The **Loop Controller** wraps the cycle and enforces hard limits on retrieval passes, token budget, and latency to prevent runaway costs.

---

## Tech stack

| Concern | Choice |
|---|---|
| Runtime | .NET 10 |
| AI orchestration | Microsoft Semantic Kernel 1.73 |
| LLM | Azure OpenAI (GPT-4o for synthesis, GPT-4o-mini for planning/evaluation) |
| Embeddings | Azure OpenAI `text-embedding-3-small` (1536 dimensions) |
| Vector store | In-memory (local dev) · Azure AI Search (production) |
| Document parsing | PdfPig (PDF), custom parser (Markdown) |
| Testing | xUnit 2.9 + FluentAssertions 8.8 |
| API | ASP.NET Core Minimal APIs |
| Configuration | `appsettings.json` + user-secrets (local) + Azure Key Vault (deployed) |

---

## Project structure

```
Archie.sln
requests/                    # .http files for manual API testing (REST Client)
src/
├── Archie.Api/              # Minimal API host — thin HTTP layer, composition root
├── Archie.Core/             # Domain logic — zero infrastructure dependencies
│   ├── Ingestion/           # IDocumentChunker, IEmbeddingService, IDocumentIngestionPipeline
│   ├── Planning/            # IQueryPlanner, QueryPlan
│   ├── Retrieval/           # IRetriever, IVectorStore
│   ├── Evaluation/          # IContextEvaluator, ContextEvaluation
│   ├── Synthesis/           # ISynthesizer
│   └── Models/              # DocumentChunk, RetrievedChunk, QueryRequest/Response, Citation
├── Archie.Infrastructure/   # Implementations of Core interfaces
│   ├── AzureOpenAI/         # LLM + embedding clients (SkEmbeddingService)
│   ├── VectorStore/         # InMemoryVectorStore (local) · Azure AI Search (planned)
│   ├── Retrieval/           # BasicRetriever
│   ├── Synthesis/           # BasicSynthesizer
│   ├── Ingestion/           # DocumentIngestionPipeline
│   ├── DocumentParsing/     # MarkdownDocumentChunker
│   └── Prompts/             # Embedded prompt templates (.txt)
├── Archie.Eval/             # Evaluation harness — measures retrieval and answer quality
│   ├── Datasets/            # Question/answer pairs
│   ├── Metrics/             # Precision@k, faithfulness, relevance scorers
│   └── Reports/             # JSON output for tracking quality over time
└── Archie.Tests/
    ├── Unit/                # Core logic tests — all infrastructure mocked
    └── Integration/         # Infrastructure tests — real Azure services
```

**Dependency rule:** `Archie.Core` has no NuGet dependencies beyond the BCL. All Azure and Semantic Kernel packages live in `Archie.Infrastructure` or `Archie.Api`. Interfaces are defined in Core and implemented in Infrastructure.

---

## Getting started

### Prerequisites

- .NET 10 SDK
- Azure subscription with Azure OpenAI Service deployed:
  - Chat model: GPT-4o
  - Embedding model: `text-embedding-3-small`
- Azure AI Search is **not required for local development** — the API uses an in-memory vector store by default.

> **Endpoint format:** Use the AI Foundry / Cognitive Services base URL — `https://<resource>.cognitiveservices.azure.com/` — not an `*.openai.azure.com` URL. The SDK constructs the full deployment path internally.

### Configure secrets

```bash
dotnet user-secrets init --project src/Archie.Api
dotnet user-secrets set "AzureOpenAI:Endpoint"            "https://your-resource.cognitiveservices.azure.com/" --project src/Archie.Api
dotnet user-secrets set "AzureOpenAI:ApiKey"              "your-key"                                           --project src/Archie.Api
dotnet user-secrets set "AzureOpenAI:ChatDeployment"      "gpt-4o"                                             --project src/Archie.Api
dotnet user-secrets set "AzureOpenAI:EmbeddingDeployment" "text-embedding-3-small"                             --project src/Archie.Api
```

### Build and run

```bash
dotnet restore
dotnet build
dotnet run --project src/Archie.Api
```

### Ingest documents and query

The easiest way to interact with the API is via the `.http` file in `requests/archie.http`. Open it in VS Code with the [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) extension — each request gets a clickable **Send Request** link. Edit the `@baseUrl` and `@docsDir` variables at the top of the file to match your setup.

Alternatively, with curl (use forward slashes on Windows):

```bash
# Ingest a directory of markdown files
curl -X POST http://localhost:5000/api/ingest \
  -H "Content-Type: application/json" \
  -d '{"directory": "C:/path/to/your/docs"}'

# Ask a question
curl -X POST http://localhost:5000/api/query \
  -H "Content-Type: application/json" \
  -d '{"question": "What are the security implications of our auth approach?"}'
```

### Run tests

```bash
dotnet test
```

### Run the evaluation harness

```bash
dotnet run --project src/Archie.Eval -- --dataset ./eval/datasets/base.json --output ./eval/reports/
```

---

## Development phases

| Phase | Goal | Status |
|---|---|---|
| **1 — Vertical slice** | Single query end-to-end: markdown ingestion, fixed-size chunking, vector retrieval, basic synthesis | **Complete** |
| **2 — Agent loop** | Query decomposition, context evaluation, re-retrieval, loop controller with budget enforcement | Planned |
| **3 — Evaluation pipeline** | Automated metrics (precision@k, faithfulness, relevance), LLM-as-judge, JSON reports | Planned |
| **4 — Hybrid retrieval** | Vector + BM25, cross-encoder re-ranking, PDF support, token/cost tracking | Planned |
| **5 — Advanced** | Graph retrieval, multi-model routing, streaming, caching, web UI | Future |

Phase 1 completion criteria: ingest a set of markdown files and ask a question that returns a sourced answer. ✓

---

## Open design questions

These are intentionally unresolved — the plan is to build, measure, then decide:

- **Chunking strategy** — Fixed-size with overlap vs. semantic chunking vs. document-structure-aware chunking. Measure retrieval precision for each before committing.
- **When to decompose** — What heuristics or prompts reliably distinguish single-pass vs. multi-pass questions?
- **Re-ranking vs. re-retrieval** — When context is insufficient, is it better to re-rank existing results with a cross-encoder, or execute a new retrieval with a reformulated query?
- **Evaluation without ground truth** — LLM-as-judge, faithfulness scoring (does the answer follow from the context?), retrieval relevance scoring — which combination is most reliable?

---

## Contributing conventions

- C# 12+ — file-scoped namespaces, primary constructors, records for DTOs
- Nullable reference types enabled everywhere — no suppression without an explanatory comment
- Async all the way — no `.Result` or `.Wait()`; `CancellationToken` through every async chain
- Sealed classes by default; one public type per file
- Prompt templates stored as embedded resources under `Prompts/` — no inline strings in C# methods
- Structured logging at every agent loop step with a correlation ID
