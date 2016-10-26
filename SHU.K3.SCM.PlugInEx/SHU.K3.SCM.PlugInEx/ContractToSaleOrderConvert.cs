using Kingdee.BOS.Core.Metadata.ConvertElement.PlugIn;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kingdee.BOS.Core.Metadata.ConvertElement.PlugIn.Args;
using Kingdee.BOS.App.Core.PlugInProxy;
using Kingdee.BOS.Core.DynamicForm;
using Kingdee.BOS.App.Core.DefaultValueService;
using Kingdee.BOS.Core;
using Kingdee.BOS.Core.Metadata.FieldElement;
using System.ComponentModel;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.Core.SqlBuilder;
using Kingdee.BOS.Core.Metadata;
using Kingdee.BOS.Contracts;
using Kingdee.BOS.App;

namespace SHU.K3.SCM.PlugInEx.BillConvertPlugIn
{
    [Description("处理销售合同单号下推")]
    public class ContractToSaleOrderConvert : AbstractConvertPlugIn
    {
        private long _srcID;
        /*
        public override void OnAfterCreateLink(CreateLinkEventArgs e)
        {
            base.OnAfterCreateLink(e);
            int srcfid = 0;
            ExtendedDataEntity[] array = e.TargetExtendedDataEntities.FindByEntityKey("FBillHead");//目标单据
            BaseDataField srcinfoentry = e.SourceBusinessInfo.GetField("fid") as BaseDataField;
            BusinessInfo srcinfo = e.SourceBusinessInfo.GetObjectData();
            DynamicFormModelProxy modelProxy = GetModelProxy(e);
            ExtendedDataEntity[] array2 = array;
            for (int i = 0; i < array2.Length; i++)
            {
                ExtendedDataEntity extendedDataEntity = array2[i];
                modelProxy.DataObject = extendedDataEntity.DataEntity;
                modelProxy.DataObject["F_WDS_salecontract"] = srcinfo.getd
               // extendedDataEntity.DataEntity["CreateOrgId_Id"] = 0;
               // extendedDataEntity.DataEntity["CreateOrgId"] = null;
            }
             
        }
        */
        public override void AfterConvert(AfterConvertEventArgs e)
        {
            base.AfterConvert(e);
            ExtendedDataEntity[] array = e.Result.FindByEntityKey("FBillHead");//目标单据
            //取源单信息
            IMetaDataService service = ServiceHelper.GetService<IMetaDataService>();
            FormMetadata formMetadata = (FormMetadata)service.Load(base.Context, "WDS_CONTRACT", true);//SHU_WDS_CONTRACT   WDS_CONTRACTBD
            IViewService viewService = ServiceFactory.GetViewService(base.Context);
            DynamicObject value = viewService.LoadSingle(base.Context, this._srcID, formMetadata.BusinessInfo.GetDynamicObjectType());
            ExtendedDataEntity[] array2 = array;
            for (int i = 0; i < array2.Length; i++)
            {
                ExtendedDataEntity extendedDataEntity = array2[i];
                //DynamicObjectCollection dynamicObjectCollection = extendedDataEntity.DataEntity["FBillHead"] as DynamicObjectCollection;
                extendedDataEntity["F_WDS_salecontract"] = value;
                extendedDataEntity["F_WDS_salecontract_Id"] = this._srcID;
            }
        }


        public override void OnGetSourceData(GetSourceDataEventArgs e)
        {
            base.OnGetSourceData(e);
            DynamicObject dynamicObject = e.SourceData.FirstOrDefault<DynamicObject>();
            if (dynamicObject != null)
            {
                this._srcID = Convert.ToInt64(dynamicObject["FID"]);
            }
        }


        protected DynamicFormModelProxy GetModelProxy(CreateLinkEventArgs e)
        {
            DynamicFormModelProxy dynamicFormModelProxy = new DynamicFormModelProxy();
            FormServiceProvider formServiceProvider = new FormServiceProvider();
            formServiceProvider.Add(typeof(IDefaultValueCalculator), new DefaultValueCalculator());
            dynamicFormModelProxy.SetContext(base.Context, e.TargetBusinessInfo, formServiceProvider);
            dynamicFormModelProxy.BeginIniti();
            return dynamicFormModelProxy;
        }


        protected DynamicObject GetControlPolocy(int fid)
        {
            QueryBuilderParemeter queryBuilderParemeter = new QueryBuilderParemeter();
            queryBuilderParemeter.FormId = "SHU_WDS_CONTRACT";
            queryBuilderParemeter.SelectItems = SelectorItemInfo.CreateItems("FID");
            queryBuilderParemeter.FilterClauseWihtKey = string.Format(" FID ='{0}' ", fid);
            IQueryService service = ServiceHelper.GetService<IQueryService>();
            queryBuilderParemeter.DefaultJoinOption = QueryBuilderParemeter.JoinOption.InnerJoin;
            DynamicObjectCollection rs = service.GetDynamicObjectCollection(base.Context, queryBuilderParemeter, null);
            DynamicObject obj = null;
            if (rs != null && rs.Count() > 0) {
                obj = rs[0];
            }
            return obj;
        }
    }
}
