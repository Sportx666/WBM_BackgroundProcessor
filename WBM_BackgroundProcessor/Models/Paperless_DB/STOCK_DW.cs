using System.ComponentModel.DataAnnotations;

namespace WBM_BackgroundProcessor.Models.Paperless_DB
{
    public class STOCK_DW
    {
        [Key]
        public string KEYID { get; set; }
        public string SITE_NO { get; set; }
        public string PROD_NO { get; set; }
        public string PROD_DESC { get; set; }
        public Nullable<decimal> PROD_WEIGHT { get; set; }
        public Nullable<decimal> PROD_WIDTH { get; set; }
        public Nullable<decimal> PROD_DEPTH { get; set; }
        public Nullable<decimal> PROD_HEIGHT { get; set; }
        public Nullable<decimal> UNIT_QTY { get; set; }
        public Nullable<decimal> PAL_QTY { get; set; }
        public Nullable<decimal> CARTON_QTY { get; set; }
        public string RANDOM_PICKING { get; set; }
        public string FIFO_FLG { get; set; }
        public string PROD_TYPE { get; set; }
        public string PFACE_POSITION { get; set; }
        public Nullable<decimal> PFACE_REP_LEV { get; set; }
        public string PPICK_CHK { get; set; }
        public string UFACE_POSITION { get; set; }
        public Nullable<decimal> UPICK_REP_LEV { get; set; }
        public string UPICK_CHK_COUNT { get; set; }
        public string AUTO_CLEAR_LOAD { get; set; }
        public string REDUNDANT_FLG { get; set; }
        public Nullable<decimal> UREP_QTY { get; set; }
        public string PROD_MOB_DESC { get; set; }
        public string RETURN_REP_FLG { get; set; }
        public string PFACE_PAL_NO { get; set; }
        public Nullable<decimal> EXPIRY_DAYS { get; set; }
        public string QUA_DAYS { get; set; }
        public string AUTO_BATCH_REL { get; set; }
        public string PAL_LAYERS { get; set; }
        public Nullable<decimal> LAYER_CARTONS { get; set; }
        public Nullable<decimal> BULK_AGE_DAYS { get; set; }
        public string SLIFE_DAYS { get; set; }
        public string PROD_GROUP { get; set; }
        public Nullable<decimal> BU1_QTY { get; set; }
        public Nullable<decimal> BU2_QTY { get; set; }
        public Nullable<decimal> BU3_QTY { get; set; }
        public Nullable<decimal> BU4_QTY { get; set; }
        public string PROD_SCAN_COUNT { get; set; }
        public string SLIFE_PER_OR_DAYS { get; set; }
        public Nullable<decimal> DISTRESSED_DAYS { get; set; }
        public Nullable<decimal> BU3_HEIGHT { get; set; }
        public Nullable<decimal> BU3_WIDTH { get; set; }
        public Nullable<decimal> BU3_DEPTH { get; set; }
        public Nullable<decimal> BU3_WEIGHT { get; set; }
        public string PA_TYPE { get; set; }
        public string NON_STOCK_FLG { get; set; }
        public string OVERSIZE_PER { get; set; }
        public string CRITICAL_DAYS { get; set; }
        public string UN_CODE { get; set; }
        public Nullable<decimal> MAX_PFACE_QTY { get; set; }
        public string EAN_PROD_CODE { get; set; }
        public Nullable<System.DateTime> CREATE_DATE { get; set; }
        public string OWNER { get; set; }
        public string SERIAL_FLG { get; set; }
        public string UN_HAZARDOUS { get; set; }
        public string UN_SHIP_NAME { get; set; }
        public string COMBUSTIBLE { get; set; }
        public Nullable<decimal> MIN_LEVEL { get; set; }
        public Nullable<decimal> MAX_LEVEL { get; set; }
        public string ROTATION_DAYS { get; set; }
        public string PALLET_TYPE { get; set; }
        public string WALK_ORD { get; set; }
        public Nullable<System.DateTime> UPDATE_DATE { get; set; }
        public Nullable<System.TimeSpan> UPDATE_TIME { get; set; }
        public string UPDATE_USER { get; set; }
        public string BATCH_MANAGED { get; set; }
        public string COUNT_BACK_MODE { get; set; }
        public string DG_CLASS { get; set; }
        public string SUB_RISK { get; set; }
        public string MAND_UBD_FLG { get; set; }
        public string PROD_STAT { get; set; }
        public string STOCK_KJ_KG { get; set; }

    }
}
