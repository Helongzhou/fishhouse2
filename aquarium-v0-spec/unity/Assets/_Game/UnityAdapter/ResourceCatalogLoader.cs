using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aquarium.UnityAdapter
{
    public static class ResourceCatalogLoader
    {
        public static void LoadAvailable(DemoLocalization target, bool enableQa = false)
        {
            try
            {
                TextAsset manifestAsset = Resources.Load<TextAsset>("Localization/locales");
                if (manifestAsset == null) return; // Complete emergency English remains available.
                LocalizationManifest manifest = JsonUtility.FromJson<LocalizationManifest>(manifestAsset.text);
                if (manifest == null || manifest.locales == null) throw new ArgumentException("Missing locale registry.");
                List<LocalizationEnvelope> catalogs = new List<LocalizationEnvelope>();
                foreach (LocaleMetadata locale in manifest.locales)
                {
                    if (locale == null) throw new ArgumentException("Missing locale metadata.");
                    if (locale.status == "planned" || (locale.status == "qa" && !enableQa)) continue;
                    TextAsset asset = Resources.Load<TextAsset>("Localization/" + locale.code);
                    if (asset == null) continue; // Configure rejects absent ready/release catalogs atomically.
                    LocalizationEnvelope envelope = JsonUtility.FromJson<LocalizationEnvelope>(asset.text);
                    if (envelope == null || !string.Equals(envelope.language, locale.code, StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException("Catalog language differs from its registered filename.");
                    catalogs.Add(envelope);
                }
                target.Configure(manifest, catalogs, enableQa);
            }
            catch (Exception error)
            {
                Debug.LogWarning("Localization resources rejected; previous configuration retained: " + error.Message);
            }
        }
    }
}
