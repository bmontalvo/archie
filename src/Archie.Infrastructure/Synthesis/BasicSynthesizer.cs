using System.Reflection;
using System.Text;
using Archie.Core.Models;
using Archie.Core.Synthesis;
using Microsoft.SemanticKernel;

namespace Archie.Infrastructure.Synthesis;

public sealed class BasicSynthesizer : ISynthesizer
{
    private readonly Kernel _kernel;
    private readonly KernelFunction _synthesizeFunction;

    public BasicSynthesizer(Kernel kernel)
    {
        _kernel = kernel;
        string promptTemplate = LoadPromptTemplate();
        _synthesizeFunction = _kernel.CreateFunctionFromPrompt(
            promptTemplate,
            functionName: "Synthesize",
            description: "Synthesize an answer from retrieved context chunks.");
    }

    public async Task<QueryResponse> SynthesizeAsync(
        QueryRequest request,
        IReadOnlyList<RetrievedChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        string context = FormatContext(chunks);

        KernelArguments args = new();
        args["question"] = request.Question;
        args["context"] = context;

        FunctionResult result = await _kernel.InvokeAsync(_synthesizeFunction, args, cancellationToken);
        string answer = result.GetValue<string>() ?? string.Empty;

        IReadOnlyList<Citation> citations = [.. chunks.Select(c => new Citation(
            SourceFile: c.SourceDocument,
            ChunkIndex: c.ChunkIndex,
            Snippet: c.Content.Length > 200 ? c.Content[..200] + "..." : c.Content))];

        TokenUsage tokenUsage = ExtractTokenUsage(result);
        return new QueryResponse(answer, citations, tokenUsage);
    }

    private static string FormatContext(IReadOnlyList<RetrievedChunk> chunks)
    {
        if (chunks.Count == 0)
            return "(No context available)";

        StringBuilder sb = new();
        for (int i = 0; i < chunks.Count; i++)
        {
            RetrievedChunk chunk = chunks[i];
            sb.AppendLine($"[{i + 1}] Source: {chunk.SourceDocument}, Chunk {chunk.ChunkIndex} (relevance: {chunk.Score:F3})");
            sb.AppendLine(chunk.Content);
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private static TokenUsage ExtractTokenUsage(FunctionResult result)
    {
        // Best-effort extraction — SK stores Azure OpenAI usage under "Usage" as OpenAI.Chat.ChatTokenUsage.
        // Use reflection to avoid a hard compile-time dependency on the openai-dotnet SDK type.
        try
        {
            if (result.Metadata?.TryGetValue("Usage", out object? usageObj) == true && usageObj is not null)
            {
                Type usageType = usageObj.GetType();
                int? prompt = usageType.GetProperty("InputTokenCount")?.GetValue(usageObj) as int?;
                int? completion = usageType.GetProperty("OutputTokenCount")?.GetValue(usageObj) as int?;
                if (prompt.HasValue && completion.HasValue)
                    return new TokenUsage(prompt.Value, completion.Value);
            }
        }
        catch
        {
            // Token extraction is best-effort; never let it fail synthesis.
        }
        return new TokenUsage(0, 0);
    }

    private static string LoadPromptTemplate()
    {
        Assembly assembly = typeof(BasicSynthesizer).Assembly;
        const string resourceName = "Archie.Infrastructure.Prompts.Synthesize.txt";
        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            throw new InvalidOperationException($"Embedded resource '{resourceName}' not found in assembly '{assembly.FullName}'.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
