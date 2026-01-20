using System.ComponentModel.DataAnnotations;

namespace WBM_BackgroundProcessor.Models.Paperless_DB
{
    public class STOCK_MOVE_DW
    {
        [Key]
        public string KEYID { get; set; }
        public string SITE_NO { get; set; }
        public string MOVE_KEY { get; set; }
        public string MOVE_TYPE { get; set; }
        public string MOB_NO { get; set; }
        public string USER_ID { get; set; }
        public Nullable<decimal> MOVE_QTY { get; set; }
        public string PAL_NO { get; set; }
        public string FROM_ZONE { get; set; }
        public string TO_ZONE { get; set; }
        public string INV_NO { get; set; }
        public string CUST_NO { get; set; }
        public string CUST_NAME { get; set; }
        public string REASON_CODE { get; set; }
        public string LOT_NO { get; set; }
        public Nullable<System.DateTime> USEBYDATE { get; set; }
        public string RECEIPTTYPE { get; set; }
        public string ORDERNO { get; set; }
        public string DOCKET { get; set; }
        public string SHOP_NO { get; set; }
        public string WAREHOUSE { get; set; }
        public string ORDER_TYPE { get; set; }
        public string PRODUCT_CODE { get; set; }
        public string CONTAINER { get; set; }
        public string CTN_MVED { get; set; }
        public string PROGRAM { get; set; }
        public string REF_NO { get; set; }
        public string LOAD_NO { get; set; }

    }
}
