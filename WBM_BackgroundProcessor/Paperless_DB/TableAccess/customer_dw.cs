using Microsoft.EntityFrameworkCore;
using WBM_BackgroundProcessor.Models;using wbm_common.NonDatabaseObjects;
using WBM_BackgroundProcessor.Models.Paperless_DB;

namespace WBM_BackgroundProcessor.Paperless_DB
{
    public partial class PaperlessDatabase
    {
        // this is for customer address table
        public async Task<Result<List<CUSTOMER_DW>>> CUSTOMER_DW_ListByKeyID(string KeyID)
        {
            try
            {
                CUSTOMER_DW customer = await _dbContext.CUSTOMER_DW.AsNoTracking().FirstOrDefaultAsync(x => x.KEYID == KeyID);

                if (customer == null)
                    return Result.Success<List<CUSTOMER_DW>>(new List<CUSTOMER_DW>());
                else
                    return Result.Success<List<CUSTOMER_DW>>(new List<CUSTOMER_DW> { customer });
            }
            catch (Exception ex)
            {
                logger.Error("CUSTOMER_DW_ListByKeyID - " + KeyID + ": " + ex);
                return Result.Exception<List<CUSTOMER_DW>>("Internal Exception occured");
            }
        }
    }
}
