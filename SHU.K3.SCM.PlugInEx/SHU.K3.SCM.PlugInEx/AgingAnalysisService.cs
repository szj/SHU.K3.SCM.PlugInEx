using Kingdee.BOS;
using Kingdee.BOS.App;
using Kingdee.BOS.App.Data;
using Kingdee.BOS.Contracts;
using Kingdee.BOS.Contracts.Report;
using Kingdee.BOS.Core.CommonFilter;
using Kingdee.BOS.Core.Enums;
using Kingdee.BOS.Core.List;
using Kingdee.BOS.Core.Report;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.Resource;
using Kingdee.BOS.Util;
using Kingdee.K3.FIN.AP.App.Report;
using Kingdee.K3.FIN.App.Core;
using Kingdee.K3.FIN.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace SHU.K3.SCM.PlugInEx.Report
{
    [Description("客户利息计算报表(应付)")]
    public class AgingAnalysisService : SysReportBaseService
    {
        private string TAB = "\t";
        private Dictionary<int, int> balanceDct = new Dictionary<int, int>();
        private Dictionary<int, Decimal> balanceDctRate = new Dictionary<int, Decimal>();
        private string ksqlSeq;
        public override void Initialize()
        {
            this.ksqlSeq = this.KSQL_SEQ;
            base.ReportProperty.BillKeyFieldName = "FID";
            base.ReportProperty.FormIdFieldName = "FFORMID";
        }
        /// <summary>
        /// SetBalanceAmtDigit
        /// </summary>
        /// <param name="AmountFieldCount"></param>
        private void SetBalanceAmtDigit(int AmountFieldCount)
        {
            List<DecimalControlField> list = new List<DecimalControlField>();
            for (int i = 0; i <= AmountFieldCount; i++)
            {
                string arg = (i == 0) ? "" : i.ToString();
                list.Add(new DecimalControlField("FDigitsFor", string.Format("FBalance{0}AmtFor", arg)));
                list.Add(new DecimalControlField("FDigits", string.Format("FBalance{0}Amt", arg)));
                //精度
                list.Add(new DecimalControlField("FDigitsFor", string.Format("FRate{0}", arg)));
                list.Add(new DecimalControlField("FDigitsFor", string.Format("FRate{0}Amt", arg)));
            }
            base.ReportProperty.DecimalControlFieldList = list;
            base.ReportProperty.IsGroupSummary = true;
        }
        /// <summary>
        /// 标题
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public override ReportTitles GetReportTitles(IRptParams filter)
        {
            ReportTitles reportTitles = new ReportTitles();
            DynamicObject customFilter = filter.FilterParameter.CustomFilter;
            if (customFilter == null)
            {
                return reportTitles;
            }
            string orgs = string.Empty;
            string sTitleValue = string.Empty;
            string sTitleValue2 = string.Empty;
            if (!Convert.ToBoolean(customFilter["IsFromFilter"]))
            {
                 
                orgs = (Context.CurrentOrganizationInfo.IsNullOrEmptyOrWhiteSpace() ? string.Empty : base.Context.CurrentOrganizationInfo.ID.ToString());
            }
            else
            {
                string text = base.Context.CurrentOrganizationInfo.IsNullOrEmptyOrWhiteSpace() ? string.Empty : base.Context.CurrentOrganizationInfo.ID.ToString();
                orgs = (customFilter["SettleOrgLst"].IsNullOrEmptyOrWhiteSpace() ? text : customFilter["SettleOrgLst"].ToString());
            }
            sTitleValue = CommonFunctionReport.GetOrgsName(orgs, base.Context);
            reportTitles.AddTitle("FSettleOrg_H", sTitleValue);
            sTitleValue2 = CommonFunctionReport.GetBaseDataName(customFilter, "Affiliation");
            reportTitles.AddTitle("FAffiliation_H", sTitleValue2);
            reportTitles.AddTitle("FDate_H", Convert.ToDateTime(customFilter["ByDate"]).ToShortDateString());
            return reportTitles;
        }
        /// <summary>
        /// 表头
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public override ReportHeader GetReportHeaders(IRptParams filter)
        {
            ReportHeader reportHeader = new ReportHeader();
            DynamicObject customFilter = filter.FilterParameter.CustomFilter;
            bool flag = false;
            if (customFilter != null)
            {
                flag = Convert.ToBoolean(customFilter["IsFromFilter"]);
            }
            reportHeader.AddChild("FContactUnit", new LocaleValue(ResManager.LoadKDString("往来单位", "003224000002056", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID));
            reportHeader.AddChild("FCurrencyName", new LocaleValue(ResManager.LoadKDString("币别", "003224000002059", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID));
            if (flag)
            {
                reportHeader.AddChild("FSettleOrgName", new LocaleValue(ResManager.LoadKDString("结算组织", "003224000002062", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID));
                reportHeader.AddChild("FPayOrgName", new LocaleValue(ResManager.LoadKDString("收付组织", "003224000011310", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID));
                reportHeader.AddChild("FPurchaseOrgName", new LocaleValue(ResManager.LoadKDString("采购组织", "003224000002065", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID));
                reportHeader.AddChild("FPurchaseDeptName", new LocaleValue(ResManager.LoadKDString("采购部门", "003224000002068", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID));
                reportHeader.AddChild("FPurchaseGroupName", new LocaleValue(ResManager.LoadKDString("采购组", "003224000002071", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID));
                reportHeader.AddChild("FPurchaserName", new LocaleValue(ResManager.LoadKDString("采购员", "003224000002074", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID));
            }
            if (customFilter == null)
            {
                this.LoadBillColumn(reportHeader, true);
                this.LoadBalanceColumn(reportHeader, false, null, null);
            }
            else
            {
                DynamicObjectCollection balColumnList = customFilter["EntAgingGrpSetting"] as DynamicObjectCollection;
                List<ColumnField> columnInfo = filter.FilterParameter.ColumnInfo;
                this.LoadBillColumn(reportHeader, Convert.ToBoolean(customFilter["ByBill"]));
                this.LoadBalanceColumn(reportHeader, flag, balColumnList, columnInfo);
            }
            int num = 0;
            foreach (ListHeader current in reportHeader.GetChilds())
            {
                if (current.GetChildCount() == 0)
                {
                    current.ColIndex = num++;
                }
                else
                {
                    current.ColIndex = num++;
                    foreach (ListHeader current2 in current.GetChilds())
                    {
                        current2.ColIndex = num++;
                    }
                }
            }
            return reportHeader;
        }
        /// <summary>
        /// 报表sql集合
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="tableName"></param>
        public override void BuilderReportSqlAndTempTable(IRptParams filter, string tableName)
        {
            DynamicObject customFilter = filter.FilterParameter.CustomFilter;
            string sortString = filter.FilterParameter.SortString;
            string filterString = filter.FilterParameter.FilterString;
            if (customFilter == null)
            {
                base.BuilderReportSqlAndTempTable(filter, tableName);
                return;
            }
            if (!Convert.ToBoolean(customFilter["IsFromFilter"]))
            {
                Kingdee.BOS.Core.CommonFilter.SummaryField summaryField = new Kingdee.BOS.Core.CommonFilter.SummaryField(new LocaleValue(ResManager.LoadKDString("往来单位", "003224000002056", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID), "FContactUnit");
                summaryField.Key = "FContactUnit";
                if (!filter.FilterParameter.GroupbyString.Contains("FContactUnit"))
                {
                    filter.FilterParameter.SummaryRows.Add(summaryField);
                    FilterParameter expr_B4 = filter.FilterParameter;
                    expr_B4.GroupbyString += "FContactUnit";
                }
            }
            this.InitBalanceDct(customFilter);
            //建临时表
            IDBService service = ServiceHelper.GetService<IDBService>();
            string[] array = service.CreateTemporaryTableName(base.Context, 3);//建３个临时表
            string tmpTableName = array[0];
            string text = array[1];
            string text2 = array[2];
            string text3 = string.Empty;
            string str = string.Empty;
            //动态列组装
            foreach (int current in this.balanceDct.Keys)
            {
                string arg = string.Format("FBalance{0}AmtFor", current + 1);
                string arg2 = string.Format("FBalance{0}Amt", current + 1);
                string arg3 = string.Format("FRate{0}", current + 1);
                string arg4 = string.Format("FRate{0}Amt", current + 1);
                text3 += string.Format("{0},{1},{2},{3},", arg, arg2,arg3,arg4);
                str += string.Format("sum({0}) as {0},sum({1}) as {1},", arg, arg2);
            }
            //各种临时表
            //===================各种费用组合======================================
            this.GetCreateTmpTableNameSql(tmpTableName);//建临时表
            this.GetCreateTmpAnalysisTblNameSql(text2);//明细临时表  text2 新的 FRate FRate{}Amt
            this.GetPayableDataSql(tmpTableName, filter);//付款信息
            this.GetPayDataSql(tmpTableName, filter);
            this.GetRefundDataSql(tmpTableName, filter);//新增日期过滤
            this.GetOtherPayableDataSql(tmpTableName, filter);
            //=======================================================
            if (Convert.ToBoolean(customFilter["ByBill"]))//按单据显示
            {
                this.GetFilterDataTable(tmpTableName, text, filterString);
                this.GetCalculateAgingSql(text, text2, filter, text3, sortString);
                //最终数据存储在tableName
                this.GetReportSql(tableName, text2, sortString, text3);
            }
            else
            {
                this.GetCalculateAgingSql(tmpTableName, text2, filter, text3, sortString);
                this.GetFilterDataTable(text2, text, filterString);
                //最终数据存储在tableName
                this.GetReportSql(tableName, text, sortString, text3);
            }
            //drop 临时表
            ITemporaryTableService service2 = ServiceFactory.GetService<ITemporaryTableService>(base.Context);
            service2.DropTable(base.Context, new HashSet<string>(array));
        }
        public override List<Kingdee.BOS.Core.Report.SummaryField> GetSummaryColumnInfo(IRptParams filter)
        {
            List<Kingdee.BOS.Core.Report.SummaryField> list = new List<Kingdee.BOS.Core.Report.SummaryField>();
            list.Add(new Kingdee.BOS.Core.Report.SummaryField("FBalanceAmtFor", BOSEnums.Enu_SummaryType.SUM));
            list.Add(new Kingdee.BOS.Core.Report.SummaryField("FBalanceAmt", BOSEnums.Enu_SummaryType.SUM));
            foreach (int current in this.balanceDct.Keys)
            {
                list.Add(new Kingdee.BOS.Core.Report.SummaryField(string.Format("FBalance{0}AmtFor", current + 1), BOSEnums.Enu_SummaryType.SUM));
                list.Add(new Kingdee.BOS.Core.Report.SummaryField(string.Format("FBalance{0}Amt", current + 1), BOSEnums.Enu_SummaryType.SUM));
            }
            return list;
        }
        private string GetSqlWhere(IRptParams filter, bool isPayable = true, bool needFilterAccountSystem = true)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(" where a.fcancelstatus = 'A' ");
            DynamicObject customFilter = filter.FilterParameter.CustomFilter;
            if (customFilter == null)
            {
                return stringBuilder.ToString();
            }
            string arg = "{ts'" + Convert.ToDateTime(customFilter["ByDate"]).ToString("yyyy-MM-dd 23:59:59") + "'}";
            stringBuilder.AppendFormat(" And a.FDate <= {0}", arg);
            string text = customFilter["SettleOrgLst"].IsNullOrEmptyOrWhiteSpace() ? string.Empty : customFilter["SettleOrgLst"].ToString();
            long num = customFilter["Affiliation_Id"].IsNullOrEmptyOrWhiteSpace() ? 0L : Convert.ToInt64(customFilter["Affiliation_Id"]);
            if (num != 0L)
            {
                IOrganizationService service = ServiceHelper.GetService<IOrganizationService>();
                List<long> list = service.GetOrgsInAffiliation(base.Context, Convert.ToInt64(text), num);
                List<long> OrgPermissionList = CommonFunction.GetPermissionOrgIdList(base.Context, base.BusinessInfo.GetForm().Id, "6e44119a58cb4a8e86f6c385e14a17ad");
                list = list.FindAll((long o) => OrgPermissionList.Contains(o));
                stringBuilder.AppendFormat("and FSettleOrgID_M.FOrgID in ({0})", string.Join<long>(",", list));
            }
            else
            {
                if (!text.IsNullOrEmptyOrWhiteSpace())
                {
                    stringBuilder.AppendFormat(" and FSettleOrgID_M.FOrgID in ({0})", text);
                }
            }
            string text2 = customFilter["ContactUnitType"].IsNullOrEmptyOrWhiteSpace() ? string.Empty : customFilter["ContactUnitType"].ToString();
            string baseDataNumber = CommonFunctionReport.GetBaseDataNumber(customFilter, "ContactUnitFrom");
            string baseDataNumber2 = CommonFunctionReport.GetBaseDataNumber(customFilter, "ContactUnitTo");
            if (isPayable)
            {
                if (text2 == "BD_Supplier")
                {
                    if (!string.IsNullOrWhiteSpace(baseDataNumber))
                    {
                        stringBuilder.AppendFormat(" and ContactType.FNumber >= '{0}'", baseDataNumber);
                    }
                    if (!string.IsNullOrWhiteSpace(baseDataNumber2))
                    {
                        stringBuilder.AppendFormat(" and ContactType.FNumber <= '{0}'", baseDataNumber2);
                    }
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(text2))
                    {
                        stringBuilder.Append(" and 1 = 0 ");
                    }
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(text2))
                {
                    stringBuilder.AppendFormat(" and FCONTACTUNITTYPE = '{0}' ", text2);
                }
                if (!string.IsNullOrWhiteSpace(baseDataNumber))
                {
                    stringBuilder.AppendFormat(" and ContactType.FNumber >= '{0}'", baseDataNumber);
                }
                if (!string.IsNullOrWhiteSpace(baseDataNumber2))
                {
                    stringBuilder.AppendFormat(" and ContactType.FNumber <= '{0}'", baseDataNumber2);
                }
            }
            DynamicObject dynamicObject = customFilter["CurrencyFrom"] as DynamicObject;
            if (dynamicObject != null && dynamicObject["Id"] != null)
            {
                stringBuilder.AppendFormat(" and a.FCurrencyID = {0}", dynamicObject["Id"]);
            }
            bool flag = !base.Context.IsMultiOrg || Convert.ToBoolean(customFilter["OutSettle"]);
            bool flag2 = !base.Context.IsMultiOrg || Convert.ToBoolean(customFilter["InSettle"]);
            DynamicObject dynamicObject2 = customFilter["AccountSystem"] as DynamicObject;
            long num2 = 0L;
            if (dynamicObject2 != null && !dynamicObject2["Id"].IsNullOrEmptyOrWhiteSpace())
            {
                num2 = (long)Convert.ToInt32(dynamicObject2["Id"]);
            }
            stringBuilder.Append(" AND (1=1 ");
            if (flag2 && !flag)
            {
                if (needFilterAccountSystem)
                {
                    stringBuilder.AppendFormat(" AND (a.FAccountSystem = {0} OR a.FAccountSystem = 0) ", num2);
                }
                stringBuilder.AppendLine("   AND ContactType.FCorrespondOrgId <> 0");
            }
            else
            {
                if (!flag2 && flag)
                {
                    if (needFilterAccountSystem)
                    {
                        stringBuilder.AppendFormat(" AND a.FAccountSystem = 0", new object[0]);
                    }
                    stringBuilder.AppendLine("   AND ContactType.FCorrespondOrgId = 0");
                }
                else
                {
                    if (flag2 && flag && needFilterAccountSystem)
                    {
                        stringBuilder.AppendFormat(" AND ((a.FAccountSystem = {0} OR a.FAccountSystem = 0) AND ContactType.FCorrespondOrgId <> 0) ", num2);
                        stringBuilder.AppendLine(" OR  (a.FAccountSystem = 0 AND ContactType.FCorrespondOrgId = 0)");
                    }
                }
            }
            stringBuilder.Append(")");
            if (!Convert.ToBoolean(customFilter["UnAudit"]))
            {
                stringBuilder.Append(" and a.FDocumentStatus = 'C'");
            }
            else
            {
                stringBuilder.Append(" and a.FDocumentStatus <> 'Z'");
            }
            return stringBuilder.ToString();
        }

        /// <summary>
        /// 动态加载列信息
        /// </summary>
        /// <param name="header"></param>
        /// <param name="isFromFilter"></param>
        /// <param name="balColumnList"></param>
        /// <param name="listField"></param>
        private void LoadBalanceColumn(ListHeader header, bool isFromFilter = false, DynamicObjectCollection balColumnList = null, List<ColumnField> listField = null)
        {
            ListHeader listHeader = header.AddChild();
            listHeader.FieldName = "FOrgCurrency";
            //listHeader.Caption = new LocaleValue(ResManager.LoadKDString("原币", "003224000002077", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID);
            listHeader.AddChild("FBalanceAmtFor", new LocaleValue(ResManager.LoadKDString("尚未付款金额", "003224000002080", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID), SqlStorageType.SqlDecimal, true);
            if (listField != null)
            {
                if (listField.Any((ColumnField c) => c.Key == "FBalanceAmtFor"))
                {
                    if (balColumnList == null || balColumnList.Count == 0 || (balColumnList.Count == 1 && balColumnList[0]["Section"].IsNullOrEmptyOrWhiteSpace()))
                    {
                        listHeader.AddChild("FRate4Amt", new LocaleValue(ResManager.LoadKDString("利息", "003224000002092", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID), SqlStorageType.SqlDecimal, true);
                        listHeader.AddChild("FRate4", new LocaleValue(ResManager.LoadKDString("利率", "003224000002092", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID), SqlStorageType.SqlDecimal, true);
                        listHeader.AddChild("FBalance4AmtFor", new LocaleValue(ResManager.LoadKDString("90天以上", "003224000002092", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID), SqlStorageType.SqlDecimal, true);

                        listHeader.AddChild("FRate3Amt", new LocaleValue(ResManager.LoadKDString("利息", "003224000002089", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID), SqlStorageType.SqlDecimal, true);
                        listHeader.AddChild("FRate3", new LocaleValue(ResManager.LoadKDString("利率", "003224000002089", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID), SqlStorageType.SqlDecimal, true);
                        listHeader.AddChild("FBalance3AmtFor", new LocaleValue(ResManager.LoadKDString("61-90天", "003224000002089", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID), SqlStorageType.SqlDecimal, true);

                        listHeader.AddChild("FRate2Amt", new LocaleValue(ResManager.LoadKDString("利息", "003224000002086", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID), SqlStorageType.SqlDecimal, true);
                        listHeader.AddChild("FRate2", new LocaleValue(ResManager.LoadKDString("利率", "003224000002086", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID), SqlStorageType.SqlDecimal, true);
                        listHeader.AddChild("FBalance2AmtFor", new LocaleValue(ResManager.LoadKDString("31-60天", "003224000002086", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID), SqlStorageType.SqlDecimal, true);

                        listHeader.AddChild("FRate1Amt", new LocaleValue(ResManager.LoadKDString("利息", "003224000002083", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID), SqlStorageType.SqlDecimal, true);
                        listHeader.AddChild("FRate1", new LocaleValue(ResManager.LoadKDString("利率", "003224000002083", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID), SqlStorageType.SqlDecimal, true);
                        listHeader.AddChild("FBalance1AmtFor", new LocaleValue(ResManager.LoadKDString("0-30天", "003224000002083", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID), SqlStorageType.SqlDecimal, true);
                        return;
                    }
                    for (int i = balColumnList.Count - 1; i >= 0; i--)
                    {
                        string value = balColumnList[i]["Section"] as string;
                        if (!value.IsNullOrEmptyOrWhiteSpace())
                        {
                            //20160808 fjfdszj
                            listHeader.AddChild(string.Format("FRate{0}Amt", i + 1), new LocaleValue("利息", base.Context.UserLocale.LCID), SqlStorageType.SqlDecimal, true);
                            listHeader.AddChild(string.Format("FRate{0}", i + 1), new LocaleValue("利率", base.Context.UserLocale.LCID), SqlStorageType.SqlDecimal, true);
                            listHeader.AddChild(string.Format("FBalance{0}AmtFor", i + 1), new LocaleValue(value, base.Context.UserLocale.LCID), SqlStorageType.SqlDecimal, true);
                        }
                    }
                }
            }
            if (!isFromFilter)
            {
                return;
            }

            /*
            ListHeader listHeader2 = header.AddChild();
            listHeader2.FieldName = "FStandardCurrency";
            listHeader2.Caption = new LocaleValue(ResManager.LoadKDString("本位币", "003224000002095", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID);
            listHeader2.AddChild("FMasterCurrencyName", new LocaleValue(ResManager.LoadKDString("币别", "003224000002059", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID));
            listHeader2.AddChild("FBalanceAmt", new LocaleValue(ResManager.LoadKDString("尚未付款金额", "003224000002080", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID), SqlStorageType.SqlDecimal, true);
            if (listField != null)
            {
                if (listField.Any((ColumnField c) => c.Key == "FBalanceAmt"))
                {
                    if (balColumnList == null || balColumnList.Count == 0 || (balColumnList.Count == 1 && balColumnList[0]["Section"].IsNullOrEmptyOrWhiteSpace()))
                    {
                        listHeader2.AddChild("FBalance1Amt", new LocaleValue(ResManager.LoadKDString("0-30天", "003224000002083", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID), SqlStorageType.SqlDecimal, true);
                        listHeader2.AddChild("FBalance2Amt", new LocaleValue(ResManager.LoadKDString("31-60天", "003224000002086", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID), SqlStorageType.SqlDecimal, true);
                        listHeader2.AddChild("FBalance3Amt", new LocaleValue(ResManager.LoadKDString("61-90天", "003224000002089", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID), SqlStorageType.SqlDecimal, true);
                        listHeader2.AddChild("FBalance4Amt", new LocaleValue(ResManager.LoadKDString("90天以上", "003224000002092", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID), SqlStorageType.SqlDecimal, true);
                        return;
                    }
                    for (int j = balColumnList.Count - 1; j >= 0; j--)
                    {
                        string value2 = balColumnList[j]["Section"] as string;
                        if (!value2.IsNullOrEmptyOrWhiteSpace())
                        {
                            listHeader2.AddChild(string.Format("FBalance{0}Amt", j + 1), new LocaleValue(value2, base.Context.UserLocale.LCID), SqlStorageType.SqlDecimal, true);
                        }
                    }
                }
            }
            */


        }


        private void InitBalanceDct(DynamicObject filterObj)
        {
            DynamicObjectCollection dynamicObjectCollection = filterObj["EntAgingGrpSetting"] as DynamicObjectCollection;
            //默认处理
            if (dynamicObjectCollection == null || dynamicObjectCollection.Count == 0 || (dynamicObjectCollection.Count == 1 && dynamicObjectCollection[0]["Section"].IsNullOrEmptyOrWhiteSpace()))
            {
                //30,60,90,0
                for (int i = 0; i < 4; i++)
                {
                    int value = (i == 3) ? 0 : ((i + 1) * 30);
                    if (this.balanceDct.ContainsKey(i))
                    {
                        this.balanceDct[i] = value;
                    }
                    else
                    {
                        this.balanceDct.Add(i, value);
                    }
                }
                return;
            }
            //动态处理
            for (int j = 0; j < dynamicObjectCollection.Count; j++)
            {
                string value2 = dynamicObjectCollection[j]["Section"] as string;
                if (!value2.IsNullOrEmptyOrWhiteSpace())
                {
                    int value3 = Convert.ToInt32(dynamicObjectCollection[j]["Days"]);
                    Decimal rate =Convert.ToDecimal(dynamicObjectCollection[j]["FRate"]);
                    if (this.balanceDct.ContainsKey(j))
                    {
                        this.balanceDct[j] = value3;
                        this.balanceDctRate[j] = rate;
                    }
                    else
                    {
                        this.balanceDct.Add(j, value3);
                        this.balanceDctRate.Add(j, rate);
                    }
                }
            }
            //设置精度
            this.SetBalanceAmtDigit(this.balanceDct.Count());//<KeyValuePair<int, int>>
        }

        /// <summary>
        /// 加载单据信息
        /// </summary>
        /// <param name="header"></param>
        /// <param name="isShowBillColumn"></param>
        private void LoadBillColumn(ListHeader header, bool isShowBillColumn = true)
        {
            if (isShowBillColumn)
            {
                header.AddChild("FBillTypeName", new LocaleValue(ResManager.LoadKDString("单据类型", "003224000002098", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID));
                header.AddChild("FBillNo", new LocaleValue(ResManager.LoadKDString("单据编号", "003224000002101", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID));
                ListHeader listHeader = header.AddChild("FDate", new LocaleValue(ResManager.LoadKDString("业务日期", "003224000002104", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID));
                listHeader.ColType = SqlStorageType.SqlSmalldatetime;
                listHeader.Width = 80;
                ListHeader listHeader2 = header.AddChild("FEndDate", new LocaleValue(ResManager.LoadKDString("到期日", "003224000002107", SubSystemType.FIN, new object[0]), base.Context.UserLocale.LCID));
                listHeader2.ColType = SqlStorageType.SqlSmalldatetime;
                listHeader2.Width = 80;
            }
        }

        /// <summary>
        /// 动态sql构建信息
        /// </summary>
        /// <param name="tmpAnalysisTblName"></param>
        /// <returns></returns>
        private string GetCreateTmpAnalysisTblNameSql(string tmpAnalysisTblName)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(string.Format("create table {0}", tmpAnalysisTblName));
            stringBuilder.AppendLine("(");
            stringBuilder.AppendLine("FID int,");
            stringBuilder.AppendLine("FFORMID nvarchar(80),");
            stringBuilder.AppendLine("FBillNo nvarchar(80),");
            stringBuilder.AppendLine("FBillTypeName nvarchar(255),");
            stringBuilder.AppendLine("FDate datetime,");
            stringBuilder.AppendLine("FEndDate datetime,");
            stringBuilder.AppendLine("FContactUnit nvarchar(255),");
            stringBuilder.AppendLine("FCurrencyName nvarchar(255),");
            stringBuilder.AppendLine("FPurchaseDeptName nvarchar(255),");
            stringBuilder.AppendLine("FPurchaseGroupName nvarchar(255),");
            stringBuilder.AppendLine("FPurchaserName nvarchar(255),");
            stringBuilder.AppendLine("FSettleOrgName nvarchar(255),");
            stringBuilder.AppendLine("FPayOrgName nvarchar(255),");
            stringBuilder.AppendLine("FPurchaseOrgName nvarchar(255),");
            stringBuilder.AppendLine("FDigitsFor int,");
            stringBuilder.AppendLine("FDigits int,");
            stringBuilder.AppendLine("FBalanceAmtFor decimal(23,10),");
            stringBuilder.AppendLine("FBalanceAmt decimal(23,10),");
            foreach (int current in this.balanceDct.Keys)
            {
                stringBuilder.AppendLine(string.Format("FBalance{0}AmtFor decimal(23,10),", current + 1));
                stringBuilder.AppendLine(string.Format("FBalance{0}Amt decimal(23,10),", current + 1));
                //20160808
                stringBuilder.AppendLine(string.Format("FRate{0} decimal(23,10),", current + 1));//利率
                stringBuilder.AppendLine(string.Format("FRate{0}Amt decimal(23,10),", current + 1));//利息
            }
            stringBuilder.AppendLine("FMasterCurrencyName varchar(36),");
            stringBuilder.AppendLine("FOrderBy int");
            stringBuilder.AppendLine(")");
            using (KDTransactionScope kDTransactionScope = new KDTransactionScope(TransactionScopeOption.RequiresNew))
            {
                DBUtils.Execute(base.Context, stringBuilder.ToString());
                kDTransactionScope.Complete();
            }
            return string.Empty;
        }
        /// <summary>
        /// 创建临时表信息
        /// </summary>
        /// <param name="tmpTableName"></param>
        /// <returns></returns>

        private string GetCreateTmpTableNameSql(string tmpTableName)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(string.Format("create table {0}", tmpTableName));
            stringBuilder.AppendLine("(");
            stringBuilder.AppendLine("FID int,");
            stringBuilder.AppendLine("FBillNo nvarchar(80),");
            stringBuilder.AppendLine("FDate datetime,");
            stringBuilder.AppendLine("FEndDate datetime,");
            stringBuilder.AppendLine("FFORMID nvarchar(80),");
            stringBuilder.AppendLine("FContactUnitType nvarchar(80),");
            stringBuilder.AppendLine("FContactUnit nvarchar(255),");
            stringBuilder.AppendLine("FPurchaseDeptName nvarchar(255),");
            stringBuilder.AppendLine("FCurrencyName nvarchar(255),");
            stringBuilder.AppendLine("FBillTypeName nvarchar(255),");
            stringBuilder.AppendLine("FPurchaseGroupName nvarchar(255),");
            stringBuilder.AppendLine("FPurchaserName nvarchar(255),");
            stringBuilder.AppendLine("FSettleOrgName nvarchar(255),");
            stringBuilder.AppendLine("FPayOrgName nvarchar(255),");
            stringBuilder.AppendLine("FPurchaseOrgName nvarchar(255),");
            stringBuilder.AppendLine("FNotWrittenOffAmountFor decimal(23,10),");
            stringBuilder.AppendLine("FNotWrittenOffAmount decimal(23,10),");
            stringBuilder.AppendLine("FDigitsFor int,");
            stringBuilder.AppendLine("FDigits int,");
            stringBuilder.AppendLine("FMasterCurrencyName nvarchar(255),");
            stringBuilder.AppendLine("FOrderBy int,");
            stringBuilder.AppendLine("FBALANCEAMTFOR decimal(23,10),");
            stringBuilder.AppendLine("FBalanceAmt decimal(23,10)");
            stringBuilder.AppendLine(")");
            using (KDTransactionScope kDTransactionScope = new KDTransactionScope(TransactionScopeOption.RequiresNew))
            {
                DBUtils.Execute(base.Context, stringBuilder.ToString());
                kDTransactionScope.Complete();
            }
            return string.Empty;
        }

        /// <summary>
        /// 得到付款信息
        /// </summary>
        /// <param name="tmpTableName"></param>
        /// <param name="filter"></param>
        private void GetPayableDataSql(string tmpTableName, IRptParams filter)
        {
            DynamicObject customFilter = filter.FilterParameter.CustomFilter;
            string arg = "{ts'" + Convert.ToDateTime(customFilter["ByDate"]).ToString("yyyy-MM-dd 23:59:59") + "'}";
            int lCID = base.Context.UserLocale.LCID;
            bool flag = Convert.ToBoolean(customFilter["UnAudit"]);
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendFormat("Insert into {0} ", tmpTableName);
            stringBuilder.AppendLine(" ( FID,FBillNo,FDate,FEndDate,FFORMID,FContactUnitType,FContactUnit,FPurchaseDeptName,FCurrencyName,FBillTypeName,FPurchaseGroupName,");
            stringBuilder.AppendLine(" FPurchaserName,FSettleOrgName,FPayOrgName,FPurchaseOrgName,FNotWrittenOffAmountFor,FNotWrittenOffAmount,FDigitsFor,FDigits,FMasterCurrencyName,FBalanceAmtFor,FBalanceAmt,FOrderBy )");
            stringBuilder.AppendLine("select   a.FID,a.FBillNo,a.FDate,b.FEndDate, 'AP_Payable' as  FFORMID,");
            stringBuilder.AppendLine("'BD_Supplier' as FContactUnitType,");
            stringBuilder.AppendLine("FSupplierID.FName as FContactUnit,");
            stringBuilder.AppendLine("ISNULL(FPurchaseDeptID.FName,' ') as FPurchaseDeptName,");
            stringBuilder.AppendLine("FCurrencyID.fname as FCurrencyName,");
            stringBuilder.AppendLine("FBillTypeID.Fname as FBillTypeName,");
            stringBuilder.AppendLine("ISNULL(FPurchaserGroupID.FName,' ') as FPurchaseGroupName,");
            stringBuilder.AppendLine("ISNULL(FPurchaserID.FName,' ') as FPurchaserName,");
            stringBuilder.AppendLine("FSettleOrgID.FName as FSettleOrgName,");
            stringBuilder.AppendLine("isnull(FPayOrgID.FName,' ') as FPayOrgName,");
            stringBuilder.AppendLine("ISNULL(FPurchaseOrgID.FName,' ') as FPurchaseOrgName,");
            if (!flag)
            {
                stringBuilder.AppendLine("(b.FPayAmountFor - ISNULL(ARmatchRecord.FCURWRITTENOFFAMOUNTFOR,0) - ISNULL(APmatchRecord.FCURWRITTENOFFAMOUNTFOR,0)) as FNotWrittenOffAmountFor,");
                stringBuilder.AppendLine("(b.FPayAmount - ISNULL(ARmatchRecord.FCURWRITTENOFFAMOUNTFOR,0) * fin.FExchangeRate - ISNULL(APmatchRecord.FCURWRITTENOFFAMOUNTFOR,0) * fin.FExchangeRate) as FNotWrittenOffAmount,");
            }
            else
            {
                stringBuilder.AppendLine("(b.FPayAmountFor- ISNULL(ARmatchRecord.FCURWRITTENOFFAMOUNTFOR,0)  - ISNULL(APmatchRecord.FCURWRITTENOFFAMOUNTFOR,0)  - ISNULL(PAY.FRelateAmount,0) - ISNULL(REFUND.FRelateAmount,0)) as FNotWrittenOffAmountFor,");
                stringBuilder.AppendLine("(b.FPayAmount - ISNULL(ARmatchRecord.FCURWRITTENOFFAMOUNTFOR,0) * fin.FExchangeRate- ISNULL(APmatchRecord.FCURWRITTENOFFAMOUNTFOR,0) * fin.FExchangeRate - ISNULL(PAY.FRelateAmount,0) * fin.FEXCHANGERATE - ISNULL(REFUND.FRelateAmount,0) * fin.FEXCHANGERATE) as FNotWrittenOffAmount,");
            }
            stringBuilder.AppendLine("isnull(g.FAmountDigits,2) as FDigitsFor,");
            stringBuilder.AppendLine("isnull(t0.FAmountDigits,2) as FDigits,");
            stringBuilder.AppendLine("t1.FName as FMasterCurrencyName,");
            if (!flag)
            {
                stringBuilder.AppendLine("(b.FPayAmountFor - ISNULL(ARmatchRecord.FCURWRITTENOFFAMOUNTFOR,0) - ISNULL(APmatchRecord.FCURWRITTENOFFAMOUNTFOR,0)) as FBalanceAmtFor,");
                stringBuilder.AppendLine("(b.FPayAmount - ISNULL(ARmatchRecord.FCURWRITTENOFFAMOUNTFOR,0) * fin.FExchangeRate - ISNULL(APmatchRecord.FCURWRITTENOFFAMOUNTFOR,0) * fin.FExchangeRate) as FBalanceAmt,");
            }
            else
            {
                stringBuilder.AppendLine("(b.FPayAmountFor- ISNULL(ARmatchRecord.FCURWRITTENOFFAMOUNTFOR,0)  - ISNULL(APmatchRecord.FCURWRITTENOFFAMOUNTFOR,0)  - ISNULL(PAY.FRelateAmount,0) - ISNULL(REFUND.FRelateAmount,0)) as FBalanceAmtFor,");
                stringBuilder.AppendLine("(b.FPayAmount - ISNULL(ARmatchRecord.FCURWRITTENOFFAMOUNTFOR,0) * fin.FExchangeRate- ISNULL(APmatchRecord.FCURWRITTENOFFAMOUNTFOR,0) * fin.FExchangeRate - ISNULL(PAY.FRelateAmount,0) * fin.FEXCHANGERATE - ISNULL(REFUND.FRelateAmount,0) * fin.FEXCHANGERATE) as FBalanceAmt,");
            }
            stringBuilder.AppendLine("1 as FOrderBy");
            stringBuilder.AppendLine(string.Format("from {0} a", "T_AP_PAYABLE"));
            stringBuilder.AppendLine(string.Format("inner join {0} fin on a.FID = fin.FID", "T_AP_PAYABLEFIN"));
            stringBuilder.AppendLine(string.Format("inner join {0} b on a.FID = b.FID", "T_AP_PAYABLEPLAN"));
            stringBuilder.AppendLine(string.Format("left join {0} ContactType on a.FSupplierID = ContactType.fitemid and ContactType.fformid = 'BD_Supplier'", "V_FIN_CONTACTTYPE"));
            stringBuilder.AppendLine(string.Format("left join {0} as FSupplierID on ContactType.fitemid = FSupplierID.fitemid and ContactType.fformid = 'BD_Supplier' And FSupplierID.Flocaleid = {1}", "V_FIN_CONTACTTYPE_l", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} e on a.FPurchaseDeptID = e.FDeptID", "t_Bd_Department"));
            stringBuilder.AppendLine(string.Format("left join {0} FPurchaseDeptID on a.FPurchaseDeptID = FPurchaseDeptID.FDeptID and FPurchaseDeptID.Flocaleid = {1}", "t_Bd_Department_l", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} g on a.FCurrencyID = g.FCurrencyID", "t_bd_currency"));
            stringBuilder.AppendLine(string.Format("left join {0} FCurrencyID on a.FCurrencyID = FCurrencyID.FCurrencyID and FCurrencyID.Flocaleid = {1}", "t_bd_currency_l", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} FBillTypeID on a.FBillTypeID = FBillTypeID.Fbilltypeid and FBillTypeID.FlocaleID = {1}", "t_bas_billtype_l", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} FPurchaserGroupID on a.FPurchaserGroupID = FPurchaserGroupID.FEntryID and FPurchaserGroupID.Flocaleid = {1}", "V_BD_OPERATORGROUP_L", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} FPurchaserID on a.FPurchaserID = FPurchaserID.FID and FPurchaserID.Flocaleid = {1}", "V_BD_BUYER_L", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} FSettleOrgID_M on a.FSettleOrgID = FSettleOrgID_M.FOrgID", "T_ORG_Organizations"));
            stringBuilder.AppendLine(string.Format("left join {0} FSettleOrgID on a.FSettleOrgID = FSettleOrgID.FOrgID and FSettleOrgID.Flocaleid = {1}", "T_ORG_Organizations_l", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} FPayOrgID on a.FPAYORGID = FPayOrgID.FOrgID  and FPayOrgID.FLocaleid={1}", "T_ORG_Organizations_l", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} FPurchaseOrgID on a.FPurchaseOrgID = FPurchaseOrgID.FOrgID and FPurchaseOrgID.Flocaleid = {1}", "T_ORG_Organizations_l", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} t0 on fin.FMainBookStdCurrID = t0.FCurrencyID ", "t_bd_currency"));
            stringBuilder.AppendLine(string.Format("left join {0} t1 on fin.FMainBookStdCurrID = t1.FCurrencyID and t1.Flocaleid = {1}", "t_bd_currency_l", lCID));
            stringBuilder.AppendLine("left join");
            stringBuilder.AppendLine(string.Format("(select LogEntry.FSourceFromID,FSrcBillId,FSrcRowId, \r\n                                isnull(sum(LogEntry.FCURWRITTENOFFAMOUNTFOR),0) as FCURWRITTENOFFAMOUNTFOR \r\n                                from T_AP_PAYMatchLog PayLog\r\n                                inner join T_AP_PAYMatchLogENTRY LogEntry on PayLog.Fid = LogEntry.Fid\r\n                                where PayLog.FReportDate <= {0} and LogEntry.FSrcDate <={0} and LogEntry.FSourceFromID = 'AP_Payable'\r\n                                group by LogEntry.FSourceFromID,FSrcBillId,FSrcRowId) APmatchRecord", arg));
            stringBuilder.AppendLine(" on a.FId = APmatchRecord.FSrcBillId and b.FEntryId = APmatchRecord.FSrcRowId");
            stringBuilder.AppendFormat("LEFT JOIN (", new object[0]);
            stringBuilder.AppendLine(string.Format(" select LogEntry.FSourceFromID,FSrcBillId,FSrcRowId, \r\n                                isnull(sum(LogEntry.FCURWRITTENOFFAMOUNTFOR),0) as FCURWRITTENOFFAMOUNTFOR \r\n                                from t_ar_recmacthlog PayLog\r\n                                inner join t_ar_recmacthlogEntry LogEntry on PayLog.Fid = LogEntry.Fid\r\n                                where PayLog.FReportDate <= {0} and LogEntry.FSrcDate <={0} And LogEntry.FSourceFromID = 'AP_Payable'\r\n                                group by LogEntry.FSourceFromID,FSrcBillId,FSrcRowId) ARmatchRecord", arg));
            stringBuilder.AppendLine(" on a.FId = ARmatchRecord.FSrcBillId and b.FEntryId = ARmatchRecord.FSrcRowId");
            if (flag)
            {
                stringBuilder.AppendFormat("LEFT JOIN ( ", new object[0]);
                stringBuilder.AppendFormat("select LK.FSID,SUM(LK.FREALPAYAMOUNT_S) AS FRelateAmount from t_ap_paybillsrcentry_lk LK\r\n                                INNER JOIN t_ap_paybillsrcentry SE on LK.Fentryid = SE.Fentryid\r\n                                inner join t_ap_paybill P on SE.FID = P.FID\r\n                                where  P.FCANCELSTATUS = 'A' AND P.FDOCUMENTSTATUS<>'C' AND P.FDOCUMENTSTATUS<>'Z' AND P.FDATE <= {0} And ((P.FAPPROVEDATE is NULL ) Or (P.FAPPROVEDATE > {0}))\r\n                                And lk.FSTABLENAME= '{1}'\r\n                                group by LK.FSID", arg, "T_AP_PAYABLEPLAN");
                stringBuilder.Append(") as PAY on b.FENTRYID = PAY.FSID ");
                stringBuilder.AppendFormat("LEFT JOIN (", new object[0]);
                stringBuilder.AppendFormat("select LK.FSID,SUM(0 - LK.FREALREFUNDAMOUNT_S) AS FRelateAmount from t_ap_refundbillsrcentry_lk LK\r\n                                INNER JOIN t_ap_refundbillsrcentry SE on LK.Fentryid = SE.Fentryid\r\n                                inner join t_ap_refundbill R on SE.FID = R.FID\r\n                                where R.FCANCELSTATUS = 'A' AND  R.FDOCUMENTSTATUS<>'C' AND R.FDOCUMENTSTATUS<>'Z' AND R.FDATE <= {0} And ((R.FAPPROVEDATE is NULL ) Or (R.FAPPROVEDATE > {0}))\r\n                                And lk.FSTABLENAME = '{1}'\r\n                                group by LK.FSID", arg, "T_AP_PAYABLEPLAN");
                stringBuilder.Append(") as REFUND on b.FENTRYID = REFUND.FSID ");
            }
            //新增过滤条件
            stringBuilder.AppendLine(this.GetSqlWhere(filter, true, true));
            DBUtils.ExecuteWithTime(base.Context, stringBuilder.ToString(), null, 1000);
        }

        /// <summary>
        /// 其他付款信息
        /// </summary>
        /// <param name="tmpTableName"></param>
        /// <param name="filter"></param>
        private void GetOtherPayableDataSql(string tmpTableName, IRptParams filter)
        {
            int lCID = base.Context.UserLocale.LCID;
            StringBuilder stringBuilder = new StringBuilder();
            DynamicObject customFilter = filter.FilterParameter.CustomFilter;
            string str = Convert.ToDateTime(customFilter["ByDate"]).ToString("yyyy-MM-dd 23:59:59");
            bool flag = Convert.ToBoolean(customFilter["UnAudit"]);
            stringBuilder.AppendLine(string.Format("insert into {0} ", tmpTableName));
            stringBuilder.AppendLine(" ( FID,FBillNo,FDate,FEndDate,FFORMID,FContactUnitType,FContactUnit,FPurchaseDeptName,FCurrencyName,FBillTypeName,FPurchaseGroupName,");
            stringBuilder.AppendLine(" FPurchaserName,FSettleOrgName,FPayOrgName,FPurchaseOrgName,FNotWrittenOffAmountFor,FNotWrittenOffAmount,FDigitsFor,FDigits,FMasterCurrencyName,FBalanceAmtFor,FBalanceAmt,FOrderBy )");
            stringBuilder.AppendLine("select a.FID,a.FBillNo,a.FDate,a.FENDDATE as FEndDate,'AP_OtherPayable' as FFORMID,");
            stringBuilder.AppendLine("a.FContactUnitType as FContactUnitType,");
            stringBuilder.AppendLine("ContactType_l.fname AS FContactUnit,");
            stringBuilder.AppendLine("' ' as FPurchaseDeptName,");
            stringBuilder.AppendLine("FCurrencyID.fname as FCurrencyName,");
            stringBuilder.AppendLine("FBillTypeID.Fname as FBillTypeName,");
            stringBuilder.AppendLine("' ' as FPurchaseGroupName,");
            stringBuilder.AppendLine("' ' as FPurchaserName,");
            stringBuilder.AppendLine("FSettleOrgID.FName as FSettleOrgName,");
            stringBuilder.AppendLine("isnull(FPayOrgID.FName,' ') as FPayOrgName,");
            stringBuilder.AppendLine("' ' as FPurchaseOrgName,");
            if (!flag)
            {
                stringBuilder.AppendLine("(a.FTOTALAMOUNTFOR - ISNULL(ARmatchRecord.FCURWRITTENOFFAMOUNTFOR,0) - ISNULL(APmatchRecord.FCURWRITTENOFFAMOUNTFOR,0)) as FNotWrittenOffAmountFor,");
                stringBuilder.AppendLine("(a.FTOTALAMOUNT - ISNULL(ARmatchRecord.FCURWRITTENOFFAMOUNTFOR,0) * a.FExchangeRate - ISNULL(APmatchRecord.FCURWRITTENOFFAMOUNTFOR,0) * a.FExchangeRate) as FNotWrittenOffAmount,");
            }
            else
            {
                stringBuilder.AppendLine("(a.FTOTALAMOUNTFOR- ISNULL(ARmatchRecord.FCURWRITTENOFFAMOUNTFOR,0) - ISNULL(APmatchRecord.FCURWRITTENOFFAMOUNTFOR,0) - ISNULL(PAY.FRelateAmount,0) - ISNULL(REFUND.FRelateAmount,0)) as FNotWrittenOffAmountFor,");
                stringBuilder.AppendLine("(a.FTOTALAMOUNT - ISNULL(ARmatchRecord.FCURWRITTENOFFAMOUNTFOR,0) * a.FExchangeRate- ISNULL(APmatchRecord.FCURWRITTENOFFAMOUNTFOR,0) * a.FExchangeRate - ISNULL(PAY.FRelateAmount,0) * a.FEXCHANGERATE - ISNULL(REFUND.FRelateAmount,0) * a.FEXCHANGERATE) as FNotWrittenOffAmount,");
            }
            stringBuilder.AppendLine("isnull(g.FAmountDigits,2) as FDigitsFor,");
            stringBuilder.AppendLine("isnull(t0.FAmountDigits,2) as FDigits,");
            stringBuilder.AppendLine("t1.FName as FMasterCurrencyName,");
            if (!flag)
            {
                stringBuilder.AppendLine("(a.FTOTALAMOUNTFOR - ISNULL(ARmatchRecord.FCURWRITTENOFFAMOUNTFOR,0) - ISNULL(APmatchRecord.FCURWRITTENOFFAMOUNTFOR,0)) as FNotWrittenOffAmountFor,");
                stringBuilder.AppendLine("(a.FTOTALAMOUNT - ISNULL(ARmatchRecord.FCURWRITTENOFFAMOUNTFOR,0) * a.FExchangeRate - ISNULL(APmatchRecord.FCURWRITTENOFFAMOUNTFOR,0) * a.FExchangeRate) as FNotWrittenOffAmount,");
            }
            else
            {
                stringBuilder.AppendLine("(a.FTOTALAMOUNTFOR- ISNULL(ARmatchRecord.FCURWRITTENOFFAMOUNTFOR,0) - ISNULL(APmatchRecord.FCURWRITTENOFFAMOUNTFOR,0) - ISNULL(PAY.FRelateAmount,0) - ISNULL(REFUND.FRelateAmount,0)) as FNotWrittenOffAmountFor,");
                stringBuilder.AppendLine("(a.FTOTALAMOUNT - ISNULL(ARmatchRecord.FCURWRITTENOFFAMOUNTFOR,0) * a.FExchangeRate- ISNULL(APmatchRecord.FCURWRITTENOFFAMOUNTFOR,0) * a.FExchangeRate - ISNULL(PAY.FRelateAmount,0) * a.FEXCHANGERATE - ISNULL(REFUND.FRelateAmount,0) * a.FEXCHANGERATE) as FNotWrittenOffAmount,");
            }
            stringBuilder.AppendLine("2 as FOrderBy");
            stringBuilder.AppendLine(string.Format(" from {0} a", "T_AP_OTHERPAYABLE"));
            stringBuilder.Append("LEFT JOIN V_FIN_CONTACTTYPE ContactType ON a.FContactUnitType = ContactType.fformid and a.FContactUnit = ContactType.fitemid ");
            stringBuilder.AppendFormat("LEFT JOIN V_FIN_CONTACTTYPE_l ContactType_l ON ContactType.fitemid = ContactType_l.fitemid and ContactType_l.FLOCALEID = {0} ", lCID);
            stringBuilder.AppendLine(string.Format("left join {0} g on a.FCurrencyID = g.FCurrencyID", "t_bd_currency"));
            stringBuilder.AppendLine(string.Format("left join {0} FCurrencyID on a.FCurrencyID = FCurrencyID.FCurrencyID and FCurrencyID.Flocaleid = {1}", "t_bd_currency_l", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} FBillTypeID on a.FBillTypeID = FBillTypeID.Fbilltypeid and FBillTypeID.FlocaleID = {1}", "t_bas_billtype_l", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} FSettleOrgID_M on a.FSettleOrgID = FSettleOrgID_M.FOrgID", "T_ORG_Organizations"));
            stringBuilder.AppendLine(string.Format("left join {0} FSettleOrgID on a.FSettleOrgID = FSettleOrgID.FOrgID and FSettleOrgID.Flocaleid = {1}", "T_ORG_Organizations_l", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} FPayOrgID on a.FPAYORGID = FPayOrgID.FOrgID  and FPayOrgID.FLocaleid={1}", "T_ORG_Organizations_l", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} t0 on a.FMAINBOOKSTDCURRID = t0.FCurrencyID ", "t_bd_currency"));
            stringBuilder.AppendLine(string.Format("left join {0} t1 on a.FMAINBOOKSTDCURRID = t1.FCurrencyID and t1.Flocaleid = {1}", "t_bd_currency_l", lCID));
            stringBuilder.AppendLine("LEFT JOIN");
            stringBuilder.AppendLine(string.Format("(SELECT LogEntry.FSourceFromID,FSrcBillId, \r\n                                isnull(sum(LogEntry.FCURWRITTENOFFAMOUNTFOR),0) as FCURWRITTENOFFAMOUNTFOR \r\n                                from T_AP_PAYMatchLog PayLog\r\n                                inner join T_AP_PAYMatchLogENTRY LogEntry on PayLog.Fid = LogEntry.Fid\r\n                                where PayLog.FReportDate <= {0} and LogEntry.FSRCDATE <= {0} and LogEntry.FSourceFromID = 'AP_OtherPayable'\r\n                                group by LogEntry.FSourceFromID,FSrcBillId) APmatchRecord", "{ts'" + str + "'}"));
            stringBuilder.AppendLine(" on a.FId = APmatchRecord.FSrcBillId ");
            stringBuilder.AppendLine(" LEFT JOIN ");
            stringBuilder.AppendLine(string.Format("(SELECT LogEntry.FSourceFromID,FSrcBillId, \r\n                                isnull(sum(LogEntry.FCURWRITTENOFFAMOUNTFOR),0) as FCURWRITTENOFFAMOUNTFOR \r\n                                from T_AR_RECMacthLog PayLog\r\n                                inner join T_AR_RECMacthLogENTRY LogEntry on PayLog.Fid = LogEntry.Fid\r\n                                where PayLog.FReportDate <= {0} AND LogEntry.FSRCDATE <= {0}  and LogEntry.FSourceFromID = 'AP_OtherPayable'\r\n                                group by LogEntry.FSourceFromID,FSrcBillId) ARmatchRecord", "{ts'" + str + "'}"));
            stringBuilder.AppendLine(" on a.FId = ARmatchRecord.FSrcBillId ");
            if (flag)
            {
                stringBuilder.AppendFormat("LEFT JOIN (", new object[0]);
                stringBuilder.AppendFormat("select LK.FSBILLID,SUM(LK.FREALPAYAMOUNT_S) AS FRelateAmount from t_ap_paybillsrcentry_lk LK\r\n                                INNER JOIN t_ap_paybillsrcentry SE on LK.Fentryid = SE.Fentryid\r\n                                inner join t_ap_paybill P on SE.FID = P.FID\r\n                                where P.FCANCELSTATUS = 'A' AND P.FDATE <= {0} And ((P.FAPPROVEDATE is NULL ) Or (P.FAPPROVEDATE > {0})) \r\n                                and  lk.FSTABLENAME = '{1}'\r\n                                group by LK.FSBILLID", "{ts'" + str + "'}", "T_AP_OTHERPAYABLE");
                stringBuilder.Append(") as PAY on a.FID = PAY.FSBILLID ");
                stringBuilder.AppendFormat("LEFT JOIN (", new object[0]);
                stringBuilder.AppendFormat("select LK.FSBILLID,SUM(0 - LK.FREALREFUNDAMOUNT_S) AS FRelateAmount from t_ap_refundbillsrcentry_lk LK\r\n                                INNER JOIN t_ap_refundbillsrcentry SE on LK.Fentryid = SE.Fentryid\r\n                                inner join t_ap_refundbill R on SE.FID = R.FID\r\n                                where R.FCANCELSTATUS = 'A' AND  R.FDATE <= {0} And ((R.FAPPROVEDATE is NULL ) Or (R.FAPPROVEDATE > {0}))\r\n                                and LK.FSTABLENAME = '{1}'\r\n                                group by LK.FSBILLID", "{ts'" + str + "'}", "T_AP_OTHERPAYABLE");
                stringBuilder.Append(") as REFUND on a.FID = REFUND.FSBILLID ");
            }
            stringBuilder.AppendLine(this.GetSqlWhere(filter, false, true));
            DBUtils.Execute(base.Context, stringBuilder.ToString());
        }
        private void GetPayDataSql(string tmpTableName, IRptParams filter)
        {
            DynamicObject customFilter = filter.FilterParameter.CustomFilter;
            string arg = "{ts'" + Convert.ToDateTime(customFilter["ByDate"]).ToString("yyyy-MM-dd 23:59:59") + "'}";
            int lCID = base.Context.UserLocale.LCID;
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(string.Format("insert into {0} ", tmpTableName));
            stringBuilder.AppendLine(" ( FID,FBillNo,FDate,FEndDate,FFORMID,FContactUnitType,FContactUnit,FPurchaseDeptName,FCurrencyName,FBillTypeName,FPurchaseGroupName,");
            stringBuilder.AppendLine(" FPurchaserName,FSettleOrgName,FPayOrgName,FPurchaseOrgName,FNotWrittenOffAmountFor,FNotWrittenOffAmount,FDigitsFor,FDigits,FMasterCurrencyName,FBalanceAmtFor,FBalanceAmt,FOrderBy )");
            stringBuilder.AppendLine("select  a.FID,a.FBillNo,a.FDate,a.FDate as FEndDate,'AP_PAYBILL' as FFORMID,");
            stringBuilder.AppendLine("a.FContactUnitType as FContactUnitType,");
            stringBuilder.AppendLine("ContactType_l.fname AS FContactUnit,");
            stringBuilder.AppendLine("ISNULL(FPurchaseDeptID.FName,' ') as FPurchaseDeptName,");
            stringBuilder.AppendLine("FCurrencyID.fname as FCurrencyName,");
            stringBuilder.AppendLine("FBillTypeID.Fname as FBillTypeName,");
            stringBuilder.AppendLine("ISNULL(FPurchaserGroupID.FName,' ') as FPurchaseGroupName,");
            stringBuilder.AppendLine("ISNULL(FPurchaserID.FName,' ') as FPurchaserName,");
            stringBuilder.AppendLine("FSettleOrgID.FName as FSettleOrgName,");
            stringBuilder.AppendLine("isnull(FPayOrgID.FName,' ') as FPayOrgName,");
            stringBuilder.AppendLine("ISNULL(FPurchaseOrgID.FName,' ') as FPurchaseOrgName,");
            stringBuilder.AppendLine("-1 * (ISNULL(ENTRY.FNotWrittenOffAmountFor,0) + ISNULL(t2.FCURWRITTENOFFAMOUNTFOR,0) - ISNULL(SLK.FRelateAmount,0)) as FNotWrittenOffAmountFor,");
            stringBuilder.AppendLine("-1 * a.FExchangeRate * (ISNULL(ENTRY.FNotWrittenOffAmountFor,0) + ISNULL(t2.FCURWRITTENOFFAMOUNTFOR,0) - ISNULL(SLK.FRelateAmount,0)) as FNotWrittenOffAmount,");
            stringBuilder.AppendLine("isnull(g.FAmountDigits,2) as FDigitsFor,");
            stringBuilder.AppendLine("isnull(t0.FAmountDigits,2) as FDigits,");
            stringBuilder.AppendLine("t1.FName as FMasterCurrencyName,");
            stringBuilder.AppendLine("-1 * (ISNULL(ENTRY.FNotWrittenOffAmountFor,0) + ISNULL(t2.FCURWRITTENOFFAMOUNTFOR,0) - ISNULL(SLK.FRelateAmount,0)) as FBalanceAmtFor,");
            stringBuilder.AppendLine("-1 * a.FExchangeRate * (ISNULL(ENTRY.FNotWrittenOffAmountFor,0) + ISNULL(t2.FCURWRITTENOFFAMOUNTFOR,0) - ISNULL(SLK.FRelateAmount,0)) as FBalanceAmt,");
            stringBuilder.AppendLine("3 as FOrderBy");
            stringBuilder.AppendLine(string.Format(" from {0} a", "T_AP_PAYBILL"));
            stringBuilder.AppendLine("inner join");
            stringBuilder.AppendLine(string.Format("(select FID, sum(FPAYTOTALAMOUNTFOR - FWRITTENOFFAMOUNTFOR) as FNotWrittenOffAmountFor \r\n                            from {0}\r\n                            where  FPURPOSEID in (select fid from T_CN_RECPAYPURPOSE where FFINMANEGEMENT = '{1}')\r\n                            group by FID) ENTRY on a.FID = ENTRY.FID", "T_AP_PAYBILLENTRY", Convert.ToInt32(FinManagement.NumberContact)));
            stringBuilder.Append("LEFT JOIN V_FIN_CONTACTTYPE ContactType ON a.FContactUnitType = ContactType.fformid and a.FContactUnit = ContactType.fitemid ");
            stringBuilder.AppendFormat("LEFT JOIN V_FIN_CONTACTTYPE_l ContactType_l ON ContactType.fitemid = ContactType_l.fitemid  and ContactType_l.FLOCALEID = {0} ", lCID);
            stringBuilder.AppendLine(string.Format("left join {0} e on a.FPurchaseDeptID = e.FDeptID", "t_Bd_Department"));
            stringBuilder.AppendLine(string.Format("left join {0} FPurchaseDeptID on a.FPurchaseDeptID = FPurchaseDeptID.FDeptID and FPurchaseDeptID.Flocaleid = {1}", "t_Bd_Department_l", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} g on a.FCurrencyID = g.FCurrencyID", "t_bd_currency"));
            stringBuilder.AppendLine(string.Format("left join {0} FCurrencyID on a.FCurrencyID = FCurrencyID.FCurrencyID and FCurrencyID.Flocaleid = {1}", "t_bd_currency_l", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} FBillTypeID on a.FBillTypeID = FBillTypeID.Fbilltypeid and FBillTypeID.FlocaleID = {1}", "t_bas_billtype_l", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} FPurchaserGroupID on a.FPurchaserGroupID = FPurchaserGroupID.FEntryID and FPurchaserGroupID.Flocaleid = {1}", "V_BD_OPERATORGROUP_L", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} FPurchaserID on a.FPurchaserID = FPurchaserID.FID and FPurchaserID.Flocaleid = {1}", "V_BD_BUYER_L", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} FSettleOrgID_M on a.FSettleOrgID = FSettleOrgID_M.FOrgID", "T_ORG_Organizations"));
            stringBuilder.AppendLine(string.Format("left join {0} FSettleOrgID on a.FSettleOrgID = FSettleOrgID.FOrgID and FSettleOrgID.Flocaleid = {1}", "T_ORG_Organizations_l", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} FPayOrgID on a.FPAYORGID = FPayOrgID.FOrgID  and FPayOrgID.FLocaleid={1}", "T_ORG_Organizations_l", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} FPurchaseOrgID on a.FPurchaseOrgID = FPurchaseOrgID.FOrgID and FPurchaseOrgID.Flocaleid = {1}", "T_ORG_Organizations_l", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} t0 on a.FMAINBOOKID = t0.FCurrencyID ", "t_bd_currency"));
            stringBuilder.AppendLine(string.Format("left join {0} t1 on a.FMAINBOOKID = t1.FCurrencyID and t1.Flocaleid = {1}", "t_bd_currency_l", lCID));
            stringBuilder.AppendLine(string.Format(" inner join {0} bas on a.Fpayorgid = bas.Forgid", "T_BAS_SYSTEMPROFILE"));
            stringBuilder.AppendLine(" and bas.Fcategory = 'AP' AND FKEY = 'APStartDate' and ");
            stringBuilder.AppendLine(" ((a.fdate >= TO_DATE(FVALUE,'yyyy-MM-dd HH24:mi:ss') and a.FISINIT = '0') ");
            stringBuilder.AppendLine(" or (a.fdate <= TO_DATE(FVALUE, 'yyyy-MM-dd HH24:mi:ss') and a.FISINIT = '1'))");
            stringBuilder.AppendLine("left join");
            stringBuilder.AppendLine(string.Format("(select LogEntry.FSourceFromid,FSrcBillId, \r\n                                isnull(sum(LogEntry.FCURWRITTENOFFAMOUNTFOR),0) as FCURWRITTENOFFAMOUNTFOR \r\n                                from T_AP_PAYMatchLog PayLog\r\n                                inner join T_AP_PAYMatchLogENTRY LogEntry on PayLog.Fid = LogEntry.Fid\r\n                                where PayLog.FReportDate > {0} and LogEntry.FSourceFromid = 'AP_PAYBILL'\r\n                                group by LogEntry.FSourceFromid,FSrcBillId) t2", arg));
            stringBuilder.AppendLine(" on a.FId = t2.FSrcBillId");
            stringBuilder.AppendLine(" left join (");
            stringBuilder.AppendLine("select b.fid, sum(FRealPayAmount_S) as FRelateAmount \r\n                                from t_ap_paybillsrcentry_lk lk\r\n                                inner join t_ap_paybillsrcentry src on lk.fentryid = src.fentryid\r\n                                inner join t_ap_paybill b on src.fid = b.fid\r\n                                where b.fdocumentstatus <> 'C' AND b.FCANCELSTATUS = 'A'\r\n                                group by b.fid) SLK");
            stringBuilder.AppendLine(" on a.FId = SLK.fid");
            stringBuilder.AppendLine(this.GetSqlWhere(filter, false, true));
            DBUtils.ExecuteWithTime(base.Context, stringBuilder.ToString(), null, 1000);
        }
        private void GetRefundDataSql(string tmpTableName, IRptParams filter)
        {
            DynamicObject customFilter = filter.FilterParameter.CustomFilter;
            string arg = "{ts'" + Convert.ToDateTime(customFilter["ByDate"]).ToString("yyyy-MM-dd 23:59:59") + "'}";
            int lCID = base.Context.UserLocale.LCID;
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(string.Format("insert into {0} ", tmpTableName));
            stringBuilder.AppendLine(" ( FID,FBillNo,FDate,FEndDate,FFORMID,FContactUnitType,FContactUnit,FPurchaseDeptName,FCurrencyName,FBillTypeName,FPurchaseGroupName,");
            stringBuilder.AppendLine(" FPurchaserName,FSettleOrgName,FPayOrgName,FPurchaseOrgName,FNotWrittenOffAmountFor,FNotWrittenOffAmount,FDigitsFor,FDigits,FMasterCurrencyName,FBalanceAmtFor,FBalanceAmt,FOrderBy )");
            stringBuilder.AppendLine("select a.FID,a.FBillNo,a.FDate,a.FDate as FEndDate,'AP_REFUNDBILL' as FFORMID,");
            stringBuilder.AppendLine("a.FContactUnitType as FContactUnitType,");
            stringBuilder.AppendLine("ContactType_l.fname  AS FContactUnit,");
            stringBuilder.AppendLine("ISNULL(FPurchaseDeptID.FName,' ') as FPurchaseDeptName,");
            stringBuilder.AppendLine("FCurrencyID.fname as FCurrencyName,");
            stringBuilder.AppendLine("FBillTypeID.Fname as FBillTypeName,");
            stringBuilder.AppendLine("ISNULL(FPurchaserGroupID.FName,' ') as FPurchaseGroupName,");
            stringBuilder.AppendLine("ISNULL(FPurchaserID.FName,' ') as FPurchaserName,");
            stringBuilder.AppendLine("FSettleOrgID.FName as FSettleOrgName,");
            stringBuilder.AppendLine("isnull(FPayOrgID.FName,' ') as FPayOrgName,");
            stringBuilder.AppendLine("ISNULL(FPurchaseOrgID.FName,' ') as FPurchaseOrgName,");
            stringBuilder.AppendLine("(ISNULL(ENTRY.FNotWrittenOffAmountFor,0) + ISNULL(t2.FCURWRITTENOFFAMOUNTFOR,0) - ISNULL(SLK.FRelateAmount,0)) as FNotWrittenOffAmountFor,");
            stringBuilder.AppendLine("(ISNULL(ENTRY.FNotWrittenOffAmountFor,0) + ISNULL(t2.FCURWRITTENOFFAMOUNTFOR,0) - ISNULL(SLK.FRelateAmount,0)) * a.FExchangeRate as FNotWrittenOffAmount,");
            stringBuilder.AppendLine("isnull(g.FAmountDigits,2) as FDigitsFor,");
            stringBuilder.AppendLine("isnull(t0.FAmountDigits,2) as FDigits,");
            stringBuilder.AppendLine("t1.FName as FMasterCurrencyName,");
            stringBuilder.AppendLine("(ISNULL(ENTRY.FNotWrittenOffAmountFor,0) + ISNULL(t2.FCURWRITTENOFFAMOUNTFOR,0) - ISNULL(SLK.FRelateAmount,0)) as FBalanceAmtFor,");
            stringBuilder.AppendLine("(ISNULL(ENTRY.FNotWrittenOffAmountFor,0) + ISNULL(t2.FCURWRITTENOFFAMOUNTFOR,0) - ISNULL(SLK.FRelateAmount,0)) * a.FExchangeRate as FBalanceAmt,");
            stringBuilder.AppendLine("4 as FOrderBy");
            stringBuilder.AppendLine(string.Format(" from {0} a", "T_AP_REFUNDBILL"));
            stringBuilder.AppendLine("inner join");
            stringBuilder.AppendLine(string.Format("(select FID, sum(FREFUNDAMOUNTFOR - FWRITTENOFFAMOUNTFOR) as FNotWrittenOffAmountFor \r\n                            from {0}\r\n                            where FPURPOSEID in (select fid from T_CN_RECPAYPURPOSE where FFINMANEGEMENT = '{1}')\r\n                            group by FID) ENTRY on a.FID = ENTRY.FID", "T_AP_REFUNDBILLENTRY", Convert.ToInt32(FinManagement.NumberContact)));
            stringBuilder.Append("LEFT JOIN V_FIN_CONTACTTYPE ContactType ON a.FContactUnitType = ContactType.fformid and a.FContactUnit = ContactType.fitemid ");
            stringBuilder.AppendFormat("LEFT JOIN V_FIN_CONTACTTYPE_l ContactType_l ON ContactType.fitemid = ContactType_l.fitemid and ContactType_l.FLOCALEID = {0} ", lCID);
            stringBuilder.AppendLine(string.Format("left join {0} e on a.FPurchaseDeptID = e.FDeptID", "t_Bd_Department"));
            stringBuilder.AppendLine(string.Format("left join {0} FPurchaseDeptID on a.FPurchaseDeptID = FPurchaseDeptID.FDeptID and FPurchaseDeptID.Flocaleid = {1}", "t_Bd_Department_l", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} g on a.FCurrencyID = g.FCurrencyID", "t_bd_currency"));
            stringBuilder.AppendLine(string.Format("left join {0} FCurrencyID on a.FCurrencyID = FCurrencyID.FCurrencyID and FCurrencyID.Flocaleid = {1}", "t_bd_currency_l", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} FBillTypeID on a.FBillTypeID = FBillTypeID.Fbilltypeid and FBillTypeID.FlocaleID = {1}", "t_bas_billtype_l", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} FPurchaserGroupID on a.FPurchaserGroupID = FPurchaserGroupID.FEntryID and FPurchaserGroupID.Flocaleid = {1}", "V_BD_OPERATORGROUP_L", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} FPurchaserID on a.FPurchaserID = FPurchaserID.FID and FPurchaserID.Flocaleid = {1}", "V_BD_BUYER_L", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} FSettleOrgID_M on a.FSettleOrgID = FSettleOrgID_M.FOrgID", "T_ORG_Organizations"));
            stringBuilder.AppendLine(string.Format("left join {0} FSettleOrgID on a.FSettleOrgID = FSettleOrgID.FOrgID and FSettleOrgID.Flocaleid = {1}", "T_ORG_Organizations_l", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} FPayOrgID on a.FPAYORGID = FPayOrgID.FOrgID  and FPayOrgID.FLocaleid={1}", "T_ORG_Organizations_l", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} FPurchaseOrgID on a.FPurchaseOrgID = FPurchaseOrgID.FOrgID and FPurchaseOrgID.Flocaleid = {1}", "T_ORG_Organizations_l", lCID));
            stringBuilder.AppendLine(string.Format("left join {0} t0 on a.FMAINBOOKCURRID = t0.FCurrencyID ", "t_bd_currency"));
            stringBuilder.AppendLine(string.Format("left join {0} t1 on a.FMAINBOOKCURRID = t1.FCurrencyID and t1.Flocaleid = {1}", "t_bd_currency_l", lCID));
            stringBuilder.AppendLine(string.Format(" inner join {0} bas on a.Fpayorgid = bas.Forgid", "T_BAS_SYSTEMPROFILE"));
            stringBuilder.AppendLine(" and bas.Fcategory = 'AP' AND FKEY = 'APStartDate' and ");
            stringBuilder.AppendLine(" ((a.fdate >= TO_DATE(FVALUE,'yyyy-MM-dd HH24:mi:ss') and a.FISINIT = '0') ");
            stringBuilder.AppendLine(" or (a.fdate <= TO_DATE(FVALUE, 'yyyy-MM-dd HH24:mi:ss') and a.FISINIT = '1'))");
            stringBuilder.AppendLine("left join");
            stringBuilder.AppendLine(string.Format("(select LogEntry.FSourceFromid,FSrcBillId, \r\n                                isnull(sum(LogEntry.FCURWRITTENOFFAMOUNTFOR),0) as FCURWRITTENOFFAMOUNTFOR \r\n                                from T_AP_PAYMatchLog PayLog\r\n                                inner join T_AP_PAYMatchLogENTRY LogEntry on PayLog.Fid = LogEntry.Fid\r\n                                where PayLog.FReportDate > {0} and LogEntry.FSourceFromid = 'AP_REFUNDBILL'\r\n                                group by LogEntry.FSourceFromid,FSrcBillId) t2", arg));
            stringBuilder.AppendLine(" on a.FId = t2.FSrcBillId");
            stringBuilder.AppendLine(" left join (");
            stringBuilder.AppendLine("select b.fid, sum(FRealRefundAmount_S) as FRelateAmount \r\n                                from t_ap_refundbillsrcentry_lk lk\r\n                                inner join t_ap_refundbillsrcentry src on lk.fentryid = src.fentryid\r\n                                inner join t_ap_refundbill b on src.fid = b.fid\r\n                                where b.fdocumentstatus <> 'C' and b.FCANCELSTATUS = 'A'\r\n                                group by b.fid) SLK");
            stringBuilder.AppendLine(" on a.FId = SLK.fid");
            stringBuilder.AppendLine(this.GetSqlWhere(filter, false, true));
            DBUtils.Execute(base.Context, stringBuilder.ToString());
        }
        private void GetFilterDataTable(string tmpTableName, string tmpFilterTblName, string filterString)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendFormat("select * into {0} from {1} ", tmpFilterTblName, tmpTableName);
            if (!string.IsNullOrWhiteSpace(filterString))
            {
                stringBuilder.AppendFormat(" where {0}", filterString);
            }
            DBUtils.Execute(base.Context, stringBuilder.ToString());
        }
        /// <summary>
        /// 组合最终数据
        /// </summary>
        /// <param name="tmpTableName"></param>
        /// <param name="tmpAnalysisTblName"></param>
        /// <param name="filter"></param>
        /// <param name="balFieldList"></param>
        /// <param name="sortBy"></param>
        private void GetCalculateAgingSql(string tmpTableName, string tmpAnalysisTblName, IRptParams filter, string balFieldList, string sortBy = null)
        {
            DynamicObject customFilter = filter.FilterParameter.CustomFilter;
            string calcDateString = "{ts'" + Convert.ToDateTime(customFilter["ByDate"]).ToString("yyyy-MM-dd") + "'}";
            string calcDateStd = (Convert.ToInt32(customFilter["AgingCalStd"]) == 1) ? "FDate" : "FEndDate";
            StringBuilder stringBuilder = new StringBuilder();
            if (Convert.ToBoolean(customFilter["ByBill"]))
            {
                stringBuilder.Append(this.GetCalculateAgingSqlByBill(tmpTableName, tmpAnalysisTblName, balFieldList, calcDateString, calcDateStd));
            }
            else
            {
                stringBuilder.Append(this.GetCalculateAgingSqlByGroup(tmpTableName, tmpAnalysisTblName, balFieldList, calcDateString, calcDateStd, filter));
            }
            if (!sortBy.IsNullOrEmptyOrWhiteSpace())
            {
                stringBuilder.AppendLine(string.Format("order by {0}", sortBy));
            }
            DBUtils.Execute(base.Context, stringBuilder.ToString());
        }
        private string GetCalculateAgingSqlByBill(string tmpTableName, string tmpAnalysisTblName, string balFieldList, string calcDateString, string calcDateStd)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(string.Format("insert into {0}(FID,FBillNo,FBillTypeName,FDate,FEndDate,FFORMID,", tmpAnalysisTblName));
            stringBuilder.AppendLine("FContactUnit,FCurrencyName,");
            stringBuilder.AppendLine("FPurchaseDeptName,FPurchaseGroupName,FPurchaserName,");
            stringBuilder.AppendLine("FSettleOrgName,FPayOrgName,FPurchaseOrgName,");
            stringBuilder.AppendLine("FDigitsFor,FDigits,FBalanceAmtFor,FBalanceAmt,");
            stringBuilder.AppendLine(balFieldList);
            stringBuilder.AppendLine("FMasterCurrencyName,FOrderBy)");
            stringBuilder.AppendLine("select a.FID,a.FBillNo,a.FBillTypeName,a.FDate,a.FEndDate,a.FFORMID,");
            stringBuilder.AppendLine("a.FContactUnit,a.FCurrencyName,");
            stringBuilder.AppendLine("a.FPurchaseDeptName,a.FPurchaseGroupName,a.FPurchaserName,");
            stringBuilder.AppendLine("a.FSettleOrgName,a.FPayOrgName,a.FPurchaseOrgName,");
            stringBuilder.AppendLine("a.FDigitsFor,a.FDigits,");
            stringBuilder.AppendLine("a.FNotWrittenOffAmountFor as FBalanceAmtFor,");
            stringBuilder.AppendLine("a.FNotWrittenOffAmount as FBalanceAmt,");
            foreach (int current in this.balanceDct.Keys)
            {
                stringBuilder.AppendLine(string.Format("(t{0}.FBalance{0}AmtFor) as FBalance{0}AmtFor,(t{0}.FBalance{0}Amt) as FBalance{0}Amt,", current + 1));
            }
            stringBuilder.AppendLine("a.FMasterCurrencyName,a.FOrderBy");
            stringBuilder.AppendLine(string.Format("from {0} a", tmpTableName));
            foreach (int current2 in this.balanceDct.Keys)
            {
                stringBuilder.AppendLine("left join");
                stringBuilder.AppendLine("(");
                stringBuilder.AppendLine(this.TAB + string.Format("select FId, FBillNo,FEndDate,sum(FNotWrittenOffAmountFor) as FBalance{0}AmtFor,sum(FNotWrittenOffAmount) as FBalance{0}Amt ", current2 + 1));
                stringBuilder.AppendLine(this.TAB + string.Format("from {0}", tmpTableName));
                if (current2 == 0)
                {
                    stringBuilder.AppendLine(this.TAB + string.Format("where  datediff(dd,{0},{1}) <= {2}", calcDateStd, calcDateString, this.balanceDct[current2]));
                }
                else
                {
                    if (this.balanceDct[current2] == 0)
                    {
                        stringBuilder.AppendLine(this.TAB + string.Format("where datediff(dd,{0},{1}) > {2} ", calcDateStd, calcDateString, this.balanceDct[current2 - 1]));
                    }
                    else
                    {
                        stringBuilder.AppendLine(this.TAB + string.Format("where datediff(dd,{0},{1}) > {2} and datediff(dd,{0},{1}) <= {3}", new object[]
                        {
                            calcDateStd,
                            calcDateString,
                            this.balanceDct[current2 - 1],
                            this.balanceDct[current2]
                        }));
                    }
                }
                stringBuilder.AppendLine(this.TAB + "group by FId, FBillNo,FEndDate");
                stringBuilder.AppendLine(string.Format(") t{0} on a.FId = t{0}.FId and a.FBillNo = t{0}.FBillNo and a.FEndDate = t{0}.FEndDate", current2 + 1));
            }
            stringBuilder.AppendLine(string.Format("where datediff(dd,a.FDate,{0}) >= 0 and a.FNotWrittenOffAmountFor <> 0", calcDateString));
            return stringBuilder.ToString();
        }

        /// <summary>
        /// 关键sql
        /// </summary>
        /// <param name="tmpTableName"></param>
        /// <param name="tmpAnalysisTblName"></param>
        /// <param name="balFieldList"></param>
        /// <param name="calcDateString"></param>
        /// <param name="calcDateStd"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        private string GetCalculateAgingSqlByGroup(string tmpTableName, string tmpAnalysisTblName, string balFieldList, string calcDateString, string calcDateStd, IRptParams filter)
        {
            filter.FilterParameter.GroupbyString.Contains("FSettleOrgName");
            filter.FilterParameter.GroupbyString.Contains("FPayOrgName");
            List<string> list = new List<string>
            {
                "FContactUnit",
                "FCurrencyName",
                "FSettleOrgName",
                "FPayOrgName",
                "FDigitsFor"
            };
            DynamicObject customFilter = filter.FilterParameter.CustomFilter;
            List<ColumnField> columnInfo = filter.FilterParameter.ColumnInfo;
            bool flag = false;
            if (columnInfo.Any((ColumnField c) => c.Key == "FMasterCurrencyName") && Convert.ToBoolean(customFilter["IsFromFilter"]))
            {
                flag = true;
                list.Add("FMasterCurrencyName");
                list.Add("FDigits");
            }
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(string.Format("insert into {0}(FContactUnit,FCurrencyName,", tmpAnalysisTblName));
            stringBuilder.AppendLine("FPurchaseDeptName,FPurchaseGroupName,FPurchaserName,");
            stringBuilder.AppendLine("FSettleOrgName,FPayOrgName,FPurchaseOrgName,");
            stringBuilder.AppendLine("FDigitsFor,FDigits,FBalanceAmtFor,FBalanceAmt,");
            stringBuilder.AppendLine(balFieldList);
            stringBuilder.AppendLine("FMasterCurrencyName,FOrderBy)");
            stringBuilder.AppendLine("select a.FContactUnit,a.FCurrencyName,");
            stringBuilder.AppendLine("'' as FPurchaseDeptName,'' as FPurchaseGroupName,'' as FPurchaserName,");
            stringBuilder.AppendLine("a.FSettleOrgName as FSettleOrgName,");
            stringBuilder.AppendLine("a.FPayOrgName as FPayOrgName,");
            stringBuilder.AppendLine("'' as FPurchaseOrgName,");
            stringBuilder.AppendLine("a.FDigitsFor,");
            stringBuilder.AppendLine(string.Format("{0} as FDigits,", flag ? "a.FDigits" : "''"));
            stringBuilder.AppendLine("sum(FNotWrittenOffAmountFor) as FBalanceAmtFor,");
            stringBuilder.AppendLine("sum(FNotWrittenOffAmount) as FBalanceAmt,");
            foreach (int current in this.balanceDct.Keys)
            {
                stringBuilder.AppendLine(string.Format("sum(t{0}.FBalance{0}AmtFor) as FBalance{0}AmtFor,sum(t{0}.FBalance{0}Amt) as FBalance{0}Amt,", current + 1));
                //20160808新增利息，利率 fjfdszj
                stringBuilder.AppendLine(string.Format(balanceDctRate[current]+ " as FRate{0},"+ balanceDctRate[current] + "*sum(t{0}.FBalance{0}AmtFor) as FRate{0}Amt,", current + 1));
            }
            stringBuilder.AppendLine(string.Format("{0} as FMasterCurrencyName,", flag ? "a.FMasterCurrencyName" : "''"));
            stringBuilder.AppendLine("0 as FOrderBy  ");
            stringBuilder.AppendLine(string.Format("from {0} a", tmpTableName));
            foreach (int current2 in this.balanceDct.Keys)
            {
                stringBuilder.AppendLine("left join");
                stringBuilder.AppendLine("(");
                stringBuilder.AppendLine(this.TAB + string.Format("select FId, FBillNo,FEndDate, FNotWrittenOffAmountFor as FBalance{0}AmtFor,FNotWrittenOffAmount as FBalance{0}Amt ", current2 + 1));
                stringBuilder.AppendLine(this.TAB + string.Format("from {0}", tmpTableName));
                if (current2 == 0)
                {
                    stringBuilder.AppendLine(this.TAB + string.Format("where  datediff(dd,{0},{1}) <= {2}", calcDateStd, calcDateString, this.balanceDct[current2]));
                }
                else
                {
                    if (this.balanceDct[current2] == 0)
                    {
                        stringBuilder.AppendLine(this.TAB + string.Format("where datediff(dd,{0},{1}) > {2} ", calcDateStd, calcDateString, this.balanceDct[current2 - 1]));
                    }
                    else
                    {
                        stringBuilder.AppendLine(this.TAB + string.Format("where datediff(dd,{0},{1}) > {2} and datediff(dd,{0},{1}) <= {3}", new object[]
                        {
                            calcDateStd,
                            calcDateString,
                            this.balanceDct[current2 - 1],
                            this.balanceDct[current2]
                        }));
                    }
                }
                stringBuilder.AppendLine(string.Format(") t{0} on a.FId = t{0}.FId and a.FBillNo = t{0}.FBillNo and a.FEndDate = t{0}.FEndDate", current2 + 1));
            }
            stringBuilder.AppendLine(string.Format("where datediff(dd,a.FDate,{0}) >= 0 and a.FNotWrittenOffAmountFor <> 0", calcDateString));
            stringBuilder.AppendLine(this.TAB + string.Format("group by a.{0} ", string.Join(",a.", list)));
            //测试Debug
            //Console.WriteLine(stringBuilder.ToString());
            return stringBuilder.ToString();
        }
        /// <summary>
        /// 执行完结果集合
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="tmpAnalysisTblName"></param>
        /// <param name="sortString"></param>
        /// <param name="balFieldList"></param>
        private void GetReportSql(string tableName, string tmpAnalysisTblName, string sortString, string balFieldList)
        {
            string arg = sortString;
            if (string.IsNullOrWhiteSpace(sortString))
            {
                arg = "FContactUnit asc,FDate asc,FOrderBy asc";
            }
            this.KSQL_SEQ = string.Format(this.ksqlSeq, arg);
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("select a.*,");
            stringBuilder.AppendLine(string.Format("{0} into {1} from {2} a ", this.KSQL_SEQ, tableName, tmpAnalysisTblName));
            DBUtils.Execute(Context, stringBuilder.ToString());
        }

    }
}
