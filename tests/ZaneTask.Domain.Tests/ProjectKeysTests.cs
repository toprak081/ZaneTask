using ZaneTask.Contracts;

namespace ZaneTask.Domain.Tests;

/// <summary>Key suggestions shown in the "New project" dialog and used by the API when no key is given.</summary>
public class ProjectKeysTests
{
    [Theory]
    [InlineData("Website", "WEB")]
    [InlineData("Mobile App", "MA")]
    [InlineData("Web Sitesi Projesi", "WSP")]
    [InlineData("Şirket İçi Ürün", "SIU")]
    [InlineData("çğış", "CGI")]
    [InlineData("Q4 marketing site plan", "QMSP")]
    [InlineData("A", "PRJ")]
    [InlineData("   ", "PRJ")]
    [InlineData("2026 Roadmap", "ROA")]
    [InlineData("!!!", "PRJ")]
    public void Suggest_builds_a_valid_key_from_the_name(string name, string expected)
    {
        var key = ProjectKeys.Suggest(name);

        Assert.Equal(expected, key);
        Assert.True(ProjectKeys.IsValid(key));
    }
}
