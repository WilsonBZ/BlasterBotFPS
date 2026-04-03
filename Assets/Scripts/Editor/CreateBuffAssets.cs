using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot editor utility that creates all missing buff effect and buff data assets
/// for the Homing Sting and Molotov Explosion buffs, then adds them to BuffLibrary.
/// Run via Tools > Create New Buff Assets.
/// </summary>
public static class CreateBuffAssets
{
    private const string EffectsDir  = "Assets/Scripts/BuffSystem/Effects";
    private const string DataDir     = "Assets/Scripts/BuffSystem/Data";
    private const string LibraryPath = "Assets/Scripts/BuffSystem/Data/BuffLibrary.asset";

    [MenuItem("Tools/Create New Buff Assets")]
    public static void Create()
    {
        CreateEffect<HomingStingEffect>(
            $"{EffectsDir}/HomingStingEffect.asset",
            fx => { fx.turnSpeed = 180f; fx.searchRadius = 30f; });

        CreateEffect<MolotovExplosionEffect>(
            $"{EffectsDir}/MolotovExplosionEffect.asset",
            fx => { /* molotovZonePrefab wired up separately */ });

        CreateData(
            $"{DataDir}/HomingStingBuff.asset",
            "Homing Sting",
            "StingGun projectiles home in on the nearest enemy.",
            40,
            AssetDatabase.LoadAssetAtPath<BuffEffect>($"{EffectsDir}/HomingStingEffect.asset"));

        CreateData(
            $"{DataDir}/MolotovExplosionBuff.asset",
            "Molotov Explosion",
            "GazGun explosions leave a lingering fire zone on the floor.",
            35,
            AssetDatabase.LoadAssetAtPath<BuffEffect>($"{EffectsDir}/MolotovExplosionEffect.asset"));

        // Register both buffs in the BuffLibrary if it exists.
        BuffLibrary library = AssetDatabase.LoadAssetAtPath<BuffLibrary>(LibraryPath);
        if (library != null)
        {
            RegisterInLibrary(library, $"{DataDir}/HomingStingBuff.asset");
            RegisterInLibrary(library, $"{DataDir}/MolotovExplosionBuff.asset");
            EditorUtility.SetDirty(library);
        }
        else
        {
            Debug.LogWarning("[CreateBuffAssets] BuffLibrary not found — manually add HomingStingBuff and MolotovExplosionBuff to it.");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CreateBuffAssets] Done. Remember to assign MolotovZone prefab to MolotovExplosionEffect.molotovZonePrefab.");
    }

    private static void CreateEffect<T>(string path, System.Action<T> configure) where T : ScriptableObject
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) { configure(existing); EditorUtility.SetDirty(existing); return; }

        T asset = ScriptableObject.CreateInstance<T>();
        configure(asset);
        AssetDatabase.CreateAsset(asset, path);
    }

    private static void CreateData(string path, string buffName, string description, int weight, BuffEffect effect)
    {
        BuffData existing = AssetDatabase.LoadAssetAtPath<BuffData>(path);
        if (existing == null)
        {
            existing = ScriptableObject.CreateInstance<BuffData>();
            AssetDatabase.CreateAsset(existing, path);
        }

        existing.buffName    = buffName;
        existing.description = description;
        existing.weight      = weight;
        existing.effect      = effect;
        EditorUtility.SetDirty(existing);
    }

    private static void RegisterInLibrary(BuffLibrary library, string dataPath)
    {
        BuffData data = AssetDatabase.LoadAssetAtPath<BuffData>(dataPath);
        if (data == null) return;

        SerializedObject so   = new SerializedObject(library);
        SerializedProperty sp = so.FindProperty("buffs");
        if (sp == null) return;

        for (int i = 0; i < sp.arraySize; i++)
            if (sp.GetArrayElementAtIndex(i).objectReferenceValue == data) return;

        sp.arraySize++;
        sp.GetArrayElementAtIndex(sp.arraySize - 1).objectReferenceValue = data;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
