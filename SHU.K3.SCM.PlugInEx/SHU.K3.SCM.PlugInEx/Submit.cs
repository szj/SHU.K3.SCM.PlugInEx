using Kingdee.BOS;
using Kingdee.BOS.App.Data;
using Kingdee.BOS.Core;
using Kingdee.BOS.Core.DynamicForm.PlugIn;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using Kingdee.BOS.Core.Validation;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.Resource;
using Kingdee.BOS.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SHU.K3.SCM.PlugInEx.Bill.DELIVERYNOTICE
{
    /// <summary>
	/// 发货通知单提交检验是否信贷管控服务端插件
	/// </summary>
	[Description("发货通知单提交检验是否信贷管")]
    public class Submit : AbstractOperationServicePlugIn
    {
        private class SubmitValidator : AbstractValidator
        {
            public override void Validate(ExtendedDataEntity[] dataEntities, ValidateContext validateContext, Context ctx)
            {
                if (dataEntities.IsNullOrEmpty() || dataEntities.Length == 0)
                {
                    return;
                }
                for (int i = 0; i < dataEntities.Length; i++)
                {
                    ExtendedDataEntity extendedDataEntity = dataEntities[i];
                    long fid= Convert.ToInt64(extendedDataEntity["Id"]);//发货通知单->销售订单->销售合同
                    long srcfid = 0;//发货通知单->销售订单->销售合同
                    string srcbillno = "";
                    string arg = extendedDataEntity["BillNo"].ToString();
                    String flag = "0";
                   // DynamicObjectCollection source = (DynamicObjectCollection)extendedDataEntity["SAL_DELIVERYNOTICEFIN"];//明细实体名称
                   // long finfid=Convert.ToInt64(source[0]["id"]);
                    string strSql = "select FBILLALLAMOUNT from T_SAL_DELIVERYNOTICEFIN where fid="+ fid;
                    Decimal amt = DBUtils.ExecuteScalar<Decimal>(ctx, strSql, 0, null);;//当前金额
                    Decimal srcamt = new Decimal(0.00);//当前金额
                    Decimal auditamt = new Decimal(0.00);//当前金额
                    //1.查询销售合同总额及fid;
                    StringBuilder stringBuilder = new StringBuilder();
                    //方案一:
                    /*
                    stringBuilder.AppendLine(" select distinct main.FBILLNO,main.FID,fin.F_WDS_SUPAMOUNT,main.F_WDS_Iscredit from T_CRM_CONTRACT main ");
                    stringBuilder.AppendLine(" left outer join T_CRM_CONTRACTFIN fin on main.FID=fin.FID");
                    stringBuilder.AppendLine(" left outer join  T_CRM_CONTRACTENTRY sec on main.FID=sec.FID");
                    stringBuilder.AppendLine(" where sec.FENTRYID in(");
                    stringBuilder.AppendLine("   SELECT FSID FROM t_BF_InstanceEntry WHERE (FSTableName = 'T_CRM_CONTRACTENTRY1') and (FTTABLENAME='T_SAL_ORDERENTRY') ");
                    stringBuilder.AppendLine("   and FTID in(SELECT FSID FROM t_BF_InstanceEntry WHERE (FSTableName = 'T_SAL_ORDERENTRY') and (FTTABLENAME='T_SAL_DELIVERYNOTICEENTRY') ");
                    stringBuilder.AppendLine(string.Format("and ftid in(select FENTRYID from T_SAL_DELIVERYNOTICEENTRY where FID={0}))", fid));
                    stringBuilder.AppendLine(" ) ");
                    */
                    stringBuilder.AppendLine(" select distinct main.FBILLNO,main.FID,fin.F_WDS_SUPAMOUNT,main.F_WDS_Iscredit from T_CRM_CONTRACT main ");
                    stringBuilder.AppendLine(" left outer join T_CRM_CONTRACTFIN fin on main.FID=fin.FID");
                    stringBuilder.AppendLine(" where main.FID in (");
                    stringBuilder.AppendLine(string.Format(" select F_SHU_CONTRACTID from T_SAL_DELIVERYNOTICE where FID={0} ", fid));
                    stringBuilder.AppendLine(" ) ");


                    using (IDataReader read=DBUtils.ExecuteReader(ctx, stringBuilder.ToString())) {
                        if (read.Read()) {
                            srcbillno = read["FBILLNO"].ToString();
                            srcfid = Convert.ToInt64(read["FID"]);
                            flag = read["F_WDS_Iscredit"].ToString();
                            srcamt = Convert.ToDecimal(read["F_WDS_SUPAMOUNT"]);
                        }
                        read.Close();
                    }
                    //是否信贷
                    if (flag.Equals("0")) {
                        return;
                    }
                    //2.查询发货通知单及已经审核总额
                    stringBuilder = stringBuilder.Clear();
                    //方案一:
                    /*
                    stringBuilder.AppendLine(" select isnull(sum(FBillAllAmount),0) amt");
                    stringBuilder.AppendLine(" from(");
                    stringBuilder.AppendLine("   select distinct main.FID,fin.FBillAllAmount from T_SAL_DELIVERYNOTICE main ");
                    stringBuilder.AppendLine("   left outer join T_SAL_DELIVERYNOTICEFIN fin on main.FID=fin.FID");
                    stringBuilder.AppendLine("   left outer join  T_SAL_DELIVERYNOTICEENTRY sec on main.FID=sec.FID");
                    stringBuilder.AppendLine("   where main.FDOCUMENTSTATUS in('C','B') and sec.FENTRYID in ( ");
                    stringBuilder.AppendLine("     SELECT FTID FROM t_BF_InstanceEntry WHERE (FSTableName = 'T_SAL_ORDERENTRY') and (FTTABLENAME='T_SAL_DELIVERYNOTICEENTRY') ");
                    stringBuilder.AppendLine("     and FSID in(SELECT FTID FROM t_BF_InstanceEntry WHERE (FSTableName = 'T_CRM_CONTRACTENTRY1') and (FTTABLENAME='T_SAL_ORDERENTRY') ");
                    stringBuilder.AppendLine(string.Format(" and FSID in(select FENTRYID from T_CRM_CONTRACTENTRY where fid={0})", srcfid));
                    stringBuilder.AppendLine(" ) ");
                    stringBuilder.AppendLine(" ) ");
                    stringBuilder.AppendLine(" )AAA ");
                    */


                    stringBuilder.AppendLine(" select isnull(sum(FBillAllAmount),0) amt");
                    stringBuilder.AppendLine(" from(");
                    stringBuilder.AppendLine("   select distinct main.FID,fin.FBillAllAmount from T_SAL_DELIVERYNOTICE main ");
                    stringBuilder.AppendLine("   left outer join T_SAL_DELIVERYNOTICEFIN fin on main.FID=fin.FID");
                    stringBuilder.AppendLine("   where main.FDOCUMENTSTATUS in('C','B')  ");
                    stringBuilder.AppendLine(string.Format(" and main.F_SHU_CONTRACTID={0}", srcfid));
                    stringBuilder.AppendLine(" )AAA ");


                    using (IDataReader read = DBUtils.ExecuteReader(ctx, stringBuilder.ToString()))
                    {
                        if (read.Read())
                        {
                            auditamt = Convert.ToDecimal(read["amt"]);
                        }
                        read.Close();
                    }
                    //3.当前金额+已经审核 与合同金额比较.
                    if (srcamt.CompareTo(auditamt+ amt)==-1)
                    {
                            validateContext.AddError(null, new ValidationErrorInfo("", fid.ToString(), extendedDataEntity.DataEntityIndex, extendedDataEntity.RowIndex, "DeliveryNotice500", string.Format(ResManager.LoadKDString("发货通知单价税合计超过销售合同供货金额,销售合同单号{0}，金额{1},已经确认金额{2}，本次确认金额{3}！", "004103000014337", SubSystemType.SCM, new object[0]), srcbillno, Math.Round(srcamt, 2).ToString(), Math.Round(auditamt, 2).ToString(), Math.Round(amt, 2).ToString()), "", ErrorLevel.Error));
                    }
                }
            }
        }
        /// <summary>
        /// 需处理子段
        /// </summary>
        /// <param name="e"></param>
        public override void OnPreparePropertys(PreparePropertysEventArgs e)
        {
           e.FieldKeys.Add("BillAllAmount");
        }

        public override void OnAddValidators(AddValidatorsEventArgs e)
        {
            Submit.SubmitValidator submitValidator = new Submit.SubmitValidator();
            submitValidator.AlwaysValidate = true;
            submitValidator.EntityKey = "FBillHead";
            e.Validators.Add(submitValidator);
        }
    }
}
