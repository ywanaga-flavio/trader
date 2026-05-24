using LLama;
using LLama.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Trader.News.Worker.Summarization;

/// <summary>
/// Summarizes article text using a local GGUF model via LLamaSharp.
/// Falls back gracefully when the model file is not present or fails to load.
/// </summary>
public sealed class LlamaArticleSummarizerService : IArticleSummarizerService, IDisposable
{
    private readonly ILogger<LlamaArticleSummarizerService> _logger;
    private readonly ArticleSummarizerOptions _options;
    private readonly LLamaWeights? _weights;
    private ModelParams _modelParams = null!;

    /// <inheritdoc/>
    public bool IsAvailable => _weights is not null;

    public LlamaArticleSummarizerService(
        IOptions<ArticleSummarizerOptions> options,
        ILogger<LlamaArticleSummarizerService> logger)
    {
        _logger = logger;
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.ModelPath) || !File.Exists(_options.ModelPath))
        {
            _logger.LogWarning(
                "ArticleSummarizer model not found at '{ModelPath}'. Summarization disabled.",
                _options.ModelPath);
            return;
        }

        _modelParams = new ModelParams(_options.ModelPath)
        {
            ContextSize  = _options.ContextSize,
            GpuLayerCount = _options.GpuLayerCount,
        };

        try
        {
            _weights = LLamaWeights.LoadFromFile(_modelParams);
            _logger.LogInformation(
                "LLamaSharp model loaded from '{ModelPath}' (ctx={ContextSize}, gpu={GpuLayers}).",
                _options.ModelPath, _options.ContextSize, _options.GpuLayerCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load LLamaSharp model from '{ModelPath}'.", _options.ModelPath);
        }
    }

    /// <inheritdoc/>
    public async Task<string?> SummarizeAsync(string text, CancellationToken cancellationToken = default)
    {
        if (_weights is null) return null;

        try
        {
            var executor = new StatelessExecutor(_weights, _modelParams, _logger);

            var prompt = $"{_options.SystemPrompt}\n\nArticle:\n{text}\n\nSummary:";

            var inferenceParams = new InferenceParams
            {
                MaxTokens   = _options.MaxSummaryTokens,
                AntiPrompts = ["Article:", "\n\n"],
            };

            var sb = new System.Text.StringBuilder();
            await foreach (var token in executor.InferAsync(prompt, inferenceParams, cancellationToken))
            {
                sb.Append(token);
            }

            var summary = sb.ToString().Trim();
            return summary.Length == 0 ? null : summary;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Summarization inference failed.");
            return null;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _weights?.Dispose();
    }
}
