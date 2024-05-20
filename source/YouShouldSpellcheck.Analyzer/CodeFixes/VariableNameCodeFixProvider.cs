namespace YouShouldSpellcheck.Analyzer.CodeFixes
{
  using System.Collections.Immutable;
  using System.Composition;
  using System.Threading;
  using System.Threading.Tasks;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CodeFixes;
  using Microsoft.CodeAnalysis.CSharp.Syntax;

  [ExportCodeFixProvider(LanguageNames.CSharp, "", Name = nameof(VariableNameCodeFixProvider)), Shared]
  public class VariableNameCodeFixProvider : IdentifierCodeFixProvider<VariableDeclaratorSyntax>
  {
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(VariableNameSpellcheckAnalyzer.VariableNameDiagnosticId);

    protected override SyntaxToken GetIdentifierToken(VariableDeclaratorSyntax declarationToken)
    {
      return declarationToken.Identifier;
    }

    protected override async Task<ISymbol?> GetDeclaredSymbolAsync(Document document, VariableDeclaratorSyntax typeDecl, CancellationToken cancellationToken)
    {
      // Get the symbol representing the type to be renamed.
      var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
      return semanticModel?.GetDeclaredSymbol(typeDecl, cancellationToken);
    }
  }
}