using System.Text.Json;
using Lugat.Application.Ai;

namespace Lugat.Application.Tests.Ai;

public class AiTextTests
{
    // What a learner saw in the sentence ladder (L-43): the end of the model's own tool call inside the explanation.
    private const string Leaked = "\"Do‘konda\" - harakat joyi uchun \"에서\" qo‘shimchasi ishlatiladi (\"가게에 가다\" - do‘konga bormoq).</explanationUz> <parameter name=\"trap\">null";

    [Fact]
    public void A_leaked_tool_fragment_is_cut_off_and_the_rest_stays()
    {
        Assert.Equal("\"Do‘konda\" - harakat joyi uchun \"에서\" qo‘shimchasi ishlatiladi (\"가게에 가다\" - do‘konga bormoq).", AiText.Clean(Leaked));
        Assert.Equal("Oddiy izoh: 학교에 가요.", AiText.Clean("Oddiy izoh: 학교에 가요."));
        Assert.Equal("Matn", AiText.Clean("Matn <invoke name=\"x\">"));
        Assert.Null(AiText.CleanOrNull(null));
    }

    [Fact]
    public void Every_string_of_a_json_answer_is_cleaned()
    {
        var json = JsonSerializer.Serialize(new { verdict = "correct", explanationUz = Leaked, items = new[] { new { hint = "a</hintUz>b" } }, n = 3 });

        var clean = JsonDocument.Parse(AiText.CleanJson(json)).RootElement;

        Assert.EndsWith("do‘konga bormoq).", clean.GetProperty("explanationUz").GetString());
        Assert.Equal("a", clean.GetProperty("items")[0].GetProperty("hint").GetString());
        Assert.Equal(3, clean.GetProperty("n").GetInt32());
        Assert.Equal("{\"ok\":\"<b>x\"}", AiText.CleanJson("{\"ok\":\"<b>x\"}")); // an opening tag alone is not a leak
    }

    private const string ListSchema = """{"type":"object","properties":{"words":{"type":"array"},"note":{"type":"string"}}}""";

    [Theory]
    [InlineData("""{"words":"[{\"id\":1}]"}""", """{"words":[{"id":1}]}""")]
    [InlineData("""{"words":"{\"words\":[{\"id\":1}]}"}""", """{"words":[{"id":1}]}""")]
    [InlineData("""{"words":[{"id":1}],"note":"[x]"}""", """{"words":[{"id":1}],"note":"[x]"}""")]
    [InlineData("""{"words":"[broken"}""", """{"words":"[broken"}""")]
    public void A_list_sent_as_a_string_is_unwrapped_only_where_the_schema_expects_one(string json, string expected) =>
        Assert.Equal(expected, AiText.Unwrap(json, ListSchema));
}
