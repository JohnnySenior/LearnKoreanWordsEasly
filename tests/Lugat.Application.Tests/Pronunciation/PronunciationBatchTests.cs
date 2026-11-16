using System.Text.Json;
using Lugat.Application.Pronunciation;

namespace Lugat.Application.Tests.Pronunciation;

public class PronunciationBatchTests
{
    private readonly PronunciationBatchService.Word _word = new(Guid.NewGuid(), "국물", "sho‘rva suvi", 1);

    [Fact]
    public void A_reading_and_an_uncertain_answer_are_distinct_and_neither_is_approved()
    {
        var other = new PronunciationBatchService.Word(Guid.NewGuid(), "학교", "maktab", 2);
        var json = JsonSerializer.Serialize(new { words = new object[] {
            new { id = _word.Id, korean = "국물", reading = "궁물", explanationUz = "Tovush o‘zgaradi." },
            new { id = other.Id, korean = "학교", reading = (string?)null, explanationUz = (string?)null },
        }});
        var parsed = PronunciationBatchService.Parse(json, [_word, other]);
        Assert.Equal("궁물", parsed[_word.Id].Reading);
        Assert.Null(parsed[other.Id].Reading);
        Assert.Equal(2, parsed.Count);
    }

    [Theory]
    [InlineData("invalid json")]
    [InlineData("{\"words\":[null]}")]
    [InlineData("{\"words\":null}")]
    public void Malformed_rows_do_not_become_readings(string json) => Assert.Empty(PronunciationBatchService.Parse(json, [_word]));

    [Fact]
    public void Mismatched_ids_spelling_invalid_Hangul_and_duplicates_are_rejected()
    {
        string Row(Guid id, string korean, string reading) => JsonSerializer.Serialize(new { id, korean, reading, explanationUz = "Izoh." });
        foreach (var rows in new[] { Row(Guid.NewGuid(), "국물", "궁물"), Row(_word.Id, "다른말", "궁물"), Row(_word.Id, "국물", "gungmul"),
            Row(_word.Id, "국물", "궁물") + "," + Row(_word.Id, "국물", "궁물") })
            Assert.Empty(PronunciationBatchService.Parse("{\"words\":[" + rows + "]}", [_word]));
    }

    [Fact]
    public void The_request_preserves_ids_and_meanings_as_data_and_uses_existing_budgets()
    {
        var request = PronunciationBatchService.BuildRequest([_word]);
        Assert.Equal("catalog-draft", request.Feature);
        Assert.Contains(_word.Id.ToString(), request.UserContent);
        using var schema = JsonDocument.Parse(request.JsonSchema);
        Assert.Equal(10, schema.RootElement.GetProperty("properties").GetProperty("words").GetProperty("maxItems").GetInt32());
        Assert.Equal("group-deck", PronunciationBatchService.BuildRequest([_word], true).Feature);
    }
}
