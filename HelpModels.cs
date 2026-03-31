namespace PassNotes;

internal sealed record HelpTocItem(string Title, string FileName, string? Anchor);

internal sealed record HelpNavState(string FileName, string? Anchor)
{
    public override string ToString() => Anchor is null ? FileName : $"{FileName}#{Anchor}";
}
