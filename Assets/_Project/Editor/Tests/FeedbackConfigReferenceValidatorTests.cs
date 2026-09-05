using NUnit.Framework;

public sealed class FeedbackConfigReferenceValidatorTests
{
    [Test]
    public void KnownFeedbackComponents_HaveExpectedFeedbackConfigReferences()
    {
        var issues = FeedbackConfigReferenceValidator.Validate();

        if (issues.Count == 0)
        {
            Assert.Pass();
            return;
        }

        Assert.Fail(string.Join("\n", issues));
    }
}
