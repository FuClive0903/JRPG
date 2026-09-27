using System.Collections.Generic;
using TMPro;
using UnityEngine;

public static class ChineseFontFallback
{
    private static TMP_FontAsset fontAsset;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Setup()
    {
        if (fontAsset == null)
        {
            fontAsset = Resources.Load<TMP_FontAsset>("Fonts/NotoSansSC-Dynamic");
            if (fontAsset == null)
            {
                Debug.LogError("Missing Chinese TMP font asset. Run Tools/UI/Configure Chinese Font in the editor.");
                return;
            }
        }

        List<TMP_FontAsset> fallbacks = TMP_Settings.fallbackFontAssets ??
            new List<TMP_FontAsset>();
        if (!fallbacks.Contains(fontAsset))
        {
            fallbacks.Add(fontAsset);
            TMP_Settings.fallbackFontAssets = fallbacks;
        }
    }
}
