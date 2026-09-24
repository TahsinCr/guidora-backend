using Business.BusinessRules.Moderation;
using Business.DependencyResolvers.Autofac;
using Autofac;

namespace Tests.Business.Moderation;

public sealed class DeterministicContentModerationRuleTests
{
    private readonly DeterministicContentModerationRule _rule = new();

    [Fact]
    public void AutofacBuildInitializesOneSingletonMatcher()
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<AutofacBusinessModule>();

        using var container = builder.Build();

        Assert.Same(container.Resolve<IContentModerationRule>(), container.Resolve<IContentModerationRule>());
    }

    [Theory]
    [InlineData("fuck")]
    [InlineData("f.u.c.k")]
    [InlineData("fück")]
    [InlineData("fuuuuuck")]
    public void RejectsEnglishBlockedTermsAndCommonObfuscation(string content)
    {
        Assert.False(_rule.EnsureAllowed(content).Success);
    }

    [Fact]
    public void NormalizesTurkishDotlessIAndSeparators()
    {
        Assert.False(_rule.EnsureAllowed("s.ı.k").Success);
    }

    [Theory]
    [InlineData("classical engineering tools")]
    [InlineData("a useful assessment workflow")]
    [InlineData("Clean architecture for product catalogs")]
    public void DoesNotRejectTermsOnlyFoundInsideLargerWords(string content)
    {
        Assert.True(_rule.EnsureAllowed(content).Success);
    }

    [Fact]
    public void EmptyAndNullValuesAreAllowed()
    {
        Assert.True(_rule.EnsureAllowed(null, string.Empty, "   ").Success);
    }
}
