using System.ComponentModel.DataAnnotations;

namespace WBM_BackgroundProcessor.Models.Paperless_DB
{
    public class PICK_DETAIL_DW
    {
        [Key]
        public string KEYID { get; set; }
        public string SITE_NO { get; set; }
        public string PDETAIL_ID { get; set; }
        public string PROD_NO { get; set; }
        public Nullable<decimal> QTY_ORD { get; set; }
        public Nullable<decimal> OUT_FPICK_QTY { get; set; }
        public Nullable<decimal> OUT_PPICK_QTY { get; set; }
        public Nullable<decimal> OUT_UPICK_QTY { get; set; }
        public Nullable<System.DateTime> DATE_PICKED { get; set; }
        public Nullable<System.TimeSpan> TIME_PICKED { get; set; }
        public string MOB_NO { get; set; }
        public string HOST_LNE_NO { get; set; }
        public string HOLD_STATUS { get; set; }
        public string COMM1 { get; set; }
        public Nullable<System.DateTime> UBD { get; set; }
        public string PAL_NO_PICKED { get; set; }
        public Nullable<decimal> QTY_PICKED { get; set; }
        public string PAL_NO { get; set; }
        public string FPICK_ZONE { get; set; }
        public string PPICK_ZONE { get; set; }
        public string UPICK_ZONE { get; set; }
        public Nullable<System.DateTime> PROCESS_DATE { get; set; }
        public Nullable<System.TimeSpan> PROCESS_TIME { get; set; }
        public string PROCESS_USER { get; set; }
        public string SEAR_CRITERIA { get; set; }
        public Nullable<decimal> TOT_WEIGHT { get; set; }
        public string BATCH_QTY_PICKED { get; set; }
        public string LOT_NO { get; set; }
        public Nullable<decimal> PAL_WEIGHT { get; set; }
        public string USER_NAME { get; set; }
        public string PAL_STATUS { get; set; }
        public Nullable<decimal> QTY_CONFIRMED { get; set; }
        public Nullable<decimal> ALLOCATED_QTY { get; set; }
        public string PAL_TYPE { get; set; }

    }
}
