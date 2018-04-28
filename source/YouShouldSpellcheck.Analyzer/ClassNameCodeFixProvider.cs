namespace YouShouldSpellcheck.Analyzer
{
  using System.Collections.Immutable;
  using System.Composition;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CodeFixes;
  using Microsoft.CodeAnalysis.CSharp.Syntax;

  [ExportCodeFixProvider(LanguageNames.CSharp, "", Name = nameof(ClassNameCodeFixProvider)), Shared]
  public class ClassNameCodeFixProvider : IdentifierCodeFixProvider<ClassDeclarationSyntax>
  {
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(ClassNameSpellcheckAnalyzer.ClassNameDiagnosticId);

    protected override SyntaxToken GetIdentifierToken(ClassDeclarationSyntax declarationToken)
    {
      return declarationToken.Identifier;
    }
  }
}