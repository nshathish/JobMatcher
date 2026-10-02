using JobMatcher.Application.Jobs;

namespace JobMatcher.Application.Tests.Jobs;

public sealed class JobSourcePolicyTests
{
    [Theory]
    [InlineData("https://uk.indeed.com/viewjob?jk=abc", true)]
    [InlineData("https://indeed.com/viewjob?jk=abc", true)]
    [InlineData("https://indeed.co.uk/viewjob?jk=abc", true)]
    [InlineData("https://example.indeed.com/viewjob?jk=abc", true)]
    [InlineData("https://notindeed.com/viewjob?jk=abc", false)]
    [InlineData("https://example.com/jobs/123", false)]
    public void IsIndeed_ClassifiesTrustedIndeedHosts(string url, bool expected)
    {
        Assert.Equal(expected, JobSourcePolicy.IsIndeed(new Uri(url)));
    }
}
