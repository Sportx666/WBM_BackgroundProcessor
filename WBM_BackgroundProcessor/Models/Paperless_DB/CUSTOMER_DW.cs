using System.ComponentModel.DataAnnotations;

namespace WBM_BackgroundProcessor.Models.Paperless_DB
{
    public class CUSTOMER_DW
    {
        [Key]
        public string KEYID { get; set; }
        public string SITE_NO { get; set; }
        public string CUST_NO { get; set; }
        public string CUST_NAME { get; set; }
        public string CUST_ST1 { get; set; }
        public string CUST_SUB { get; set; }
        public string CUST_PCODE { get; set; }
        public string SLIFE_REQ_FLG { get; set; }
        public string LIFO_FLG { get; set; }
        public string CARRIER_CODE { get; set; }
        public string REGION_CODE { get; set; }
        public string CUST_ST2 { get; set; }
        public string CUST_ST3 { get; set; }
        public Nullable<decimal> SLIFE_DAYS { get; set; }
        public string EAN_LABEL_FLG { get; set; }
        public Nullable<decimal> EAN_LABEL_COPY { get; set; }
        public string SHELF_LIFE_ { get; set; }
        public string DESP_LABEL_COPY { get; set; }
        public string CUST_COMP_NAME { get; set; }
        public string SEND_ASN { get; set; }
        public string CARTON_LABEL { get; set; }
        public string BUILD_PALLET_FLAG { get; set; }
        public Nullable<decimal> MAX_CUBE { get; set; }
        public Nullable<decimal> MAX_WEIGHT { get; set; }
        public string STATE { get; set; }
        public string IGNORE_LAST_UBD { get; set; }
        public string CUST_DEL_CODE { get; set; }
        public string HOST_ORDER_INSTR { get; set; }
        public string EAN_FULL_COPIES { get; set; }
        public string DOUBLE_STACK { get; set; }
        public string CARRIER_DEFAULT { get; set; }
        public string TI_HI_FLAG { get; set; }
        public string BAR_PROD_SPLIT { get; set; }
        public string ASN_TYPE { get; set; }
        public string CREATE_INVOICE { get; set; }
        public string PRINT_STD_DESP { get; set; }
        public string EMAIL_ADDR { get; set; }
        public string ORD_TYPE { get; set; }
        public string MAST_CUST_FLAG { get; set; }
        public string CHILD_CUST_NOS { get; set; }
        public string TRUCK_LOAD_ORD { get; set; }
        public string EDI_CLIENT { get; set; }
        public string RECEIVER_ID { get; set; }
        public string SENDER_ID { get; set; }
        public string VENDOR_CODE { get; set; }
        public string PBUILD_CPROD_STD { get; set; }
        public string TEL_NO { get; set; }
        public string STORE_NO { get; set; }
        public string SP_COMMENTS { get; set; }
        public string PRINT_CTN_LABEL { get; set; }
        public string SINGLE_PROD_PER_PAL { get; set; }
        public string FIFO_FLG { get; set; }
        public string DEL_INST { get; set; }
        public string CRITICAL_CARE { get; set; }
        public string PBUILD_PTYPE { get; set; }
        public string PSLIP_FLG { get; set; }
        public string DEFAULT_CARRIER { get; set; }
        public string CHARGE_FREIGHT { get; set; }
        public string DG_LBL_FLG { get; set; }
        public string FREIGHT_PAYER_TYPE { get; set; }
        public string FREIGHT_ACCOUNT_NO { get; set; }

    }
}
