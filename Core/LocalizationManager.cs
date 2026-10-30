using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace WindowsSplitter.Core
{
    public static class LocalizationManager
    {
        public const string LangEnglish = "en";
        public const string LangVietnamese = "vi";

        private static string _currentLanguage = LangEnglish;
        public static string CurrentLanguage => _currentLanguage;

        public static event Action? LanguageChanged;

        private static ResourceDictionary? _currentLanguageDictionary;

        public static void Initialize(string? savedLanguage)
        {
            string targetLang = NormalizeLanguage(savedLanguage);
            SetLanguage(targetLang, false);
        }

        public static string NormalizeLanguage(string? lang)
        {
            if (string.Equals(lang, LangVietnamese, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(lang, "vi-VN", StringComparison.OrdinalIgnoreCase))
            {
                return LangVietnamese;
            }
            return LangEnglish; // English is default
        }

        public static void SetLanguage(string lang, bool notify = true)
        {
            _currentLanguage = NormalizeLanguage(lang);

            string dictUriString = _currentLanguage == LangVietnamese
                ? "pack://application:,,,/Resources/Languages/Strings.vi.xaml"
                : "pack://application:,,,/Resources/Languages/Strings.en.xaml";

            try
            {
                var newDict = new ResourceDictionary { Source = new Uri(dictUriString, UriKind.Absolute) };

                // Locate existing language dictionary in Application resources if present
                var appDictionaries = Application.Current.Resources.MergedDictionaries;
                var oldDict = appDictionaries.FirstOrDefault(d => 
                    d.Source != null && (d.Source.OriginalString.Contains("Strings.en.xaml") || d.Source.OriginalString.Contains("Strings.vi.xaml")));

                if (oldDict != null)
                {
                    int index = appDictionaries.IndexOf(oldDict);
                    appDictionaries[index] = newDict;
                }
                else
                {
                    appDictionaries.Add(newDict);
                }

                _currentLanguageDictionary = newDict;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load language dictionary '{dictUriString}': {ex.Message}");
            }

            if (notify)
            {
                LanguageChanged?.Invoke();
            }
        }

        public static string GetString(string key, params object[] args)
        {
            object? val = null;
            if (_currentLanguageDictionary != null && _currentLanguageDictionary.Contains(key))
            {
                val = _currentLanguageDictionary[key];
            }
            else if (Application.Current?.Resources?.Contains(key) == true)
            {
                val = Application.Current.Resources[key];
            }

            string result = val?.ToString() ?? key;
            if (args != null && args.Length > 0)
            {
                try
                {
                    return string.Format(result, args);
                }
                catch
                {
                    return result;
                }
            }
            return result;
        }
    }
}
