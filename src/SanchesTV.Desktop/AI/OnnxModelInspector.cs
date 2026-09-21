using System.IO;
using Microsoft.ML.OnnxRuntime;

namespace SanchesTV.Desktop.AI;

public sealed class OnnxModelInspector
{
    public string Inspect(string modelPath)
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException("Modelo ONNX não encontrado.", modelPath);

        using var session = new InferenceSession(modelPath);
        var lines = new List<string>
        {
            $"Modelo: {Path.GetFileName(modelPath)}",
            $"Inputs: {session.InputMetadata.Count}",
            $"Outputs: {session.OutputMetadata.Count}"
        };

        foreach (var input in session.InputMetadata)
            lines.Add($"IN  {input.Key}: {input.Value.ElementType.Name} [{string.Join(" × ", input.Value.Dimensions)}]");

        foreach (var output in session.OutputMetadata)
            lines.Add($"OUT {output.Key}: {output.Value.ElementType.Name} [{string.Join(" × ", output.Value.Dimensions)}]");

        return string.Join(Environment.NewLine, lines);
    }

    public string RuntimeVersion => typeof(InferenceSession).Assembly.GetName().Version?.ToString() ?? "?";
}
