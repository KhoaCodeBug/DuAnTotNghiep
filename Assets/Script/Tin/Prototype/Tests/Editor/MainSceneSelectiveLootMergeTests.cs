using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MainSceneSelectiveLootMergeTests
{
    private const string MainScenePath = "Assets/Scenes/Main.unity";
    private const string StableVipHousePath = "Assets/Khoa/House/cannhasieuvipprodachinhsua_FIXED.prefab";
    private const string RejectedVariantPath = "Assets/Khoa/House/cannhasieuvipprodachinhsua Variant.prefab";

    private static Type ResolveGameType(string typeName)
    {
        Type direct = Type.GetType(typeName);
        if (direct != null) return direct;
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(typeName))
            .FirstOrDefault(type => type != null);
    }

    [Test]
    public void MainScene_PreservesStableQuestFlow_AndAddsOnlyValidNetworkLootInstances()
    {
        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
        try
        {
            Transform[] transforms = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .ToArray();

            Type questLocationType = ResolveGameType("QuestLocationIdentity");
            Assert.That(questLocationType, Is.Not.Null, "QuestLocationIdentity type must compile.");
            Component[] locations = transforms
                .Select(transform => transform.GetComponent(questLocationType))
                .Where(location => location != null)
                .ToArray();
            PropertyInfo hasValidId = questLocationType.GetProperty("HasValidId");
            PropertyInfo locationId = questLocationType.GetProperty("LocationId");
            Assert.That(hasValidId, Is.Not.Null);
            Assert.That(locationId, Is.Not.Null);
            Assert.That(locations.All(location => (bool)hasValidId.GetValue(location)), Is.True,
                "Every quest location must keep a non-empty scene-instance ID.");

            // Seven house prefabs already carry a fallback identity in the stable baseline.
            // The 68 scene-authored identities are the authoritative per-instance IDs used
            // to distinguish repeated house prefab instances at runtime.
            Component[] sceneAuthoredLocations = locations
                .Where(location => PrefabUtility.GetCorrespondingObjectFromSource(location) == null)
                .ToArray();
            Assert.That(locations.Length, Is.EqualTo(75),
                "Selective scene merge must preserve the stable set of 75 loaded identities (68 scene-authored plus 7 prefab fallbacks).");
            Assert.That(sceneAuthoredLocations.Length, Is.EqualTo(68),
                "Selective scene merge must preserve every authoritative scene-instance quest location.");
            Assert.That(sceneAuthoredLocations
                    .Select(location => (string)locationId.GetValue(location))
                    .Distinct(StringComparer.Ordinal)
                    .Count(),
                Is.EqualTo(68), "Authoritative scene-instance quest location IDs must remain unique after the merge.");

            GameObject[] prefabRoots = transforms.Select(transform => transform.gameObject)
                .Where(PrefabUtility.IsAnyPrefabInstanceRoot)
                .ToArray();
            Assert.That(prefabRoots.Count(root =>
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == StableVipHousePath),
                Is.EqualTo(13), "All 13 stable VIP-house instances must remain present.");
            Assert.That(prefabRoots.Any(root =>
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == RejectedVariantPath),
                Is.False, "The quest-identity-breaking house Variant must not be instantiated.");

            GameObject[] allKitchenLoot = prefabRoots.Where(root =>
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root)
                        .StartsWith("Assets/Khoa/Loot/Prefab_Kitchen"))
                .ToArray();
            Assert.That(allKitchenLoot.Length, Is.EqualTo(158),
                "Main must contain the stable 82 kitchen-loot instances plus the 76 reviewed additions.");
            Assert.That(allKitchenLoot.Select(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot)
                    .Distinct().Count(),
                Is.GreaterThanOrEqualTo(21), "Main must keep at least all 21 prefab variants used by the reviewed additions.");

            Type lootContainerType = ResolveGameType("LootContainer");
            Type networkObjectType = ResolveGameType("Fusion.NetworkObject");
            Assert.That(lootContainerType, Is.Not.Null, "LootContainer type must compile.");
            Assert.That(networkObjectType, Is.Not.Null, "Fusion.NetworkObject type must be available.");
            var sortKeys = new HashSet<long>();
            var parentPositions = new HashSet<string>();
            foreach (GameObject lootObject in allKitchenLoot)
            {
                Component container = lootObject.GetComponent(lootContainerType);
                Assert.That(container, Is.Not.Null, lootObject.name + " is missing LootContainer.");
                SerializedObject serializedContainer = new SerializedObject(container);
                Assert.That(serializedContainer.FindProperty("lootTable").objectReferenceValue, Is.Not.Null,
                    lootObject.name + " is missing its loot table.");
                Assert.That(serializedContainer.FindProperty("interactDistance").floatValue, Is.GreaterThan(0f),
                    lootObject.name + " must remain interactable.");
                Assert.That(lootObject.GetComponent<Collider2D>(), Is.Not.Null,
                    lootObject.name + " is missing its interaction collider.");
                Assert.That(lootObject.GetComponent<SpriteRenderer>(), Is.Not.Null,
                    lootObject.name + " is missing its visible renderer.");

                Component networkObject = lootObject.GetComponent(networkObjectType);
                Assert.That(networkObject, Is.Not.Null, lootObject.name + " is missing NetworkObject.");
                SerializedProperty sortKey = new SerializedObject(networkObject).FindProperty("SortKey");
                Assert.That(sortKey, Is.Not.Null, lootObject.name + " has no baked Fusion SortKey.");
                Assert.That(sortKey.longValue, Is.Not.Zero, lootObject.name + " has an invalid Fusion SortKey.");
                Assert.That(sortKeys.Add(sortKey.longValue), Is.True,
                    lootObject.name + " duplicates another kitchen-loot Fusion SortKey.");

                int parentId = lootObject.transform.parent == null ? 0 : lootObject.transform.parent.GetInstanceID();
                string parentKey = parentId + ":" +
                    lootObject.transform.localPosition.ToString("F4");
                Assert.That(parentPositions.Add(parentKey), Is.True,
                    lootObject.name + " overlaps another kitchen-loot instance at the same parent/local position.");
            }

            Type mainQuestManagerType = ResolveGameType("MainQuestManager");
            Assert.That(mainQuestManagerType, Is.Not.Null, "MainQuestManager type must compile.");
            Component[] questManagers = transforms
                .Select(transform => transform.GetComponent(mainQuestManagerType))
                .Where(manager => manager != null)
                .ToArray();
            Assert.That(questManagers.Length, Is.EqualTo(1), "Main must keep exactly one MainQuestManager.");
            // This is legacy serialized data in the stable scene (the current
            // MainQuestManager no longer exposes the field). Keep it byte-level
            // so the selective merge cannot silently delete stable scene data.
            string sceneYaml = File.ReadAllText(MainScenePath);
            Assert.That(sceneYaml, Does.Contain("  hospitalRadioHearingDistance: 8"),
                "Selective integration must not delete the stable Hospital Radio hearing-distance data.");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
