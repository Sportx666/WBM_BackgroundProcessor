using Microsoft.EntityFrameworkCore;
using WBM_BackgroundProcessor.Models;using wbm_common.NonDatabaseObjects;
using WBM_BackgroundProcessor.Models.Paperless_DB;

namespace WBM_BackgroundProcessor.Paperless_DB
{
    public partial class PaperlessDatabase
    {
        public async Task<Result<List<PICK_DETAIL_DW>>> PICK_DETAIL_DW_ListByKeyIDStartsWith(string KEYID)
        {
            try
            {
                List<PICK_DETAIL_DW> listpickdetail = await _dbContext.PICK_DETAIL_DW.AsNoTracking().Where(x => x.KEYID.StartsWith(KEYID)).ToListAsync();

                if (listpickdetail == null)
                    return Result.Success<List<PICK_DETAIL_DW>>(new List<PICK_DETAIL_DW>());
                else
                    return Result.Success<List<PICK_DETAIL_DW>>(listpickdetail);
            }
            catch (Exception ex)
            {
                logger.Error("PICK_DETAIL_DW_ListByKeyIDStartsWith - " + KEYID + ": " + ex);
                return Result.Exception<List<PICK_DETAIL_DW>>("Internal Exception occured");
            }
        }
    }
}
