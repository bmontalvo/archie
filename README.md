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
| Vector store | Azure AI Search |
| Document parsing | PdfPig (PDF), custom parser (Markdown) |
| Testing | xUnit 2.9 + FluentAssertions 8.8 |
| API | ASP.NET Core Minimal APIs |
| Configuration | `appsettings.json` + user-secrets (local) + Azure Key Vault (deployed) |

---

## Project structure

```
Archie.sln
src/
├── Archie.Api/              # Minimal API host — thin HTTP layer, composition root
├── Archie.Core/             # Domain logic — zero infrastructure dependencies
│   ├── Planning/            # IQueryPlanner, QueryPlan
│   ├── Retrieval/           # IRetriever
│   ├── Evaluation/          # IContextEvaluator, ContextEvaluation
│   ├── Synthesis/           # ISynthesisEngine
│   └── Models/              # RetrievedChunk, AnswerResult
├── Archie.Infrastructure/   # Implementations of Core interfaces
│   ├── AzureOpenAI/         # LLM + embedding clients
│   ├── VectorStore/         # Azure AI Search implementation
│   └── DocumentParsing/     # PDF + Markdown ingestion
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
- Azure subscription with:
  - Azure OpenAI Service (GPT-4o and `text-embedding-3-small` deployed)
  - Azure AI Search (Free tier works for local development)

### Configure secrets

```bash
dotnet user-secrets init --project src/Archie.Api
dotnet user-secrets set "AzureOpenAI:Endpoint"           "https://your-resource.openai.azure.com/" --project src/Archie.Api
dotnet user-secrets set "AzureOpenAI:ApiKey"             "your-key"                                 --project src/Archie.Api
dotnet user-secrets set "AzureOpenAI:ChatDeployment"     "gpt-4o"                                   --project src/Archie.Api
dotnet user-secrets set "AzureOpenAI:EmbeddingDeployment" "text-embedding-3-small"                  --project src/Archie.Api
dotnet user-secrets set "AzureAISearch:Endpoint"         "https://your-search.search.windows.net"   --project src/Archie.Api
dotnet user-secrets set "AzureAISearch:ApiKey"           "your-key"                                 --project src/Archie.Api
```

### Build and run

```bash
dotnet restore
dotnet build
dotnet run --project src/Archie.Api
```

### Ingest documents and query

```bash
# Ingest a directory of markdown files
curl -X POST http://localhost:5000/api/ingest \
  -H "Content-Type: application/json" \
  -d '{"directory": "./docs"}'

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
| **1 — Vertical slice** | Single query end-to-end: markdown ingestion, fixed-size chunking, vector retrieval, basic synthesis | In progress |
| **2 — Agent loop** | Query decomposition, context evaluation, re-retrieval, loop controller with budget enforcement | Planned |
| **3 — Evaluation pipeline** | Automated metrics (precision@k, faithfulness, relevance), LLM-as-judge, JSON reports | Planned |
| **4 — Hybrid retrieval** | Vector + BM25, cross-encoder re-ranking, PDF support, token/cost tracking | Planned |
| **5 — Advanced** | Graph retrieval, multi-model routing, streaming, caching, web UI | Future |

Phase 1 is done when: you can ingest a set of markdown files and ask a question that returns a sourced answer.

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
