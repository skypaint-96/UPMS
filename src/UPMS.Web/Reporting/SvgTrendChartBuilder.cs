namespace UPMS.Web.Reporting;

using System.Globalization;
using System.Text;

public static class SvgTrendChartBuilder
{
    public static string BuildLifecycleTrendChart(IReadOnlyList<LifecycleMonthlyPoint> points, string title)
    {
        if (points.Count == 0)
            return "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"800\" height=\"220\"><text x=\"12\" y=\"24\">No data available.</text></svg>";

        const int width = 820;
        const int height = 260;
        const int left = 56;
        const int right = 24;
        const int top = 40;
        const int bottom = 44;

        var plotWidth = width - left - right;
        var plotHeight = height - top - bottom;

        var maxValue = Math.Max(1, points.Max(p => Math.Max(p.BacklogAtMonthEnd, Math.Max(p.OpenedCount, p.ResolvedCount))));
        var xStep = points.Count == 1 ? plotWidth : plotWidth / (double)(points.Count - 1);

        string backlog = BuildPolyline(points, p => p.BacklogAtMonthEnd, left, top, plotHeight, xStep, maxValue);
        string opened = BuildPolyline(points, p => p.OpenedCount, left, top, plotHeight, xStep, maxValue);
        string resolved = BuildPolyline(points, p => p.ResolvedCount, left, top, plotHeight, xStep, maxValue);

        StringBuilder sb = new();
        sb.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 820 260\" role=\"img\" aria-label=\"Lifecycle trend chart\">");
        sb.AppendLine("<style>.axis{stroke:#777;stroke-width:1}.grid{stroke:#e6e6e6;stroke-width:1}.label{font:12px sans-serif;fill:#333}.title{font:600 14px sans-serif;fill:#111}.legend{font:12px sans-serif;fill:#333}.series-backlog{fill:none;stroke:#1f77b4;stroke-width:2}.series-opened{fill:none;stroke:#2ca02c;stroke-width:2}.series-resolved{fill:none;stroke:#d62728;stroke-width:2}</style>");
        sb.AppendLine($"<text class=\"title\" x=\"{left}\" y=\"22\">{Xml(title)}</text>");

        for (var i = 0; i <= 4; i++)
        {
            var y = top + (plotHeight * i / 4.0);
            var value = maxValue - (maxValue * i / 4.0);
            sb.AppendLine($"<line class=\"grid\" x1=\"{left}\" y1=\"{Fmt(y)}\" x2=\"{width - right}\" y2=\"{Fmt(y)}\" />");
            sb.AppendLine($"<text class=\"label\" x=\"{left - 10}\" y=\"{Fmt(y + 4)}\" text-anchor=\"end\">{Math.Round(value)}</text>");
        }

        sb.AppendLine($"<line class=\"axis\" x1=\"{left}\" y1=\"{top + plotHeight}\" x2=\"{width - right}\" y2=\"{top + plotHeight}\" />");
        sb.AppendLine($"<line class=\"axis\" x1=\"{left}\" y1=\"{top}\" x2=\"{left}\" y2=\"{top + plotHeight}\" />");

        for (var i = 0; i < points.Count; i++)
        {
            var x = left + (i * xStep);
            sb.AppendLine($"<text class=\"label\" x=\"{Fmt(x)}\" y=\"{height - 18}\" text-anchor=\"middle\">{Xml(points[i].Label)}</text>");
        }

        sb.AppendLine($"<polyline class=\"series-backlog\" points=\"{backlog}\" />");
        sb.AppendLine($"<polyline class=\"series-opened\" points=\"{opened}\" />");
        sb.AppendLine($"<polyline class=\"series-resolved\" points=\"{resolved}\" />");

        var legendX = width - right - 220;
        sb.AppendLine($"<line class=\"series-backlog\" x1=\"{legendX}\" y1=\"18\" x2=\"{legendX + 20}\" y2=\"18\" /><text class=\"legend\" x=\"{legendX + 28}\" y=\"22\">Backlog</text>");
        sb.AppendLine($"<line class=\"series-opened\" x1=\"{legendX + 90}\" y1=\"18\" x2=\"{legendX + 110}\" y2=\"18\" /><text class=\"legend\" x=\"{legendX + 118}\" y=\"22\">Opened</text>");
        sb.AppendLine($"<line class=\"series-resolved\" x1=\"{legendX + 180}\" y1=\"18\" x2=\"{legendX + 200}\" y2=\"18\" /><text class=\"legend\" x=\"{legendX + 208}\" y=\"22\">Resolved</text>");
        sb.AppendLine("</svg>");

        return sb.ToString();
    }

    private static string BuildPolyline(
        IReadOnlyList<LifecycleMonthlyPoint> points,
        Func<LifecycleMonthlyPoint, int> selector,
        int left,
        int top,
        double plotHeight,
        double xStep,
        int maxValue)
    {
        return string.Join(" ", points.Select((point, index) =>
        {
            var x = left + (index * xStep);
            var y = top + plotHeight - (selector(point) / (double)maxValue * plotHeight);
            return $"{Fmt(x)},{Fmt(y)}";
        }));
    }

    private static string Xml(string value) => System.Security.SecurityElement.Escape(value) ?? string.Empty;

    private static string Fmt(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
