using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WBM_BackgroundProcessor.Models.Paperless_DB
{
    public class CONTAINER_DW
    {
        [Key]
        public string KEYID { get; set; }
        [Column("SITE.NO")]
        public string? SITE_NO { get; set; }
        public string? CONTAINER { get; set; }
        public string? PONUMBER { get; set; }
        public DateTime? DATE_RECD { get; set; }
        public string? SUPP_INVNO { get; set; }
        public TimeSpan? TIME_RECD { get; set; }
        public string? CONTAINER_TYPE { get; set; }
        public string? CONT_SIZE { get; set; }
        public string? CONT_VESSEL_NAME { get; set; }
        public string? CONT_VOYAGE_NO { get; set; }
        public string? CONT_PROD_LINE_NO { get; set; }
        public string? CONT_PRODUCTS { get; set; }
        public decimal? CONT_PROD_CTN_QTY { get; set; }
        public decimal? CONT_PRODUCT_QTY { get; set; }
        public string? CONT_TI_COUNT { get; set; }
        public string? CONT_HI_COUNT { get; set; }
        public string? OWNER { get; set; }
        public DateTime? ETA_DATE { get; set; }
        public DateTime? UNPACK_DATE { get; set; }
        public TimeSpan? UNPACK_TIME { get; set; }
        public string? CONTAINER_STATUS { get; set; }
        public string? PROD_TYPE { get; set; }
        public DateTime? USE_BY_DATE { get; set; }
        public string? ACT_BATCH_DATE { get; set; }
        public string? LOT_NO { get; set; }
    }
}
