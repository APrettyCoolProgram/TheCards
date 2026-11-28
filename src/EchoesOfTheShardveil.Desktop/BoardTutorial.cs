// 261127_code
// 261127_documentation

using EchoesOfTheShardveil.Core;

namespace EchoesOfTheShardveil.Desktop;

/// <summary>Tracks the optional pre-match rules walkthrough.</summary>
internal sealed class BoardTutorial
{
    private static readonly Lazy<TutorialContent?> Content = new(LoadContent);
    private static readonly TutorialPrompt DefaultPrompt = new(
        "BEFORE THE DUEL",
        "Want to learn the rules?",
        "Learn the board layout, card details, and rules of a duel.",
        "You can skip the guide and go straight to your opening hand.",
        "Skip for now",
        "Learn the rules");

    /// <summary>Starts at the configured prompt or first lesson, if a guide is available.</summary>
    /// <param name="showGuide">Whether walkthrough content is wanted for this duel.</param>
    public BoardTutorial(bool showGuide = true)
    {
        IsOpen = showGuide && Content.Value is not null;
        PageIndex = IsOpen && Prompt is null ? 0 : -1;
    }

    /// <summary>Gets the opening prompt, if configured.</summary>
    public static TutorialPrompt? Prompt => Content.Value?.Prompt;

    /// <summary>Gets the lessons in the order they are shown.</summary>
    public static IReadOnlyList<TutorialPage> Pages => Content.Value?.Pages ?? [];

    /// <summary>Gets the current lesson, or -1 while asking whether to show the guide.</summary>
    public int PageIndex { get; private set; }

    /// <summary>Gets whether the walkthrough is still showing.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Gets whether the opening question is showing.</summary>
    public bool IsPrompt => IsOpen && PageIndex < 0;

    /// <summary>Gets whether a previous lesson or the opening prompt is available.</summary>
    public bool CanGoBack => IsOpen && PageIndex > (Prompt is null ? 0 : -1);

    /// <summary>Gets whether the final lesson is showing.</summary>
    public bool IsLastPage => IsOpen && PageIndex == Pages.Count - 1;

    /// <summary>Moves to the next lesson, or closes the walkthrough after the last one.</summary>
    public void Next()
    {
        if (!IsOpen)
        {
            return;
        }
        if (IsLastPage)
        {
            Close();
        }
        else
        {
            PageIndex++;
        }
    }

    /// <summary>Moves to the previous lesson, or back to the opening question.</summary>
    public void Back()
    {
        if (CanGoBack)
        {
            PageIndex--;
        }
    }

    /// <summary>Ends the walkthrough.</summary>
    public void Close() => IsOpen = false;

    /// <summary>Loads the matchup tutorial, validates its pages, and fills in current rules values.</summary>
    /// <returns>The resolved tutorial content, or null if the matchup has no tutorial.</returns>
    private static TutorialContent? LoadContent()
    {
        if (MatchSetup.Current.Tutorial is not { } reference)
        {
            return null;
        }

        var path = AppDataJson.ResolveReference(reference);
        var tutorial = AppDataJson.Load<TutorialData>(path);
        if (tutorial.Pages is not { Length: > 0 } ||
            tutorial.Pages.Any(page => page is null || string.IsNullOrWhiteSpace(page.Title) ||
                page.Points is not { Count: > 0 } || page.Points.Any(string.IsNullOrWhiteSpace)))
        {
            throw new InvalidDataException($"'{path}' must contain complete tutorial pages.");
        }

        var prompt = tutorial.ShowPrompt ? tutorial.Prompt ?? DefaultPrompt : null;
        if (prompt is not null && (string.IsNullOrWhiteSpace(prompt.Label) ||
            string.IsNullOrWhiteSpace(prompt.Title) || string.IsNullOrWhiteSpace(prompt.Description) ||
            string.IsNullOrWhiteSpace(prompt.SkipHint) || string.IsNullOrWhiteSpace(prompt.SkipButton) ||
            string.IsNullOrWhiteSpace(prompt.StartButton)))
        {
            throw new InvalidDataException($"'{path}' must provide complete tutorial prompt text.");
        }

        var formatText = new FormatText(MatchFormats.Active);
        var pages = tutorial.Pages.Select(page => new TutorialPage(page.Title,
            [.. page.Points.Select(formatText.Expand)],
            page.Illustration, ResolveExample(page, tutorial, path))).ToArray();
        return new TutorialContent(pages, prompt);
    }

    /// <summary>Resolves a lesson's card illustration, using the guide-wide reference as a fallback.</summary>
    /// <param name="page">The lesson being loaded.</param>
    /// <param name="tutorial">The guide containing the lesson.</param>
    /// <param name="path">The tutorial file path used in error messages.</param>
    /// <returns>The example card, or null for a lesson without a card illustration.</returns>
    private static CardDefinition? ResolveExample(TutorialPageData page, TutorialData tutorial, string path)
    {
        if (page.Illustration != TutorialIllustration.Card)
        {
            return null;
        }

        var id = page.ExampleCardId ?? tutorial.ExampleCardId;
        var set = page.ExampleCardSet ?? tutorial.ExampleCardSet;
        if (string.IsNullOrWhiteSpace(id) || !CardSet.Load(set).TryGetValue(id, out var card) ||
            card.Type is not (CardType.Unit or CardType.Spell or CardType.Relic))
        {
            throw new InvalidDataException(
                $"Lesson '{page.Title}' in '{path}' must reference a Unit, Spell or Relic in set '{set}'.");
        }
        return card;
    }

    /// <summary>The tutorial content as stored in AppData.</summary>
    /// <param name="ExampleCardId">The default example card ID for lessons without their own reference.</param>
    /// <param name="Pages">The lessons in display order.</param>
    /// <param name="ExampleCardSet">The default set for examples.</param>
    /// <param name="ShowPrompt">Whether to ask before opening the first lesson.</param>
    /// <param name="Prompt">The opening prompt text, or null for the default wording.</param>
    private sealed record TutorialData(TutorialPageData[] Pages, string? ExampleCardId = null,
        string ExampleCardSet = "Alpha", bool ShowPrompt = true, TutorialPrompt? Prompt = null);

    /// <summary>One lesson as stored in AppData.</summary>
    /// <param name="Title">The heading.</param>
    /// <param name="Points">The bulleted paragraphs.</param>
    /// <param name="Illustration">The picture shown beside the text.</param>
    /// <param name="ExampleCardId">An optional card ID for this lesson.</param>
    /// <param name="ExampleCardSet">An optional set name for this lesson's card.</param>
    private sealed record TutorialPageData(string Title, IReadOnlyList<string> Points,
        TutorialIllustration Illustration = TutorialIllustration.None, string? ExampleCardId = null,
        string? ExampleCardSet = null);

    /// <summary>The resolved tutorial available to the board.</summary>
    /// <param name="Pages">The lessons with rules values filled in.</param>
    /// <param name="Prompt">The opening prompt, if shown.</param>
    private sealed record TutorialContent(TutorialPage[] Pages, TutorialPrompt? Prompt);
}

/// <summary>The text shown before the first lesson.</summary>
/// <param name="Label">The small heading above the title.</param>
/// <param name="Title">The main heading.</param>
/// <param name="Description">What the guide covers.</param>
/// <param name="SkipHint">What happens when the guide is skipped.</param>
/// <param name="SkipButton">The label for skipping the guide.</param>
/// <param name="StartButton">The label for opening the first lesson.</param>
internal sealed record TutorialPrompt(string Label, string Title, string Description, string SkipHint,
    string SkipButton, string StartButton);

/// <summary>The picture shown beside a lesson.</summary>
internal enum TutorialIllustration
{
    /// <summary>Text only.</summary>
    None,
    /// <summary>A diagram of the board.</summary>
    Board,
    /// <summary>An example card.</summary>
    Card,
}

/// <summary>One lesson of the walkthrough.</summary>
/// <param name="Title">The heading.</param>
/// <param name="Points">The bulleted paragraphs.</param>
/// <param name="Illustration">The picture shown beside the text.</param>
/// <param name="ExampleCard">The card shown in this lesson, if any.</param>
internal sealed record TutorialPage(string Title, IReadOnlyList<string> Points,
    TutorialIllustration Illustration, CardDefinition? ExampleCard);
