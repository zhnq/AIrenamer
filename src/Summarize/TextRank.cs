using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AIRename.Summarize;

public static class TextRank
{
    // 简易中文/英文分词：
    // - 连续中文字符视为一个词
    // - 英文/数字按 \w 分组并转小写
    private static readonly Regex EnToken = new Regex("[A-Za-z0-9_]+", RegexOptions.Compiled);
    private static readonly Regex CnToken = new Regex("[\u4e00-\u9fa5]+", RegexOptions.Compiled);

    public static List<(string Token, double Score)> ExtractTopN(string text, int topN = 20, int window = 4, int maxTokens = 2000)
    {
        var tokens = Tokenize(text).Take(maxTokens).ToList();
        if (tokens.Count == 0) return new List<(string, double)>();

        // 构建共现图
        var graph = new Dictionary<string, HashSet<string>>();
        for (int i = 0; i < tokens.Count; i++)
        {
            var w = tokens[i];
            if (!graph.TryGetValue(w, out var neighbors))
            {
                neighbors = new HashSet<string>();
                graph[w] = neighbors;
            }
            for (int j = i + 1; j < Math.Min(i + window, tokens.Count); j++)
            {
                if (tokens[i] == tokens[j]) continue;
                neighbors.Add(tokens[j]);
                if (!graph.TryGetValue(tokens[j], out var back))
                {
                    back = new HashSet<string>();
                    graph[tokens[j]] = back;
                }
                back.Add(tokens[i]);
            }
        }

        // 初始化分数
        const double d = 0.85; // 阻尼系数
        var scores = graph.Keys.ToDictionary(k => k, k => 1.0);

        // 迭代计算 PageRank
        for (int iter = 0; iter < 20; iter++)
        {
            var newScores = new Dictionary<string, double>(scores.Count);
            foreach (var v in graph.Keys)
            {
                double sum = 0.0;
                foreach (var u in graph[v])
                {
                    var deg = graph[u].Count;
                    if (deg > 0) sum += scores[u] / deg;
                }
                newScores[v] = (1 - d) + d * sum;
            }
            scores = newScores;
        }

        // 过滤过短、纯数字、停用词候选（简单规则）
        bool IsValid(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return false;
            if (t.Length < 2) return false;
            if (t.All(char.IsDigit)) return false;
            return true;
        }

        var result = scores
            .Where(kv => IsValid(kv.Key))
            .OrderByDescending(kv => kv.Value)
            .Take(topN)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();
        return result;
    }

    public static List<string> Tokenize(string text)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return list;

        foreach (Match m in CnToken.Matches(text))
        {
            list.Add(m.Value);
        }
        foreach (Match m in EnToken.Matches(text))
        {
            list.Add(m.Value.ToLowerInvariant());
        }
        return list;
    }
}