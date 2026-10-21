using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WBM_BackgroundProcessor.Models;
using WBM_BackgroundProcessor.Models.Paperless_DB;

namespace WBM_BackgroundProcessor.Paperless_DB
{
    public partial class PaperlessDatabase
    {
        public async Task<Result<List<PURCH_ORD_PROD_DW>>> PURCH_ORD_PROD_DW_ListByKeyID(string KeyID)
        {
            try
            {
                List<PURCH_ORD_PROD_DW> pickheads = await _dbContext.PURCH_ORD_PROD_DW.AsNoTracking().Where(x => x.KEYID.StartsWith(KeyID)).ToListAsync();

                if (pickheads == null)
                    return Result.Success<List<PURCH_ORD_PROD_DW>>(new List<PURCH_ORD_PROD_DW>());
                else
                    return Result.Success<List<PURCH_ORD_PROD_DW>>(pickheads);
            }
            catch (Exception ex)
            {
                logger.Error("PURCH_ORD_PROD_DW_ListByKeyID - " + KeyID + ": " + ex);
                return Result.Exception<List<PURCH_ORD_PROD_DW>>("Internal Exception occured");
            }
        }
    }
}
