using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YouShouldSpellcheck.Analyzer
{
  public class DescAttribute : Attribute
  {
    public DescAttribute(string description)
    {
    }
  }

  /// <summary>
  /// This is my summary of the class.
  /// </summary>
  /// <remarks>
  /// My special remarks.
  /// In two lines.
  /// </remarks>
  [Desc("Some text")]
  class Foo
  {
  }
}
