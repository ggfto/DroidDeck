using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DroidDeck.Software
{
    public class SoftwareData
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(200)]
        public string? Name { get; set; }

        [System.ComponentModel.DataAnnotations.StringLength(500)]
        public string? Action { get; set; }
    }
}