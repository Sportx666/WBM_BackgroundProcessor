using OfficeOpenXml.FormulaParsing.Excel.Functions.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WBM_BackgroundProcessor.Models.Paperless_DB;
using WBM_BackgroundProcessor.Polling;
using wbm_common.DataObjects;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace WBM_BackgroundProcessor.Models.DataHolders
{
    public class ReceiptDataHolder
    {
        public ReceiptDataHolder(int CompanyID, int SiteID, int OwnerID)
        {
            TransactionHeader = new TransactionHeader.dbRow();
            ListTransactionDetail = new List<TransactionDetail.dbRow>();
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
        #endregion

        public int OwnerID { get; set; }

        public int CompanyID { get; set; }

        public int SiteID { get; set; }
        public string UnCutID { get; set; }

        #region Paperless Datatables

        // Paperless Datatables

        public List<PURCH_ORD_PROD_DW> ListPurchOrdProdDWs { get; set; }

        private List<STOCK_MOVE_DW> _ListStockMoveDW;

        public List<STOCK_MOVE_DW> ListStockMoveDW
        {
            get { return _ListStockMoveDW; }
            set
            {
                _ListStockMoveDW = value;
                SetListPALNO();
                SetListSKU();
            }
        }
        public List<string> ListPALNO { get; set; }

        public List<DB_TRANS> ListDBTrans { get; set; }

        public List<string> ListSKU { get; set; }

        #endregion

        #region Warehouse Billing Module Datatables

        // Warehouse Billing Module Datatables

        public List<ActionRateCode.dbRow> ListActionRateCode { get; set; }

        public List<Stock.dbRow> ListStock { get; set; }

        public TransactionHeader.dbRow TransactionHeader { get; set; }

        public List<TransactionDetail.dbRow> ListTransactionDetail { get; set; }

        public List<ZoneProcessing.dbRow> ListZoneProcessing { get; set; }
        public List<StockRateCategory.dbRow> ListStockRateCategory { get; set; }

        public List<ProcessingRule.dbRow> ListProcessingRule { get; set; }
        public List<DevanLookup.dbRow> ListDevanLookup { get; set; }
        public List<ContainerHeavyLift.dbRow> ListContainerHeavyLift { get; set; }
        public List<ContainerLift.dbRow> ListContainerLift { get; set; }

        #endregion

        private Boolean Calculated_IsPriority { get; set; }

        public int TotalCheckIns { get; set; }
        public int TotalPutaways { get; set; }

        private int UnitTypeID_Carton = 1;
        private int UnitTypeID_Pallet = 2;
        private int ActionTypeID_Putaway = 2;
        private int ActionTypeID_Loadin = 3;

        public Dictionary<string, ActionRateCodeStat> DictActionRateCodeStat { get; set; }

        public bool IsEDI { get; set; }
        public string ConSize { get; set; }
        public bool IsConLoose { get; set; }
        public bool IsReturn { get; set; }

        // Processing
        #region processing

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
                ConSize = "40";
            if (ListPurchOrdProdDWs.Any(x => x.CONTAINER_SIZE != null && x.CONTAINER_SIZE != ""))
                ConSize = ListPurchOrdProdDWs.FirstOrDefault(x => x.CONTAINER_SIZE != null && x.CONTAINER_SIZE != "")?.CONTAINER_SIZE;
        }

        public void SetIsConLoose()
        {
            IsConLoose = true;// ListPurchOrdProdDWs.Any(x => x.UNLOAD_TYPE != null && x.UNLOAD_TYPE != "");
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

        public bool CheckInPutawayPass()
        {
            TotalCheckIns = (int)ListStockMoveDW.Where(sm => sm.MOVE_TYPE == "CHECK.IN").Sum(x => x.MOVE_QTY.GetValueOrDefault());
            TotalPutaways = (int)ListStockMoveDW.Where(sm => sm.MOVE_TYPE == "PUT.AWAY").Count();
            return true;// TotalCheckIns == TotalPutaways;
        }

        public void REC_RETURN_MAN()
        {
            if (IsReturn && !IsEDI)
                CreateTransactionDetail(44, "", "Each", "Manual Receipt Return", 1);
        }
        public void REC_RETURN_EDI()
        {
            if (IsReturn && IsEDI)
                CreateTransactionDetail(43, "", "Each", "EDI Receipt Return", 1);
        }
        public void REC_STD_MAN()
        {
            if (!IsReturn && !IsEDI)
                CreateTransactionDetail(22, "", "Each", "Manual Standard Receipt", 1);
        }
        public void REC_STD_EDI()
        {
            if (!IsReturn && IsEDI)
                CreateTransactionDetail(23, "", "Each", "EDI Standard Receipt", 1);
        }
        public void REC_PRIORITY()
        {
            if (Calculated_IsPriority)
            {
                CreateTransactionDetail(21, "", "Each", "Priority Receipt", 1);
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
                    CreateTransactionDetail(29, "", "Each", "Container Lift", ConLiftDefaultLifts.DefaultNumberOfLifts);
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
                CreateTransactionDetail(0, singleAppliedRow.RateCollectionRateChargeCode, "Each", "Devan Loose", getCtnCnt());
            }
        }

        private int getCtnCnt()
        {
            int looseCtns = DictActionRateCodeStat.Where(x => x.Value.UnitTypeID == UnitTypeID_Carton).Sum(x => x.Value.Quantity);
            int CtnsOnPallets = ListStockMoveDW.Where(x => x.MOVE_TYPE == "PUT.AWAY").Sum(x => (int)x.MOVE_QTY.GetValueOrDefault());
            return looseCtns + CtnsOnPallets;
        }

        public void REC_CONTHEAVYLIFT()
        {
            //ReceiptDataHolder.ContainerHeavyLift
            if (ConSize != null && ConSize != "" && IsConLoose)
            {
                // this will return whatever is first of matching Owner + Site, Site, Owner, generic owner + site
                ContainerHeavyLift.dbRow ConLiftDefaultHeavyLifts = ListContainerHeavyLift.Where(x => (x.SiteID == 0 || x.SiteID == SiteID)
                                                                    && (x.OwnerID == 0 || x.OwnerID == OwnerID)).OrderBy(x => x.OwnerID).ThenBy(x => x.SiteID).LastOrDefault();
                int numUnderRule = 0;
                int numOverRule = 0;
                foreach(STOCK_MOVE_DW StkMv in ListStockMoveDW)//where unload?
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
                        // error
                    }
                }
                // eg 300 / (3+1) = 75, 75% > 50%, passes
                if ((decimal)numOverRule * 100 / (numOverRule + numUnderRule) >= ConLiftDefaultHeavyLifts.Percentage)
                    CreateTransactionDetail(0, ConLiftDefaultHeavyLifts.RateCollectionRateChargeCode, "Each", "Container Heavy Lift", 1);
            }
        }
        public void REC_WRAPIN()
        {
            //ReceiptDataHolder.WrapIn_Pal
            if (TotalCheckIns > TotalPutaways) // this means we repackaged X number of checkins and wrapped into Y number of putaways so charge Y
                CreateTransactionDetail(25, "", "Each", "Wrap In Pallet", TotalPutaways);
            else if (TotalCheckIns < TotalPutaways)  // this means we unpacked all and put into Y putaways, charge Y
                CreateTransactionDetail(25, "", "Each", "Wrap In Pallet", TotalPutaways);
            // equal means we did nothing
        }
        public void REC_DEVANPALLET()
        {
            //ReceiptDataHolder.DevanPallet_ChargeCode; quantity is LOADIN_Pallet
            if (ConSize != null && ConSize != "" && !IsConLoose)
                CreateTransactionDetail(42, "", "Each", "Devan Pallet", 1);
        }
        public void REC_LOADIN()
        {
            foreach (ActionRateCodeStat arc_stat in DictActionRateCodeStat.Values)
            {
                if (arc_stat.ActionTypeID == ActionTypeID_Loadin)
                {
                    if (arc_stat.UnitTypeID == UnitTypeID_Pallet)
                        CreateTransactionDetail(0, arc_stat.ActionChargeCode, "Each", "Loadin full pallet", arc_stat.Quantity);
                    if (arc_stat.UnitTypeID == UnitTypeID_Carton)
                        CreateTransactionDetail(0, arc_stat.ActionChargeCode, "Each", "Loadin carton", arc_stat.Quantity);
                }
            }
        }
        public void REC_HANDLEIN()
        {
            //ReceiptDataHolder.HandleIn.ListHandleIn
            /*foreach (ActionRateCodeStat arc_stat in DictActionRateCodeStat.Values)
            {
                //if (arc_stat.ActionTypeID == ActionTypeID_HandleIn) //seems to only be handleout not handle in in the db
                {
                    string ChargeDescription = "Each";
                    string FullDescription = "Pick carton";

                    if (arc_stat.UnitTypeID == 2)
                        FullDescription = "Pick full pallet";

                    CreateTransactionDetail(0, arc_stat.ActionChargeCode, ChargeDescription, FullDescription, arc_stat.Quantity);
                }
            }*/
            //if (!IsContainer)
            //    if (putaway > palletunloaded)
            //        if (ListPickDetailDW.Select(x => x.PROD_NO))
                        //CreateTransactionDetail(0, "", "Each", "Handle In Reciept", 1);
        }
        public void REC_PUTAWAY()
        {
            foreach (ActionRateCodeStat arc_stat in DictActionRateCodeStat.Values)
            {
                if (arc_stat.ActionTypeID == ActionTypeID_Putaway)
                {
                    if (arc_stat.UnitTypeID == UnitTypeID_Pallet)
                        CreateTransactionDetail(0, arc_stat.ActionChargeCode, "Each", "Putaway full pallet", arc_stat.Quantity);
                    if (arc_stat.UnitTypeID == UnitTypeID_Carton)
                        CreateTransactionDetail(0, arc_stat.ActionChargeCode, "Each", "Putaway carton", arc_stat.Quantity);
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
            CreateTransactionHeader();
            SetIsReturn();
            SetIsEDI();
            SetConSize();
            SetIsConLoose();
            Process_Stock();

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
                        REC_CONTHEAVYLIFT();
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
                    default:
                        // should not be here...
                        break;
                }
            }
            AddDebugInfo();
            TransactionHeader.LastAmendedMethod = debugString.ToString();
            return true;
        }
        StringBuilder debugString = new StringBuilder();
        public void AddDebugInfo()
        {
            debugString.Append("Sup Name: " + ListPurchOrdProdDWs[0].SUPP_NAME + " ");

            debugString.Append("Load Flag: " + ListPurchOrdProdDWs[0].PO_CLR_LOAD_FLG + ", ");

            if (ListStockMoveDW != null)
            {
                int CountPWD = Convert.ToInt32(ListStockMoveDW.Sum(r => r.MOVE_QTY));
                debugString.Append("StkMv: " + CountPWD + ", ");
            }


        }
        public void CreateTransactionHeader()
        {
            TransactionHeader.CompanyID = CompanyID;
            TransactionHeader.OwnerID = OwnerID;
            TransactionHeader.SiteID = SiteID;
            TransactionHeader.TransactionSourceID = 1; // Paperless
            TransactionHeader.TransactionDateTime = Convert.ToDateTime(ListPurchOrdProdDWs[0].DATE_RECV);
            TransactionHeader.TransactionTypeID = 2; // Order
            TransactionHeader.TransactionReference = ListPurchOrdProdDWs[0].PO_NO.Split('*')[0];
            TransactionHeader.Description = "Receipt";
            TransactionHeader.InvoiceID = 0;
            TransactionHeader.InvoiceDateTime = DateTime.Now;
            TransactionHeader.CreatedDateTime = DateTime.Now;
            TransactionHeader.CreatedMethod = "WBM_BGProcessor";
            TransactionHeader.CreatedUserID = GS.selfUserID;
            TransactionHeader.LastAmendedDateTime = TransactionHeader.CreatedDateTime;
            TransactionHeader.LastAmendedMethod = "";
            TransactionHeader.LastAmendedUserID = TransactionHeader.CreatedUserID;

            TransactionHeader.CreatedMethod = UnCutID;
        }

        public void CreateTransactionDetail(int RateFunctionID, string RateCode, string ChargeDescription, string FullDescription,
            int Quantity)
        {
            if (RateCode != "NOCHARGE")
            {
                TransactionDetail.dbRow tdRow = new TransactionDetail.dbRow();
                tdRow.TransactionHeaderID = 0;
                tdRow.CompanyID = TransactionHeader.CompanyID;
                tdRow.OwnerID = TransactionHeader.OwnerID;
                tdRow.SiteID = TransactionHeader.SiteID;
                tdRow.TransactionDateTime = TransactionHeader.TransactionDateTime;
                tdRow.RateCodeID = RateFunctionID;
                tdRow.RateCode = RateCode;
                tdRow.ChargeDescription = ChargeDescription;
                tdRow.FullDescription = FullDescription;
                tdRow.Quantity = Quantity;

                tdRow.CreatedDateTime = TransactionHeader.CreatedDateTime;
                tdRow.CreatedMethod = TransactionHeader.CreatedMethod;
                tdRow.CreatedUserID = TransactionHeader.CreatedUserID;
                tdRow.LastAmendedDateTime = TransactionHeader.LastAmendedDateTime;
                tdRow.LastAmendedMethod = TransactionHeader.LastAmendedMethod;
                tdRow.LastAmendedUserID = TransactionHeader.LastAmendedUserID;

                ListTransactionDetail.Add(tdRow);
            }

        }
        public void Process_Stock()
        {
            Stock.dbRow stock_row = null;
            Decimal qty = 0;

            int this_unitTypeID = 0;

            foreach (STOCK_MOVE_DW sm in ListStockMoveDW)
            {
                ZoneProcessing.dbRow ZP = ListZoneProcessing.FirstOrDefault(x => x.ZoneNumber == sm.FROM_ZONE);
                if (sm.MOVE_TYPE == "PUT.AWAY")
                    this_unitTypeID = UnitTypeID_Pallet;
                else if (sm.MOVE_TYPE == "FULL.REP" && ZP != null && ZP.IsReceiving)
                    this_unitTypeID = UnitTypeID_Carton;
                else continue;

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
                        // Error....
                    }
                }
            }

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
        #endregion
    }
}
