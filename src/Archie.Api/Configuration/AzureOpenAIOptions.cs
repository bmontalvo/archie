using System.ComponentModel.DataAnnotations;

public class AzureOpenAIOptions
{
    [Required]
    public string Endpoint { get; set; } = "";

    [Required]
    public string ApiKey { get; set; } = "";

    [Required]
    public string ChatDeploymentName { get; set; } = "";
}