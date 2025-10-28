using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace AIRename.TextExtract;

public class TitleCandidate
{
    public string Text { get; set; } = string.Empty;
    public double Weight { get; set; }
    public double Confidence { get; set; }
    public bool IsConfident { get; set; }
    public int LineIndex { get; set; }
}

internal class TitleRules
{
    public int Version { get; set; } = 1;
    public double ConfidenceCutoff { get; set; } = 0.6;
    public int MaxCandidateLines { get; set; } = 20;
    public IdealLen IdealTitleLength { get; set; } = new IdealLen { Min = 6, Max = 40 };
    public Weights Weights { get; set; } = new Weights();
    public List<string> Stopwords { get; set; } = new List<string>();
    public Keywords Keywords { get; set; } = new Keywords();
    public List<string> Punctuation { get; set; } = new List<string>();
}

internal class IdealLen { public int Min { get; set; } = 6; public int Max { get; set; } = 40; }
internal class Weights
{
    public PositionWeights Position { get; set; } = new PositionWeights();
    public double UppercaseBonus { get; set; } = 0.25;
    public double KeywordBonus { get; set; } = 0.35;
    public double FilenameBonus { get; set; } = 0.35;
    public double LengthPenalty { get; set; } = 0.30;
    public double DigitPenalty { get; set; } = 0.30;
    public double PunctuationPenalty { get; set; } = 0.25;
    public double StopwordPenalty { get; set; } = 0.50;
}
internal class PositionWeights { public double Line1 { get; set; } = 1.0; public double Line2_3 { get; set; } = 0.8; public double Line4_5 { get; set; } = 0.6; public double Others { get; set; } = 0.4; }
internal class Keywords { public List<string> Zh { get; set; } = new List<string>(); public List<string> En { get; set; } = new List<string>(); }

public static class TitleHeuristics
{
    private static TitleRules? _rules;
    private static readonly object _lock = new object();

    public static TitleCandidate GetBestTitle(string filePath, string text)
    {
        var rules = LoadRules();
        var lines = SplitLinesForCandidates(text, rules.MaxCandidateLines);
        var filenameStem = GetFileStem(filePath);

        var weights = new List<double>();
        var candidates = new List<TitleCandidate>();

        for (int i = 0; i < lines.Count; i++)
        {
            var line = SanitizeLine(lines[i]);
            if (string.IsNullOrWhiteSpace(line)) { continue; }

            double w = 0.0;
            w += PositionWeight(i, rules.Weights.Position);

            w += UppercaseBonus(line) * rules.Weights.UppercaseBonus;
            w += KeywordBonus(line, rules.Keywords) * rules.Weights.KeywordBonus;
            w += FilenameBonus(line, filenameStem) * rules.Weights.FilenameBonus;

            w -= LengthPenalty(line.Length, rules.IdealTitleLength) * rules.Weights.LengthPenalty;
            w -= DigitPenalty(line) * rules.Weights.DigitPenalty;
            w -= PunctuationPenalty(line, rules.Punctuation) * rules.Weights.PunctuationPenalty;
            w -= StopwordPenalty(line, rules.Stopwords) * rules.Weights.StopwordPenalty;

            weights.Add(w);
            candidates.Add(new TitleCandidate { Text = line, Weight = w, LineIndex = i });
        }

        if (candidates.Count == 0)
        {
            return new TitleCandidate { Text = filenameStem, Weight = 0.0, Confidence = 1.0, IsConfident = true, LineIndex = -1 };
        }

        double avg = weights.Average();
        var best = candidates.OrderByDescending(c => c.Weight).First();
        var confidence = avg <= 1e-6 ? 1.0 : (best.Weight / avg);

        best.Confidence = confidence;
        best.IsConfident = confidence >= rules.ConfidenceCutoff;
        return best;
    }

    public static double GetConfidenceCutoff() => LoadRules().ConfidenceCutoff;

    private static TitleRules LoadRules()
    {
        if (_rules != null) return _rules;
        lock (_lock)
        {
            if (_rules != null) return _rules;
            var rules = GetDefault();
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var candidatePaths = new[]
                {
                    System.IO.Path.Combine(baseDir, "TitleRules.json"),
                    System.IO.Path.Combine(baseDir, "src", "TextExtract", "TitleRules.json"),
                    System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "src", "TextExtract", "TitleRules.json")
                };
                var path = candidatePaths.FirstOrDefault(System.IO.File.Exists);
                if (path != null)
                {
                    var json = System.IO.File.ReadAllText(path);
                    var loaded = JsonSerializer.Deserialize<TitleRules>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (loaded != null) rules = MergeDefaults(rules, loaded);
                }
            }
            catch
            {
                // 忽略配置读取错误，使用默认规则
            }
            _rules = rules;
            return _rules;
        }
    }

    private static TitleRules GetDefault()
    {
        return new TitleRules
        {
            ConfidenceCutoff = 0.6,
            MaxCandidateLines = 20,
            IdealTitleLength = new IdealLen { Min = 6, Max = 40 },
            Weights = new Weights
            {
                Position = new PositionWeights { Line1 = 1.0, Line2_3 = 0.8, Line4_5 = 0.6, Others = 0.4 },
                UppercaseBonus = 0.25, KeywordBonus = 0.35, FilenameBonus = 0.35,
                LengthPenalty = 0.30, DigitPenalty = 0.30, PunctuationPenalty = 0.25, StopwordPenalty = 0.50
            },
            Stopwords = new List<string> { "目录", "索引", "声明", "致谢", "摘要", "附录", "参考文献", "contents", "index", "appendix", "acknowledgements", "abstract", "references" },
            Keywords = new Keywords
            {
                Zh = new List<string> { "方案", "说明", "报告", "通知", "计划", "规定", "指引", "总结", "手册", "合同", "报价" },
                En = new List<string> { "Report", "Guideline", "Proposal", "Manual", "Contract", "Summary", "Plan", "Specification", "Notice" }
            },
            Punctuation = new List<string> { ".", ",", ";", ":", "!", "?", "，", "。", "；", "：", "！", "？" }
        };
    }

    private static TitleRules MergeDefaults(TitleRules defaults, TitleRules loaded)
    {
        loaded.Version = loaded.Version == 0 ? defaults.Version : loaded.Version;
        loaded.ConfidenceCutoff = loaded.ConfidenceCutoff == 0 ? defaults.ConfidenceCutoff : loaded.ConfidenceCutoff;
        loaded.MaxCandidateLines = loaded.MaxCandidateLines == 0 ? defaults.MaxCandidateLines : loaded.MaxCandidateLines;
        loaded.IdealTitleLength ??= defaults.IdealTitleLength;
        loaded.Weights ??= defaults.Weights;
        loaded.Stopwords = (loaded.Stopwords?.Count ?? 0) == 0 ? defaults.Stopwords : loaded.Stopwords!;
        loaded.Keywords ??= defaults.Keywords;
        loaded.Punctuation = (loaded.Punctuation?.Count ?? 0) == 0 ? defaults.Punctuation : loaded.Punctuation!;
        return loaded;
    }

    private static List<string> SplitLinesForCandidates(string text, int maxLines)
    {
        var lines = new List<string>();
        using var sr = new System.IO.StringReader(text ?? string.Empty);
        string? line;
        while ((line = sr.ReadLine()) != null && lines.Count < maxLines)
        {
            lines.Add(line.Trim());
        }
        return lines;
    }

    private static string GetFileStem(string filePath)
    {
        var stem = System.IO.Path.GetFileNameWithoutExtension(filePath);
        return Normalize(stem);
    }

    private static string SanitizeLine(string s)
    {
        s = s.Trim();
        s = s.Replace("\t", " ").Replace("  ", " ");
        return Normalize(s);
    }

    private static string Normalize(string s)
    {
        s = s.Trim();
        s = s.Replace("（", "(").Replace("）", ")").Replace("【", "[").Replace("】", "]");
        return s;
    }

    private static double PositionWeight(int index, PositionWeights pw)
    {
        if (index == 0) return pw.Line1;
        if (index <= 2) return pw.Line2_3;
        if (index <= 4) return pw.Line4_5;
        return pw.Others;
    }

    private static double UppercaseBonus(string line)
    {
        var letters = line.Where(char.IsLetter).ToArray();
        if (letters.Length == 0) return 0;
        var upper = letters.Count(char.IsUpper);
        var ratio = (double)upper / letters.Length;
        return ratio;
    }

    private static double KeywordBonus(string line, Keywords kw)
    {
        var bonus = 0.0;
        foreach (var k in kw.Zh) if (line.Contains(k, StringComparison.Ordinal)) { bonus += 1.0; break; }
        foreach (var k in kw.En) if (line.Contains(k, StringComparison.OrdinalIgnoreCase)) { bonus += 1.0; break; }
        return bonus > 0 ? 1.0 : 0.0;
    }

    private static double FilenameBonus(string line, string stem)
    {
        var l = StripNonWord(line).ToLowerInvariant();
        var s = StripNonWord(stem).ToLowerInvariant();
        if (string.IsNullOrEmpty(l) || string.IsNullOrEmpty(s)) return 0.0;

        if (s.Contains(l) || l.Contains(s)) return 1.0;

        var lw = l.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries).Distinct().ToHashSet();
        var sw = s.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries).Distinct().ToHashSet();
        if (lw.Count == 0 || sw.Count == 0) return 0.0;

        var inter = lw.Intersect(sw).Count();
        var union = lw.Union(sw).Count();
        var jaccard = union == 0 ? 0.0 : (double)inter / union;
        return jaccard;
    }

    private static string StripNonWord(string s)
    {
        var arr = s.Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch)).ToArray();
        return new string(arr);
    }

    private static double LengthPenalty(int len, IdealLen ideal)
    {
        if (len == 0) return 1.0;
        if (len < ideal.Min) return (double)(ideal.Min - len) / ideal.Min;
        if (len > ideal.Max) return (double)(len - ideal.Max) / ideal.Max;
        return 0.0;
    }

    private static double DigitPenalty(string line)
    {
        var digits = line.Count(char.IsDigit);
        var len = line.Length == 0 ? 1 : line.Length;
        var ratio = (double)digits / len;
        return ratio;
    }

    private static double PunctuationPenalty(string line, List<string> punc)
    {
        return punc.Any(p => line.Contains(p)) ? 1.0 : 0.0;
    }

    private static double StopwordPenalty(string line, List<string> stops)
    {
        return stops.Any(sw => line.Equals(sw, StringComparison.OrdinalIgnoreCase) || line.Contains(sw, StringComparison.OrdinalIgnoreCase)) ? 1.0 : 0.0;
    }
}