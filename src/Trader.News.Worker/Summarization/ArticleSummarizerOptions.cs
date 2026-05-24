namespace Trader.News.Worker.Summarization;

/// <summary>
/// Configuration for the LLamaSharp-backed article summarizer.
/// Bound from the <c>ArticleSummarizer</c> configuration section.
/// </summary>
public sealed class ArticleSummarizerOptions
{
    /// <summary>Path to the GGUF model file (e.g. qwen2.5-1.5b-instruct-q4_k_m.gguf).</summary>
    public string ModelPath { get; set; } = string.Empty;

    /// <summary>Context window size in tokens. Default 2048.</summary>
    public uint ContextSize { get; set; } = 2048;

    /// <summary>Maximum tokens to generate for the summary. Default 150.</summary>
    public int MaxSummaryTokens { get; set; } = 150;

    /// <summary>Number of model layers to offload to GPU. 0 = CPU-only. Default 20.</summary>
    public int GpuLayerCount { get; set; } = 20;

    /// <summary>System prompt sent before the article text.</summary>
    public string SystemPrompt { get; set; } =
        "You are a news summarizer. Summarize the following article in 2-3 concise sentences, " +
        "preserving the main facts. Respond only with the summary, no preamble or extra commentary.";
}
