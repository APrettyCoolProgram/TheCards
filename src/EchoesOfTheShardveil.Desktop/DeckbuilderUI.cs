// 261127_code
// 261127_documentation

using EchoesOfTheShardveil.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace EchoesOfTheShardveil.Desktop;

/// <summary>Edits the player's single-player deck and retains the draft for the current session.</summary>
internal sealed class DeckbuilderUI : IScreen
{
    private const int Width = 1120;
    private const int Height = 760;
    private const int RowHeight = 35;
    private const int VisibleRows = 14;
    private const int VisibleDetailLines = 8;
    private const int HighestEnergyBucket = 6;
    private static readonly Rectangle BackButton = new(28, 28, 140, 42);
    private static readonly Rectangle SearchField = new(480, 28, 286, 42);
    private static readonly Rectangle ClearSearchButton = new(728, 34, 32, 30);
    private static readonly Rectangle TypeFilterButton = new(782, 28, 144, 42);
    private static readonly Rectangle CollectionPanel = new(28, 94, 668, 574);
    private static readonly Rectangle SetFilterButton = new(186, 107, 164, 29);
    private static readonly Rectangle InDeckFilterButton = new(360, 107, 112, 29);
    private static readonly Rectangle SortCostButton = new(44, 140, 53, 24);
    private static readonly Rectangle SortNameButton = new(105, 140, 65, 24);
    private static readonly Rectangle PreviousPageButton = new(600, 107, 36, 29);
    private static readonly Rectangle NextPageButton = new(644, 107, 36, 29);
    private static readonly Rectangle DetailsPanel = new(714, 94, 378, 408);
    private static readonly Rectangle DeckPanel = new(714, 520, 378, 148);
    private static readonly Rectangle RevertButton = new(500, 688, 132, 44);
    private static readonly Rectangle RestoreButton = new(642, 688, 150, 44);
    private static readonly Rectangle ApplyButton = new(802, 688, 146, 44);
    private static readonly Rectangle PlayDraftButton = new(958, 688, 134, 44);
    private static readonly Color Background = new(12, 17, 22);
    private static readonly Color Panel = new(23, 34, 42);
    private static readonly Color Border = new(59, 80, 87);
    private static readonly Color Muted = new(170, 191, 194);
    private static readonly Color Gold = new(226, 190, 117);
    private static readonly Color Teal = new(38, 81, 81);

    private readonly GameServices services;
    private readonly SavedDeck savedDeck;
    private readonly WindowControls controls;
    private readonly InputTracker input = new();
    private readonly IReadOnlyList<CardDefinition> starter = StarterDecks.Create();
    private readonly (string Set, CardDefinition Card)[] collectionCards;
    private readonly int[] draftCopies;
    private readonly string[] setNames;
    private readonly CardType[] cardTypes;
    private int[] filteredCards = [];
    private bool inDeckOnly;
    private bool sortByName;
    private bool searchFocused;
    private string search = "";
    private int setFilterIndex = -1;
    private int typeFilterIndex = -1;
    private int[] appliedCopies;
    private int selected = -1;
    private string? artworkPath;
    private bool loadArtwork;
    private Texture2D? artworkTexture;
    private int firstVisible;
    private Point? pointer;
    private List<(string Text, Color Color)> detailLines = [];
    private string selectedTypeLine = "";
    private string selectedSetLine = "";
    private int detailScroll;
    private string status = "";

    /// <summary>Builds an editable list from the player starter recipe's sets.</summary>
    /// <param name="services">Shared graphics and audio resources.</param>
    /// <param name="savedDeck">The active single-player deck and its save file.</param>
    public DeckbuilderUI(GameServices services, SavedDeck savedDeck)
    {
        this.services = services;
        this.savedDeck = savedDeck;
        CardRulesText.LoadCatalogs();
        controls = new WindowControls(services.Audio, isBoard: false);
        collectionCards = [.. StarterDecks.PlayerPool.OrderBy(entry => entry.Card.Cost).ThenBy(entry => entry.Card.Name)
            .ThenBy(entry => entry.Set, StringComparer.Ordinal)];
        draftCopies = new int[collectionCards.Length];
        setNames = [.. collectionCards.Select(entry => entry.Set).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
        cardTypes = [.. collectionCards.Select(entry => entry.Card.Type).Distinct().Order()];
        SetDraft(savedDeck.ActiveDeck);
        if (savedDeck.UsedStarterFallback)
        {
            status = "Saved deck unavailable. Using starter deck.";
        }
        appliedCopies = (int[])draftCopies.Clone();
    }

    /// <inheritdoc />
    public string Title => "Echoes of the Shardveil - Deckbuilder";

    /// <summary>Gets the largest first row the list can scroll to.</summary>
    private int MaximumOffset => Math.Max(0, filteredCards.Length - VisibleRows);
    /// <summary>Gets the filtered row just past the last visible one.</summary>
    private int VisibleEnd => Math.Min(firstVisible + VisibleRows, filteredCards.Length);
    /// <summary>Gets the number of cards in the draft.</summary>
    private int CardCount => draftCopies.Sum();
    /// <summary>Gets whether the draft differs from the applied deck.</summary>
    private bool HasDraftChanges => !draftCopies.SequenceEqual(appliedCopies);
    /// <summary>Gets whether the draft has the required number of cards.</summary>
    private bool HasCompleteDeck => CardCount == MatchFormats.Active.DeckSize;
    /// <summary>Gets whether the draft can be saved as the active deck.</summary>
    private bool CanApply => HasCompleteDeck &&
        (savedDeck.UsedStarterFallback || HasDraftChanges);

    /// <summary>Gets a snapshot for a playtest without applying or saving the draft.</summary>
    /// <returns>The complete draft, or null if it still needs edits.</returns>
    public IReadOnlyList<CardDefinition>? CreatePlaytestDeck() => HasCompleteDeck
        ? collectionCards.SelectMany((entry, index) => Enumerable.Repeat(entry.Card, draftCopies[index])).ToArray()
        : null;

    /// <summary>Reports that a playtest could not load its content, keeping the draft open.</summary>
    public void ShowPlaytestError() => status = "Could not start playtest. Check AppData.";

    /// <summary>Ignores the click that opened the editor from the menu.</summary>
    public void Activate()
    {
        input.Reset();
        pointer = null;
        searchFocused = false;
    }

    /// <summary>Edits the card-name filter when its field has focus.</summary>
    /// <param name="character">The character received from the game window.</param>
    public void EnterSearchText(char character)
    {
        if (!searchFocused || controls.IsConfirming)
        {
            return;
        }

        switch (character)
        {
            case '\b' when search.Length > 0:
                SetSearch(search[..^1]);
                break;
            case '\r' or '\n' or '\t':
                searchFocused = false;
                break;
            default:
                if (!char.IsControl(character) && search.Length < 64)
                {
                    SetSearch(search + character);
                }
                break;
        }
    }

    /// <summary>Moves the selection through the current filtered collection and keeps it visible.</summary>
    /// <param name="key">The navigation key.</param>
    public void NavigateCollection(Keys key)
    {
        if (searchFocused || controls.IsConfirming || filteredCards.Length == 0)
        {
            return;
        }

        var row = Array.IndexOf(filteredCards, selected);
        var destination = key switch
        {
            Keys.Up => Math.Max(0, row - 1),
            Keys.Down => Math.Min(filteredCards.Length - 1, row + 1),
            Keys.PageUp => Math.Max(0, row - VisibleRows),
            Keys.PageDown => Math.Min(filteredCards.Length - 1, row + VisibleRows),
            Keys.Home => 0,
            Keys.End => filteredCards.Length - 1,
            _ => -1,
        };
        if (destination < 0)
        {
            return;
        }

        Select(filteredCards[destination]);
        if (destination < firstVisible)
        {
            firstVisible = destination;
        }
        else if (destination >= VisibleEnd)
        {
            firstVisible = destination - VisibleRows + 1;
        }
    }

    /// <inheritdoc />
    public ScreenTransition Update(GameTime gameTime, Rectangle client, bool isActive)
    {
        var frame = input.Poll();
        var viewport = services.Device.Viewport;
        var fitted = ScreenLayout.Fit(viewport, Width, Height);
        pointer = isActive && ScreenLayout.TryMap(frame.Pointer, client, viewport, fitted, Width, Height, out var point)
            ? point : null;
        if (!isActive)
        {
            return ScreenTransition.None;
        }

        if (frame.EscapePressed)
        {
            if (controls.IsConfirming)
            {
                controls.ToggleExitPrompt();
            }
            else if (searchFocused)
            {
                searchFocused = false;
            }
            else
            {
                return ScreenTransition.ReturnToMenu;
            }
        }
        else if (frame.LeftPressed)
        {
            var action = controls.Click(frame.Pointer, client, viewport);
            if (action == WindowControlResult.ConfirmedExit)
            {
                return ScreenTransition.ExitGame;
            }
            if (action == WindowControlResult.Ignored && pointer is { } position)
            {
                if (BackButton.Contains(position))
                {
                    return ScreenTransition.ReturnToMenu;
                }
                if (HasCompleteDeck && PlayDraftButton.Contains(position))
                {
                    return ScreenTransition.PlayDraft;
                }
                HandleClick(position);
            }
        }

        if (!controls.IsConfirming && frame.ScrollDirection != 0 && pointer is { } scrollPosition)
        {
            if (CollectionPanel.Contains(scrollPosition))
            {
                ScrollCards(frame.ScrollDirection);
            }
            else if (DetailsPanel.Contains(scrollPosition))
            {
                ScrollDetails(frame.ScrollDirection);
            }
        }

        return ScreenTransition.None;
    }

    /// <inheritdoc />
    public void Draw(GameTime gameTime)
    {
        var device = services.Device;
        device.Clear(Background);
        var viewport = device.Viewport;
        var fitted = ScreenLayout.Fit(viewport, Width, Height);
        if (fitted.Width <= 0 || fitted.Height <= 0)
        {
            return;
        }

        var transform = Matrix.CreateScale((float)fitted.Width / Width, (float)fitted.Height / Height, 1)
            * Matrix.CreateTranslation(fitted.X, fitted.Y, 0);
        services.SpriteBatch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: transform);
        DrawButton(BackButton, "Main menu", true);
        DrawText("Deckbuilder", 192, 22, 43, UiColors.Ivory);
        DrawSearch();
        if (cardTypes.Length > 1)
        {
            Span<char> text = stackalloc char[32];
            DrawButton(TypeFilterButton, typeFilterIndex < 0 ? "All types >"
                : BitmapText.Format(text, $"{cardTypes[typeFilterIndex]} >"), true, primary: typeFilterIndex >= 0);
        }
        DrawCollection();
        DrawDetails();
        DrawDeckSummary();
        DrawText(status.Length == 0 && HasDraftChanges ? "Unsaved changes" : status, 28, 699, 20, Muted);
        DrawButton(RevertButton, "Revert edits", HasDraftChanges);
        DrawButton(RestoreButton, "Restore starter", true);
        DrawButton(ApplyButton, "Use this deck", CanApply, primary: true);
        DrawButton(PlayDraftButton, "Play draft", HasCompleteDeck);
        services.SpriteBatch.End();
        controls.Draw(services.SpriteBatch, services.Pixel, services.Font, viewport);
    }

    /// <inheritdoc />
    public void RequestClose() => controls.RequestExitConfirmation();

    /// <inheritdoc />
    public void Dispose() => artworkTexture?.Dispose();

    /// <summary>Handles a click on the search field, deck buttons, filters, paging or a card row.</summary>
    /// <param name="position">The pointer in design coordinates.</param>
    private void HandleClick(Point position)
    {
        searchFocused = SearchField.Contains(position);
        if (searchFocused)
        {
            if (search.Length > 0 && ClearSearchButton.Contains(position))
            {
                SetSearch("");
            }
        }
        else if (RevertButton.Contains(position) && HasDraftChanges)
        {
            SetDraft(savedDeck.ActiveDeck);
            status = "Draft reverted to the applied deck.";
        }
        else if (RestoreButton.Contains(position))
        {
            SetDraft(starter);
            status = "Starter deck restored to the draft.";
        }
        else if (ApplyButton.Contains(position) && CanApply)
        {
            var entries = collectionCards.Select((card, index) => (card.Set, card.Card, Copies: draftCopies[index]))
                .Where(entry => entry.Copies > 0);
            if (savedDeck.TryApply(entries))
            {
                appliedCopies = (int[])draftCopies.Clone();
                status = "Deck saved for Single player.";
            }
            else
            {
                status = "Could not save deck. Your draft is still here.";
            }
        }
        else if (setNames.Length > 1 && SetFilterButton.Contains(position))
        {
            CycleSetFilter();
        }
        else if (cardTypes.Length > 1 && TypeFilterButton.Contains(position))
        {
            typeFilterIndex = typeFilterIndex == cardTypes.Length - 1 ? -1 : typeFilterIndex + 1;
            ShowCollectionFromTop();
        }
        else if (InDeckFilterButton.Contains(position))
        {
            inDeckOnly = !inDeckOnly;
            ShowCollectionFromTop();
        }
        else if (SortCostButton.Contains(position) || SortNameButton.Contains(position))
        {
            sortByName = SortNameButton.Contains(position);
            ShowCollectionFromTop();
        }
        else if (PreviousPageButton.Contains(position))
        {
            ScrollCards(-VisibleRows);
        }
        else if (NextPageButton.Contains(position))
        {
            ScrollCards(VisibleRows);
        }
        else
        {
            for (var visibleIndex = firstVisible; visibleIndex < VisibleEnd; visibleIndex++)
            {
                var index = filteredCards[visibleIndex];
                var row = RowBounds(visibleIndex - firstVisible);
                if (!row.Contains(position))
                {
                    continue;
                }

                Select(index);
                if (RemoveButtonBounds(row).Contains(position) && draftCopies[index] > 0)
                {
                    draftCopies[index]--;
                    status = "";
                    if (inDeckOnly && draftCopies[index] == 0)
                    {
                        RefreshCollection();
                    }
                }
                else if (AddButtonBounds(row).Contains(position) && CanAdd(index, CardCount))
                {
                    draftCopies[index]++;
                    status = "";
                }
                break;
            }
        }
    }

    /// <summary>Cycles through the available sets and the unfiltered collection.</summary>
    private void CycleSetFilter()
    {
        setFilterIndex = setFilterIndex == setNames.Length - 1 ? -1 : setFilterIndex + 1;
        ShowCollectionFromTop();
    }

    /// <summary>Updates the search text and returns to the first matching row.</summary>
    /// <param name="text">The new search text.</param>
    private void SetSearch(string text)
    {
        search = text;
        ShowCollectionFromTop();
    }

    /// <summary>Reapplies sorting and filters and returns to the first matching row.</summary>
    private void ShowCollectionFromTop()
    {
        firstVisible = 0;
        RefreshCollection();
    }

    /// <summary>Applies sorting and filters and keeps the visible rows and selected card in range.</summary>
    private void RefreshCollection()
    {
        var query = search.Trim();
        var matching = Enumerable.Range(0, collectionCards.Length)
            .Where(index => (setFilterIndex < 0 || collectionCards[index].Set == setNames[setFilterIndex]) &&
                (typeFilterIndex < 0 || collectionCards[index].Card.Type == cardTypes[typeFilterIndex]) &&
                (!inDeckOnly || draftCopies[index] > 0) &&
                collectionCards[index].Card.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        if (sortByName)
        {
            matching = matching.OrderBy(index => collectionCards[index].Card.Name,
                StringComparer.CurrentCultureIgnoreCase);
        }
        filteredCards = [.. matching];
        firstVisible = Math.Min(firstVisible, MaximumOffset);
        if (filteredCards.Length == 0)
        {
            selected = -1;
            detailLines.Clear();
            detailScroll = 0;
            SetArtwork(null);
        }
        else if (!filteredCards.Contains(selected))
        {
            Select(filteredCards[firstVisible]);
        }
    }

    /// <summary>Moves the first visible collection row within the available range.</summary>
    /// <param name="rows">The signed number of rows to move.</param>
    private void ScrollCards(int rows) => firstVisible = Math.Clamp(firstVisible + rows, 0, MaximumOffset);

    /// <summary>Checks the deck size and the combined copy count across cards with the same name.</summary>
    /// <param name="index">The card to add.</param>
    /// <param name="count">The current draft size.</param>
    /// <returns>Whether another copy can be added.</returns>
    private bool CanAdd(int index, int count)
    {
        // A plain loop, because every visible row asks this on every frame.
        var sameName = 0;
        for (var other = 0; other < collectionCards.Length; other++)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(collectionCards[index].Card.Name, collectionCards[other].Card.Name))
            {
                sameName += draftCopies[other];
            }
        }
        return count < MatchFormats.Active.DeckSize && sameName < MatchFormats.Active.MaxCopiesPerName;
    }

    /// <summary>Replaces the draft with the given deck and refreshes the collection.</summary>
    /// <param name="deck">The cards to put into the draft.</param>
    private void SetDraft(IReadOnlyList<CardDefinition> deck)
    {
        for (var index = 0; index < collectionCards.Length; index++)
        {
            draftCopies[index] = deck.Count(card => ReferenceEquals(card, collectionCards[index].Card));
        }
        RefreshCollection();
    }

    /// <summary>Shows a card and prepares its rules and flavor text for the detail panel.</summary>
    /// <param name="index">The selected card's index.</param>
    private void Select(int index)
    {
        if (selected == index)
        {
            return;
        }

        var (set, card) = collectionCards[index];
        var presentation = CardSet.GetPresentation(set, card.Id);
        selected = index;
        selectedTypeLine = CardRulesText.TypeLine(card);
        selectedSetLine = $"Set: {set}";
        detailScroll = 0;
        detailLines = [.. CardRulesText.Describe(card).SelectMany(rule => BitmapText.Wrap(rule, 37))
            .Select(rule => (rule, UiColors.Ivory))];
        if (!string.IsNullOrWhiteSpace(presentation?.FlavorText))
        {
            if (detailLines.Count > 0)
            {
                detailLines.Add(("", Muted));
            }
            foreach (var paragraph in presentation.FlavorText.Split('\n', StringSplitOptions.TrimEntries))
            {
                var lines = paragraph.Length == 0 ? [""] : BitmapText.Wrap(paragraph, 37);
                detailLines.AddRange(lines.Select(line => (line, Muted)));
            }
        }
        SetArtwork(presentation?.Artwork);
    }

    /// <summary>Scrolls rules and flavor text without moving the card header.</summary>
    /// <param name="direction">The signed scroll direction.</param>
    private void ScrollDetails(int direction) =>
        detailScroll = Math.Clamp(detailScroll + direction, 0, Math.Max(0, detailLines.Count - VisibleDetailLines));

    /// <summary>Replaces the active preview when the selected card's artwork changes.</summary>
    /// <param name="path">The image path, or null if there is no artwork.</param>
    private void SetArtwork(string? path)
    {
        if (artworkPath == path)
        {
            return;
        }

        artworkTexture?.Dispose();
        artworkTexture = null;
        artworkPath = path;
        loadArtwork = path is not null;
    }

    /// <summary>Loads optional artwork once per selection, falling back to text if it is unavailable.</summary>
    private void LoadArtwork()
    {
        if (!loadArtwork || artworkPath is null)
        {
            return;
        }

        loadArtwork = false;
        try
        {
            using var stream = File.OpenRead(artworkPath);
            artworkTexture = Texture2D.FromStream(services.Device, stream, DefaultColorProcessors.PremultiplyAlpha);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            InvalidOperationException or ArgumentException or NotSupportedException)
        {
            // Missing or unreadable artwork leaves the card details available.
        }
    }

    /// <summary>Draws deck size and the energy cost of every copy in the draft.</summary>
    private void DrawDeckSummary()
    {
        DrawBox(DeckPanel, Panel);
        var count = CardCount;
        var format = MatchFormats.Active;
        DrawText("Your deck", 734, 534, 29, UiColors.Ivory);
        Span<char> text = stackalloc char[48];
        DrawText(BitmapText.Format(text, $"{count} / {format.DeckSize} cards"), 734, 574, 22, Gold);
        const int meterWidth = 140;
        FillRectangle(new Rectangle(734, 611, meterWidth, 5), Border);
        FillRectangle(new Rectangle(734, 611, (int)((long)meterWidth * count / format.DeckSize), 5), Gold);
        DrawText(BitmapText.Format(text, $"{format.MaxCopiesPerName} copies per name"), 734, 631, 16, Muted);

        DrawText("Energy curve", 895, 534, 19, UiColors.Ivory);
        const int baseline = 626;
        FillRectangle(new Rectangle(894, baseline, 175, 1), Border);
        Span<int> buckets = stackalloc int[HighestEnergyBucket + 1];
        CountEnergyCosts(buckets);
        var peak = 1;
        foreach (var bucket in buckets)
        {
            peak = Math.Max(peak, bucket);
        }
        for (var cost = 0; cost < buckets.Length; cost++)
        {
            var center = 906 + cost * 25;
            var copiesAtCost = buckets[cost];
            if (copiesAtCost > 0)
            {
                var barHeight = Math.Max(2, (int)(48L * copiesAtCost / peak));
                FillRectangle(new Rectangle(center - 7, baseline - barHeight, 14, barHeight), Teal);
                BitmapText.DrawCentered(services.SpriteBatch, services.Font, BitmapText.Format(text, $"{copiesAtCost}"),
                    center, baseline - barHeight - 16, 14, UiColors.Ivory);
            }
            var label = BitmapText.Format(text, $"{cost}{(cost == HighestEnergyBucket ? "+" : "")}");
            BitmapText.DrawCentered(services.SpriteBatch, services.Font, label, center, baseline + 5, 15, Muted);
        }
    }

    /// <summary>Counts draft copies at each energy cost, grouping higher costs into the final bucket.</summary>
    /// <param name="buckets">Receives the counts in ascending cost order. It must start zeroed.</param>
    private void CountEnergyCosts(Span<int> buckets)
    {
        for (var index = 0; index < collectionCards.Length; index++)
        {
            buckets[Math.Min(collectionCards[index].Card.Cost, HighestEnergyBucket)] += draftCopies[index];
        }
    }

    /// <summary>Draws the name filter, its text cursor and its clear button.</summary>
    private void DrawSearch()
    {
        const int textHeight = 20;
        const int textLimit = 22;
        const int left = 492;
        const int top = 39;
        DrawBox(SearchField, searchFocused ? Teal : Panel);
        var visibleText = search.AsSpan(Math.Max(0, search.Length - textLimit));
        DrawText(search.Length == 0 ? "Search card names" : visibleText, left, top, textHeight,
            search.Length == 0 ? Muted : UiColors.Ivory);
        if (searchFocused && search.Length > 0)
        {
            FillRectangle(new Rectangle(left + visibleText.Length * BitmapText.Advance(textHeight), top + 1, 1, 18), Gold);
        }
        if (search.Length > 0)
        {
            DrawButton(ClearSearchButton, "X", true);
        }
    }

    /// <summary>Draws the card list and the copy controls for each card.</summary>
    private void DrawCollection()
    {
        DrawBox(CollectionPanel, Panel);
        DrawText("Card pool", 48, 106, 29, UiColors.Ivory);
        DrawButton(SortCostButton, "Cost", true, primary: !sortByName);
        DrawButton(SortNameButton, "Name", true, primary: sortByName);
        DrawText("Type", 391, 143, 17, Muted);
        DrawText("Set", 454, 143, 17, Muted);
        DrawText("Copies", 575, 143, 17, Muted);
        if (setNames.Length > 1)
        {
            var label = setFilterIndex < 0 ? "All sets" : BitmapText.Truncate(setNames[setFilterIndex], 12);
            Span<char> setText = stackalloc char[32];
            DrawButton(SetFilterButton, BitmapText.Format(setText, $"{label} >"), true);
        }
        DrawButton(InDeckFilterButton, "In deck", true, primary: inDeckOnly);
        if (filteredCards.Length == 0)
        {
            DrawText("No cards match these filters.", 48, 176, 20, Muted);
        }
        Span<char> text = stackalloc char[48];
        if (MaximumOffset > 0)
        {
            DrawText(BitmapText.Format(text, $"{firstVisible + 1}-{VisibleEnd} of {filteredCards.Length}"), 483, 112, 16, Muted);
            DrawButton(PreviousPageButton, "<", firstVisible > 0);
            DrawButton(NextPageButton, ">", firstVisible < MaximumOffset);
        }

        var count = CardCount;
        for (var visibleIndex = firstVisible; visibleIndex < VisibleEnd; visibleIndex++)
        {
            var index = filteredCards[visibleIndex];
            var row = RowBounds(visibleIndex - firstVisible);
            var (set, card) = collectionCards[index];
            FillRectangle(row, index == selected ? Teal : new Color(29, 43, 51));
            if (index == selected)
            {
                FillRectangle(new Rectangle(row.X, row.Y, 3, row.Height), Gold);
            }

            DrawText(BitmapText.Format(text, $"{card.Cost}"), 60, row.Top + 5, 22, Gold);
            DrawText(BitmapText.Truncate(card.Name, 24), 111, row.Top + 5, 22, UiColors.Ivory);
            DrawText(BitmapText.Format(text, $"{card.Type}"), 391, row.Top + 6, 20, Muted);
            DrawText(BitmapText.Truncate(set, 9), 454, row.Top + 7, 17, Muted);
            DrawText(BitmapText.Format(text, $"{draftCopies[index]}"), 595, row.Top + 5, 22, UiColors.Ivory);
            DrawButton(RemoveButtonBounds(row), "-", draftCopies[index] > 0);
            DrawButton(AddButtonBounds(row), "+", CanAdd(index, count));
        }
    }

    /// <summary>Draws the selected card's stats and rules.</summary>
    private void DrawDetails()
    {
        DrawBox(DetailsPanel, Panel);
        if (selected < 0)
        {
            return;
        }

        var card = collectionCards[selected].Card;
        LoadArtwork();
        if (artworkTexture is { } image)
        {
            var bounds = ScreenLayout.Fit(new Viewport(0, 0, 143, 113), image.Width, image.Height);
            bounds.Offset(929, 146);
            services.SpriteBatch.Draw(image, bounds, Color.White);
        }

        DrawText(BitmapText.Truncate(card.Name, 23), 734, 113, 29, UiColors.Ivory);
        DrawText(BitmapText.Truncate(selectedTypeLine, artworkTexture is null ? 33 : 18), 734, 153, 21, Muted);
        DrawText(BitmapText.Truncate(selectedSetLine, artworkTexture is null ? 37 : 20), 734, 173, 18, Muted);
        Span<char> text = stackalloc char[48];
        DrawText(BitmapText.Format(text, $"{card.Cost} energy"), 734, 193, 22, Gold);
        if (card.Type == CardType.Unit)
        {
            DrawText(BitmapText.Format(text, $"{card.Power} Power   {card.Health} Health"), 734, 227, 22, UiColors.Ivory);
        }
        FillRectangle(new Rectangle(734, 269, 338, 1), Border);
        var end = Math.Min(detailScroll + VisibleDetailLines, detailLines.Count);
        for (var index = detailScroll; index < end; index++)
        {
            var line = detailLines[index];
            DrawText(line.Text, 734, 286 + (index - detailScroll) * 25, 19, line.Color);
        }
        if (detailLines.Count > VisibleDetailLines)
        {
            DrawText(BitmapText.Format(text, $"Scroll  {detailScroll + 1}-{end} of {detailLines.Count}"), 734, 481, 15, Muted);
        }
    }

    /// <summary>Gets the design bounds of a card row.</summary>
    /// <param name="index">The card's index.</param>
    /// <returns>The row bounds.</returns>
    private static Rectangle RowBounds(int index) => new(44, 167 + index * RowHeight, 636, RowHeight - 2);

    /// <summary>Gets the bounds of the button that removes one copy.</summary>
    /// <param name="row">The card row.</param>
    /// <returns>The remove button bounds.</returns>
    private static Rectangle RemoveButtonBounds(Rectangle row) => new(540, row.Y + 2, 32, 29);

    /// <summary>Gets the bounds of the button that adds one copy.</summary>
    /// <param name="row">The card row.</param>
    /// <returns>The add button bounds.</returns>
    private static Rectangle AddButtonBounds(Rectangle row) => new(632, row.Y + 2, 32, 29);

    /// <summary>Draws a button with hover and disabled states.</summary>
    /// <param name="bounds">The button bounds.</param>
    /// <param name="label">The button caption.</param>
    /// <param name="enabled">Whether the button can be used.</param>
    /// <param name="primary">Whether to accent the main action.</param>
    private void DrawButton(Rectangle bounds, ReadOnlySpan<char> label, bool enabled, bool primary = false)
    {
        var hovered = enabled && pointer is { } position && bounds.Contains(position) && !controls.IsConfirming;
        DrawBox(bounds, hovered ? new Color(53, 99, 101) : primary && enabled ? Teal : Panel);
        const int textHeight = 20;
        BitmapText.DrawCentered(services.SpriteBatch, services.Font, label, bounds.Center.X,
            bounds.Center.Y - textHeight / 2, textHeight, enabled ? UiColors.Ivory : new Color(121, 141, 145));
    }

    /// <summary>Draws a panel with a one-pixel border.</summary>
    /// <param name="bounds">The outer bounds.</param>
    /// <param name="color">The interior color.</param>
    private void DrawBox(Rectangle bounds, Color color)
    {
        FillRectangle(bounds, Border);
        bounds.Inflate(-1, -1);
        FillRectangle(bounds, color);
    }

    /// <summary>Draws a filled rectangle.</summary>
    /// <param name="bounds">The rectangle to fill.</param>
    /// <param name="color">The fill color.</param>
    private void FillRectangle(Rectangle bounds, Color color) => services.SpriteBatch.Draw(services.Pixel, bounds, color);

    /// <summary>Draws a line in the shared bitmap font.</summary>
    /// <param name="label">The text to draw.</param>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="height">The text height.</param>
    /// <param name="color">The text color.</param>
    private void DrawText(ReadOnlySpan<char> label, int x, int y, int height, Color color) =>
        BitmapText.Draw(services.SpriteBatch, services.Font, label, x, y, height, color);
}
