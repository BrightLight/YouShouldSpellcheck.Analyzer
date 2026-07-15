namespace YouShouldSpellcheck.Analyzer.Test
{
  using System.Linq;
  using NUnit.Framework;

  [TestFixture]
  public class LanguageToolIntegrationTests
  {
    [Test]
    public void CompilerAnalyzerHasNoLanguageToolNetworkDependency()
    {
      var referencedAssemblies = typeof(SpellcheckAnalyzerBase).Assembly
        .GetReferencedAssemblies()
        .Select(assembly => assembly.Name)
        .ToArray();

      Assert.That(referencedAssemblies, Does.Not.Contain("RestSharp"));
      Assert.That(referencedAssemblies, Does.Not.Contain("Microsoft.VisualStudio.Threading"));
    }
  }
}
