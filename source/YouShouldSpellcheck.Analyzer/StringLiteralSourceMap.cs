namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Collections.Generic;
  using System.Collections.Immutable;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.Text;

  /// <summary>Maps offsets in a literal's decoded value to offsets in its source token.</summary>
  internal sealed class StringLiteralSourceMap
  {
    private readonly int[] sourcePositions;

    private StringLiteralSourceMap(SyntaxToken token, int[] sourcePositions)
    {
      this.Token = token;
      this.sourcePositions = sourcePositions;
    }

    public SyntaxToken Token { get; }

    public Location ContentLocation => Location.Create(
      this.Token.SyntaxTree!,
      TextSpan.FromBounds(this.sourcePositions[0], this.sourcePositions[this.sourcePositions.Length - 1]));

    public int GetSourcePosition(int valueOffset) => this.sourcePositions[valueOffset];

    public ImmutableArray<int> SourcePositions => this.sourcePositions.ToImmutableArray();

    public static StringLiteralSourceMap Create(SyntaxToken token)
    {
      var text = token.Text;
      var value = token.ValueText;
      var tokenStart = token.SpanStart;
      var positions = new List<int>(value.Length + 1);
      var contentStart = GetContentStart(text);
      positions.Add(tokenStart + contentStart);

      if (text.StartsWith("@\"", StringComparison.Ordinal))
      {
        MapVerbatim(text, value, tokenStart, contentStart, positions);
      }
      else if (IsRaw(text))
      {
        MapRaw(text, value, tokenStart, contentStart, positions);
      }
      else
      {
        MapRegular(text, value, tokenStart, contentStart, positions);
      }

      // A malformed literal can have no corresponding source characters. Keep locations valid.
      while (positions.Count <= value.Length)
      {
        positions.Add(token.Span.End - 1);
      }

      return new StringLiteralSourceMap(token, positions.ToArray());
    }

    private static int GetContentStart(string text)
    {
      if (text.StartsWith("@\"", StringComparison.Ordinal)) return 2;
      var quote = text.IndexOf('"');
      return quote < 0 ? 0 : quote + CountQuotes(text, quote);
    }

    private static bool IsRaw(string text)
    {
      var quote = text.IndexOf('"');
      return quote >= 0 && CountQuotes(text, quote) >= 3;
    }

    private static int CountQuotes(string text, int start)
    {
      var count = 0;
      while (start + count < text.Length && text[start + count] == '"') count++;
      return count;
    }

    private static void MapVerbatim(string text, string value, int tokenStart, int source, List<int> positions)
    {
      for (var valueIndex = 0; valueIndex < value.Length && source < text.Length - 1; valueIndex++)
      {
        if (text[source] == '"' && source + 1 < text.Length && text[source + 1] == '"') source += 2;
        else source++;
        positions.Add(tokenStart + source);
      }
    }

    private static void MapRegular(string text, string value, int tokenStart, int source, List<int> positions)
    {
      var valueIndex = 0;
      while (valueIndex < value.Length && source < text.Length - 1)
      {
        var consumed = 1;
        var produced = 1;
        if (text[source] == '\\' && source + 1 < text.Length)
        {
          var escape = text[source + 1];
          consumed = escape == 'u' ? 6 : escape == 'U' ? 10 : escape == 'x' ? 2 + CountHexDigits(text, source + 2, 4) : 2;
          if (escape == 'U' && source + 10 <= text.Length && int.TryParse(text.Substring(source + 2, 8), System.Globalization.NumberStyles.HexNumber, null, out var codePoint))
          {
            produced = char.ConvertFromUtf32(codePoint).Length;
          }
        }

        source += consumed;
        for (var producedIndex = 0; producedIndex < produced && valueIndex < value.Length; producedIndex++, valueIndex++)
        {
          positions.Add(tokenStart + (producedIndex + 1 == produced ? source : source - consumed));
        }
      }
    }

    private static int CountHexDigits(string text, int start, int maximum)
    {
      var count = 0;
      while (count < maximum && start + count < text.Length && Uri.IsHexDigit(text[start + count])) count++;
      return count;
    }

    private static void MapRaw(string text, string value, int tokenStart, int source, List<int> positions)
    {
      var closingQuotes = CountTrailingQuotes(text);
      var contentEnd = text.Length - Math.Max(closingQuotes, 3);
      for (var valueIndex = 0; valueIndex < value.Length; valueIndex++)
      {
        while (source < contentEnd && text[source] != value[valueIndex]) source++;
        if (source >= contentEnd) break;
        source++;
        positions.Add(tokenStart + source);
      }
    }

    private static int CountTrailingQuotes(string text)
    {
      var count = 0;
      for (var index = text.Length - 1; index >= 0 && text[index] == '"'; index--) count++;
      return count;
    }
  }
}
