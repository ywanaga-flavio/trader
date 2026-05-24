using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Trader.News.Data.Enums;

namespace Trader.News.Worker.Analysis;

/// <summary>
/// Zero-shot NLI news analysis using an ONNX-format mDeBERTa-v3 model.
/// See <c>resources/models/classification/README.md</c> for download instructions.
///
/// Both topic classification (<see cref="NewsClassification"/>) and sentiment
/// (<see cref="NewsValuation"/>) are resolved via NLI: for each candidate label a
/// forward pass is run with (article_text, hypothesis), and the label with the
/// highest entailment score wins.
///
/// Gracefully degrades: returns <c>null</c> when the model files are absent.
/// </summary>
public sealed class OnnxNliAnalysisService : INewsAnalysisService, IDisposable
{
    private readonly ILogger<OnnxNliAnalysisService> _logger;
    private readonly InferenceSession? _session;
    private readonly UnigramTokenizer? _tokenizer;
    private readonly bool _isAvailable;

    // mDeBERTa-v3 special token IDs (DeBERTa-v3-base sentencepiece vocabulary).
    // These match the values in tokenizer_config.json for MoritzLaurer/mDeBERTa-v3-base-mnli-xnli.
    private const long ClsTokenId = 1L;
    private const long SepTokenId = 2L;
    private const int MaxSequenceLength = 512;

    // NLI output label order for MoritzLaurer/mDeBERTa-v3-base-mnli-xnli:
    // id2label: {0: entailment, 1: neutral, 2: contradiction}
    private const int EntailmentIndex = 0;

    // Templates applied to each candidate label before NLI inference.
    // Matches the format used by HuggingFace zero-shot-classification pipeline.
    private const string HypothesisTemplateEn = "This example is {0}.";
    private const string HypothesisTemplateEs = "Este texto es {0}.";

    private static readonly (NewsClassification Label, string LabelKeyEn, string LabelKeyEs)[] ClassificationCandidates =
    [
        (NewsClassification.Market,        "about financial markets, stocks, or trading",           "sobre mercados financieros, acciones o trading"),
        (NewsClassification.Economic,      "about economics, inflation, GDP, or monetary policy",   "sobre economía, inflación, PIB o política monetaria"),
        (NewsClassification.Political,     "about politics, government, elections, or congress",    "sobre política, gobierno, elecciones o congreso"),
        (NewsClassification.Technology,    "about technology, artificial intelligence, or software", "sobre tecnología, inteligencia artificial o software"),
        (NewsClassification.Corporate,     "about a company, its earnings, or a merger",            "sobre una empresa, sus ganancias o una fusión"),
        (NewsClassification.International, "about international politics, war, or foreign affairs",  "sobre política internacional, guerra o relaciones exteriores"),
    ];

    private static readonly (NewsValuation Label, string LabelKeyEn, string LabelKeyEs)[] SentimentCandidates =
    [
        (NewsValuation.Positive, "positive news for financial markets",    "noticias positivas para los mercados financieros"),
        (NewsValuation.Negative, "negative news for financial markets",    "noticias negativas para los mercados financieros"),
        (NewsValuation.Neutral,  "neutral or merely informational news",   "noticias neutrales o meramente informativas"),
    ];

    public OnnxNliAnalysisService(IConfiguration config, ILogger<OnnxNliAnalysisService> logger)
    {
        _logger = logger;

        // Default path: <workspace_root>/resources/models/classification
        // Override with NewsAnalysis:ModelPath in appsettings.json or environment variable.
        var modelDir = config["NewsAnalysis:ModelPath"]
            ?? ResolveDefaultModelDir();

        var modelFile     = Path.Combine(modelDir, "model.onnx");
        var tokenizerFile = Path.Combine(modelDir, "tokenizer.json");

        if (!File.Exists(modelFile) || !File.Exists(tokenizerFile))
        {
            _logger.LogWarning(
                "ONNX model or tokenizer not found at {ModelDir}. " +
                "Follow resources/models/classification/README.md to download the files. " +
                "News analysis will be skipped until model files are present.",
                modelDir);
            _isAvailable = false;
            return;
        }

        try
        {
            var sessionOptions = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                EnableMemoryPattern    = true,
            };

            _session = new InferenceSession(modelFile, sessionOptions);

            _tokenizer = new UnigramTokenizer(tokenizerFile);

            _isAvailable = true;
            _logger.LogInformation("ONNX news analysis model loaded from {ModelDir}.", modelDir);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load ONNX analysis model. News analysis will be skipped.");
            _isAvailable = false;
        }
    }

    /// <inheritdoc />
    public Task<NewsAnalysisResult?> AnalyzeAsync(
        string title,
        string? summary,
        CancellationToken cancellationToken = default)
    {
        if (!_isAvailable)
            return Task.FromResult<NewsAnalysisResult?>(null);

        try
        {
            // Combine title and summary as the NLI premise.
            var text = string.IsNullOrWhiteSpace(summary)
                ? title
                : $"{title}. {summary}";

            bool isSpanish = IsSpanish(text);
            var (classLabel, classScore) = RunZeroShotNli(text, ClassificationCandidates, isSpanish);
            var (sentLabel, sentScore)   = RunZeroShotNli(text, SentimentCandidates, isSpanish);

            return Task.FromResult<NewsAnalysisResult?>(new NewsAnalysisResult(
                ClassificationId:    (int)classLabel,
                ClassificationScore: classScore,
                SentimentId:         (int)sentLabel,
                SentimentScore:      sentScore));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NLI inference failed; item will be stored without analysis fields.");
            return Task.FromResult<NewsAnalysisResult?>(null);
        }
    }

    // ── Internals ────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs zero-shot NLI over all <paramref name="candidates"/> and returns the label
    /// with the highest entailment score.
    /// </summary>
    private (TLabel BestLabel, double BestScore) RunZeroShotNli<TLabel>(
        string premise,
        (TLabel Label, string LabelKeyEn, string LabelKeyEs)[] candidates,
        bool isSpanish)
    {
        string template  = isSpanish ? HypothesisTemplateEs : HypothesisTemplateEn;
        TLabel bestLabel = candidates[0].Label;
        double bestScore = -1.0;

        foreach (var (label, labelKeyEn, labelKeyEs) in candidates)
        {
            string labelKey = isSpanish ? labelKeyEs : labelKeyEn;
            var score = RunNliForward(premise, labelKey, template);
            if (score > bestScore)
            {
                bestScore = score;
                bestLabel = label;
            }
        }

        return (bestLabel, bestScore);
    }

    /// <summary>
    /// Runs a single NLI forward pass and returns the softmax entailment probability.
    /// </summary>
    private double RunNliForward(string premise, string labelKey, string hypothesisTemplate)
    {
        var hypothesis = string.Format(hypothesisTemplate, labelKey);
        var (inputIds, attentionMask) = Tokenize(premise, hypothesis);

        int seqLen = inputIds.Length;
        var shape  = new int[] { 1, seqLen };

        // DeBERTa-V3 uses disentangled attention — no token_type_ids input.
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids",      new DenseTensor<long>(inputIds,      shape)),
            NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(attentionMask, shape)),
        };

        using var results = _session!.Run(inputs);
        var logits = results.First(r => r.Name == "logits").AsEnumerable<float>().ToArray();

        // Numerically stable softmax.
        float max = logits.Max();
        double[] expLogits = logits.Select(l => Math.Exp(l - max)).ToArray();
        double sumExp = expLogits.Sum();

        return expLogits[EntailmentIndex] / sumExp;
    }

    /// <summary>
    /// Encodes the NLI input pair as: <c>[CLS] premise [SEP] hypothesis [SEP]</c>.
    /// </summary>
    private (long[] InputIds, long[] AttentionMask) Tokenize(
        string premise, string hypothesis)
    {
        var premiseIds    = _tokenizer!.Encode(premise);
        var hypothesisIds = _tokenizer.Encode(hypothesis);

        // Build token sequence: [CLS] premise [SEP] hypothesis [SEP]
        var inputIds = new List<long>(premiseIds.Count + hypothesisIds.Count + 3)
        {
            ClsTokenId,
        };
        inputIds.AddRange(premiseIds.Select(id => (long)id));
        inputIds.Add(SepTokenId);
        inputIds.AddRange(hypothesisIds.Select(id => (long)id));
        inputIds.Add(SepTokenId);

        // Truncate to MaxSequenceLength, preserving the trailing SEP.
        if (inputIds.Count > MaxSequenceLength)
        {
            inputIds = [.. inputIds.Take(MaxSequenceLength - 1), SepTokenId];
        }

        var attentionMask = Enumerable.Repeat(1L, inputIds.Count).ToArray();
        return (inputIds.ToArray(), attentionMask);
    }

    /// <summary>
    /// Heuristic language detector. Returns <c>true</c> when the text is likely Spanish,
    /// based on accented characters (fast path) or high-frequency Spanish function words.
    /// Falls back to <c>false</c> (English/other) when neither signal is present.
    /// </summary>
    private static bool IsSpanish(string text)
    {
        // Fast path: accented characters that are exclusive to Spanish.
        foreach (char c in text)
        {
            if (c is 'á' or 'é' or 'í' or 'ó' or 'ú' or 'ñ' or 'ü' or
                     'Á' or 'É' or 'Í' or 'Ó' or 'Ú' or 'Ñ' or 'Ü')
                return true;
        }

        // Slower path: count common Spanish function words.
        string[] words = text.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int hits = 0;
        foreach (string raw in words)
        {
            string w = raw.TrimEnd('.', ',', ':', ';', '!', '?', '"', '\'', ')');
            if (w is "el" or "la" or "de" or "que" or "en" or "un" or "una" or
                     "los" or "las" or "por" or "con" or "del" or "se" or "al" or
                     "es" or "su" or "para" or "como" or "pero" or "son" or "fue")
                hits++;
        }
        return hits >= 2;
    }

    private static string ResolveDefaultModelDir()
    {
        // Walk up from the binary output directory to the solution/workspace root,
        // then navigate to resources/models/classification.
        var baseDir = AppContext.BaseDirectory;
        var candidate = Path.GetFullPath(
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "resources", "models", "classification"));
        return candidate;
    }

    public void Dispose() => _session?.Dispose();
}
