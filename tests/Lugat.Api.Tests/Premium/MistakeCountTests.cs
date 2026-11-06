using System.Net;
using System.Net.Http.Json;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Contracts;
using Lugat.Domain.Learning;

namespace Lugat.Api.Tests.Premium;

/// <summary>L-64: the Premium page shows a free learner how many words they missed — the number, never the words.</summary>
public class MistakeCountTests
{
    [Fact]
    public async Task A_free_learner_gets_the_number_of_missed_words_and_not_the_words()
    {
        await using var factory = new ApiFactory { Premium = false, TableWords = ApiFactory.Words("가", 3) };
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        Assert.Equal(new MistakeCountDto(0, 60), await client.GetFromJsonAsync<MistakeCountDto>("/api/mistakes/count", ApiFactory.Json));

        (await client.PostAsync(ApiFactory.TestWordsPath, null)).EnsureSuccessStatusCode();
        var session = (await (await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, false), ApiFactory.Json))
            .Content.ReadFromJsonAsync<StartSessionResponse>(ApiFactory.Json))!.Session!;
        (await client.PostAsJsonAsync($"/api/sessions/{session.Id}/answers",
            new AnswerRequest(Guid.NewGuid(), session.Position, ReviewAnswer.Forgot, false, 2000), ApiFactory.Json)).EnsureSuccessStatusCode();

        var response = await client.GetAsync("/api/mistakes/count");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Equal("""{"count":1,"days":60}""", json); // only the two numbers: no word, no meaning
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/mistakes")).StatusCode); // the notebook stays Premium
    }

    [Fact]
    public async Task A_guest_gets_401()
    {
        await using var factory = new ApiFactory();
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/mistakes/count")).StatusCode);
    }
}
