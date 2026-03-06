namespace Archie.Infrastructure.AzureOpenAI;

/// <summary>
/// Configuration for Azure OpenAI services. Bind from the "AzureOpenAI" config section.
/// </summary>
public sealed record AzureOpenAIOptions(
    string Endpoint,
    string ApiKey,
    string ChatDeployment,
    string EmbeddingDeployment);
