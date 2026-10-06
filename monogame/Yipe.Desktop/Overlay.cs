namespace Yipe.Desktop;

internal sealed record OverlayOption(string Label, Action Choose);

/// <summary>A paused interaction with paged prose and a scrollable choice list.</summary>
internal sealed class Overlay
{
    public string Title { get; }
    public string[] Lines { get; }
    public OverlayOption[] Options { get; }
    public Action? Back { get; }
    public int Selected { get; set; }
    public int Page { get; set; }
    public bool Combat { get; init; }
    public int FirstPortrait { get; init; }
    public int LinesPerPage => Combat ? 2 : Options.Length > 1 ? 5 : 10;
    public int PageCount => Math.Max(1, (Lines.Length + LinesPerPage - 1) / LinesPerPage);

    public Overlay(string title, string text, IEnumerable<OverlayOption> options, Action? back = null)
    {
        Title = title;
        Lines = Wrap(text, 44).ToArray();
        Options = options.ToArray();
        Back = back;
    }

    public static IEnumerable<string> Wrap(string text, int width)
    {
        foreach (var paragraph in text.Replace("\r", "").Split('\n'))
        {
            var line = "";
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length != 0 && line.Length + word.Length + 1 > width) { yield return line; line = ""; }
                var remaining = word;
                while (remaining.Length > width)
                {
                    if (line.Length != 0) { yield return line; line = ""; }
                    yield return remaining[..width]; remaining = remaining[width..];
                }
                line += (line.Length == 0 ? "" : " ") + remaining;
            }
            yield return line;
        }
    }
}
