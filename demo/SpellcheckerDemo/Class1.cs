using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace SpellcheckerDemo
{
    public class Class1
    {
        [DisplayName("Name1")]
        [Display(Name ="Infrmation 1")]
        public string Namez1 { get; }

        [DisplayName("Name1")]
        public string Name2 { get; }

        [DisplayName("Informtion 1")]
        public string Info1 { get; }

        [DisplayName("Name1")]
        public string Info2 { get; set; }
    }
}
