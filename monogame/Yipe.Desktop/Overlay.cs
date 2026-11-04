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
    public bool Keyboard { get; init; }
    public CombatPresentation? Presentation { get; init; }
    public bool Targeting { get; set; }
    public int PreviewSpell { get; set; } = -1;
    public int? InspectedEnemy { get; set; }
    public bool EnemyDetails { get; set; }
    public bool AutoAdvance { get; init; }
    public bool ContinueRequested { get; set; }
    public double CombatTime { get; set; }
    public int FirstPortrait { get; init; }
    public NpcActor Npc { get; init; }
    public bool NpcCelebrating { get; init; }
    public double NpcTime { get; set; }
    public int LinesPerPage => Combat ? (Options.Length == 1 ? 4 : 2)
        : Npc != NpcActor.None ? (Options.Length > 1 ? 4 : 8)
        : Options.Length > 1 ? 5 : 10;
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
