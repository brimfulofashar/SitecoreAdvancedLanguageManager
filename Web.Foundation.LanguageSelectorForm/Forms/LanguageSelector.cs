using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sitecore;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.DataProviders;
using Sitecore.Data.Items;
using Sitecore.Data.Managers;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Shell.Applications.ContentManager.Galleries;
using Sitecore.Web;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Sheer;
using Sitecore.Web.UI.XmlControls;
using Version = Sitecore.Data.Version;

namespace Web.Foundation.LanguageSelectorForm.Forms
{
    public class LanguageSelector : GalleryForm
    {
        /// <summary>Raises the load event.</summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> instance containing the event data.</param>
        /// <remarks>
        ///     This method notifies the server control that it should perform actions common to each HTTP
        ///     request for the page it is associated with, such as setting up a database query. At this
        ///     stage in the page lifecycle, server controls in the hierarchy are created and initialized,
        ///     view state is restored, and form controls reflect client-side data. Use the IsPostBack
        ///     property to determine whether the page is being loaded in response to a client postback,
        ///     or if it is being loaded and accessed for the first time.
        /// </remarks>
        private readonly Dictionary<string, List<CountryLanguage>> _languageCountryList =
            new Dictionary<string, List<CountryLanguage>>();

        /// <summary></summary>
        protected Scrollbox Languages;

        /// <summary></summary>
        protected GalleryMenu Options;

        /// <summary>Handles the message.</summary>
        /// <param name="message">The message.</param>
        public override void HandleMessage(Message message)
        {
            Assert.ArgumentNotNull(message, nameof(message));
            if (message.Name == "event:click")
                return;
            Invoke(message, true);
        }

        protected override void OnLoad(EventArgs e)
        {
            Assert.ArgumentNotNull(e, nameof(e));
            base.OnLoad(e);
            if (Context.ClientPage.IsEvent)
                return;
            var currentItem = GetCurrentItem();
            if (currentItem == null)
                return;

            var database = Database.GetDatabase("master");
            using (new ThreadCultureSwitcher(Context.Language.CultureInfo))
            {
                var masterLanguages = database.GetItem("/sitecore/system/Languages").Children;

                foreach (Item masterLanguage in masterLanguages)
                {
                    var isoCode = masterLanguage.Fields["Regional Iso Code"].Value;
                    if (!string.IsNullOrEmpty(isoCode) || masterLanguage.Language != null)
                    {
                        var isValid = LanguageManager.IsValidLanguageName(isoCode);
                        var isRegistered = LanguageManager.LanguageRegistered(isoCode);
                        if (!isRegistered)
                        {
                            var registered = LanguageManager.RegisterLanguage(isoCode);
                        }

                        var ciString = string.Empty;
                        var riString = string.Empty;
                        if (string.IsNullOrEmpty(isoCode))
                        {
                            ciString = masterLanguage.Language.CultureInfo.TwoLetterISOLanguageName;
                            riString = string.Empty;
                        }
                        else
                        {
                            ciString = isoCode.Substring(0, 2);
                            riString = isoCode.Substring(3, 2);
                        }

                        var ci = new CultureInfo(ciString);
                        // Populate the new CultureAndRegionInfoBuilder object with region information.
                        var ri = !string.IsNullOrEmpty(riString) ? new RegionInfo(riString) : null;
                        var language = Language.Parse(isoCode);

                        var dataSource = currentItem.Database.DataManager.DataSource;
                        var itemInformation = dataSource.GetItemInformation(currentItem.ID);

                        currentItem = GetCurrentItem();
                        var customLocaleVersionItem = database.GetItem(currentItem.ID, language);

                        database.Caches.DataCache.RemoveItemInformation(currentItem.ID);

                        CallContext CreateCallContext()
                        {
                            return new CallContext(currentItem.Database.DataManager,
                                currentItem.Database.GetDataProviders().Length);
                        }

                        var versionUri = new VersionUri(language, Version.Latest);

                        currentItem.Database.GetDataProviders().First().RemoveVersion(itemInformation.ItemDefinition,
                            versionUri, CreateCallContext());

                        currentItem.Database.GetDataProviders().First()
                            .GetItemVersions(itemInformation.ItemDefinition, CreateCallContext());

                        if (!_languageCountryList.ContainsKey(ci.EnglishName))
                            _languageCountryList.Add(ci.EnglishName, new List<CountryLanguage>());
                        _languageCountryList[ci.EnglishName].Add(new CountryLanguage(currentItem,
                            customLocaleVersionItem, ri != null ? ri.EnglishName : ci.EnglishName, ci.EnglishName,
                            string.IsNullOrEmpty(language.Name) ? ci.TwoLetterISOLanguageName : language.Name));
                    }
                }

                foreach (var key in _languageCountryList.Keys.OrderBy(x => x))
                {
                    var countryLanguages = _languageCountryList[key];
                    var addLanguageHeader = true;
                    foreach (var countryLanguage in countryLanguages.OrderBy(x => x.Country))
                    {
                        AddLocaleToControl(countryLanguage.CurrentItem, countryLanguage.LocalisedItem,
                            countryLanguage.Language, countryLanguage.Country, countryLanguage.ClickString,
                            addLanguageHeader);
                        addLanguageHeader = false;
                    }
                }
            }

            var obj = Client.CoreDatabase.GetItem("/sitecore/content/Applications/Content Editor/Menues/Languages");
            if (obj == null)
                return;
            Options.AddFromDataSource(obj, string.Empty);
        }

        private void AddLocaleToControl(Item currentItem, Item obj2, string languageName, string countryName,
            string clickString, bool addLanguageHeader)
        {
            var control = ControlFactory.GetControl("Gallery.Languages.Option") as XmlControl;
            Assert.IsNotNull(control, typeof(XmlControl));
            Context.ClientPage.AddControl(Languages, control);
            string str1;
            if (obj2 != null)
            {
                var length = obj2.Versions.GetVersionNumbers(true).Length;
                if (obj2.IsFallback)
                {
                    str1 = Translate.Text("Fallback version");
                }
                else
                {
                    string str2;
                    if (length != 1)
                        str2 = Translate.Text("{0} versions.", length.ToString());
                    else
                        str2 = Translate.Text("1 version.");
                    str1 = str2;
                }
            }
            else
            {
                str1 = Translate.Text("0 versions.");
            }

            if (languageName.Contains("-") && countryName.StartsWith("Unknown"))
            {
                var ciString = languageName.Substring(0, 2);
                var riString = languageName.Substring(3, 2);
                var ci = new CultureInfo(ciString);
                // Populate the new CultureAndRegionInfoBuilder object with region information.
                var ri = new RegionInfo(riString);
                languageName = ci.EnglishName;
                countryName = ri.EnglishName;
            }

            control["LanguageHeader"] = addLanguageHeader ? languageName : string.Empty;
            control["Header"] = countryName;
            control["Description"] = str1;
            control["Click"] = string.Format("item:load(id={0},language={1},version=0)", currentItem.ID, clickString);
            control["ClassName"] =
                !languageName.Equals(WebUtil.GetQueryString("la"), StringComparison.OrdinalIgnoreCase)
                    ? "scMenuPanelItem"
                    : (object)"scMenuPanelItemSelected";
        }

        /// <summary>Gets the current item.</summary>
        /// <returns>The current item.</returns>
        private static Item GetCurrentItem()
        {
            var queryString1 = WebUtil.GetQueryString("db");
            var queryString2 = WebUtil.GetQueryString("id");
            var language = Language.Parse(WebUtil.GetQueryString("la"));
            var version = Version.Parse(WebUtil.GetQueryString("vs"));
            var database = Factory.GetDatabase(queryString1);
            Assert.IsNotNull(database, queryString1);
            var item = database.GetItem(queryString2, language, version);
            return item;
        }
    }

    public class CountryLanguage
    {
        public CountryLanguage(Item currentItem, Item localisedItem, string country, string language,
            string clickString)
        {
            CurrentItem = currentItem;
            LocalisedItem = localisedItem;
            Country = country;
            Language = language;
            ClickString = clickString;
        }

        public Item CurrentItem { get; }
        public Item LocalisedItem { get; }
        public string Country { get; }
        public string Language { get; }
        public string ClickString { get; }
    }
}