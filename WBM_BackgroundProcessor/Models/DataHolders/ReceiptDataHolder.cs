using NLog;
using NLog.Web;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WBM_BackgroundProcessor.DataLookups;
using WBM_BackgroundProcessor.Models.Paperless_DB;
using WBM_BackgroundProcessor.Polling;
using wbm_common.DataObjects;
using wbm_common.NonDatabaseObjects;
using wbm_common.Paperless_DB_DataObjects;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace WBM_BackgroundProcessor.Models.DataHolders
{
    public class ReceiptDataHolder
    {
        private static Logger logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
        public ReceiptDataHolder(int CompanyID, int SiteID, int OwnerID)
        {
            DictActionRateCodeStat = new Dictionary<string, ActionRateCodeStat>();

            this.CompanyID = CompanyID;
            this.SiteID = SiteID;
            this.OwnerID = OwnerID;
        }

        #region Subclasses
        public class ActionRateCodeStat
        {
            public int ActionTypeID { get; set; }

            public int UnitTypeID { get; set; }

            public string ActionChargeCode { get; set; }

            public int Quantity { get; set; }
        }

        public class ChargeDetails
        {
            public int RateChargeCodeID { get; set; }
            public string RateChargeCode { get; set; }
            public string Desciption { get; set; }

            public ChargeDetails(int RateChargeCodeID, string RateChargeCode, string Description)
            {
                this.RateChargeCodeID = RateChargeCodeID;
                this.RateChargeCode = RateChargeCode;
                this.Desciption = Description;
            }
        }


        #endregion

        public int OwnerID { get; set; }

        public int CompanyID { get; set; }

        public int SiteID { get; set; }
        public string UnCutID { get; set; }

        #region Paperless Datatables

        // Paperless Datatables

        public List<PURCH_ORD_PROD_DW> ListPurchOrdProdDWs { get; set; }
        public PURCH_ORD_PROD_DW PurchOrdProdDWWithDate { get; set; }


        private List<STOCK_MOVE_DW> _ListStockMoveDW;

        public List<STOCK_MOVE_DW> ListStockMoveDW
        {
            get { return _ListStockMoveDW; }
            set
            {
                _ListStockMoveDW = value;
                SetListPALNO();
                SetListSKU();
                setCustomer();
            }
        }
        public List<string> ListPALNO { get; set; }

        public List<DB_TRANS> ListDBTrans { get; set; }
        public List<WBM_PALLET_DW_ASATINSERT> ListPalletAsInsert { get; set; }
        public List<CONTAINER_DW> ListDBConts { get; set; }
        public List<string> ListConts { get; set; }

        public List<string> ListSKU { get; set; }
        public string CustNo { get; set; }
        public string CustName { get; set; }

        #endregion

        #region Warehouse Billing Module Datatables

        // Warehouse Billing Module Datatables

        public List<ActionRateCode.dbRow> ListActionRateCode { get; set; }

        public List<Stock.dbRow> ListStock { get; set; }

        public TransactionHolder TransactionHolder { get; set; }

        public List<RateCollection.dbRow> ListRateCollection { get; set; }


        public List<ZoneProcessing.dbRow> ListZoneProcessing { get; set; }
        public List<StockRateCategory.dbRow> ListStockRateCategory { get; set; }

        public List<ProcessingRule.dbRow> ListProcessingRule { get; set; }
        public List<DevanLookup.dbRow> ListDevanLookup { get; set; }
        public List<ContainerHeavyLift.dbRow> ListContainerHeavyLift { get; set; }
        public List<ContainerLift.dbRow> ListContainerLift { get; set; }
        #endregion

        private Boolean Calculated_IsPriority { get; set; }

        public int TotalCheckIns { get; set; }
        public int TotalPutawaysAndReplns { get; set; }

        private int UnitTypeID_Carton = 1;
        private int UnitTypeID_Pallet = 2;
        private int ActionTypeID_Putaway = 2;
        private int ActionTypeID_Loadin = 3;
        private int ActionTypeID_HandleIn = 4;

        public Dictionary<string, ActionRateCodeStat> DictActionRateCodeStat { get; set; }

        public bool IsEDI { get; set; }
        public string ConSize { get; set; }
        public bool IsConLoose { get; set; }
        public bool IsReturn { get; set; }
        public bool IsContainer { get; set; }

        public string failedErrorMessage = "";

        private int PalletCount_CheckIn = 0;
        private int PalletCount_PutAway = 0;
        private int PalletCount_FulLRep = 0;
        private int Calculated_Wraps = 0;

        // Processing

        #region processing

        public void setPurchOrdProdDWWithDate()
        {
            PurchOrdProdDWWithDate = ListPurchOrdProdDWs.FirstOrDefault(x => x.DATE_RECV != null && x.PO_CLR_LOAD_FLG == "U");
        }

        public void SetIsReturn()
        {
            IsReturn = ListPurchOrdProdDWs.Any(x => x.RECP_TYPE == "MC" || x.RECP_TYPE == "MN" ||
                                                x.RECP_TYPE == "RC" || x.RECP_TYPE == "RN");
        }

        public void SetIsEDI()
        {
            IsEDI = ListPurchOrdProdDWs.Any(x => x.FROM_HOST_DATE != null);
        }

        public void SetConSize()
        {
            if (ListPurchOrdProdDWs.Any(x => x.RECP_TYPE == "CO"))
            {
                IsContainer = true;
                ConSize = "40";

                // overwrite default if we find a size
                //if (ListDBConts.Any(x => x.CONT_SIZE != null))
                //    ConSize = ListDBConts.Where(x => x.CONT_SIZE != null).First().CONT_SIZE;
            }

            // new version, we get from ListPalletAsInsert
            if (ListPalletAsInsert.Any(x => x.CONTAINER_SIZE != null))
                ConSize = ListPalletAsInsert.Where(x => x.CONTAINER_SIZE != null).First().CONTAINER_SIZE;
        }

        public void SetIsConLoose()
        {
            //IsConLoose = ListPurchOrdProdDWs.Any(x => x.UNLOAD_TYPE != null && x.UNLOAD_TYPE != "");
            // new use ListPalletAsInsert
            IsConLoose = ListPalletAsInsert.Any(x => x.UNLOAD_TYPE != null && x.UNLOAD_TYPE != "");
        }

        public void SetListPALNO()
        {
            // Build list of Pallet Licennce plates
            ListPALNO = new List<string>();

            if (_ListStockMoveDW != null)
                ListPALNO = _ListStockMoveDW.Select(sm => sm.PAL_NO).Distinct().ToList();
        }
        public void SetListSKU()
        {
            // Build list of SKUs
            ListSKU = new List<string>();
            if (ListStockMoveDW != null)
                ListSKU = ListStockMoveDW.Select(sm => sm.PRODUCT_CODE).Distinct().ToList();
        }

        public void setCustomer()
        {
            if (ListStockMoveDW != null)
            {
                List<string> custnames = ListStockMoveDW.Select(sm => sm.CUST_NAME).Where(x => x != null && x != "").Distinct().ToList();
                List<string> custnos = ListStockMoveDW.Select(sm => sm.CUST_NO).Where(x => x != null && x != "").Distinct().ToList();
                if (custnos.Count() > 0)
                    CustNo = custnos.First();
            }
        }

        public void setPalletCount()
        {
            foreach (STOCK_MOVE_DW smRow in ListStockMoveDW)
            {
                switch (smRow.MOVE_TYPE)
                {
                    case "CHECK.IN":
                        PalletCount_CheckIn++;
                        break;
                    case "PUT.AWAY":
                        PalletCount_PutAway++;
                        break;
                    case "FULL.REP":
                        PalletCount_FulLRep++;
                        break;
                }
            }
        }

        public bool CheckInPutawayPass()
        {
            TotalCheckIns = (int)ListStockMoveDW.Where(sm => sm.MOVE_TYPE == "CHECK.IN").Sum(x => x.MOVE_QTY.GetValueOrDefault());
            TotalPutawaysAndReplns = (int)ListStockMoveDW.Where(sm => sm.MOVE_TYPE == "PUT.AWAY"
            || (sm.MOVE_TYPE == "FULL.REP"
                            // this line just checks that we can find the site so we won't get a null reference, since there are a lot of VALID ones won't be found by our search
                            && ListZoneProcessing.Any(y => y.ZoneNumber == sm.FROM_ZONE && y.SiteID == wbm_api.GetSiteNumberCompanyID(sm.SITE_NO).Item1)
                            // now check it's receiving
                            && ListZoneProcessing.First(y => y.ZoneNumber == sm.FROM_ZONE && y.SiteID == wbm_api.GetSiteNumberCompanyID(sm.SITE_NO).Item1).IsReceiving)
                            ).Sum(x => x.MOVE_QTY.GetValueOrDefault());
            return TotalCheckIns == TotalPutawaysAndReplns;
        }

        public void REC_RETURN_MAN()
        {
            if (IsReturn && !IsEDI)
                CreateTransactionDetailPreProcess(44, "Each", 1);
        }
        public void REC_RETURN_EDI()
        {
            if (IsReturn && IsEDI)
                CreateTransactionDetailPreProcess(43, "Each", 1);
        }
        public void REC_STD_MAN()
        {
            if (!IsReturn && !IsEDI)
                CreateTransactionDetailPreProcess(22, "Each", 1);
        }
        public void REC_STD_EDI()
        {
            if (!IsReturn && IsEDI)
                CreateTransactionDetailPreProcess(23, "Each", 1);
        }
        public void REC_PRIORITY()
        {
            if (Calculated_IsPriority)
            {
                CreateTransactionDetailPreProcess(21, "Each", 1);
            }
        }
        public void REC_CONLIFT()
        {
            if (ConSize != null && ConSize != "")
            {
                // this will return whatever is first of matching Owner + Site, Site, Owner, generic owner + site
                ContainerLift.dbRow ConLiftDefaultLifts = ListContainerLift.Where(x => (x.SiteID == 0 || x.SiteID == SiteID)
                                                                    && (x.OwnerID == 0 || x.OwnerID == OwnerID)).OrderBy(x => x.OwnerID).ThenBy(x => x.SiteID).LastOrDefault();
                if (ConLiftDefaultLifts != null && ConLiftDefaultLifts.DefaultNumberOfLifts != 0)
                    CreateTransactionDetailPreProcess(29, "Each", ConLiftDefaultLifts.DefaultNumberOfLifts);
            }
        }
        public void REC_DEVANLOOSE()
        {
            //ReceiptDataHolder.DevanLooseChargeCode; quantity is TotalCartonCount
            if (ConSize != null && ConSize != "" && IsConLoose && int.TryParse(ConSize, out int intConSize) && (intConSize == 20 || intConSize == 40) && getCtnCnt() > 0)
            {
                List<DevanLookup.dbRow> devanSubLookup = ListDevanLookup.Where(x => x.ContainerSizeID == intConSize).ToList();
                // take smallest applicable
                DevanLookup.dbRow singleAppliedRow = devanSubLookup.Where(x => x.MaximumSKUCount >= ListSKU.Count()).OrderBy(x => x.MaximumSKUCount).First();

                CreateTransactionDetailPreProcess(singleAppliedRow.RateCollectionRateChargeCode, "Each", getCtnCnt());
            }
        }

        private int getCtnCnt()
        {
            int looseCtns = DictActionRateCodeStat.Where(x => x.Value.UnitTypeID == UnitTypeID_Carton).Sum(x => x.Value.Quantity);
            int CtnsOnPallets = ListStockMoveDW.Where(x => x.MOVE_TYPE == "PUT.AWAY").Sum(x => (int)x.MOVE_QTY.GetValueOrDefault());
            return looseCtns + CtnsOnPallets;
        }

        public bool REC_CONTHEAVYLIFT()
        {
            //ReceiptDataHolder.ContainerHeavyLift
            if (ConSize != null && ConSize != "" && IsConLoose)
            {
                // this will return whatever is first of matching Owner + Site, Site, Owner, generic owner + site
                ContainerHeavyLift.dbRow ConLiftDefaultHeavyLifts = ListContainerHeavyLift.Where(x => (x.SiteID == 0 || x.SiteID == SiteID)
                                                                    && (x.OwnerID == 0 || x.OwnerID == OwnerID)).OrderBy(x => x.OwnerID).ThenBy(x => x.SiteID).LastOrDefault();
                int numUnderRule = 0;
                int numOverRule = 0;
                foreach (STOCK_MOVE_DW StkMv in ListStockMoveDW)//where unload?
                {
                    Stock.dbRow Stk = ListStock.FirstOrDefault(x => x.ProductCode == StkMv.PRODUCT_CODE);
                    if (Stk != null)
                    {
                        if (Stk.Weight >= ConLiftDefaultHeavyLifts.MinWeight || Stk.Length >= ConLiftDefaultHeavyLifts.MinLength)
                            numOverRule = numOverRule + (int)StkMv.MOVE_QTY.GetValueOrDefault();
                        else
                            numUnderRule = numUnderRule + (int)StkMv.MOVE_QTY.GetValueOrDefault();
                    }
                    else
                    {
                        failedErrorMessage = "ReceiptDataHolder REC_CONTHEAVYLIFT: Stock move not found in stock " + StkMv.PRODUCT_CODE + ", " + UnCutID;
                        logger.Error(failedErrorMessage);
                        return false;
                    }
                }
                // eg 300 / (3+1) = 75, 75% > 50%, passes
                if ((decimal)numOverRule * 100 / (numOverRule + numUnderRule) >= ConLiftDefaultHeavyLifts.Percentage)
                    CreateTransactionDetailPreProcess(ConLiftDefaultHeavyLifts.RateCollectionRateChargeCode, "Each", 1);
            }
            return true;
        }
        public void REC_WRAPIN()
        {

            if (IsConLoose)
            {
                // This is the number of pallets put away
                if (PalletCount_PutAway > 0)
                {
                    Calculated_Wraps = PalletCount_PutAway;
                }
            }
            else
            {
                // This is either a palletised container or a truck unload
                // Compare the CHECK.IN quantity vs the PUT.AWAY and FULL.REP quantity and if different, charge the PUT.AWAY
                if (PalletCount_CheckIn != (PalletCount_PutAway + PalletCount_FulLRep))
                {
                    Calculated_Wraps = PalletCount_PutAway;
                }
            }

            if (Calculated_Wraps > 0)
                CreateTransactionDetailPreProcess(25, "Each", Calculated_Wraps);
        }

        public void REC_CAPIN_PAL()
        {
            // Same logic as wraps, this is for a pallet cap
            if (Calculated_Wraps > 0)
                CreateTransactionDetailPreProcess(54, "Each", Calculated_Wraps);
        }

        public void REC_LINERIN_PAL()
        {
            // Same logic as wraps, this is for a pallet liner

            if (Calculated_Wraps > 0)
                CreateTransactionDetailPreProcess(53, "Each", Calculated_Wraps);
        }

        public void REC_DEVANPALLET()
        {
            //ReceiptDataHolder.DevanPallet_ChargeCode; quantity is LOADIN_Pallet
            if (ConSize != null && ConSize != "" && !IsConLoose && int.TryParse(ConSize, out int intConSize) && (intConSize == 20 || intConSize == 40) && TotalPutawaysAndReplns > 0)
            {
                List<DevanLookup.dbRow> devanSubLookup = ListDevanLookup.Where(x => x.ContainerSizeID == intConSize).ToList();
                // take smallest applicable
                DevanLookup.dbRow singleAppliedRow = devanSubLookup.Where(x => x.MaximumSKUCount >= TotalPutawaysAndReplns).OrderBy(x => x.MaximumSKUCount).First();
                CreateTransactionDetailPreProcess(42, "Each", TotalPutawaysAndReplns);
            }
        }
        public void REC_LOADIN()
        {
            foreach (ActionRateCodeStat arc_stat in DictActionRateCodeStat.Values)
            {
                if (arc_stat.ActionTypeID == ActionTypeID_Loadin)
                {
                    CreateTransactionDetailPreProcess(arc_stat.ActionChargeCode, "Each", arc_stat.Quantity);
                }
            }
        }
        public void REC_HANDLEIN()
        {
            foreach (ActionRateCodeStat arc_stat in DictActionRateCodeStat.Values)
            {
                if (arc_stat.ActionTypeID == ActionTypeID_HandleIn)
                {
                    CreateTransactionDetailPreProcess(arc_stat.ActionChargeCode, "Each", arc_stat.Quantity);
                }
            }
        }
        public void REC_PUTAWAY()
        {
            foreach (ActionRateCodeStat arc_stat in DictActionRateCodeStat.Values)
            {
                if (arc_stat.ActionTypeID == ActionTypeID_Putaway)
                {
                    CreateTransactionDetailPreProcess(arc_stat.ActionChargeCode, "Each", arc_stat.Quantity);
                }
            }
        }
        public void REC_MINRECEIPT()
        {
            //ReceiptDataHolder.MinReceipt
            //if (DevanLoose_ChargeCode < DevanPallet_ChargeCode)
            //CreateTransactionDetail(40, "", "Each", "Minimum Receipt", 1);
        }

        public bool ProcessReceipt()
        {
            if (TransactionHolder == null || TransactionHolder.TransactionHeader_Row == null)
            {
                TransactionHolder = new TransactionHolder();
                TransactionHolder.TransactionHeader_Row = new TransactionHeader.dbRow();
            CreateTransactionHeader();
            }
            else
            {
                TransactionHolder.TransactionHeader_Row.InvoiceExFuel = 0;
                TransactionHolder.TransactionHeader_Row.InvoiceExGST = 0;
                TransactionHolder.TransactionHeader_Row.InvoiceFuel = 0;
                TransactionHolder.TransactionHeader_Row.InvoiceGST = 0;
            }
            if (TransactionHolder.ListTransactionDetail == null)
                TransactionHolder.ListTransactionDetail = new List<TransactionDetail.dbRow>();
            if (TransactionHolder.PaperlessTransactionXref_Row == null)
                CreatePaperlessTransactionXRef();
            SetIsReturn();
            SetIsEDI();
            SetConSize();
            SetIsConLoose();
            setPalletCount();
            Process_Stock();
            Process_dbTrans();

            foreach (ProcessingRule.dbRow pr in ListProcessingRule)
            {
                switch (pr.ProcessID)
                {
                    case 1:
                        // REC_RETURN_MAN
                        REC_RETURN_MAN();
                        break;
                    case 2:
                        // REC_RETURN_EDI
                        REC_RETURN_EDI();
                        break;
                    case 3:
                        // REC_STD_MAN
                        REC_STD_MAN();
                        break;
                    case 4:
                        // REC_STD_EDI
                        REC_STD_EDI();
                        break;
                    case 5:
                        // REC_PRIORITY
                        REC_PRIORITY();
                        break;
                    case 6:
                        // REC_CONLIFT
                        REC_CONLIFT();
                        break;
                    case 7:
                        // REC_DEVANLOOSE
                        REC_DEVANLOOSE();
                        break;
                    case 8:
                        // REC_CONTHEAVYLIFT
                        if (!REC_CONTHEAVYLIFT())
                            return false;
                        break;
                    case 9:
                        // REC_WRAPIN
                        REC_WRAPIN();
                        break;
                    case 10:
                        // REC_DEVANPALLET
                        REC_DEVANPALLET();
                        break;
                    case 11:
                        // REC_LOADIN
                        REC_LOADIN();
                        break;
                    case 12:
                        // REC_HANDLEIN
                        REC_HANDLEIN();
                        break;
                    case 13:
                        // REC_PUTAWAY
                        REC_PUTAWAY();
                        break;
                    case 14:
                        // REC_MINRECEIPT
                        REC_MINRECEIPT();
                        break;
                    case 31:
                        // REC_CAPIN_PAL
                        REC_CAPIN_PAL();
                        break;
                    case 32:
                        // REC_LINERIN_PAL
                        REC_LINERIN_PAL();
                        break;
                    default:
                        // should not be here...
                        break;
                }
            }
            if (addDebug)
                AddDebugInfo();
            TransactionHolder.TransactionHeader_Row.LastAmendedMethod = debugString.ToString();
            return true;
        }

        StringBuilder debugString = new StringBuilder();
        public bool addDebug = false;
        public void AddDebugInfo()
        {
            debugString.Append("Sup Name: " + PurchOrdProdDWWithDate.SUPP_NAME + " ");

            debugString.Append("Load Flag: " + PurchOrdProdDWWithDate.PO_CLR_LOAD_FLG + ", ");

            debugString.Append("ConSize: " + ConSize + ", ");
            debugString.Append("IsEDI: " + IsEDI.ToString() + ", ");
            debugString.Append("IsConLoose: " + IsConLoose.ToString() + ", ");
            debugString.Append("IsReturn: " + IsReturn.ToString() + ", ");

            if (ListDBConts != null)
                debugString.Append("ListDBConts: " + string.Join(", ", ListDBConts.Select(x => x.CONT_SIZE).ToList()) + ", ");

            if (ListStockMoveDW != null)
            {
                int CountPWD = Convert.ToInt32(ListStockMoveDW.Sum(r => r.MOVE_QTY));
                debugString.Append("StkMv: " + CountPWD + ", ");
            }
            if (ListStock != null)
                debugString.Append("ListStock CatIDs: " + string.Join(", ", ListStock.Select(x => x.CategoryID.ToString()).Distinct()) + ", ");

            //if (ListSKU != null)
            //    debugString.Append("ListSKU: " + string.Join(", ", ListSKU) + ", ");

            //if (ListPALNO != null)
            //    debugString.Append("ListPALNO: " + string.Join(", ", ListPALNO) + ", ");

            debugString.Append("TotalCheckIns: " + TotalCheckIns.ToString() + ", ");
            debugString.Append("TotalPutaways: " + TotalPutawaysAndReplns.ToString() + ", ");

            if (ListDBTrans != null)
            {
                debugString.Append("ListDBTrans Pick Types: " + string.Join(", ", ListDBTrans.Select(x => x.PICK_TYPE).Distinct()) + ", ");
                debugString.Append("ListDBTrans Action: " + string.Join(", ", ListDBTrans.Select(x => x.ACTION).Distinct()) + ", ");
                debugString.Append("ListDBTrans Date Stamp: " + string.Join(", ", ListDBTrans.Select(x => x.DATE_STAMP.GetValueOrDefault().ToString("g")).Distinct()) + ", ");
                debugString.Append("ListDBTrans Zone: " + string.Join(", ", ListDBTrans.Select(x => x.ZONE).Distinct()) + ", ");
                debugString.Append("ListDBTrans Invoice Num: " + string.Join(", ", ListDBTrans.Select(x => x.INVOICE_NO).Distinct()) + ", ");
            }
        }
        public void CreateTransactionHeader()
        {
            TransactionHolder.TransactionHeader_Row.CompanyID = CompanyID;
            TransactionHolder.TransactionHeader_Row.OwnerID = OwnerID;
            TransactionHolder.TransactionHeader_Row.SiteID = SiteID;
            TransactionHolder.TransactionHeader_Row.TransactionSourceID = 1; // Paperless
            TransactionHolder.TransactionHeader_Row.TransactionDateTime = Convert.ToDateTime(PurchOrdProdDWWithDate.DATE_RECV);
            TransactionHolder.TransactionHeader_Row.TransactionTypeID = 2; // Receipt
            TransactionHolder.TransactionHeader_Row.TransactionReference = PurchOrdProdDWWithDate.PO_NO.Split('*')[0];
            TransactionHolder.TransactionHeader_Row.Description = PurchOrdProdDWWithDate.SUPP_NAME; //supplier name, container num
            if (PurchOrdProdDWWithDate.CONTAINER_NO != null && PurchOrdProdDWWithDate.CONTAINER_NO != "")
                TransactionHolder.TransactionHeader_Row.Description = TransactionHolder.TransactionHeader_Row.Description + ", " + PurchOrdProdDWWithDate.CONTAINER_NO;
            TransactionHolder.TransactionHeader_Row.InvoiceHeaderID = 0;
            TransactionHolder.TransactionHeader_Row.CreatedDateTime = DateTime.Now;
            TransactionHolder.TransactionHeader_Row.CreatedMethod = "WBM_BGProcessor";
            TransactionHolder.TransactionHeader_Row.CreatedUserID = GS.selfUserID;
            TransactionHolder.TransactionHeader_Row.LastAmendedDateTime = TransactionHolder.TransactionHeader_Row.CreatedDateTime;
            TransactionHolder.TransactionHeader_Row.LastAmendedMethod = "";
            TransactionHolder.TransactionHeader_Row.LastAmendedUserID = TransactionHolder.TransactionHeader_Row.CreatedUserID;

            TransactionHolder.TransactionHeader_Row.CreatedMethod = UnCutID;
        }

        public void CreatePaperlessTransactionXRef()
        {
            TransactionHolder.PaperlessTransactionXref_Row = new PaperlessTransactionXref.dbRow();
            TransactionHolder.PaperlessTransactionXref_Row.CompanyID = CompanyID;
            TransactionHolder.PaperlessTransactionXref_Row.PaperlessKeyID = UnCutID;
            TransactionHolder.PaperlessTransactionXref_Row.PaperlessDataTypeID = 2; // 1 is Order, 2 is Purchase Order, 3 maybe we add as receipt? Still testing
            TransactionHolder.PaperlessTransactionXref_Row.ReferenceNumber = PurchOrdProdDWWithDate.PO_NO.Split('*')[0]; // one of these should change
            TransactionHolder.PaperlessTransactionXref_Row.TransactionHeaderID = TransactionHolder.TransactionHeader_Row.ID;
            TransactionHolder.PaperlessTransactionXref_Row.CreatedDateTime = TransactionHolder.TransactionHeader_Row.CreatedDateTime;
            TransactionHolder.PaperlessTransactionXref_Row.CreatedMethod = TransactionHolder.TransactionHeader_Row.CreatedMethod;
            TransactionHolder.PaperlessTransactionXref_Row.CreatedUserID = TransactionHolder.TransactionHeader_Row.CreatedUserID;
            TransactionHolder.PaperlessTransactionXref_Row.LastAmendedDateTime = TransactionHolder.TransactionHeader_Row.LastAmendedDateTime;
            TransactionHolder.PaperlessTransactionXref_Row.LastAmendedMethod = TransactionHolder.TransactionHeader_Row.LastAmendedMethod;
            TransactionHolder.PaperlessTransactionXref_Row.LastAmendedUserID = TransactionHolder.TransactionHeader_Row.LastAmendedUserID;
        }

        public void CreateTransactionDetailPreProcess(int RateFunctionID, string ChargeBy, int Quantity)
        {
            ChargeDetails cd = GetRateCollectionRateCodeIDRateChargeCodeDescription(RateFunctionID);

            CreateTransactionDetail(cd.RateChargeCodeID, cd.RateChargeCode, ChargeBy, cd.Desciption, Quantity);
        }

        public void CreateTransactionDetailPreProcess(string RateChargeCode, string ChargeBy, int Quantity)
        {
            if (RateChargeCode != "NOCHARGE")
            {
                ChargeDetails cd = GetRateCollectionRateCodeIDRateChargeCodeDescription(0, RateChargeCode);

                CreateTransactionDetail(cd.RateChargeCodeID, cd.RateChargeCode, ChargeBy, cd.Desciption, Quantity);
            }
        }

        public void CreateTransactionDetail(int RateCodeID, string RateChargeCode, string ChargeBy, string FullDescription,
    int Quantity)
        {
            if (RateChargeCode != "NOCHARGE")
            {
                TransactionDetail.dbRow tdRow = new TransactionDetail.dbRow();
                tdRow.TransactionHeaderID = 0;
                tdRow.CompanyID = TransactionHolder.TransactionHeader_Row.CompanyID;
                tdRow.OwnerID = TransactionHolder.TransactionHeader_Row.OwnerID;
                tdRow.SiteID = TransactionHolder.TransactionHeader_Row.SiteID;
                tdRow.TransactionDateTime = TransactionHolder.TransactionHeader_Row.TransactionDateTime;
                tdRow.RateCodeID = RateCodeID;
                tdRow.RateCode = RateChargeCode;
                tdRow.ChargeDescription = ChargeBy;
                tdRow.FullDescription = FullDescription;
                tdRow.Quantity = Quantity;

                tdRow.CreatedDateTime = TransactionHolder.TransactionHeader_Row.CreatedDateTime;
                tdRow.CreatedMethod = TransactionHolder.TransactionHeader_Row.CreatedMethod;
                tdRow.CreatedUserID = TransactionHolder.TransactionHeader_Row.CreatedUserID;
                tdRow.LastAmendedDateTime = TransactionHolder.TransactionHeader_Row.LastAmendedDateTime;
                tdRow.LastAmendedMethod = TransactionHolder.TransactionHeader_Row.LastAmendedMethod;
                tdRow.LastAmendedUserID = TransactionHolder.TransactionHeader_Row.LastAmendedUserID;

                TransactionHolder.ListTransactionDetail.Add(tdRow);
            }
        }

        public bool Process_dbTrans()
        {
            Stock.dbRow stock_row = null;
            Decimal qty = 0;

            int this_unitTypeID = 0;
            Boolean Process_Putaway = true;

            int Carton_Count_I02 = 0;
            int Pallet_Count_I04 = 0;
            int Carton_Count_CTNUNLOAD = 0;
            int Pallet_Count_UNLPAL = 0;
            int Carton_Count_Breakdown = 0;

            Decimal QtyAs2DP = 0;
            int Qty = 0;

            foreach (DB_TRANS dt in ListDBTrans)
            {
                // Quantity is stored with an implied 2 decimal points eg 2100 = 21.
                if (dt.QUANTITY.HasValue)
                {
                    QtyAs2DP = dt.QUANTITY.Value / 100m;
                }
                Qty = Decimal.ToInt32(QtyAs2DP);

                switch (dt.ACTION)
                {
                    case "I02":
                        Carton_Count_I02 += Qty;
                        break;
                    case "I04":
                        Pallet_Count_I04 += Qty;
                        break;
                    case "CTN.UNLOAD":
                        Carton_Count_CTNUNLOAD += Qty;
                        break;
                    case "UNL.PAL":
                        Pallet_Count_UNLPAL += Qty;
                        break;
                    case "PAL.BDOWN":
                        Carton_Count_Breakdown += Qty;
                        break;
                }
            }


            // Records should have either I02/I04 or CTN.UNLOAD, UNL.PAL so take the greater

            int Unload_Carton_Count = 0;
            int Unload_Pallet_Count = 0;

            if ((Carton_Count_I02 + Pallet_Count_I04) > (Carton_Count_CTNUNLOAD + Pallet_Count_UNLPAL))
            {
                Unload_Carton_Count = Carton_Count_I02;
                Unload_Pallet_Count = Pallet_Count_I04;
            }
            else
            {
                Unload_Carton_Count = Carton_Count_CTNUNLOAD;
                Unload_Pallet_Count = Pallet_Count_UNLPAL;
            }

            // Need a dummy record because we have no guarantee of the actual product codes

            Stock.dbRow dummy_stock_row = new Stock.dbRow
            {
                StockRateCategoryID = 0,
                Length = 0,
                Width = 0
            };

            if (Unload_Carton_Count > 0)
            {
                DBTrans_Add_To_DictActionRateCodeStat(dummy_stock_row, ActionTypeID_Loadin, UnitTypeID_Carton, Unload_Carton_Count);
            }

            if (Unload_Pallet_Count > 0)
            {
                DBTrans_Add_To_DictActionRateCodeStat(dummy_stock_row, ActionTypeID_Loadin, UnitTypeID_Pallet, Unload_Pallet_Count);
            }

            if (Carton_Count_Breakdown > 0)
            {
                DBTrans_Add_To_DictActionRateCodeStat(dummy_stock_row, ActionTypeID_HandleIn, UnitTypeID_Carton, Carton_Count_Breakdown);
            }

            return true;
        }

        private void DBTrans_Add_To_DictActionRateCodeStat(Stock.dbRow stock_row, int ThisActionTypeID, int ThisUnitTypeID, int Quantity)
        {
            string ActionChargeCode = GetActionChargeCode(stock_row, ThisActionTypeID, ThisUnitTypeID);
            ActionRateCodeStat arc_stat = null;
            if (DictActionRateCodeStat.ContainsKey(ActionChargeCode))
            {
                arc_stat = DictActionRateCodeStat[ActionChargeCode];
            }
            else
            {
                arc_stat = new ActionRateCodeStat() { ActionTypeID = ThisActionTypeID, UnitTypeID = ThisUnitTypeID, ActionChargeCode = ActionChargeCode };
                DictActionRateCodeStat.Add(ActionChargeCode, arc_stat);
            }
            arc_stat.Quantity += Quantity;
        }


        public bool Process_Stock()
        {
            Stock.dbRow stock_row = null;
            Decimal qty = 0;

            int this_unitTypeID = 0;
            Boolean Process_Putaway = true;

            foreach (STOCK_MOVE_DW sm in ListStockMoveDW)
            {
                #region Process Putaway

                ZoneProcessing.dbRow ZP = ListZoneProcessing.FirstOrDefault(x => x.ZoneNumber == sm.FROM_ZONE);

                Process_Putaway = true;
                if (sm.MOVE_TYPE == "PUT.AWAY")
                    this_unitTypeID = UnitTypeID_Pallet;
                else if (sm.MOVE_TYPE == "FULL.REP" && ZP != null && ZP.IsReceiving)
                    this_unitTypeID = UnitTypeID_Carton;
                else Process_Putaway = false;

                if (Process_Putaway)
                {
                    stock_row = ListStock.FirstOrDefault(y => y.ProductCode == sm.PRODUCT_CODE);
                    // Count MOVE.QTY grouped on ActionChargeCode
                    if (stock_row != null)
                    {
                        string ActionChargeCode = GetActionChargeCode(stock_row, ActionTypeID_Putaway, this_unitTypeID);

                        if (ActionChargeCode != null)
                        {
                            ActionRateCodeStat arc_stat = null;
                            if (DictActionRateCodeStat.ContainsKey(ActionChargeCode))
                            {
                                arc_stat = DictActionRateCodeStat[ActionChargeCode];
                            }
                            else
                            {
                                arc_stat = new ActionRateCodeStat() { ActionTypeID = ActionTypeID_Putaway, UnitTypeID = this_unitTypeID, ActionChargeCode = ActionChargeCode };
                                DictActionRateCodeStat.Add(ActionChargeCode, arc_stat);
                            }

                            if (this_unitTypeID == UnitTypeID_Carton)
                            {
                                qty = sm.MOVE_QTY.HasValue ? sm.MOVE_QTY.Value : 0;
                                arc_stat.Quantity += Convert.ToInt32(qty);
                            }
                            else if (this_unitTypeID == UnitTypeID_Pallet)
                            {
                                arc_stat.Quantity += 1;
                            }
                        }
                        else
                        {
                            failedErrorMessage = "ReceiptDataHolder Process Stock: action charge code not found on " + sm.PRODUCT_CODE + ", " + sm.MOVE_TYPE + ", " + UnCutID;
                            logger.Error(failedErrorMessage);
                            return false;
                        }
                    }
                    else
                    {
                        failedErrorMessage = "ReceiptDataHolder Process Stock: stock row not found on " + sm.PRODUCT_CODE + ", " + sm.MOVE_TYPE + ", " + UnCutID;
                        logger.Error(failedErrorMessage);
                        return false;
                    }
                }
                #endregion
            }
            return true;
        }

        // these either need to be in their own class since I can't make order ones static
        private string GetActionChargeCode(Stock.dbRow stock_row, int ActionTypeID, int UnitTypeID)
        {
            List<ActionRateCode.dbRow> ListPotentialMatches = new List<ActionRateCode.dbRow>();

            string ActionChargeCode = null;
            if (stock_row != null)
            {
                ActionRateCode.dbRow found_row = null;
                Boolean DisqualifyRule = false;
                foreach (ActionRateCode.dbRow arc in ListActionRateCode)
                {
                    // Non negotiable rules
                    if (arc.ActionTypeID != ActionTypeID)
                        continue;

                    if (arc.UnitTypeID != UnitTypeID)
                        continue;

                    if (arc.IsPriority.HasValue && arc.IsPriority.Value != Calculated_IsPriority)
                        continue;

                    if (arc.SiteID > 0 && arc.SiteID != SiteID)
                        continue;

                    //--

                    if (arc.StockRateCategoryID > 0 && arc.StockRateCategoryID != stock_row.StockRateCategoryID)
                        continue;

                    if (arc.LengthBreak > 0 && arc.LengthBreak < stock_row.Length)
                        continue;

                    if (arc.WeightBreak > 0 && arc.WeightBreak < stock_row.Weight)
                        continue;

                    ListPotentialMatches.Add(arc);
                }

                if (ListPotentialMatches.Count > 0)
                {
                    if (ListPotentialMatches.Count == 1)
                        found_row = ListPotentialMatches[0];
                    else
                    {
                        found_row = ChooseActionChargeCode(ListPotentialMatches);
                    }
                }
                if (found_row != null)
                {
                    ActionChargeCode = found_row.RateCollectionRateChargeCode;
                }
            }
            return ActionChargeCode;
        }

        private ActionRateCode.dbRow ChooseActionChargeCode(List<ActionRateCode.dbRow> ListPotentialMatches)
        {
            ActionRateCode.dbRow found_row = null;

            if (ListPotentialMatches != null)
            {
                // Heirachy
                found_row = ListPotentialMatches.Where(arc => arc.StockRateCategoryID > 0 && arc.SiteID > 0).FirstOrDefault();
                if (found_row != null)
                    return found_row;

                found_row = ListPotentialMatches.Where(arc => arc.StockRateCategoryID > 0).FirstOrDefault();
                if (found_row != null)
                    return found_row;

                found_row = ListPotentialMatches.Where(arc => arc.SiteID > 0 && arc.LengthBreak > 0 && arc.WeightBreak > 0).FirstOrDefault();
                if (found_row != null)
                    return found_row;

                found_row = ListPotentialMatches.Where(arc => arc.SiteID > 0 && arc.LengthBreak > 0).FirstOrDefault();
                if (found_row != null)
                    return found_row;

                found_row = ListPotentialMatches.Where(arc => arc.SiteID > 0 && arc.WeightBreak > 0).FirstOrDefault();
                if (found_row != null)
                    return found_row;

                found_row = ListPotentialMatches.Where(arc => arc.LengthBreak > 0 && arc.WeightBreak > 0).FirstOrDefault();
                if (found_row != null)
                    return found_row;

                found_row = ListPotentialMatches.Where(arc => arc.LengthBreak > 0).FirstOrDefault();
                if (found_row != null)
                    return found_row;

                found_row = ListPotentialMatches.Where(arc => arc.WeightBreak > 0).FirstOrDefault();
                if (found_row != null)
                    return found_row;

                found_row = ListPotentialMatches.Where(arc => arc.SiteID > 0).FirstOrDefault();
                if (found_row != null)
                    return found_row;

                // If we're here then just return the first row
                return ListPotentialMatches[0];
            }
            else
                return null;

        }

        private ChargeDetails GetRateCollectionRateCodeIDRateChargeCodeDescription(int RateFunctionID, string RateChargeCode = null)
        {
            RateCollection.dbRow rc;

            if (RateFunctionID > 0)
                rc = ListRateCollection.FirstOrDefault(r => r.RateFunctionID == RateFunctionID);
            else
                rc = ListRateCollection.FirstOrDefault(r => r.RateChargeCode == RateChargeCode);

            if (rc != null)
                return new ChargeDetails(rc.RateCodeID, rc.RateChargeCode, rc.Description);
            else
            {
                if (RateFunctionID > 0)
                    return new ChargeDetails(0, "Not Found: " + RateFunctionID.ToString(), "Not Found: " + RateFunctionID.ToString());
                else
                    return new ChargeDetails(0, "Not Found: " + RateChargeCode, "Not Found: " + RateChargeCode);

            }
        }


        #endregion
    }
}
