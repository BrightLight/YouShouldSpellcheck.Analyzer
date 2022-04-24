namespace YouShouldSpellcheck.Analyzer
{
  using System.Threading;
  using System.Threading.Tasks;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.CSharp.Syntax;

  public abstract class MemberIdentifierCodeFixProvider<T> : IdentifierCodeFixProvider<T>
    where T : MemberDeclarationSyntax
  {
    protected override async Task<ISymbol> GetDeclaredSymbolAsync(Document document, T typeDecl, CancellationToken cancellationToken)
    {
      // Get the symbol representing the type to be renamed.
      var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
      return semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken);
    }
  }
}