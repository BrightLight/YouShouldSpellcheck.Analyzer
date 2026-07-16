namespace YouShouldSpellcheck.Analyzer.Test
{
  using System;
  using System.Linq;
  using System.Reflection;
  using System.Runtime.CompilerServices;
  using Microsoft.CodeAnalysis.Diagnostics;
  using NUnit.Framework;

  [TestFixture]
  public class AnalyzerSafetyTests
  {
    [Test]
    public void AnalyzerAssemblyHasVersionedIdentity()
    {
      var assemblyVersion = typeof(SpellcheckAnalyzerBase).Assembly.GetName().Version;

      Assert.That(assemblyVersion, Is.Not.Null);
      Assert.That(assemblyVersion, Is.Not.EqualTo(new Version(0, 0, 0, 0)));
    }

    [Test]
    public void AnalyzerCallbacksDoNotUseAsyncVoid()
    {
      var asyncVoidMethods = typeof(SpellcheckAnalyzerBase).Assembly
        .GetTypes()
        .Where(type => typeof(DiagnosticAnalyzer).IsAssignableFrom(type))
        .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        .Where(method => method.ReturnType == typeof(void))
        .Where(method => method.GetCustomAttribute<AsyncStateMachineAttribute>() != null)
        .Select(method => $"{method.DeclaringType?.FullName}.{method.Name}")
        .ToArray();

      Assert.That(asyncVoidMethods, Is.Empty, "Analyzer methods must not outlive their Roslyn callback context.");
    }

    [Test]
    public void ExportedDiagnosticsHaveDescriptions()
    {
      var diagnosticsWithoutDescriptions = new YouShouldSpellcheckDiagnosticAnalyzer()
        .SupportedDiagnostics
        .Where(descriptor => string.IsNullOrWhiteSpace(descriptor.Description.ToString()))
        .Select(descriptor => descriptor.Id)
        .ToArray();

      Assert.That(diagnosticsWithoutDescriptions, Is.Empty, "Every exported rule must provide a description for analyzer hosts.");
    }
  }
}
