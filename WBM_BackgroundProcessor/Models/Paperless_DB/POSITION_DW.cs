using System.ComponentModel.DataAnnotations;

namespace WBM_BackgroundProcessor.Models.Paperless_DB
{
    public class POSITION_DW
    {
        [Key]
        public string KEYID { get; set; }
        public string SITE_NO { get; set; }
        public string POSITION { get; set; }
        public string POS_CHK_NO { get; set; }
        public string POS_STATUS { get; set; }
        public string POS_TYPE { get; set; }
        public string POS_BLK_STORAGE { get; set; }
        public string ZONE { get; set; }
        public string SZONE { get; set; }
        public Nullable<decimal> POS_WEIGHT { get; set; }
        public Nullable<decimal> POS_WIDTH { get; set; }
        public Nullable<decimal> POS_DEPTH { get; set; }
        public Nullable<decimal> POS_HEIGHT { get; set; }
        public string PAL_NO { get; set; }
        public string TIE_NO { get; set; }
        public Nullable<decimal> POS_CUB_LEFT { get; set; }
        public Nullable<decimal> PA_CUB { get; set; }
        public string WALK_ORD { get; set; }
        public Nullable<decimal> NO_PALS { get; set; }
        public Nullable<decimal> MAX_PALLETS { get; set; }
        public string EMPTY_FLG { get; set; }
        public string RACK_TYPE { get; set; }
        public Nullable<decimal> MIN_PALLETS { get; set; }

    }
}
