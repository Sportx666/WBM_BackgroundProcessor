using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WBM_BackgroundProcessor.Models.Paperless_DB
{
    public class PALLET_DW
    {
        [Key]
        public string KEYID { get; set; }
        [Column("SITE.NO")]
        public string? SITE_NO { get; set; }
        public string? PAL_NO { get; set; }
        public string? PROD_NO { get; set; }
        public string? CUR_POSITION { get; set; }
        public string? CUR_ZONE { get; set; }
        public string? PAL_STATUS { get; set; }
        public string? JOB_TYPE { get; set; }
        public decimal? PAL_QTY { get; set; }
        public decimal? UNIT_QTY { get; set; }
        public decimal? PAL_TOT_QTY { get; set; }
        public decimal? PAL_WEIGHT { get; set; }
        public decimal? PAL_WIDTH { get; set; }
        public decimal? PAL_DEPTH { get; set; }
        public decimal? PAL_HEIGHT { get; set; }
        public string? SUPP_ORD_NO { get; set; }
        public DateTime? DATE_RECV { get; set; }
        public TimeSpan? TIME_RECV { get; set; }
        public string? BATCH_NO { get; set; }
        public decimal? CHKIN_CARTON_QTY { get; set; }
        public string? RECP_TYPE { get; set; }
        public decimal? QTY_BU_1 { get; set; }
        public decimal? QTY_BU_2 { get; set; }
        public DateTime? USE_BY_DATE { get; set; }
        public decimal? BATCH_QTY { get; set; }
        public decimal? TARE_WEIGHT { get; set; }
        public string? LOT_NO { get; set; }
        public DateTime? LST_CHRG_DATE { get; set; }
        public TimeSpan? LST_CHRG_TIME { get; set; }
        public string? WAY_BILL_NO { get; set; }
        public string? PAL_TYPE { get; set; }
        public string? ACTION_CODE { get; set; }
        public DateTime? ACTION_DATE { get; set; }
        public string? CONTAINER_NO { get; set; }
        public string? PHEAD_ID { get; set; }
        public DateTime? INV_DATE { get; set; }
        public string? CONTAINER_SIZE { get; set; }
        public string? UNLOAD_TYPE { get; set; }
        public string? PAL_HOLD_STATUS { get; set; }
        public string? PAL_HOLD_REF { get; set; }
        public string? PAL_HOLD_REASON { get; set; }
        public DateTime? CREATE_DATE { get; set; }
    }
}
