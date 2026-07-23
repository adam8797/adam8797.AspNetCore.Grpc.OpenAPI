// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

namespace Grpc.Community.OpenApi.Internal.XmlComments;

/// <summary>
/// Cleans up the raw inner XML of a documentation comment for display:
/// removes shared leading whitespace, collapses <c>&lt;see cref="..." /&gt;</c>
/// references down to their simple name, and unwraps <c>&lt;para&gt;</c> paragraphs.
/// </summary>
internal static partial class XmlCommentsTextHelper
{
    public static string Humanize(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var trimmed = TrimIndent(text);
        trimmed = ReplaceSeeTagsRegex().Replace(trimmed, match =>
        {
            var cref = match.Groups["cref"].Value;
            var separatorIndex = cref.LastIndexOfAny(new[] { '.', ':' });
            return separatorIndex < 0 ? cref : cref.Substring(separatorIndex + 1);
        });
        trimmed = ReplaceParaTagsRegex().Replace(trimmed, "\n$1");

        return trimmed.Trim();
    }

    private static string TrimIndent(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var commonIndent = int.MaxValue;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            var indent = line.Length - line.TrimStart().Length;
            if (indent < commonIndent)
            {
                commonIndent = indent;
            }
        }

        if (commonIndent is 0 or int.MaxValue)
        {
            return text;
        }

        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length >= commonIndent)
            {
                lines[i] = lines[i].Substring(commonIndent);
            }
        }

        return string.Join("\n", lines);
    }

    [GeneratedRegex("<see\\s+cref\\s*=\\s*\"(?<cref>[^\"]+)\"\\s*/>")]
    private static partial Regex ReplaceSeeTagsRegex();

    [GeneratedRegex("<para>(.*?)</para>", RegexOptions.Singleline)]
    private static partial Regex ReplaceParaTagsRegex();
}
