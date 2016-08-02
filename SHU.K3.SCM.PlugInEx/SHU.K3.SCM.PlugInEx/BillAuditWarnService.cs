using Kingdee.BOS.Core.Warn.PlugIn;
using System;
using System.ComponentModel;
using Kingdee.BOS.Core.Warn.PlugIn.Args;

namespace SHU.K3.SCM.PlugInEx.Warn
{
    [Description("单据审核预警")]
    public class BillAuditWarnService : AbstractWarnServicePlugIn
    { 
        public override void AfterWarnConditionParse(AfterWarnConditionParseArgs e)
        {
            int numstart = Convert.ToInt32(e.WarnCondition.CustomFilterObject["FNumStart"]);
            int numend = Convert.ToInt32(e.WarnCondition.CustomFilterObject["FNumEnd"]);
            string text = string.Format(" datediff(hh,FModifyDate,GETDATE())>={0} and datediff(hh,FModifyDate,GETDATE())<={1}", numstart,numend);
            if (string.IsNullOrWhiteSpace(e.Filter))
            {
                e.Filter = text;
            }
            else
            {
                e.Filter = " and " + text;
            }
            base.AfterWarnConditionParse(e);
        }
    }
}
