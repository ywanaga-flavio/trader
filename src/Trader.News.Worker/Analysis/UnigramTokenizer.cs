using System.Text;
using System.Text.Json;

namespace Trader.News.Worker.Analysis;

/// <summary>
/// Pure C# SentencePiece Unigram tokenizer loaded from a HuggingFace
/// <c>tokenizer.json</c> file.
///
/// Supports mDeBERTa-v3 and similar multilingual models that use the
/// Unigram LM algorithm with Metaspace pre-tokenisation
/// (<c>prepend_scheme="always"</c>, <c>replacement="▁"</c>).
///
/// Encoding pipeline:
/// <list type="number">
///   <item>NFKC Unicode normalisation.</item>
///   <item>Metaspace: whitespace runs are replaced by ▁ (U+2581); ▁ is
///         always prepended to the beginning of the sequence.</item>
///   <item>Viterbi dynamic-programming segmentation that maximises the sum
///         of token log-probabilities.  Unknown characters fall back to
///         individual UTF-8 byte tokens (indices 4–259).</item>
/// </list>
/// </summary>
internal sealed class UnigramTokenizer
{
    /// <summary>Maximum number of Unicode characters considered for a single token.
    /// Caps inner-loop iterations; most tokens are well under this limit.</summary>
    private const int MaxTokenLen = 32;

    /// <summary>
    /// Byte-fallback tokens &lt;0x00&gt;…&lt;0xFF&gt; start at vocab index 4
    /// in the mDeBERTa-v3 vocabulary (indices 0-3 are [PAD],[CLS],[SEP],[UNK]).
    /// </summary>
    private const int ByteFallbackOffset = 4;

    private readonly float[] _scores;
    private readonly Dictionary<string, int> _pieceToId;

    /// <summary>
    /// Loads the vocabulary from the HuggingFace <c>tokenizer.json</c> at
    /// <paramref name="tokenizerJsonPath"/>.
    /// </summary>
    public UnigramTokenizer(string tokenizerJsonPath)
    {
        using var stream = File.OpenRead(tokenizerJsonPath);
        using var doc    = JsonDocument.Parse(stream);

        var vocabArray = doc.RootElement
            .GetProperty("model")
            .GetProperty("vocab");

        int count = vocabArray.GetArrayLength();
        _scores    = new float[count];
        _pieceToId = new Dictionary<string, int>(count, StringComparer.Ordinal);

        int id = 0;
        foreach (var entry in vocabArray.EnumerateArray())
        {
            string piece = entry[0].GetString()!;
            float  score = entry[1].GetSingle();
            _scores[id] = score;
            _pieceToId.TryAdd(piece, id); // keep first occurrence on any collision
            id++;
        }
    }

    /// <summary>
    /// Encodes <paramref name="text"/> to a list of token IDs
    /// (without any special tokens such as [CLS] or [SEP]).
    /// </summary>
    public List<int> Encode(string text)
    {
        // Step 1 — NFKC normalisation.
        var normalized = text.Normalize(NormalizationForm.FormKC);

        // Step 2 — Metaspace pre-tokenisation.
        var metaspaced = ApplyMetaspace(normalized);

        // Step 3 — Viterbi segmentation.
        return Viterbi(metaspaced);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Replaces whitespace runs with ▁ and unconditionally prepends ▁ at the
    /// beginning of the sequence (<c>prepend_scheme="always"</c>).
    /// </summary>
    private static string ApplyMetaspace(string text)
    {
        var sb = new StringBuilder(text.Length + 2);
        sb.Append('\u2581');      // leading ▁
        bool prevWasSpace = true; // skip double-▁ if text itself starts with a space

        foreach (char c in text)
        {
            if (c is ' ' or '\t' or '\r' or '\n')
            {
                if (!prevWasSpace) { sb.Append('\u2581'); prevWasSpace = true; }
            }
            else
            {
                sb.Append(c);
                prevWasSpace = false;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Viterbi dynamic programming over the normalised character sequence.
    /// Returns a list of vocab token IDs.
    /// </summary>
    private List<int> Viterbi(string text)
    {
        int n    = text.Length;
        var dp   = new float[n + 1];
        var back = new int[n + 1];

        Array.Fill(dp, float.NegativeInfinity);
        dp[0] = 0f;

        for (int i = 0; i < n; i++)
        {
            float cur = dp[i];
            if (float.IsNegativeInfinity(cur)) continue;

            int maxLen = Math.Min(MaxTokenLen, n - i);
            for (int len = 1; len <= maxLen; len++)
            {
                string piece = text.Substring(i, len);
                if (_pieceToId.TryGetValue(piece, out int vid))
                {
                    float candidate = cur + _scores[vid];
                    if (candidate > dp[i + len])
                    {
                        dp[i + len] = candidate;
                        back[i + len] = i;
                    }
                }
            }

            // Byte fallback: if position i+1 is still unreachable, consume one char
            // with a heavy penalty so it is only used as a last resort.
            if (float.IsNegativeInfinity(dp[i + 1]))
            {
                dp[i + 1] = cur - 100f;
                back[i + 1] = i;
            }
        }

        // Reconstruct token IDs from backpointers (built in reverse, then flipped).
        var result = new List<int>();
        for (int pos = n; pos > 0;)
        {
            int    start = back[pos];
            string piece = text.Substring(start, pos - start);

            if (_pieceToId.TryGetValue(piece, out int id))
            {
                result.Add(id);
            }
            else
            {
                // Emit one byte-fallback token per UTF-8 byte (reversed; corrected by Reverse below).
                byte[] bytes = Encoding.UTF8.GetBytes(piece);
                for (int b = bytes.Length - 1; b >= 0; b--)
                    result.Add(ByteFallbackOffset + bytes[b]);
            }

            pos = start;
        }

        result.Reverse();
        return result;
    }
}
