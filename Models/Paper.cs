using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccelConfHtmlScraper.Models
{
    public class Paper
    {
        public int Year { get; set; }   
        public string Category { get; set; }
        public string SubCategory { get; set; }
        public string PaperId { get; set; }
        public string PaperName { get; set; }
        public string AuthorName { get; set; }
        public string AuthorPlace { get; set; }
        public bool IsPrimaryAuthor { get; set; }
        public string Description { get; set; }
    }
}
