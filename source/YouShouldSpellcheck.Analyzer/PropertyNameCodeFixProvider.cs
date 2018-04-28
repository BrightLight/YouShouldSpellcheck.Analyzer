namespace YouShouldSpellcheck.Analyzer
{
  using System.Collections.Immutable;
  using System.Composition;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CodeFixes;
  using Microsoft.CodeAnalysis.CSharp.Syntax;

  [ExportCodeFixProvider(LanguageNames.CSharp, "", Name = nameof(PropertyNameCodeFixProvider)), Shared]
  public class PropertyNameCodeFixProvider : IdentifierCodeFixProvider<PropertyDeclarationSyntax>
  {
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(PropertyNameSpellcheckAnalyzer.PropertyNameDiagnosticId);

    protected override SyntaxToken GetIdentifierToken(PropertyDeclarationSyntax declarationToken)
    {
      return declarationToken.Identifier;
    }
  }
}