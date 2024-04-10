namespace YouShouldSpellcheck.Analyzer.CodeFixes
{
  using System.Collections.Immutable;
  using System.Composition;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CodeFixes;
  using Microsoft.CodeAnalysis.CSharp.Syntax;

  [ExportCodeFixProvider(LanguageNames.CSharp, "", Name = nameof(MethodNameCodeFixProvider)), Shared]
  public class MethodNameCodeFixProvider : MemberIdentifierCodeFixProvider<MethodDeclarationSyntax>
  {
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(MethodNameSpellcheckAnalyzer.MethodNameDiagnosticId);

    protected override SyntaxToken GetIdentifierToken(MethodDeclarationSyntax declarationToken)
    {
      return declarationToken.Identifier;
    }
  }
}