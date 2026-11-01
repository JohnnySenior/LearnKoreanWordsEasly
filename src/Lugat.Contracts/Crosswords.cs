using Lugat.Domain.Games;

namespace Lugat.Contracts;

/// <summary>A grid cell; <see cref="Syllable"/> only when it is known (a found or revealed word, or a hint).</summary>
public sealed record CrosswordCellDto(int Row, int Col, string? Syllable, int? Number);

/// <summary>
/// A clue. <see cref="Pattern"/> holds the syllables known so far, null where still hidden. <see cref="Korean"/> is
/// set only once the word is found or revealed. <see cref="ExampleKo"/> (with the word blanked) only after that hint.
/// </summary>
public sealed record CrosswordClueDto(
    int Index,
    int Number,
    bool Down,
    string Uzbek,
    int Length,
    CrosswordWordState State,
    string? Korean,
    IReadOnlyList<string?> Pattern,
    bool HasExample,
    string? ExampleKo,
    string? ExampleUz,
    int Hints);

public sealed record CrosswordDto(
    Guid Id,
    CrosswordSize Size,
    CrosswordStatus Status,
    int Rows,
    int Cols,
    IReadOnlyList<CrosswordCellDto> Cells,
    IReadOnlyList<CrosswordClueDto> Clues,
    int Found,
    int Revealed,
    int WithoutHints);

/// <param name="UnitsNeeded">Open units this size needs; <see cref="Available"/> once the learner has them.</param>
public sealed record CrosswordSizeDto(CrosswordSize Size, string Title, int Words, int UnitsNeeded, bool Available);

/// <param name="Unit">"A-3".</param>
public sealed record CrosswordUnitDto(Guid UnitId, string Unit, int Words);

/// <param name="OpenUnits">Units the learner has open or passed; the crossword starts at two (L-55).</param>
/// <param name="Units">The open units in course order, to choose from.</param>
public sealed record CrosswordHomeDto(
    int OpenUnits,
    int UnitsNeeded,
    IReadOnlyList<CrosswordSizeDto> Sizes,
    CrosswordDto? Active,
    Guid? CurrentUnitId,
    IReadOnlyList<CrosswordUnitDto>? Units = null);

/// <param name="Id">Chosen by the client, so a double tap never makes two crosswords (API01).</param>
/// <param name="UnitIds">Open units to take words from, at least two (three for the large one); none: all open units.</param>
public sealed record StartCrosswordRequest(Guid Id, CrosswordSize Size, IReadOnlyList<Guid>? UnitIds = null);

public sealed record CrosswordAnswerRequest(int Index, string Answer);

public enum CrosswordHint { Example, Syllable, Reveal }

public sealed record CrosswordHintRequest(int Index, CrosswordHint Hint);

public sealed record CrosswordAnswerResultDto(bool Correct, CrosswordDto Crossword);
