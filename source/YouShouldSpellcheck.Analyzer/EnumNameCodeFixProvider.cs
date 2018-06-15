namespace YouShouldSpellcheck.Analyzer
{
  using System.Collections.Immutable;
  using System.Composition;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CodeFixes;
  using Microsoft.CodeAnalysis.CSharp.Syntax;

  [ExportCodeFixProvider(LanguageNames.CSharp, "", Name = nameof(EnumNameCodeFixProvider)), Shared]
  public class EnumNameCodeFixProvider : MemberIdentifierCodeFixProvider<EnumDeclarationSyntax>
  {
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(EnumNameSpellcheckAnalyzer.EnumNameDiagnosticId);

    protected override SyntaxToken GetIdentifierToken(EnumDeclarationSyntax declarationToken)
    {
      return declarationToken.Identifier;
    }
  }
}