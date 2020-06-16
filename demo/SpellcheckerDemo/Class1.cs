using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace SpellcheckerDemo
{
  /// <summary>
  /// This is a smple test.
  /// </summary>
  public class Class1
  {
    [DisplayName("Name1")]
    [Display(Name = "Infrmation 1")]
    public string Namez1 { get; }

    /// <summary>
    /// 
    /// </summary>
    [DisplayName("Name2")]
    public string Name2 { get; }

    [Display(Name = "Special \"escapng\" tet with\nmistakes and one more thing:\na new lne\\lines and all")]
    public string Name3 { get; }

    [DisplayName("Informtion 1")]
    public string Info1 { get; }

    [DisplayName("Name1")]
    public string Info2 { get; set; }

    [Display(Name = @"Special ""escapng"" tet with\nmistakes and one more thing:
a new lne\\lines and all")]
    public string Info3 { get; }
  }
}