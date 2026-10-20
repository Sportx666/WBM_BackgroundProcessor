using Microsoft.EntityFrameworkCore;
using WBM_BackgroundProcessor.Models;using wbm_common.NonDatabaseObjects;
using WBM_BackgroundProcessor.Models.Paperless_DB;

namespace WBM_BackgroundProcessor.Paperless_DB
{
    public partial class PaperlessDatabase
    {
        public async Task<Result<List<PALLET_DW>>> PALLET_DW_ListBySuppOrdNo(string SupOrdNo)
        {
            try
            {
                List<PALLET_DW> liststockmove = await _dbContext.PALLET_DW.AsNoTracking().Where(x => SupOrdNo == x.SUPP_ORD_NO).ToListAsync();

                if (liststockmove == null)
                    return Result.Success<List<PALLET_DW>>(new List<PALLET_DW>());
                else
                    return Result.Success<List<PALLET_DW>>(liststockmove);
            }
            catch (Exception ex)
            {
                logger.Error("PALLET_DW_ListByKeyIDStartsWith - " + SupOrdNo + ": " + ex);
                return Result.Exception<List<PALLET_DW>>("Internal Exception occured");
            }
        }

        public async Task<Result<List<WBM_PALLET_DW_ASATINSERT>>> WBM_PALLET_DW_ASASINSERT_ListByContNum(List<string> ContNums)
        {
            try
            {
                List<WBM_PALLET_DW_ASATINSERT> liststockmove = null;
                if (ContNums.Count() > 0)
                    liststockmove = await _dbContext.WBM_PALLET_DW_ASATINSERT.AsNoTracking().Where(x => ContNums.Contains(x.CONTAINER_NO)).ToListAsync();

                if (liststockmove == null)
                    return Result.Success<List<WBM_PALLET_DW_ASATINSERT>>(new List<WBM_PALLET_DW_ASATINSERT>());
                else
                    return Result.Success<List<WBM_PALLET_DW_ASATINSERT>>(liststockmove);
            }
            catch (Exception ex)
            {
                logger.Error("WBM_PALLET_DW_ASASINSERT_ListByContNum - " + String.Join(",", ContNums) + ": " + ex);
                return Result.Exception<List<WBM_PALLET_DW_ASATINSERT>>("Internal Exception occured");
            }
        }
    }
}
