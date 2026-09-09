using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WBM_BackgroundProcessor.Models;using wbm_common.NonDatabaseObjects;
using WBM_BackgroundProcessor.Models.Paperless_DB;

namespace WBM_BackgroundProcessor.Paperless_DB
{
    public partial class PaperlessDatabase
    {
        public async Task<Result<List<TRIGGER_POSITION_DW>>> TRIGGER_POSITION_DW_List()
        {
            try
            {
                List<TRIGGER_POSITION_DW> trigger_positions = _dbContext.TRIGGER_POSITION_DW.AsNoTracking().ToList();

                if (trigger_positions == null)
                    return Result.Success<List<TRIGGER_POSITION_DW>>(new List<TRIGGER_POSITION_DW>());
                else
                    return Result.Success<List<TRIGGER_POSITION_DW>>(trigger_positions);
            }
            catch (Exception ex)
            {
                logger.Error("TRIGGER_POSITION_DW_List: " + ex);
                return Result.Exception<List<TRIGGER_POSITION_DW>>("Internal Exception occured");
            }
        }

        public async Task<Result<int>> TRIGGER_POSITION_DW_Delete(TRIGGER_POSITION_DW TPDW)
        {
            try
            {
                _dbContext.TRIGGER_POSITION_DW.Remove(TPDW);
                int result = await _dbContext.SaveChangesAsync();
                return Result.Success<int>(result);
            }
            catch (Exception ex)
            {
                logger.Error("TRIGGER_POSITION_DW_Delete - " + TPDW.KEYID + ": " + ex);
                return Result.Exception<int>("Internal Exception occured");
            }
        }
    }
}
