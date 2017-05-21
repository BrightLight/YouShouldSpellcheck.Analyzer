using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YouShouldSpellcheck.Analyzer
{
  using System.Collections.Immutable;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.CSharp.Syntax;
  using Microsoft.CodeAnalysis.Diagnostics;

  [DiagnosticAnalyzer(LanguageNames.CSharp)]
  public class VariableNameSpellcheckAnalyzer : SpellcheckAnalyzerBase
  {
    public const string VariableNameDiagnosticId = "YS102";

    private const string VariableNameRuleDescription = "Variable name should be spelled correctly.";

    private static readonly DiagnosticDescriptor VariableNameRule = new DiagnosticDescriptor(VariableNameDiagnosticId, Title, MessageFormat, NamingCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: VariableNameRuleDescription);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(VariableNameRule);

    public override void Initialize(AnalysisContext context)
    {
      context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
      ////context.EnableConcurrentExecution();

      // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
      // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
      context.RegisterSyntaxNodeAction(this.AnalyzeVariableDeclarator, SyntaxKind.VariableDeclarator);
    }

    private void AnalyzeVariableDeclarator(SyntaxNodeAnalysisContext context)
    {
      try
      {
        var variableDeclaratorSyntax = context.Node as VariableDeclaratorSyntax;
        if (variableDeclaratorSyntax != null)
        {
          var identifierToken = variableDeclaratorSyntax.ChildTokens().FirstOrDefault(x => x.IsKind(SyntaxKind.IdentifierToken));
          if (identifierToken != null)
          {
            var text = identifierToken.ValueText;
            this.CheckWord(VariableNameRule, text, identifierToken.GetLocation(), context);
          }
        }
      }
      catch (Exception e)
      {
        Console.WriteLine(e);
      }
    }
  }
}
