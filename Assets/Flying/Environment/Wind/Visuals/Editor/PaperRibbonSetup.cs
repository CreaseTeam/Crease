using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;

namespace Crease.Flying.Environment.Wind.Visuals.EditorTools
{
    /// <summary>
    /// Creates the ribbon prefab and test scene, and the run-everything command.
    /// </summary>
    public static class PaperRibbonSetup
    {
        private const string VisualsFolder = "Assets/Flying/Environment/Wind/Visuals";
        private const string PrefabPath = VisualsFolder + "/PaperRibbonWind.prefab";
        private const string VfxPath = VisualsFolder + "/PaperRibbonAmbient.vfx";
        private const string ScenePath = "Assets/Scenes/Test Scenes/PaperWindVFX.unity";

        private const string PaperMaterialPath = "Assets/Folding/PaperGraph/Graphics/FoldPaper.mat";
        private const string LevelStarterPrefabPath = "Assets/Flying/LevelStarter.prefab";

        [MenuItem("Tools/Crease/Paper Ribbons/Set Up Everything", false, 0)]
        public static void SetUpEverything()
        {
            PaperRibbonMeshGenerator.Generate();
            PaperRibbonVfxGenerator.Generate();

            if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(VfxPath) == null)
            {
                Debug.LogWarning(
                    "Ribbon VFX asset was not produced, so the prefab will be created with an " +
                    "empty Visual Effect slot. Fix the generation errors above, then run " +
                    "Tools/Crease/Paper Ribbons/Create Prefab And Test Scene again.");
            }

            CreatePrefabAndScene();
        }

        [MenuItem("Tools/Crease/Paper Ribbons/Create Prefab And Test Scene", false, 40)]
        public static void CreatePrefabAndScene()
        {
            GameObject prefab = CreatePrefab();
            CreateTestScene(prefab);
        }

        private static GameObject CreatePrefab()
        {
            var root = new GameObject("PaperRibbonWind");

            try
            {
                var vfx = root.AddComponent<VisualEffect>();

                var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(VfxPath);
                if (asset != null)
                {
                    vfx.visualEffectAsset = asset;
                }

                // Culling and bounds are asset level settings on the Initialize context,
                // not component properties, so they are set by PaperRibbonVfxGenerator.

                root.AddComponent<PaperRibbonWindVfx>();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("Wrote " + PrefabPath);
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void CreateTestScene(GameObject ribbonPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var created = new List<GameObject>();

            // Shallow angle so ribbons are lit from the side.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(40f, -35f, 0f);
            created.Add(lightGo);

            // Ribbons only read as paper against paper.
            Material paper = AssetDatabase.LoadAssetAtPath<Material>(PaperMaterialPath);
            if (paper == null)
            {
                Debug.LogWarning("Could not find " + PaperMaterialPath + ", test geometry will use the default material.");
            }

            created.Add(CreateSlab("Ground", new Vector3(0f, 0f, 0f), Vector3.zero, new Vector3(12f, 1f, 12f), paper));
            created.Add(CreateSlab("Wall Left", new Vector3(-40f, 12f, 20f), new Vector3(0f, 0f, 90f), new Vector3(3f, 1f, 6f), paper));
            created.Add(CreateSlab("Wall Back", new Vector3(15f, 10f, 55f), new Vector3(90f, 0f, 0f), new Vector3(5f, 1f, 3f), paper));
            created.Add(CreateSlab("Ledge", new Vector3(30f, 6f, -20f), Vector3.zero, new Vector3(2f, 1f, 2f), paper));

            // The real player, since the loop trigger keys off genuine turning.
            // LevelStarter is the bootstrap the other test scenes use. It brings the
            // player, the camera and the manager singletons, including InputManager,
            // which CameraController dereferences every frame.
            GameObject starter = Instantiate(LevelStarterPrefabPath, "LevelStarter", created);
            if (starter == null)
            {
                Debug.LogError(
                    "Could not find " + LevelStarterPrefabPath + ". Without it there is no " +
                    "InputManager and the camera will throw every frame.");
            }
            else
            {
                var player = starter.GetComponentInChildren<Player.KinematicBody>();
                if (player != null)
                {
                    player.transform.position = new Vector3(0f, 25f, -30f);
                }

                // Start already flying a folded plane. This is a VFX test scene, so
                // folding one by hand every run is pure friction.
                var folding = starter.GetComponentInChildren<Crease.Folding.Paper.FoldingManager>(true);
                if (folding != null)
                {
                    folding.DefaultMode = Crease.Folding.Paper.GameMode.Flying;
                }
                else
                {
                    Debug.LogWarning("No FoldingManager under LevelStarter, so the scene will start in folding mode.");
                }
            }

            if (ribbonPrefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(ribbonPrefab);
                instance.transform.position = new Vector3(0f, 25f, 0f);
                created.Add(instance);
            }

            EnsureFolder("Assets/Scenes/Test Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log(
                "Wrote " + ScenePath + " with " + created.Count + " objects.\n" +
                "Press Play and fly. Ribbons should drift ahead of you and stream past; " +
                "a hard turn should curl a few of them into loops.");
        }

        private static GameObject CreateSlab(string name, Vector3 position, Vector3 euler, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = name;
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;

            if (material != null)
            {
                go.GetComponent<Renderer>().sharedMaterial = material;
            }

            return go;
        }

        private static GameObject Instantiate(string path, string label, List<GameObject> created)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null)
            {
                Debug.LogWarning("Could not find " + label + " prefab at " + path + ", skipping it.");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            created.Add(instance);
            return instance;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string[] parts = folder.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
