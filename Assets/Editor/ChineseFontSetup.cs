using TMPro;
using UnityEditor;
using UnityEngine;

public static class ChineseFontSetup
{
    private const string FontPath = "Assets/Resources/Fonts/NotoSansSC-Dynamic.asset";

    [InitializeOnLoadMethod]
    private static void ScheduleSetup()
    {
        EditorApplication.delayCall += Configure;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += Configure;
    }

    [MenuItem("Tools/UI/Configure Chinese Font")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(
            "Assets/TextMesh Pro/Resources/TMP Settings.asset");
        var source = AssetDatabase.LoadAssetAtPath<Font>(
            "Assets/Resources/Fonts/NotoSansSC-Variable.ttf");
        if (settings == null || source == null)
        {
            Debug.LogError("Chinese font setup requires TMP Settings and NotoSansSC-Variable.ttf.");
            return;
        }

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        bool changed = false;
        if (font == null)
        {
            font = TMP_FontAsset.CreateFontAsset(source);
            if (font == null)
            {
                Debug.LogError("Cannot create the Chinese TMP font asset.");
                return;
            }
            font.name = "NotoSansSC-Dynamic";
            font.isMultiAtlasTexturesEnabled = true;
            AssetDatabase.CreateAsset(font, FontPath);
            foreach (Texture2D atlas in font.atlasTextures)
                AssetDatabase.AddObjectToAsset(atlas, font);
            AssetDatabase.AddObjectToAsset(font.material, font);
            changed = true;
        }

        // Persist the fallback so scene previews and players use the same font.
        var serialized = new SerializedObject(settings);
        SerializedProperty fallbacks = serialized.FindProperty("m_fallbackFontAssets");
        bool registered = false;
        for (int i = 0; i < fallbacks.arraySize; i++)
            registered |= fallbacks.GetArrayElementAtIndex(i).objectReferenceValue == font;
        if (!registered)
        {
            int index = fallbacks.arraySize;
            fallbacks.InsertArrayElementAtIndex(index);
            fallbacks.GetArrayElementAtIndex(index).objectReferenceValue = font;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            changed = true;
        }

        if (!changed)
            return;

        const string sample = "\u7ee7\u7eed\u6e38\u620f\u8bbe\u7f6e\u961f\u4f0d\u8c03\u6574\u8fd4\u56de\u6807\u9898\u4fdd\u5b58\u8bfb\u53d6\u6863";
        if (!font.TryAddCharacters(sample, out string missing))
            Debug.LogError("Chinese font is missing required menu characters: " + missing);
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
            if (!EditorUtility.IsPersistent(text))
                text.SetAllDirty();
        Debug.Log("Chinese TMP fallback configured for edit mode and play mode.");
    }
}
