using System.ComponentModel.DataAnnotations;

namespace WBM_BackgroundProcessor.Models.Paperless_DB
{
    public class PURCH_ORD_PROD_DW
    {
        [Key]
        public string KEYID { get; set; }
        public string SITE_NO { get; set; }
        public string PO_NO { get; set; }
        public string SUPP_NAME { get; set; }
        public string PROD_NO { get; set; }
        public Nullable<decimal> QTY_ORD { get; set; }
        public Nullable<decimal> QTY_PREV_RECV { get; set; }
        public Nullable<decimal> QTY_CUR_RECV { get; set; }
        public Nullable<System.DateTime> DATE_RECV { get; set; }
        public string PO_CLR_LOAD_FLG { get; set; }
        public string PO_INV_NO { get; set; }
        public string REASON_CODE { get; set; }
        public string LINE_STATUS { get; set; }
        public string RECP_TYPE { get; set; }
        public string WHS_CODE { get; set; }
        public Nullable<System.DateTime> FROM_HOST_DATE { get; set; }
        public Nullable<System.DateTime> PO_COMPL_DATE { get; set; }
        public string CONTAINER_NO { get; set; }
        public string PO_SUPP_INV_NO { get; set; }
        public Nullable<System.DateTime> DATE_REL { get; set; }
        public Nullable<System.DateTime> DATE_DEL { get; set; }
        public Nullable<decimal> PAL_COUNT { get; set; }
        public string CONTAINER_SIZE { get; set; }
        public string UNLOAD_TYPE { get; set; }
        public Nullable<System.DateTime> UBD { get; set; }
        public Nullable<System.DateTime> USE_BY_DATE { get; set; }

    }
}
