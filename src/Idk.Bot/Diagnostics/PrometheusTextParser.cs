using System.Globalization;
using System.Text;

namespace Idk.Bot.Diagnostics;

public sealed class PrometheusTextParser
{
    public MetricsSnapshot Parse(ServerDefinition server, DateTimeOffset capturedAt, string text)
    {
        var samples = new Dictionary<PrometheusMetricIdentity, PrometheusMetricSample>();

        foreach (var rawLine in ReadLines(text))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;

            if (!TryParseLine(line, out var sample))
                continue;

            samples[sample.Identity] = sample;
        }

        return new MetricsSnapshot(server, capturedAt, samples);
    }

    private static bool TryParseLine(string line, out PrometheusMetricSample sample)
    {
        sample = default!;

        var separator = IndexOfMetricSeparator(line);
        if (separator <= 0)
            return false;

        var nameAndLabels = line[..separator];
        var rest = line[separator..].TrimStart();
        if (rest.Length == 0)
            return false;

        var valueToken = rest.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
        if (!double.TryParse(valueToken, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
            double.IsNaN(value) ||
            double.IsInfinity(value))
        {
            return false;
        }

        var labels = ParseLabels(nameAndLabels, out var name);
        var labelsKey = BuildLabelsKey(labels);
        var identity = new PrometheusMetricIdentity(name, labelsKey);
        sample = new PrometheusMetricSample(identity, name, labels, value);
        return true;
    }

    private static IReadOnlyDictionary<string, string> ParseLabels(string nameAndLabels, out string name)
    {
        var brace = nameAndLabels.IndexOf('{');
        if (brace < 0)
        {
            name = nameAndLabels;
            return new Dictionary<string, string>();
        }

        name = nameAndLabels[..brace];
        var endBrace = nameAndLabels.LastIndexOf('}');
        if (endBrace <= brace)
            return new Dictionary<string, string>();

        var labelText = nameAndLabels[(brace + 1)..endBrace];
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var part in SplitLabelParts(labelText))
        {
            var equals = part.IndexOf('=');
            if (equals <= 0)
                continue;

            var labelName = part[..equals].Trim();
            var labelValue = part[(equals + 1)..].Trim();
            if (labelValue.Length >= 2 && labelValue[0] == '"' && labelValue[^1] == '"')
                labelValue = UnescapeLabelValue(labelValue[1..^1]);

            labels[labelName] = labelValue;
        }

        return labels;
    }

    private static IEnumerable<string> SplitLabelParts(string labelText)
    {
        var start = 0;
        var inString = false;
        var escaped = false;

        for (var i = 0; i < labelText.Length; i++)
        {
            var c = labelText[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (c != ',' || inString)
                continue;

            yield return labelText[start..i];
            start = i + 1;
        }

        yield return labelText[start..];
    }

    private static string UnescapeLabelValue(string value)
    {
        var builder = new StringBuilder(value.Length);
        var escaped = false;

        foreach (var c in value)
        {
            if (!escaped)
            {
                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                builder.Append(c);
                continue;
            }

            builder.Append(c switch
            {
                'n' => '\n',
                '\\' => '\\',
                '"' => '"',
                _ => c,
            });
            escaped = false;
        }

        if (escaped)
            builder.Append('\\');

        return builder.ToString();
    }

    private static string BuildLabelsKey(IReadOnlyDictionary<string, string> labels)
    {
        if (labels.Count == 0)
            return string.Empty;

        return string.Join(",", labels
            .OrderBy(label => label.Key, StringComparer.Ordinal)
            .Select(label => $"{label.Key}={label.Value}"));
    }

    private static int IndexOfMetricSeparator(string value)
    {
        var inLabels = false;
        var inString = false;
        var escaped = false;

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                escaped = true;
                continue;
            }

            if (c == '{' && !inString)
            {
                inLabels = true;
                continue;
            }

            if (c == '}' && !inString)
            {
                inLabels = false;
                continue;
            }

            if (c == '"' && inLabels)
            {
                inString = !inString;
                continue;
            }

            if (!inLabels && char.IsWhiteSpace(c))
                return i;
        }

        return -1;
    }

    private static IEnumerable<string> ReadLines(string text)
    {
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
            yield return line;
    }
}
