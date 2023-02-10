using Sitecore.Data.Managers;
using Sitecore.Globalization;
using Sitecore.Pipelines.HttpRequest;

namespace Web.Foundation.LanguageSelectorForm.Pipelines
{
    public class CustomCountryLanguageResolver : LanguageResolver
    {
        protected override bool TryParse(string languageName, out Language language)
        {
            var isValid = LanguageManager.IsValidLanguageName(languageName);
            var isRegistered = LanguageManager.LanguageRegistered(languageName);
            if (!isRegistered)
            {
                var registered = LanguageManager.RegisterLanguage(languageName);
            }

            return base.TryParse(languageName, out language);
        }
    }
}