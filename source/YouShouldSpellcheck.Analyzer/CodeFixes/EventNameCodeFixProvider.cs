namespace YouShouldSpellcheck.Analyzer.CodeFixes
{
  using System.Collections.Immutable;
  using System.Composition;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CodeFixes;
  using Microsoft.CodeAnalysis.CSharp.Syntax;

  [ExportCodeFixProvider(LanguageNames.CSharp, "", Name = nameof(EventNameCodeFixProvider)), Shared]
  public class EventNameCodeFixProvider : MemberIdentifierCodeFixProvider<EventDeclarationSyntax>
  {
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(EventNameSpellcheckAnalyzer.EventNameDiagnosticId);

    protected override SyntaxToken GetIdentifierToken(EventDeclarationSyntax declarationToken)
    {
      return declarationToken.Identifier;
    }
  }
}