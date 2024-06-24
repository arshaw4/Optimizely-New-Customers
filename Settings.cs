using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewMonthlyContacts
{
    public class Settings
    {
        public string baseUrl {  get; set; }
        public string optimizelyUsername { get; set; }
        public string optimizelyPassword { get; set; }
        public string optimizelyToken { get; set; }
        public string constantContactUser { get; set; }
        public string constantContactPassword { get; set; }
        public string constantContactToken { get; set; }
        public Boolean spreadsheetMode { get; set; }
        public string savePath { get; set; }
    }
}
