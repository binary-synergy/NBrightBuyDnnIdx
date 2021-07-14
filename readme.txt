NBrightBuyDnnIdx Plugin
-----------------------

This DNN module is a plugin for the NBSv3.  It enables products to be indexed into the standard DNN search database.

It requires a minimum version of DNN7.1 and NBSv3.1.

Installation
------------

- Install the NBrightBuyDnnIdx_*.*.*_Install.zip as a normal DNN module, through the DNN extension.
- Go into the NBS Back Office>Admin>Plugins. Edit and activate the "Index DNN Search" plugin listed.
- Make sure the "Index DNN Search" plugin has a "Provider Type" of scheduler.
- Ensure the NBS scheuler is running on the DNN scheudler. http://nbsdocs.nbrightproject.org/Documentation/Integratorguide/Setup.aspx

Operation
---------

The NBrightBuyDnnIdx plugin indexes products into the DNNsearch index by using the DNN scheudler.  The NBS scheduler module must be running on the DNN scheduler for the plugin to work.
If the NBS store debug setting is turned on, the plugin will clear down ALL page indexes for the portal (Including all tabs loaded by other modules) and reindex only products.

The DNN 7.1 index does tend to be very quick and in normal operation only changed products will be updated in the indexed, to avoid performace issues.

Due to performace and restrictions with the the DNN search provider API the index process does not remove deleted products from the DNN index (unless in debug mode).
To remove deleted product from the index you can either place the store in debug mode and run the NBS scheudler manually (or wait for the reindex) or use the DNN>Admin>Search-Admin option to reindex the search. 

NOTE: The NBS module should not be left in debug mode during high peak hours.

Search Type:  The NBrightBuyDnnIdx uses a tab searchtype.
Although each product is not a tab in DNN, the resulting detail is regarded as a tab with a uinque URL.
For this reason we use SearchHelper.Instance.GetSearchTypeByName("tab").SearchTypeId
