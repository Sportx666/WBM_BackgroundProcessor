using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WBM_BackgroundProcessor.Models.Paperless_DB
{
    public class ZONE_DW
    {
        [Key]
        public string KEYID { get; set; }
        [Column("SITE.NO")]
        public string? SITE_NO { get; set; }
        public string? ZONE { get; set; }
        public string? ZONE_DESC { get; set; }
        public string? PA_ALG { get; set; }
        public string? PICK_ALG { get; set; }
    }
}
