using System;
using System.Collections.Generic;
using System.Globalization;

namespace Aquarium.UnityAdapter
{
    [Serializable]
    public sealed class LocalizationEnvelope { public string language; public LocalizationEntry[] entries; }
    [Serializable]
    public sealed class LocalizationEntry { public string key, value; }
    [Serializable]
    public sealed class LocalizationManifest
    {
        public int schema_version;
        public string default_language, fallback_language;
        public LocaleMetadata[] locales;
    }
    [Serializable]
    public sealed class LocaleMetadata
    {
        public string code, native_name, english_name, culture, steam_language, direction, font_group, status, fallback;
        public bool release_required;
        public LocaleMetadata Clone() { return (LocaleMetadata)MemberwiseClone(); }
    }
    public interface IMessageFormatter { string Format(string template, CultureInfo culture, object[] arguments); }
    // Scalar .NET composite formatting only. Plural/gender grammars and RTL shaping are not implemented.
    public sealed class CompositeMessageFormatter : IMessageFormatter
    {
        public string Format(string template, CultureInfo culture, object[] arguments)
        { return arguments.Length == 0 ? template : string.Format(culture, template, arguments); }
    }

    public sealed class DemoLocalization
    {
        private Dictionary<string, LocaleMetadata> registry;
        private Dictionary<string, Dictionary<string, string>> catalogs;
        private Dictionary<string, CultureInfo> cultures;
        private List<string> localeOrder;
        private string language = "en";
        private bool qaEnabled;
        private readonly IMessageFormatter formatter;
        private static readonly string[] DemoKeys = { "demo.title", "demo.clock", "demo.controls.events", "demo.controls.camera",
            "demo.status.ready", "demo.status.saved", "demo.status.loaded", "demo.status.save_failed", "demo.status.load_failed",
            "demo.state.paused", "demo.state.running", "demo.light.on", "demo.light.off" };

        public string Language { get { return language; } }
        public CultureInfo Culture { get { return cultures[language]; } }
        // direction/font_group describe requirements; they do not promise a verified renderer/font.
        public LocaleMetadata CurrentLocale { get { return registry[language].Clone(); } }
        public IReadOnlyList<LocaleMetadata> LoadedLocales { get { return GetLocales(false, false); } }
        public IReadOnlyList<LocaleMetadata> SupportedLocales { get { return GetLocales(true, false); } }
        public IReadOnlyList<LocaleMetadata> SelectableLocales { get { return GetLocales(false, true); } }

        public DemoLocalization(IMessageFormatter messageFormatter = null)
        {
            formatter = messageFormatter ?? new CompositeMessageFormatter();
            registry = new Dictionary<string, LocaleMetadata>(StringComparer.OrdinalIgnoreCase) {
                { "en", new LocaleMetadata { code = "en", native_name = "English", english_name = "English", culture = "en-US",
                    steam_language = "english", direction = "ltr", font_group = "latin", status = "ready", fallback = "", release_required = true } }
            };
            catalogs = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase) { { "en", EmergencyEnglish() } };
            cultures = new Dictionary<string, CultureInfo>(StringComparer.OrdinalIgnoreCase) { { "en", CultureInfo.GetCultureInfo("en-US") } };
            localeOrder = new List<string> { "en" };
        }

        // Registry, coverage, format contracts and fallback graphs commit together or not at all.
        public void Configure(LocalizationManifest manifest, IEnumerable<LocalizationEnvelope> envelopes, bool enableQa = false)
        {
            if (manifest == null || manifest.schema_version != 1 || manifest.locales == null || envelopes == null ||
                !string.Equals(manifest.default_language, "en", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(manifest.fallback_language, "en", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Manifest schema and English default/fallback are required.");
            Dictionary<string, LocaleMetadata> nextRegistry = new Dictionary<string, LocaleMetadata>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, CultureInfo> nextCultures = new Dictionary<string, CultureInfo>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> steamLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> nextOrder = new List<string>();
            foreach (LocaleMetadata entry in manifest.locales)
            {
                if (entry == null) throw new ArgumentException("Missing locale metadata.");
                LocaleMetadata locale = entry.Clone(); locale.code = NormalizeCode(locale.code);
                if (string.IsNullOrEmpty(locale.code) || nextRegistry.ContainsKey(locale.code) ||
                    string.IsNullOrEmpty(locale.native_name) || string.IsNullOrEmpty(locale.english_name) ||
                    string.IsNullOrEmpty(locale.culture) || string.IsNullOrEmpty(locale.font_group) ||
                    (locale.direction != "ltr" && locale.direction != "rtl") ||
                    (locale.status != "ready" && locale.status != "draft" && locale.status != "planned" && locale.status != "qa") ||
                    (locale.status == "planned" && locale.release_required))
                    throw new ArgumentException("Invalid or duplicate locale metadata.");
                locale.fallback = NormalizeCode(locale.fallback);
                locale.steam_language = (locale.steam_language ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(entry.fallback) && string.IsNullOrEmpty(locale.fallback))
                    throw new ArgumentException("Invalid fallback language code.");
                if (!string.IsNullOrWhiteSpace(locale.steam_language) && !steamLanguages.Add(locale.steam_language.Trim()))
                    throw new ArgumentException("Nonempty Steam language mappings must be unique.");
                nextRegistry.Add(locale.code, locale);
                nextCultures.Add(locale.code, CultureInfo.GetCultureInfo(locale.culture));
                nextOrder.Add(locale.code);
            }
            if (!nextRegistry.ContainsKey("en") || nextRegistry["en"].status != "ready" || !string.IsNullOrEmpty(nextRegistry["en"].fallback))
                throw new ArgumentException("English must be a ready terminal fallback locale.");
            foreach (LocaleMetadata locale in nextRegistry.Values)
            {
                HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string cursor = locale.code, terminal = null;
                while (!string.IsNullOrEmpty(cursor))
                {
                    if (!nextRegistry.ContainsKey(cursor) || !visited.Add(cursor)) throw new ArgumentException("Unknown or cyclic locale fallback.");
                    terminal = cursor;
                    cursor = nextRegistry[cursor].fallback;
                }
                if (!string.Equals(terminal, "en", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Every locale fallback chain must terminate at English.");
            }
            Dictionary<string, Dictionary<string, string>> nextCatalogs = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (LocalizationEnvelope envelope in envelopes)
            {
                if (envelope == null) throw new ArgumentException("Missing locale catalog.");
                string code = NormalizeCode(envelope.language); LocaleMetadata locale;
                if (!nextRegistry.TryGetValue(code, out locale)) throw new ArgumentException("Unregistered catalog locale.");
                if (locale.status == "planned" || (locale.status == "qa" && !enableQa)) continue;
                if (nextCatalogs.ContainsKey(code)) throw new ArgumentException("Duplicate catalog locale.");
                nextCatalogs.Add(code, ReadCatalog(envelope));
            }
            Dictionary<string, string> english;
            if (!nextCatalogs.TryGetValue("en", out english)) throw new ArgumentException("The English catalog is required.");
            ValidateDemoContract(english, true, nextCultures["en"]);
            Dictionary<string, Dictionary<string, int>> englishTokens = new Dictionary<string, Dictionary<string, int>>();
            foreach (KeyValuePair<string, string> message in english) englishTokens.Add(message.Key, PlaceholderCounts(message.Value));
            foreach (LocaleMetadata locale in nextRegistry.Values)
            {
                if (locale.status == "planned" || (locale.status == "qa" && !enableQa)) continue;
                Dictionary<string, string> catalog;
                bool complete = locale.status == "ready" || locale.release_required;
                if (!nextCatalogs.TryGetValue(locale.code, out catalog))
                { if (complete) throw new ArgumentException("Missing required catalog: " + locale.code); else continue; }
                if (complete)
                    foreach (string key in english.Keys)
                        if (!catalog.ContainsKey(key) || string.IsNullOrEmpty(catalog[key]))
                            throw new ArgumentException("Incomplete ready/release catalog: " + locale.code + ":" + key);
                if (!string.Equals(locale.code, "en", StringComparison.OrdinalIgnoreCase))
                    foreach (KeyValuePair<string, string> message in catalog)
                    {
                        Dictionary<string, int> expected;
                        if (!englishTokens.TryGetValue(message.Key, out expected))
                            throw new ArgumentException("Unknown translated key: " + message.Key);
                        Dictionary<string, int> actual = PlaceholderCounts(message.Value);
                        if (actual.Count != expected.Count) throw new ArgumentException("Message parameter mismatch: " + message.Key);
                        foreach (KeyValuePair<string, int> token in expected)
                        {
                            int count;
                            if (!actual.TryGetValue(token.Key, out count) || count != token.Value)
                                throw new ArgumentException("Message parameter mismatch: " + message.Key);
                        }
                    }
                ValidateDemoContract(catalog, complete, nextCultures[locale.code]);
            }
            registry = nextRegistry; catalogs = nextCatalogs; cultures = nextCultures; localeOrder = nextOrder; qaEnabled = enableQa;
            SetLanguage(language);
        }

        public string SetLanguage(string code, bool allowParentFallback = true)
        { language = FindSelectable(code, allowParentFallback, false) ?? registry["en"].code; return language; }
        // Draft selection is a separate, explicit development operation, not a player setting.
        public string SetDevelopmentLanguage(string code, bool allowParentFallback = true)
        { language = FindSelectable(code, allowParentFallback, true) ?? registry["en"].code; return language; }
        private string FindSelectable(string requested, bool allowParentFallback, bool allowDraft)
        {
            string code = NormalizeCode(requested);
            while (!string.IsNullOrEmpty(code))
            {
                LocaleMetadata locale;
                if (registry.TryGetValue(code, out locale) && catalogs.ContainsKey(code) &&
                    (locale.status == "ready" || (qaEnabled && locale.status == "qa") || (allowDraft && locale.status == "draft"))) return locale.code;
                if (!allowParentFallback) break;
                int hyphen = code.LastIndexOf('-'); code = hyphen < 0 ? "" : code.Substring(0, hyphen);
            }
            return null;
        }
        public string ResolveStartupLanguage(string savedPlayerCode, string steamGameLanguage, string systemCulture)
        {
            string choice = FindSelectable(savedPlayerCode, false, false);
            if (choice == null && !string.IsNullOrWhiteSpace(steamGameLanguage))
                foreach (string code in localeOrder)
                    if (string.Equals(registry[code].steam_language, steamGameLanguage.Trim(), StringComparison.OrdinalIgnoreCase) &&
                        FindSelectable(code, false, false) != null) { choice = code; break; }
            if (choice == null) choice = FindSelectable(systemCulture, true, false);
            return SetLanguage(choice ?? "en");
        }
        public string CycleLanguage()
        {
            IReadOnlyList<LocaleMetadata> options = SelectableLocales; int current = -1;
            for (int i = 0; i < options.Count; i++) if (options[i].code == language) current = i;
            return SetLanguage(options[(current + 1) % options.Count].code);
        }
        public string CaptureLanguagePreference() { return language; }
        public string RestoreLanguagePreference(string storedCode) { return SetLanguage(storedCode); }
        public string Text(string key, params object[] arguments)
        { return formatter.Format(Resolve(key), Culture, arguments ?? new object[0]); }
        public string FormatNumber(double value, string format = "N2") { return value.ToString(format, Culture); }
        public string FormatDate(DateTime value, string format = "d") { return value.ToString(format, Culture); }

        private string Resolve(string key)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("A localization key is required.");
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase); string cursor = language;
            while (!string.IsNullOrEmpty(cursor) && visited.Add(cursor))
            {
                Dictionary<string, string> catalog; string value;
                if (catalogs.TryGetValue(cursor, out catalog) && catalog.TryGetValue(key, out value) && !string.IsNullOrEmpty(value)) return value;
                cursor = registry[cursor].fallback;
            }
            string fallback;
            if (catalogs["en"].TryGetValue(key, out fallback) && !string.IsNullOrEmpty(fallback)) return fallback;
            throw new KeyNotFoundException("Unknown localization key: " + key);
        }
        private IReadOnlyList<LocaleMetadata> GetLocales(bool readyOnly, bool selectableOnly)
        {
            List<LocaleMetadata> result = new List<LocaleMetadata>();
            foreach (string code in localeOrder)
            {
                LocaleMetadata locale = registry[code];
                if (!catalogs.ContainsKey(code) || (readyOnly && locale.status != "ready") ||
                    (selectableOnly && locale.status != "ready" && !(qaEnabled && locale.status == "qa"))) continue;
                result.Add(locale.Clone());
            }
            return result.AsReadOnly();
        }
        private static string NormalizeCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return "";
            code = code.Trim().Replace('_', '-');
            foreach (char c in code) if (!char.IsLetterOrDigit(c) && c != '-') return "";
            return code;
        }
        private static Dictionary<string, string> ReadCatalog(LocalizationEnvelope envelope)
        {
            if (envelope.entries == null) throw new ArgumentException("Missing localization entries.");
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (LocalizationEntry entry in envelope.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key) || string.IsNullOrWhiteSpace(entry.value) || values.ContainsKey(entry.key))
                    throw new ArgumentException("Entries must have unique nonempty keys and nonblank values; drafts may omit keys.");
                values.Add(entry.key, entry.value);
            }
            return values;
        }
        private static Dictionary<string, int> PlaceholderCounts(string template)
        {
            Dictionary<string, int> tokens = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < template.Length; i++)
            {
                char c = template[i];
                if (c == '}' && i + 1 < template.Length && template[i + 1] == '}') { i++; continue; }
                if (c == '}') throw new ArgumentException("Unescaped closing message brace.");
                if (c != '{') continue;
                if (i + 1 < template.Length && template[i + 1] == '{') { i++; continue; }
                int end = template.IndexOf('}', i + 1);
                if (end < 0) throw new ArgumentException("Unclosed message parameter.");
                string content = template.Substring(i + 1, end - i - 1);
                if (content.IndexOf('{') >= 0) throw new ArgumentException("Nested message grammar is not implemented.");
                int delimiter = content.IndexOfAny(new[] { ':', ',' });
                string token = (delimiter < 0 ? content : content.Substring(0, delimiter)).Trim();
                if (token.Length == 0) throw new ArgumentException("Empty message parameter.");
                bool numeric = true;
                foreach (char ch in token) if (ch < '0' || ch > '9') numeric = false;
                if (numeric) { token = token.TrimStart('0'); if (token.Length == 0) token = "0"; }
                else
                {
                    if (!char.IsLetter(token[0]) && token[0] != '_') throw new ArgumentException("Invalid named message parameter.");
                    foreach (char ch in token) if (!char.IsLetterOrDigit(ch) && ch != '_') throw new ArgumentException("Invalid named message parameter.");
                }
                int count; tokens.TryGetValue(token, out count); tokens[token] = count + 1; i = end;
            }
            return tokens;
        }
        private static void ValidateDemoContract(Dictionary<string, string> catalog, bool complete, CultureInfo culture)
        {
            foreach (string key in DemoKeys)
            {
                string value;
                if (!catalog.TryGetValue(key, out value) || string.IsNullOrEmpty(value))
                { if (complete) throw new ArgumentException("Missing demo key: " + key); else continue; }
                int count = key == "demo.clock" ? 5 : key == "demo.status.saved" ? 1 : 0;
                object[] arguments = key == "demo.clock" ? new object[] { 1.2, .3, 4, "on", "running" } :
                    key == "demo.status.saved" ? new object[] { "path" } : new object[0];
                try
                {
                    string.Format(culture, value, arguments);
                    Dictionary<string, int> slots = PlaceholderCounts(value);
                    if (slots.Count != count) throw new FormatException("Parameter count changed.");
                    for (int i = 0; i < count; i++)
                    {
                        int occurrences;
                        if (!slots.TryGetValue(i.ToString(CultureInfo.InvariantCulture), out occurrences) || occurrences != 1)
                            throw new FormatException("Parameter index or count changed.");
                    }
                }
                catch (FormatException error) { throw new ArgumentException("Invalid scalar message contract: " + key, error); }
            }
        }
        private static Dictionary<string, string> EmergencyEnglish()
        {
            return new Dictionary<string, string> {
                { "demo.title", "Aquarium interaction prototype (placeholder art)" },
                { "demo.clock", "Real: {0:F1}s  Ecology day: {1:F3} ({2}x)  Light: {3}  {4}" },
                { "demo.controls.events", "L light | G approach glass | F food | Space pause | 1 / 2 / 4 ecology" },
                { "demo.controls.camera", "Right mouse orbit | Wheel approach | F5 save | F9 load | T language" },
                { "demo.status.ready", "Approach the glass, feed, or switch the light to observe individual reactions." },
                { "demo.status.saved", "Saved: {0}" },
                { "demo.status.loaded", "Loaded saved agents and their independent random states." },
                { "demo.status.save_failed", "Save failed. See the developer console for details." },
                { "demo.status.load_failed", "Load failed; the current tank was retained. See the developer console." },
                { "demo.state.paused", "PAUSED" }, { "demo.state.running", "RUNNING" },
                { "demo.light.on", "ON" }, { "demo.light.off", "OFF" }
            };
        }
    }
}
