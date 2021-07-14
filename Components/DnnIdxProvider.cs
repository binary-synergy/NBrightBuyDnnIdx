using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Services.Search.Entities;
using DotNetNuke.Services.Search.Internals;
using NBrightCore.common;
using NBrightDNN;
using Nevoweb.DNN.NBrightBuy.Components;


namespace Nevoweb.DNN.NBrightBuy.Providers
{
    /// <summary>
    /// Implement DNN search Index
    /// </summary>
    public class DnnIdxProvider : Components.Interfaces.SchedulerInterface
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="portalfinfo"></param>
        /// <returns></returns>
        public override string DoWork(int portalId)
        {

            try
            {
                var objCtrl = new NBrightBuyController();

                // check if we have NBS in this portal by looking for default settings.
                var nbssetting = objCtrl.GetByGuidKey(portalId, -1, "SETTINGS", "NBrightBuySettings");
                if (nbssetting != null)
                {
                    var storeSettings = new StoreSettings(portalId);
                    var pluginData = new PluginData(portalId); // get plugin data to see if this scheduler is active on this portal 
                    var plugin = pluginData.GetPluginByCtrl("dnnsearchindex");

                    // MJR: This xml path is different in my installation, in mine the active flag is under the interfaces section
                    if (plugin != null && plugin.GetXmlPropertyBool("genxml/interfaces/genxml/checkbox/active"))
                    {
                        // The NBS scheduler is normally set to run hourly, therefore if we only want a process to run daily we need the logic this function.
                        // To to this we keep a last run flag on the sceduler settings
                        var setting = objCtrl.GetByGuidKey(portalId, -1, "DNNIDXSCHEDULER", "DNNIDXSCHEDULER");
                        if (setting == null)
                        {
                            setting = new NBrightInfo(true);
                            setting.ItemID = -1;
                            setting.PortalId = portalId;
                            setting.TypeCode = "DNNIDXSCHEDULER";
                            setting.GUIDKey = "DNNIDXSCHEDULER";
                            setting.ModuleId = -1;
                            setting.XMLData = "<genxml></genxml>";
                        }
                        
                        var lastrun = setting.GetXmlPropertyRaw("genxml/lastrun");
                        var lastrundate = DateTime.Now.AddYears(-99);
                        if (Utils.IsDate(lastrun)) lastrundate = Convert.ToDateTime(lastrun);

                        // MJR: Added to run under DNN 9.9.1 
                        PortalController pc = new PortalController();
                        PortalInfo objPortal = pc.GetPortal(portalId);

                        var rtnmsg = DoProductIdx(objPortal, lastrundate, storeSettings.DebugMode);
                        setting.SetXmlProperty("genxml/lastrun", DateTime.Now.ToString("s"), TypeCode.DateTime);
                        objCtrl.Update(setting);
                        if (rtnmsg != "") return rtnmsg;

                    }
                }

                return " - NBS-DNNIDX scheduler OK ";
            }
            catch (Exception ex)
            {
                return " - NBS-DNNIDX scheduler FAIL: " + ex.ToString() + " : ";
            }

        }

        private String DoProductIdx(DotNetNuke.Entities.Portals.PortalInfo portal, DateTime lastrun, Boolean debug)
        {
            if (debug)
            {
                InternalSearchController.Instance.DeleteAllDocuments(portal.PortalID, SearchHelper.Instance.GetSearchTypeByName("tab").SearchTypeId);
            }
            var searchDocs = new List<SearchDocument>();
            var culturecodeList = DnnUtils.GetCultureCodeList(portal.PortalID);
            var storeSettings = new StoreSettings(portal.PortalID);
            int indexedProductCount = 0;

            foreach (var lang in culturecodeList)
            {
                var strContent = "";

                // ********** SELECT ALL PRODUCTS ********** 
                var objCtrl = new NBrightBuyController();
                var strFilter = " and NB1.ModifiedDate > convert(datetime,'" + lastrun.ToString("s") + "') ";
                if (debug) strFilter = "";
                var l = objCtrl.GetList(portal.PortalID, -1, "PRD", strFilter);

                foreach (var p in l)
                {
                    var prodData = new ProductData(p.ItemID, portal.PortalID, lang);

                    // MJR: There was no SeoTags property so removed and got them from lang record instead
                    strContent = prodData.Info.GetXmlProperty("genxml/textbox/txtproductref") + " : " + prodData.SEODescription + " " + prodData.SEOName + " " + prodData.SEOTitle;

                    // MJR: this will always be true due to the way the string is created above (example... " :  "), rewrite to an if of the above and create this description string if above not ""
                    if (strContent != "") 
                    {
                        // MJR: There could be rubbish imported products in the db (i had some) don't index them
                        if (string.IsNullOrEmpty(prodData.ProductRef))
                            continue;

                        // index only products not disabled
                        if (prodData.Disabled)
                            continue;

                        // index only products not hidden - cant find this property, keep looking around....
                        // HERE

                        // a default metakeyword for a product - in case there are no metakeywords entered
                        var tags = new List<String>();
                        tags.Add("nbsproduct");
                            
                        // get entered metakeywords in store for me they are comma separated
                        var metakeywords = prodData.DataLangRecord.GetXmlProperty("genxml/textbox/txttagwords");
                        if (metakeywords != "")
                        {
                            var tagwords = metakeywords.Split(',');
                            foreach (string tag in tagwords)
                            {
                                tags.Add(HtmlUtils.Clean(tag, true));
                            }
                        }

                        // Get the description string
                        string strDescription = HtmlUtils.Shorten(HtmlUtils.Clean(strContent, false), 100, "...");
                        var searchDoc = new SearchDocument();

                        // Assigns a Search key the SearchItems
                        searchDoc.UniqueKey = prodData.Info.ItemID.ToString("");

                        // i need the catid for my version of sabratel openurlrewriter to work 
                        var prodCategories = objCtrl.GetList(portal.PortalID, -1, "CATXREF", " and NB1.[ParentItemId] = " + prodData.Info.ItemID);
                        if (prodCategories.Count == 0)
                            continue;
                        int catid = prodCategories.First().XrefItemId;

                        // MJR: when url rewriting the /products?ref= path style doesn't work
                        //      replace by catid=x&eid=y which gets rewritten to the full friendly url
                        //      OLD: searchDoc.QueryString = "ref=" + prodData.Info.GetXmlProperty("genxml/textbox/txtproductref");
                        searchDoc.QueryString = "catid=" + catid.ToString() + "&eid=" + prodData.Info.ItemID.ToString();

                        // remaining search index props
                        searchDoc.Title = prodData.ProductName;
                        searchDoc.Body = strContent;
                        searchDoc.Description = strDescription;
                        searchDoc.ModifiedTimeUtc = debug ? DateTime.Now.Date : prodData.Info.ModifiedDate;
                        searchDoc.AuthorUserId = 1;
                        searchDoc.TabId = storeSettings.ProductDetailTabId;
                        searchDoc.PortalId = portal.PortalID;
                        searchDoc.SearchTypeId = SearchHelper.Instance.GetSearchTypeByName("tab").SearchTypeId;
                        searchDoc.CultureCode = lang;
                        searchDoc.Tags = tags;
                        searchDoc.ModuleDefId = 0;
                        searchDoc.ModuleId = 0;

                        searchDocs.Add(searchDoc);
                        indexedProductCount += 1;
                    }
                }
            }

            // Index
            InternalSearchController.Instance.AddSearchDocuments(searchDocs);
            InternalSearchController.Instance.Commit();

            if (indexedProductCount > 0)
            {
                // its helpful to know if it actually did anything....
                return string.Format(" - NBS-DNNIDX scheduler indexed {0} items", indexedProductCount);
            }
            else
            {
                return " - NBS-DNNIDX scheduler ACTIVATED ";
            }


        }



    }
}
