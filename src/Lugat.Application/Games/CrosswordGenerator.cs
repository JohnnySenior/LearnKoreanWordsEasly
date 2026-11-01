using Lugat.Domain.Games;

namespace Lugat.Application.Games;

/// <summary>
/// A word that may go into the grid. <see cref="Recent"/>: it was in one of the learner's last crosswords, so it is
/// tried only after the others and a layout without it wins (L-55: each new crossword should bring new words).
/// </summary>
public sealed record CrosswordCandidate(Guid LearningItemId, string Korean, string Uzbek, string? ExampleKo, string? ExampleUz, bool Recent);

/// <summary>
/// Builds a crossword where one cell holds one Hangul syllable (L-55). Greedy: each next word must cross a placed one
/// on a shared syllable and may not touch other words side by side. A crossing on a word-final 다 teaches little
/// (하다, 있다, 같다…), so at most two are allowed. Many shuffled tries; the layout with most words, then the fewest
/// recent words, then the smallest box, wins. The grid stays narrow enough for a phone.
/// </summary>
public static class CrosswordGenerator
{
    public const int MaxCols = 10;

    public const int MaxRows = 14;

    private const int Attempts = 120;

    private const int MaxFinalDaCrossings = 2;

    private sealed record Placed(CrosswordCandidate Word, string[] Syl, int Row, int Col, bool Down);

    private sealed record Cell(string Syllable, bool Last);

    public static (int Rows, int Cols, List<CrosswordWord> Words) Build(IReadOnlyList<CrosswordCandidate> pool, int target, Random random)
    {
        List<Placed>? best = null;
        var bestScore = int.MinValue;
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var order = Shuffle(pool.Where(w => !w.Recent), random).Concat(Shuffle(pool.Where(w => w.Recent), random)).ToList();
            var placed = Try(order, target, random);
            var (rows, cols) = Box(placed);
            // A recent word costs more than any box (at most 10×14), so freshness comes before compactness.
            var score = placed.Count * 100_000 - placed.Count(p => p.Word.Recent) * 1_000 - rows * cols;
            if (score > bestScore)
            {
                (best, bestScore) = (placed, score);
            }
        }

        return Normalise(best ?? []);
    }

    private static List<Placed> Try(List<CrosswordCandidate> order, int target, Random random)
    {
        var grid = new Dictionary<(int, int), Cell>();
        var placed = new List<Placed>();
        void Put(CrosswordCandidate w, string[] syl, int r, int c, bool down)
        {
            for (var i = 0; i < syl.Length; i++)
            {
                grid[down ? (r + i, c) : (r, c + i)] = new Cell(syl[i], i == syl.Length - 1);
            }

            placed.Add(new Placed(w, syl, r, c, down));
        }

        var first = order[0];
        Put(first, Syllables(first.Korean), 0, 0, false);
        var rest = order.Skip(1).ToList();
        var finalDa = 0;
        var progress = true;
        // Several passes: a word that crossed nothing early may fit once others are on the board.
        while (progress && placed.Count < target)
        {
            progress = false;
            foreach (var w in rest.ToList())
            {
                if (placed.Count >= target)
                {
                    break;
                }

                var syl = Syllables(w.Korean);
                if (placed.Any(p => p.Word.Korean == w.Korean))
                {
                    rest.Remove(w);
                    continue;
                }

                var options = new List<(int R, int C, bool Down, double Score, bool Da)>();
                foreach (var ((gr, gc), cell) in grid)
                {
                    for (var i = 0; i < syl.Length; i++)
                    {
                        if (syl[i] != cell.Syllable)
                        {
                            continue;
                        }

                        var da = syl[i] == "다" && (cell.Last || i == syl.Length - 1);
                        if (da && finalDa >= MaxFinalDaCrossings)
                        {
                            continue;
                        }

                        foreach (var down in new[] { false, true })
                        {
                            var r = down ? gr - i : gr;
                            var c = down ? gc : gc - i;
                            var crossings = Fits(grid, syl, r, c, down);
                            if (crossings > 0)
                            {
                                options.Add((r, c, down, crossings - (da ? 0.5 : 0), da));
                            }
                        }
                    }
                }

                if (options.Count == 0)
                {
                    continue;
                }

                var top = options.Max(o => o.Score);
                var pick = Shuffle(options.Where(o => o.Score == top), random).First();
                finalDa += pick.Da ? 1 : 0;
                Put(w, syl, pick.R, pick.C, pick.Down);
                rest.Remove(w);
                progress = true;
            }
        }

        return placed;
    }

    /// <summary>The number of crossings, or 0 when the word cannot go there.</summary>
    private static int Fits(Dictionary<(int, int), Cell> grid, string[] syl, int r, int c, bool down)
    {
        Cell? At(int rr, int cc) => grid.GetValueOrDefault((rr, cc));
        if ((down ? At(r - 1, c) : At(r, c - 1)) is not null || (down ? At(r + syl.Length, c) : At(r, c + syl.Length)) is not null)
        {
            return 0;
        }

        var cross = 0;
        for (var i = 0; i < syl.Length; i++)
        {
            var (rr, cc) = down ? (r + i, c) : (r, c + i);
            if (At(rr, cc) is { } cell)
            {
                if (cell.Syllable != syl[i])
                {
                    return 0;
                }

                cross++;
                continue;
            }

            if ((down ? At(rr, cc - 1) : At(rr - 1, cc)) is not null || (down ? At(rr, cc + 1) : At(rr + 1, cc)) is not null)
            {
                return 0;
            }
        }

        if (cross == syl.Length)
        {
            return 0;
        }

        // A phone shows about ten cells across.
        var keys = grid.Keys.Concat(Enumerable.Range(0, syl.Length).Select(i => down ? (r + i, c) : (r, c + i))).ToList();
        var cols = keys.Max(k => k.Item2) - keys.Min(k => k.Item2) + 1;
        var rows = keys.Max(k => k.Item1) - keys.Min(k => k.Item1) + 1;
        return cols > MaxCols || rows > MaxRows ? 0 : cross;
    }

    private static (int Rows, int Cols) Box(List<Placed> placed)
    {
        var cells = placed.SelectMany(p => Enumerable.Range(0, p.Syl.Length).Select(i => p.Down ? (p.Row + i, p.Col) : (p.Row, p.Col + i))).ToList();
        return cells.Count == 0 ? (0, 0)
            : (cells.Max(x => x.Item1) - cells.Min(x => x.Item1) + 1, cells.Max(x => x.Item2) - cells.Min(x => x.Item2) + 1);
    }

    private static (int Rows, int Cols, List<CrosswordWord> Words) Normalise(List<Placed> placed)
    {
        if (placed.Count == 0)
        {
            return (0, 0, []);
        }

        var cells = placed.SelectMany(p => Enumerable.Range(0, p.Syl.Length).Select(i => p.Down ? (p.Row + i, p.Col) : (p.Row, p.Col + i))).ToList();
        var r0 = cells.Min(x => x.Item1);
        var c0 = cells.Min(x => x.Item2);
        var (rows, cols) = Box(placed);
        // Numbers in reading order, one per starting cell; an across and a down word may share one.
        var starts = placed.Select(p => (p.Row - r0, p.Col - c0)).Distinct().OrderBy(s => s.Item1).ThenBy(s => s.Item2).ToList();
        var words = placed
            .Select(p => new CrosswordWord(p.Word.LearningItemId, p.Word.Korean, p.Word.Uzbek, p.Row - r0, p.Col - c0, p.Down,
                starts.IndexOf((p.Row - r0, p.Col - c0)) + 1, p.Word.ExampleKo, p.Word.ExampleUz))
            .OrderBy(w => w.Number).ThenBy(w => w.Down)
            .ToList();
        return (rows, cols, words);
    }

    public static string[] Syllables(string korean) => korean.EnumerateRunes().Select(r => r.ToString()).ToArray();

    private static List<T> Shuffle<T>(IEnumerable<T> items, Random random)
    {
        var list = items.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }
}
