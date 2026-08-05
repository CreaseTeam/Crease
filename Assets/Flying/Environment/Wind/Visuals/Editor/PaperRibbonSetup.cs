using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;

namespace Crease.Flying.Environment.Wind.Visuals.EditorTools
{
    /// <summary>
    /// Creates the drop-in prefab and the isolated test scene for the paper ribbon
    /// effect, and offers a single command that runs the whole pipeline in order.
    ///
    /// Both are built by Unity rather than hand authored, so the serialised output is
    /// always valid for this editor version.
    /// </summary>
    public static class PaperRibbonSetup
    {
        private const string VisualsFolder = "Assets/Flying/Environment/Wind/Visuals";
        private const string PrefabPath = VisualsFolder + "/PaperRibbonWind.prefab";
        private const string VfxPath = VisualsFolder + "/PaperRibbonAmbient.vfx";
        private const string ScenePath = "Assets/Scenes/Test Scenes/PaperWindVFX.unity";

        private const string PaperMaterialPath = "Assets/Folding/PaperGraph/Graphics/FoldPaper.mat";
        private const string SkyPrefabPath = "Assets/Flying/Environment/Sky/Sky.prefab";
        private const string PlayerPrefabPath = "Assets/Flying/Player/Player.prefab";
        private const string CameraPrefabPath = "Assets/Flying/Player/Camera/Main Camera.prefab";

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

                // The spawn volume is moved every frame and is much larger than the
                // default bounds, so let it recompute rather than be culled mid flight.
                vfx.cullingFlags = VFXCullingFlags.CullNone;

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

            // Sun at a shallow angle so the ribbons are lit from the side and the
            // translucency in the output shading actually has something to read against.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(40f, -35f, 0f);
            created.Add(lightGo);

            // Ribbons only read as paper against paper, so the greybox is paper too.
            Material paper = AssetDatabase.LoadAssetAtPath<Material>(PaperMaterialPath);
            if (paper == null)
            {
                Debug.LogWarning("Could not find " + PaperMaterialPath + ", test geometry will use the default material.");
            }

            created.Add(CreateSlab("Ground", new Vector3(0f, 0f, 0f), Vector3.zero, new Vector3(12f, 1f, 12f), paper));
            created.Add(CreateSlab("Wall Left", new Vector3(-40f, 12f, 20f), new Vector3(0f, 0f, 90f), new Vector3(3f, 1f, 6f), paper));
            created.Add(CreateSlab("Wall Back", new Vector3(15f, 10f, 55f), new Vector3(90f, 0f, 0f), new Vector3(5f, 1f, 3f), paper));
            created.Add(CreateSlab("Ledge", new Vector3(30f, 6f, -20f), Vector3.zero, new Vector3(2f, 1f, 2f), paper));

            Instantiate(SkyPrefabPath, "Sky", created);

            // The real player, because the loop trigger keys off genuine turning and a
            // canned camera path would not exercise it honestly.
            GameObject player = Instantiate(PlayerPrefabPath, "Player", created);
            if (player != null)
            {
                player.transform.position = new Vector3(0f, 25f, -30f);
            }

            // Player.prefab carries no camera, so the camera comes in separately and has
            // to be pointed at the player by hand.
            GameObject cameraGo = Instantiate(CameraPrefabPath, "Main Camera", created);
            if (cameraGo != null && player != null)
            {
                cameraGo.transform.position = player.transform.position + new Vector3(0f, 3f, -10f);

                var controller = cameraGo.GetComponent<Player.Camera.CameraController>();
                if (controller != null)
                {
                    controller.Target = player.transform;
                    controller.FlightController = player.GetComponent<Player.FlightController>();
                }
            }

            if (ribbonPrefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(ribbonPrefab);
                instance.transform.position = new Vector3(0f, 25f, 0f);
                created.Add(instance);
            }

            if (Camera.main == null)
            {
                Debug.LogWarning(
                    "No camera tagged MainCamera in the scene. PaperRibbonWindVfx falls back to " +
                    "its own transform, so assign Follow Target by hand.");
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
