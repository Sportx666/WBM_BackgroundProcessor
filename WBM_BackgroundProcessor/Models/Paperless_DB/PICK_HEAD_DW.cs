using System.ComponentModel.DataAnnotations;

namespace WBM_BackgroundProcessor.Models.Paperless_DB
{
    public class PICK_HEAD_DW
    {
        [Key]
        public string KEYID { get; set; }
        public string SITE_NO { get; set; }
        public string PHEAD_ID { get; set; }
        public string CUST_NO { get; set; }
        public string CUST_NAME { get; set; }
        public string CUST_SUB { get; set; }
        public string CUST_PCODE { get; set; }
        public string CUST_ORD_NO { get; set; }
        public string PTY_NO { get; set; }
        public string PHEAD_STATUS { get; set; }
        public Nullable<decimal> NO_LNES { get; set; }
        public string FROM_FLG { get; set; }
        public string ORD_TYPE { get; set; }
        public string HOST_ORD_NO { get; set; }
        public string OUT_ZONE { get; set; }
        public Nullable<System.DateTime> TO_HOST_DATE { get; set; }
        public string DEL_INST { get; set; }
        public string CUST_ST1 { get; set; }
        public string CUST_ST2 { get; set; }
        public Nullable<System.DateTime> DATE_RECV { get; set; }
        public Nullable<System.TimeSpan> TIME_RECV { get; set; }
        public Nullable<System.DateTime> DEL_DATE { get; set; }
        public Nullable<System.TimeSpan> DEL_TIME { get; set; }
        public string ORD_TAKER { get; set; }
        public Nullable<System.TimeSpan> TO_HOST_TIME { get; set; }
        public string WHS_CODE { get; set; }
        public string GEOG_CODE { get; set; }
        public string PICK_ZONE { get; set; }
        public string CARRIER_CODE { get; set; }
        public string CONSIG_NO { get; set; }
        public string REGO_NO { get; set; }
        public Nullable<System.DateTime> DATE_DESP { get; set; }
        public string PAL_NO { get; set; }
        public string NO_LNES_PPICK_REL { get; set; }
        public string CARTON_PICK_DISPLAY { get; set; }
        public string STATE_CODE { get; set; }
        public string LOAD_NO { get; set; }
        public string CARRIER_CODE_1 { get; set; }
        public string CARRIER_CODE_2 { get; set; }
        public string CARRIER_CODE_3 { get; set; }
        public string CARRIER_CODE_4 { get; set; }
        public string CARRIER_CODE_5 { get; set; }
        public Nullable<decimal> TOT_CTN_DESP { get; set; }
        public string SP_INSTRUCTIONS { get; set; }
        public Nullable<decimal> LOOSE_CARTONS { get; set; }
        public string CUST_ST3 { get; set; }
        public string DEL_INST2_3 { get; set; }
        public string C_NO_SPLIT_CTN { get; set; }
        public string TEL_NO { get; set; }
        public string INVOICE_IND { get; set; }
        public string REF_NO { get; set; }
        public string REMARKS { get; set; }
        public string OWNER_ID { get; set; }
        public string OWNER_NAME { get; set; }
        public Nullable<decimal> NO_SHIPPERS { get; set; }
        public Nullable<System.DateTime> COMP_DATE { get; set; }
        public Nullable<System.TimeSpan> COMP_TIME { get; set; }
        public Nullable<System.DateTime> PLANNED_DESP_DATE { get; set; }
        public Nullable<System.TimeSpan> PLANNED_DESP_TIME { get; set; }
        public string CUST_DC_NO { get; set; }
        public string ASN_NO { get; set; }
        public Nullable<decimal> TOTAL_PALLETS { get; set; }
        public Nullable<decimal> CARTONS_QTY { get; set; }
        public Nullable<decimal> EST_PALLETS { get; set; }
        public Nullable<decimal> EST_SPACES { get; set; }
        public string LAST_SPLIT_FLAG { get; set; }
        public string SPLIT_SEQ_NO { get; set; }
        public string PAL_TYPE_ENTERED { get; set; }
        public string PAL_TYPE_COUNTED { get; set; }
        public string STORE_ZONE_DEPT { get; set; }
        public string SENDER_ID { get; set; }
        public string RECEIVER_ID { get; set; }
        public string RUN_NO { get; set; }
        public Nullable<decimal> TOT_PICK_CTNS { get; set; }
        public Nullable<decimal> TOT_PICK_WEIGHT { get; set; }
        public Nullable<decimal> TOT_PICK_VOL { get; set; }
        public string CUST_EMAIL { get; set; }
        public string TEL_MOB_NO { get; set; }
        public string REGION_CODE { get; set; }
        public string NO_SPLIT_CTN { get; set; }

    }
}
