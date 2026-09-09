using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WBM_BackgroundProcessor.Models.Paperless_DB
{
    public class TRIGGER_ZONE_DW
    {
        public int ID { get; set; }
        public string KEYID { get; set; }
        public string ACTION { get; set; }
        public System.DateTime ACTIONDATE { get; set; }
    }
}
