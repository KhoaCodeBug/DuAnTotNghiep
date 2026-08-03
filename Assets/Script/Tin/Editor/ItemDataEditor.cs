using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemData))]
[CanEditMultipleObjects]
public class ItemDataEditor : Editor
{
    private SerializedProperty itemName;
    private SerializedProperty icon;
    private SerializedProperty category;
    private SerializedProperty useTime;
    private SerializedProperty specificDropPrefab;
    private SerializedProperty isStackable;
    private SerializedProperty maxStack;

    // Medical
    private SerializedProperty healAmount;

    // Consumable
    private SerializedProperty hungerRestore;
    private SerializedProperty thirstRestore;
    private SerializedProperty buffDuration;
    private SerializedProperty speedMultiplier;
    private SerializedProperty maxStaminaBoost;

    // Weapon
    private SerializedProperty weaponDamage;
    private SerializedProperty fireRate;
    private SerializedProperty magazineCapacity;
    private SerializedProperty pelletCount;
    private SerializedProperty spreadAngle;
    private SerializedProperty weaponRange;
    private SerializedProperty shootNoiseRadius;
    private SerializedProperty soundVolumeMultiplier;
    private SerializedProperty ammoTypeRequired;

    // Audio
    private SerializedProperty customSingleShootSFX;
    private SerializedProperty customAutoShootSFX;
    private SerializedProperty customReloadSFX;
    private SerializedProperty customDryFireSFX;

    private void OnEnable()
    {
        itemName = serializedObject.FindProperty("itemName");
        icon = serializedObject.FindProperty("icon");
        category = serializedObject.FindProperty("category");
        useTime = serializedObject.FindProperty("useTime");
        specificDropPrefab = serializedObject.FindProperty("specificDropPrefab");
        isStackable = serializedObject.FindProperty("isStackable");
        maxStack = serializedObject.FindProperty("maxStack");

        healAmount = serializedObject.FindProperty("healAmount");

        hungerRestore = serializedObject.FindProperty("hungerRestore");
        thirstRestore = serializedObject.FindProperty("thirstRestore");
        buffDuration = serializedObject.FindProperty("buffDuration");
        speedMultiplier = serializedObject.FindProperty("speedMultiplier");
        maxStaminaBoost = serializedObject.FindProperty("maxStaminaBoost");

        weaponDamage = serializedObject.FindProperty("weaponDamage");
        fireRate = serializedObject.FindProperty("fireRate");
        magazineCapacity = serializedObject.FindProperty("magazineCapacity");
        pelletCount = serializedObject.FindProperty("pelletCount");
        spreadAngle = serializedObject.FindProperty("spreadAngle");
        weaponRange = serializedObject.FindProperty("weaponRange");
        shootNoiseRadius = serializedObject.FindProperty("shootNoiseRadius");
        soundVolumeMultiplier = serializedObject.FindProperty("soundVolumeMultiplier");
        ammoTypeRequired = serializedObject.FindProperty("ammoTypeRequired");

        customSingleShootSFX = serializedObject.FindProperty("customSingleShootSFX");
        customAutoShootSFX = serializedObject.FindProperty("customAutoShootSFX");
        customReloadSFX = serializedObject.FindProperty("customReloadSFX");
        customDryFireSFX = serializedObject.FindProperty("customDryFireSFX");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        ItemCategory selectedCategory = (ItemCategory)category.enumValueIndex;

        EditorGUILayout.LabelField("--- Cài Đặt Chung ---", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(itemName);
        EditorGUILayout.PropertyField(icon);
        EditorGUILayout.PropertyField(category);

        // Vũ khí (Weapon) không sử dụng useTime và không cộng dồn (Stack)
        if (selectedCategory != ItemCategory.Weapon)
        {
            EditorGUILayout.PropertyField(useTime);
        }

        EditorGUILayout.PropertyField(specificDropPrefab);

        if (selectedCategory != ItemCategory.Weapon)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("--- Cài Đặt Cộng Dồn ---", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(isStackable);
            if (isStackable.boolValue)
            {
                EditorGUILayout.PropertyField(maxStack);
            }
        }
        else
        {
            isStackable.boolValue = false;
            maxStack.intValue = 1;
        }

        if (selectedCategory == ItemCategory.Medical)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("--- Chỉ Số Y Tế ---", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(healAmount);
        }
        else if (selectedCategory == ItemCategory.Consumable)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("--- Dinh Dưỡng & Buff ---", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(hungerRestore);
            EditorGUILayout.PropertyField(thirstRestore);
            EditorGUILayout.PropertyField(buffDuration);
            EditorGUILayout.PropertyField(speedMultiplier);
            EditorGUILayout.PropertyField(maxStaminaBoost);
        }
        else if (selectedCategory == ItemCategory.Weapon)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("--- Chỉ Số Vũ Khí (Bắn Súng) ---", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(weaponDamage);
            EditorGUILayout.PropertyField(fireRate);
            EditorGUILayout.PropertyField(magazineCapacity);
            EditorGUILayout.PropertyField(pelletCount);
            EditorGUILayout.PropertyField(spreadAngle);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("--- Tầm Bắn & Tiếng Nổ ---", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(weaponRange);
            EditorGUILayout.PropertyField(shootNoiseRadius);
            EditorGUILayout.PropertyField(soundVolumeMultiplier);
            EditorGUILayout.PropertyField(ammoTypeRequired);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("--- m Thanh Riêng Của Vũ Khí ---", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(customSingleShootSFX);
            EditorGUILayout.PropertyField(customAutoShootSFX);
            EditorGUILayout.PropertyField(customReloadSFX);
            EditorGUILayout.PropertyField(customDryFireSFX);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
