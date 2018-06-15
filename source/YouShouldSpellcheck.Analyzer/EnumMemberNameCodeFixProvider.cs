namespace YouShouldSpellcheck.Analyzer
{
  using System.Collections.Immutable;
  using System.Composition;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CodeFixes;
  using Microsoft.CodeAnalysis.CSharp.Syntax;

  [ExportCodeFixProvider(LanguageNames.CSharp, "", Name = nameof(EnumMemberNameCodeFixProvider)), Shared]
  public class EnumMemberNameCodeFixProvider : IdentifierCodeFixProvider<EnumMemberDeclarationSyntax>
  {
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(EnumMemberNameSpellcheckAnalyzer.EnumMemberNameDiagnosticId);

    protected override SyntaxToken GetIdentifierToken(EnumMemberDeclarationSyntax declarationToken)
    {
      return declarationToken.Identifier;
    }
  }
}