using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WBM_BackgroundProcessor.Models.Paperless_DB
{
    public class CARRIERS_DW
    {
        [Key]
        public string KEYID { get; set; }
        [Column("SITE.NO")]
        public string? SITE_NO { get; set; }
        public string? CARRIER_CODE { get; set; }

    }
}
