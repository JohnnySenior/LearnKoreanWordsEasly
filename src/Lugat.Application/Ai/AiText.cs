using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Lugat.Application.Ai;

/// <summary>
/// Now and then a model writes the structure of its own answer into a text field: an explanation that ends with
/// <c>&lt;/explanationUz&gt; &lt;parameter name="trap"&gt;null</c> (L-43). Our texts never hold markup, so from such a
/// fragment on the text is cut off; the rest of the answer stays usable.
/// </summary>
public static partial class AiText
{
    /// <summary>The text up to a leaked fragment, trimmed; unchanged when there is none.</summary>
    public static string Clean(string text)
    {
        var m = Leak().Match(text);
        return m.Success ? text[..m.Index].TrimEnd() : text;
    }

    public static string? CleanOrNull(string? text) => text is null ? null : Clean(text);

    /// <summary>Every string in a JSON answer cleaned; the JSON as it was when nothing leaked or it does not parse.</summary>
    public static string CleanJson(string json)
    {
        // The markup can arrive escaped (\u003C/explanationUz\u003E), so the strings are checked, not the raw text.
        if (!json.Contains('<') && !json.Contains("\\u003C", StringComparison.OrdinalIgnoreCase))
        {
            return json;
        }

        try
        {
            var node = JsonNode.Parse(json);
            return node is null || !Leaks(node) ? json : Walk(node).ToJsonString();
        }
        catch (System.Text.Json.JsonException)
        {
            return json;
        }
    }

    /// <summary>
    /// Claude's tool answers sometimes carry a top-level list or object as a JSON string, at times holding the whole
    /// answer again: {"words":"{\"words\":[…]}"}. Where the schema expects an array or object, such a string is parsed
    /// and the repeated wrapper dropped; anything else, or JSON that does not parse, stays as it was.
    /// </summary>
    public static string Unwrap(string json, string schema)
    {
        if (!json.Contains("\"[", StringComparison.Ordinal) && !json.Contains("\"{", StringComparison.Ordinal))
        {
            return json;
        }

        try
        {
            if (JsonNode.Parse(json) is not JsonObject root || JsonNode.Parse(schema)?["properties"] is not JsonObject properties)
            {
                return json;
            }

            var changed = false;
            foreach (var key in root.Select(p => p.Key).ToList())
            {
                var type = properties[key]?["type"]?.GetValue<string>();
                if (type is not ("array" or "object"))
                {
                    continue;
                }

                var value = root[key];
                for (var depth = 0; depth < 3 && value is JsonValue v && v.TryGetValue<string>(out var text); depth++)
                {
                    var inner = JsonNode.Parse(text);
                    // The string can hold the whole answer again: {"words":[…]} inside "words".
                    value = inner is JsonObject wrapper && wrapper.Count == 1 && wrapper[key] is { } again && type == "array"
                        ? again.DeepClone()
                        : inner;
                    changed = true;
                }

                root[key] = value?.DeepClone();
            }

            return changed ? root.ToJsonString() : json;
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or FormatException)
        {
            return json;
        }
    }

    private static bool Leaks(JsonNode? node) => node switch
    {
        JsonObject o => o.Any(p => Leaks(p.Value)),
        JsonArray a => a.Any(Leaks),
        JsonValue v when v.TryGetValue<string>(out var s) => Leak().IsMatch(s),
        _ => false,
    };

    private static JsonNode Walk(JsonNode node)
    {
        switch (node)
        {
            case JsonObject o:
                foreach (var key in o.Select(p => p.Key).ToList())
                {
                    if (o[key] is { } child)
                    {
                        o[key] = Walk(child.DeepClone());
                    }
                }

                return o;
            case JsonArray a:
                for (var i = 0; i < a.Count; i++)
                {
                    if (a[i] is { } child)
                    {
                        a[i] = Walk(child.DeepClone());
                    }
                }

                return a;
            case JsonValue v when v.TryGetValue<string>(out var s):
                return JsonValue.Create(Clean(s))!;
            default:
                return node;
        }
    }

    // A closing tag such as </explanationUz>, or the opening of a tool-call element.
    [GeneratedRegex(@"</[A-Za-z_][\w:.-]*\s*>|<(?:parameter|invoke|function_calls|antml:)", RegexOptions.CultureInvariant)]
    private static partial Regex Leak();
}
