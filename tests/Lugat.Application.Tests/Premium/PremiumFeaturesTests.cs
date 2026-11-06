using Lugat.Contracts;

namespace Lugat.Application.Tests.Premium;

/// <summary>L-64: a refused API path names the feature the learner wanted.</summary>
public class PremiumFeaturesTests
{
    [Theory]
    [InlineData("/api/mistakes", "xatolar")]
    [InlineData("/api/weak-points", "zaif")]
    [InlineData("/api/ladders", "zinapoya")]
    [InlineData("/api/ladders/0198f000-0000-7000-8000-000000000001/answers", "zinapoya")]
    [InlineData("/api/traps", "tuzoqlar")]
    [InlineData("/api/listening/abc", "tinglash")]
    [InlineData("/api/practice/sentences?x=1", "gap")]
    [InlineData("/API/Mistakes/", "xatolar")]
    [InlineData("/api/mistakesx", null)] // a look-alike path is not the feature
    [InlineData("/api/tests", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void The_key_comes_from_the_path(string? path, string? key) => Assert.Equal(key, PremiumFeatures.KeyOf(path));
}
