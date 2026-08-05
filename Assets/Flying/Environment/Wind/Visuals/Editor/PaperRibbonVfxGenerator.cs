using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Crease.Flying.Environment.Wind.Visuals.EditorTools
{
    /// <summary>
    /// Builds PaperRibbonAmbient.vfx. The VFX authoring model is internal, so this
    /// drives it by reflected name. Failures abort before anything is written.
    /// </summary>
    public static class PaperRibbonVfxGenerator
    {
        private const string VisualsFolder = "Assets/Flying/Environment/Wind/Visuals";
        private const string MeshFolder = VisualsFolder + "/Meshes";
        private const string AssetPath = VisualsFolder + "/PaperRibbonAmbient.vfx";
        private const string HlslPath = VisualsFolder + "/PaperRibbonForces.hlsl";
        private const string HlslFunction = "PaperRibbonFlow";

        private const int Capacity = 64;

        #region Names
        // Reconcile against Temp/VfxApiDump.txt from the Dump VFX Model API menu item.

        private const string EditorAssembly = "Unity.VisualEffectGraph.Editor";

        // Contexts
        private const string TypeSpawner = "UnityEditor.VFX.VFXBasicSpawner";
        private const string TypeInitialize = "UnityEditor.VFX.VFXBasicInitialize";
        private const string TypeUpdate = "UnityEditor.VFX.VFXBasicUpdate";

        // First that resolves wins.
        private static readonly string[] TypeMeshOutputCandidates =
        {
            "UnityEditor.VFX.VFXComposedParticleOutput",
            "UnityEditor.VFX.VFXLitMeshOutput",
            "UnityEditor.VFX.VFXMeshOutput"
        };

        // Blocks
        private const string TypeConstantRate = "UnityEditor.VFX.Block.VFXSpawnerConstantRate";
        private const string TypePositionAABox = "UnityEditor.VFX.Block.PositionAABox";
        private const string TypeSetAttribute = "UnityEditor.VFX.Block.SetAttribute";
        private const string TypeAttributeFromCurve = "UnityEditor.VFX.Block.AttributeFromCurve";
        private const string TypeTurbulence = "UnityEditor.VFX.Block.Turbulence";
        private const string TypeCustomHlsl = "UnityEditor.VFX.Block.CustomHLSL";

        // Parameter node
        private const string TypeParameter = "UnityEditor.VFX.VFXParameter";

        // Core model API
        private const string TypeVisualEffectResource = "UnityEditor.VFX.VisualEffectResource";
        private const string TypeAssetEditorUtility = "UnityEditor.VFX.VisualEffectAssetEditorUtility";

        // Common [VFXSetting] field names
        private const string SettingAttribute = "attribute";
        private const string SettingRandom = "Random";
        private const string SettingCapacity = "capacity";
        private const string SettingShaderFile = "m_ShaderFile";
        private const string SettingFunctionName = "m_FunctionName";
        #endregion

        // Exposed blackboard properties. Names must match PaperRibbonWindVfx exactly.
        private static readonly (string Name, Type Type, object Value)[] Parameters =
        {
            ("SpawnRate", typeof(float), 6f),
            ("SpawnCenter", typeof(Vector3), Vector3.zero),
            ("SpawnBoxSize", typeof(Vector3), new Vector3(45f, 24f, 45f)),
            ("FlowVelocity", typeof(Vector3), Vector3.zero),
            ("FlowStrength", typeof(float), 0.8f),
            ("TurbulenceIntensity", typeof(float), 1.2f),
            ("LoopAxis", typeof(Vector3), Vector3.up),
            ("LoopOmega", typeof(float), 1.6f),
            ("LoopIntensity", typeof(float), 0f),
            ("LoopFraction", typeof(float), 0.15f),
            ("SizeScale", typeof(float), 1f)
        };

        // Constant stepped so the tints stay distinct instead of averaging to a wash.
        private static readonly Color[] Palette =
        {
            new Color(0.733f, 0.851f, 0.933f), // pale blue
            new Color(0.965f, 0.957f, 0.933f), // off white
            new Color(0.725f, 0.890f, 0.816f), // mint
            new Color(0.949f, 0.776f, 0.808f), // soft pink
            new Color(0.961f, 0.902f, 0.722f)  // pale cream
        };

        private static readonly List<string> Failures = new List<string>();
        private static Assembly _asm;

        // Seeding copies a package template before the build runs, so an abort has to
        // clean that up or it strands an unrelated template at the target path.
        private static bool _createdAssetThisRun;

        [MenuItem("Tools/Crease/Paper Ribbons/Generate Ambient Ribbon VFX", false, 22)]
        public static void Generate()
        {
            Failures.Clear();
            _createdAssetThisRun = false;

            _asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == EditorAssembly);

            if (_asm == null)
            {
                Debug.LogError(
                    "Visual Effect Graph package is not installed or failed to compile.\n" +
                    "Assembly '" + EditorAssembly + "' is not loaded. Add " +
                    "com.unity.visualeffectgraph 17.3.0 and let Unity resolve it first.");
                return;
            }

            Mesh[] meshes = LoadMeshes();
            if (meshes == null) return;

            object graph = OpenGraph();
            if (graph == null)
            {
                Abort();
                return;
            }

            try
            {
                Build(graph, meshes);
            }
            catch (Exception e)
            {
                Failures.Add("exception while building: " + e.GetType().Name + " " + e.Message);
                Debug.LogException(e);
            }

            if (Failures.Count > 0)
            {
                Abort();
                return;
            }

            Save(graph);
        }

        private static Mesh[] LoadMeshes()
        {
            string[] names = { "RibbonCrescent", "RibbonArc", "RibbonCurl" };
            var meshes = new Mesh[names.Length];

            for (int i = 0; i < names.Length; i++)
            {
                string path = MeshFolder + "/" + names[i] + ".asset";
                meshes[i] = AssetDatabase.LoadAssetAtPath<Mesh>(path);

                if (meshes[i] == null)
                {
                    Debug.LogError(
                        "Missing ribbon mesh at " + path + ".\n" +
                        "Run Tools/Crease/Paper Ribbons/Generate Ribbon Meshes first.");
                    return null;
                }
            }

            return meshes;
        }

        #region Asset and graph lifecycle

        /// <summary>
        /// Always regenerates from scratch, so the path holds either a correct graph or
        /// nothing. Any hand edits to the asset are lost, which is what "Generate" means.
        /// </summary>
        private static object OpenGraph()
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetPath) != null)
            {
                AssetDatabase.DeleteAsset(AssetPath);
            }

            if (!CreateEmptyAsset()) return null;
            _createdAssetThisRun = true;

            Type resourceType = Resolve(TypeVisualEffectResource);
            if (resourceType == null) return null;

            object resource = InvokeStatic(resourceType, "GetResourceAtPath", AssetPath);
            if (resource == null)
            {
                Failures.Add("VisualEffectResource.GetResourceAtPath returned null for " + AssetPath);
                return null;
            }

            object graph = Invoke(resource, "GetOrCreateGraph");
            if (graph == null)
            {
                Failures.Add("VisualEffectResource.GetOrCreateGraph returned null");
                return null;
            }

            // The seed is a stock template, so strip its contexts before rebuilding.
            ClearGraph(graph);
            return graph;
        }

        private static bool CreateEmptyAsset()
        {
            Type utility = _asm.GetType(TypeAssetEditorUtility);
            if (utility != null)
            {
                MethodInfo create = utility
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "CreateNewAsset" &&
                                         m.GetParameters().Length == 1 &&
                                         m.GetParameters()[0].ParameterType == typeof(string));

                if (create != null)
                {
                    try
                    {
                        create.Invoke(null, new object[] { AssetPath });
                        AssetDatabase.ImportAsset(AssetPath);
                        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetPath) != null) return true;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("CreateNewAsset failed, falling back to a template copy: " + e.Message);
                    }
                }
            }

            // Fallback: copy a stock template shipped with the package.
            string template = FindTemplate();
            if (template == null)
            {
                Failures.Add("could not create an empty .vfx: no CreateNewAsset and no template found in the package");
                return false;
            }

            if (!AssetDatabase.CopyAsset(template, AssetPath))
            {
                Failures.Add("failed to copy template " + template + " to " + AssetPath);
                return false;
            }

            AssetDatabase.ImportAsset(AssetPath);
            return true;
        }

        private static string FindTemplate()
        {
            try
            {
                string root = Path.GetFullPath("Packages/com.unity.visualeffectgraph");
                if (!Directory.Exists(root)) return null;

                string[] files = Directory.GetFiles(root, "*.vfx", SearchOption.AllDirectories);
                if (files.Length == 0) return null;

                string pick = files.FirstOrDefault(f => f.IndexOf("Simple", StringComparison.OrdinalIgnoreCase) >= 0)
                              ?? files[0];

                return "Packages/com.unity.visualeffectgraph" + pick.Substring(root.Length).Replace('\\', '/');
            }
            catch
            {
                return null;
            }
        }

        private static void ClearGraph(object graph)
        {
            try
            {
                int count = (int)GetMember(graph, "GetNbChildren", true);
                for (int i = count - 1; i >= 0; i--)
                {
                    object child = Invoke(graph, "GetChild", i);
                    if (child != null) Invoke(graph, "RemoveChild", child);
                }
            }
            catch (Exception e)
            {
                Failures.Add("could not clear the existing graph: " + e.Message);
            }
        }

        private static void Save(object graph)
        {
            try
            {
                Invoke(graph, "SetExpressionGraphDirty");
                Invoke(graph, "RecompileIfNeeded");

                object resource = GetMember(graph, "visualEffectResource", false)
                                  ?? InvokeStatic(Resolve(TypeVisualEffectResource), "GetResourceAtPath", AssetPath);

                if (resource != null) Invoke(resource, "WriteAsset");

                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(AssetPath);

                Debug.Log(
                    "Generated " + AssetPath + ".\n" +
                    "Open it to confirm four connected contexts and " + Parameters.Length +
                    " blackboard properties, then drop PaperRibbonWind.prefab into a scene.");

                Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetPath);
            }
            catch (Exception e)
            {
                Debug.LogError("Graph was built but writing it failed: " + e);
            }
        }

        #endregion

        #region Graph construction

        private static void Build(object graph, Mesh[] meshes)
        {
            Dictionary<string, object> parameters = CreateParameters(graph);

            object spawner = AddContext(graph, TypeSpawner);
            object initialize = AddContext(graph, TypeInitialize);
            object update = AddContext(graph, TypeUpdate);
            object output = AddOutputContext(graph);

            if (spawner == null || initialize == null || update == null || output == null) return;

            BuildSpawner(spawner, parameters);
            BuildInitialize(initialize, parameters);
            BuildUpdate(update, parameters);
            BuildOutput(output, parameters, meshes);

            LinkContext(initialize, spawner);
            LinkContext(update, initialize);
            LinkContext(output, update);
        }

        private static void BuildSpawner(object spawner, Dictionary<string, object> parameters)
        {
            object rate = AddBlock(spawner, TypeConstantRate);
            LinkParameter(parameters, "SpawnRate", rate, 0);
        }

        private static void BuildInitialize(object initialize, Dictionary<string, object> parameters)
        {
            SetSetting(initialize, SettingCapacity, (uint)Capacity);

            // The spawn volume moves with the player every frame, so recorded bounds
            // would cull the effect mid flight. Optional: the exact setting name is not
            // API, and getting it wrong only costs culling, not the effect.
            TrySetSetting(initialize, "boundsMode", "Manual");
            TrySetSetting(initialize, "boundsSettingMode", "Manual");

            object box = AddBlock(initialize, TypePositionAABox);
            LinkParameter(parameters, "SpawnCenter", box, FindSlot(box, "center", "Center", "position"));
            LinkParameter(parameters, "SpawnBoxSize", box, FindSlot(box, "size", "Size"));

            SetRandomAttribute(initialize, "lifetime", 4f, 8f);
            SetRandomAttribute(initialize, "size", 0.25f, 0.60f);
            SetRandomAttribute(initialize, "alpha", 0.72f, 0.92f);

            SetRandomAttribute(initialize, "velocity", new Vector3(-1.2f, -0.6f, -1.2f), new Vector3(1.2f, 0.6f, 1.2f));

            // Stock angle attributes, so the output needs no orient block.
            SetRandomAttribute(initialize, "angleX", 0f, 360f);
            SetRandomAttribute(initialize, "angleY", 0f, 360f);
            SetRandomAttribute(initialize, "angleZ", 0f, 360f);
            SetRandomAttribute(initialize, "angularVelocityX", -60f, 60f);
            SetRandomAttribute(initialize, "angularVelocityY", -60f, 60f);
            SetRandomAttribute(initialize, "angularVelocityZ", -60f, 60f);

            // 2.99 so the top of the range does not round up into a missing fourth mesh.
            SetRandomAttribute(initialize, "meshIndex", 0f, 2.99f);

            AddColourFromGradient(initialize);
        }

        private static void BuildUpdate(object update, Dictionary<string, object> parameters)
        {
            object hlsl = AddBlock(update, TypeCustomHlsl);
            if (hlsl != null)
            {
                var include = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(HlslPath);
                if (include == null)
                {
                    Failures.Add("missing HLSL include at " + HlslPath);
                }
                else
                {
                    SetSetting(hlsl, SettingShaderFile, include);
                    SetSetting(hlsl, SettingFunctionName, HlslFunction);
                }

                // Slot order follows the PaperRibbonFlow signature.
                LinkParameter(parameters, "FlowVelocity", hlsl, 0);
                LinkParameter(parameters, "FlowStrength", hlsl, 1);
                LinkParameter(parameters, "LoopAxis", hlsl, 2);
                LinkParameter(parameters, "LoopOmega", hlsl, 3);
                LinkParameter(parameters, "LoopIntensity", hlsl, 4);
                LinkParameter(parameters, "LoopFraction", hlsl, 5);
            }

            object turbulence = AddBlock(update, TypeTurbulence);
            if (turbulence != null)
            {
                LinkParameter(parameters, "TurbulenceIntensity", turbulence, FindSlot(turbulence, "intensity", "Intensity"));
                SetSlot(turbulence, FindSlot(turbulence, "frequency", "Frequency"), 0.12f);
                SetSlot(turbulence, FindSlot(turbulence, "octaves", "Octaves"), 2);
                SetSlot(turbulence, FindSlot(turbulence, "drag", "Drag"), 0.6f);
            }

            AddAlphaOverLife(update);
        }

        private static void BuildOutput(object output, Dictionary<string, object> parameters, Mesh[] meshes)
        {
            // First: the extra mesh slots only appear once this is raised.
            SetSetting(output, "meshCount", (uint)meshes.Length);

            for (int i = 0; i < meshes.Length; i++)
            {
                int slot = FindSlot(output, i == 0 ? "mesh" : "mesh" + (i + 1), "mesh" + (i + 1));
                SetSlot(output, slot, meshes[i]);
            }

            SetSetting(output, "blendMode", "Alpha");
            SetSetting(output, "cullMode", "Off");
            SetSetting(output, "zWriteMode", "Off");
            SetSetting(output, "useSoftParticle", false);

            object sizeScale = AddBlock(output, TypeSetAttribute);
            if (sizeScale != null)
            {
                SetSetting(sizeScale, SettingAttribute, "size");
                SetSetting(sizeScale, "Composition", "Multiply");
                LinkParameter(parameters, "SizeScale", sizeScale, 0);
            }
        }

        #endregion

        #region Block helpers

        private static void SetRandomAttribute(object context, string attribute, object min, object max)
        {
            object block = AddBlock(context, TypeSetAttribute);
            if (block == null) return;

            SetSetting(block, SettingAttribute, attribute);
            SetSetting(block, SettingRandom, "PerComponent");

            SetSlot(block, 0, min);
            SetSlot(block, 1, max);
        }

        private static void AddColourFromGradient(object context)
        {
            object block = AddBlock(context, TypeAttributeFromCurve);
            if (block == null) return;

            SetSetting(block, SettingAttribute, "color");
            SetSetting(block, "SampleMode", "Random");

            var keys = new GradientColorKey[Palette.Length];
            var alphas = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };

            for (int i = 0; i < Palette.Length; i++)
            {
                keys[i] = new GradientColorKey(Palette[i], (float)i / (Palette.Length - 1));
            }

            var gradient = new Gradient { mode = GradientMode.Fixed };
            gradient.SetKeys(keys, alphas);

            SetSlot(block, FindSlot(block, "Gradient", "gradient", "Sample"), gradient);
        }

        private static void AddAlphaOverLife(object context)
        {
            object block = AddBlock(context, TypeAttributeFromCurve);
            if (block == null) return;

            SetSetting(block, SettingAttribute, "alpha");
            SetSetting(block, "SampleMode", "OverLife");
            SetSetting(block, "Composition", "Multiply");

            var curve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.12f, 1f),
                new Keyframe(0.85f, 1f),
                new Keyframe(1f, 0f));

            SetSlot(block, FindSlot(block, "Curve", "curve", "Sample"), curve);
        }

        #endregion

        #region Reflection plumbing

        private static Type Resolve(string typeName)
        {
            Type t = _asm.GetType(typeName);
            if (t != null) return t;

            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = a.GetType(typeName);
                if (t != null) return t;
            }

            Failures.Add("type not found: " + typeName);
            return null;
        }

        private static object AddContext(object graph, string typeName)
        {
            Type t = Resolve(typeName);
            if (t == null) return null;

            var model = ScriptableObject.CreateInstance(t);
            if (model == null)
            {
                Failures.Add("could not instantiate " + typeName);
                return null;
            }

            Invoke(graph, "AddChild", model, -1, false);
            return model;
        }

        private static object AddOutputContext(object graph)
        {
            foreach (string candidate in TypeMeshOutputCandidates)
            {
                if (_asm.GetType(candidate) == null) continue;
                return AddContext(graph, candidate);
            }

            Failures.Add("no mesh output context found. Tried: " + string.Join(", ", TypeMeshOutputCandidates));
            return null;
        }

        private static object AddBlock(object context, string typeName)
        {
            Type t = Resolve(typeName);
            if (t == null) return null;

            var block = ScriptableObject.CreateInstance(t);
            if (block == null)
            {
                Failures.Add("could not instantiate block " + typeName);
                return null;
            }

            Invoke(context, "AddChild", block, -1, false);
            return block;
        }

        private static void LinkContext(object to, object from)
        {
            if (to == null || from == null) return;

            try
            {
                MethodInfo link = to.GetType()
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "LinkFrom" && m.GetParameters().Length >= 1);

                if (link == null)
                {
                    Failures.Add("VFXContext.LinkFrom not found");
                    return;
                }

                var args = new object[link.GetParameters().Length];
                args[0] = from;
                for (int i = 1; i < args.Length; i++) args[i] = 0;

                link.Invoke(to, args);
            }
            catch (Exception e)
            {
                Failures.Add("failed to link contexts: " + e.Message);
            }
        }

        private static Dictionary<string, object> CreateParameters(object graph)
        {
            var map = new Dictionary<string, object>();
            Type t = Resolve(TypeParameter);
            if (t == null) return map;

            foreach (var (name, type, value) in Parameters)
            {
                var param = ScriptableObject.CreateInstance(t);
                if (param == null)
                {
                    Failures.Add("could not instantiate VFXParameter for " + name);
                    continue;
                }

                Invoke(param, "Init", type);
                Invoke(graph, "AddChild", param, -1, false);

                SetMember(param, "exposed", true);
                SetMember(param, "exposedName", name);
                SetMember(param, "value", value);

                map[name] = param;
            }

            return map;
        }

        private static void LinkParameter(Dictionary<string, object> parameters, string name, object target, int slotIndex)
        {
            if (target == null || slotIndex < 0) return;

            if (!parameters.TryGetValue(name, out object param))
            {
                Failures.Add("no parameter named " + name);
                return;
            }

            try
            {
                object from = Invoke(param, "GetOutputSlot", 0);
                object to = Invoke(target, "GetInputSlot", slotIndex);

                if (from == null || to == null)
                {
                    Failures.Add("could not reach slots to link " + name + " (slot " + slotIndex + ")");
                    return;
                }

                Invoke(to, "Link", from, true);
            }
            catch (Exception e)
            {
                Failures.Add("failed to link " + name + ": " + e.Message);
            }
        }

        /// <summary>Returns -1 if no name matches, which callers treat as skip.</summary>
        private static int FindSlot(object container, params string[] names)
        {
            try
            {
                int count = (int)Invoke(container, "GetNbInputSlots");
                for (int i = 0; i < count; i++)
                {
                    object slot = Invoke(container, "GetInputSlot", i);
                    string slotName = GetMember(slot, "name", false) as string;
                    if (slotName == null) continue;

                    foreach (string n in names)
                    {
                        if (string.Equals(slotName, n, StringComparison.OrdinalIgnoreCase)) return i;
                    }
                }
            }
            catch
            {
                // Not found.
            }

            return -1;
        }

        private static void SetSlot(object container, int slotIndex, object value)
        {
            if (container == null || slotIndex < 0 || value == null) return;

            try
            {
                object slot = Invoke(container, "GetInputSlot", slotIndex);
                if (slot != null) SetMember(slot, "value", value);
            }
            catch (Exception e)
            {
                Failures.Add("failed to set slot " + slotIndex + ": " + e.Message);
            }
        }

        /// <summary>
        /// Sets a setting if it exists, recording nothing if it does not. For settings
        /// whose absence degrades the effect rather than breaking it.
        /// </summary>
        private static void TrySetSetting(object model, string name, object value)
        {
            if (model == null) return;

            bool exists = model.GetType()
                .GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) != null;

            if (!exists) return;

            int before = Failures.Count;
            SetSetting(model, name, value);

            // Swallow anything this optional set added.
            if (Failures.Count > before) Failures.RemoveRange(before, Failures.Count - before);
        }

        /// <summary>Enums are passed as strings, since the enum types are internal.</summary>
        private static void SetSetting(object model, string name, object value)
        {
            if (model == null) return;

            try
            {
                FieldInfo field = model.GetType()
                    .GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (field == null)
                {
                    Failures.Add("setting '" + name + "' not found on " + model.GetType().Name);
                    return;
                }

                object converted = value;

                if (field.FieldType.IsEnum && value is string s)
                {
                    if (!Enum.GetNames(field.FieldType).Contains(s))
                    {
                        Failures.Add("setting '" + name + "' has no value '" + s + "'. Valid: " +
                                     string.Join("|", Enum.GetNames(field.FieldType)));
                        return;
                    }

                    converted = Enum.Parse(field.FieldType, s);
                }
                else if (field.FieldType != value.GetType() && value is IConvertible)
                {
                    converted = Convert.ChangeType(value, field.FieldType);
                }

                MethodInfo setter = model.GetType()
                    .GetMethod("SetSettingValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (setter != null)
                {
                    setter.Invoke(model, new object[] { name, converted });
                }
                else
                {
                    field.SetValue(model, converted);
                }
            }
            catch (Exception e)
            {
                Failures.Add("failed to set setting '" + name + "': " + e.Message);
            }
        }

        private static object Invoke(object target, string method, params object[] args)
        {
            if (target == null) return null;

            MethodInfo mi = target.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == method && m.GetParameters().Length == args.Length);

            if (mi == null)
            {
                mi = target.GetType()
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == method && m.GetParameters().Length >= args.Length);

                if (mi == null)
                {
                    Failures.Add("method not found: " + target.GetType().Name + "." + method +
                                 " with " + args.Length + " args");
                    return null;
                }

                var full = new object[mi.GetParameters().Length];
                Array.Copy(args, full, args.Length);
                for (int i = args.Length; i < full.Length; i++) full[i] = mi.GetParameters()[i].DefaultValue;
                args = full;
            }

            return mi.Invoke(target, args);
        }

        private static object InvokeStatic(Type type, string method, params object[] args)
        {
            if (type == null) return null;

            MethodInfo mi = type
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == method && m.GetParameters().Length == args.Length);

            if (mi == null)
            {
                Failures.Add("static method not found: " + type.Name + "." + method);
                return null;
            }

            return mi.Invoke(null, args);
        }

        private static object GetMember(object target, string name, bool asMethod)
        {
            if (target == null) return null;

            if (asMethod) return Invoke(target, name);

            Type t = target.GetType();

            PropertyInfo p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null && p.CanRead) return p.GetValue(target, null);

            FieldInfo f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return f != null ? f.GetValue(target) : null;
        }

        private static void SetMember(object target, string name, object value)
        {
            if (target == null) return;

            Type t = target.GetType();

            PropertyInfo p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null && p.CanWrite)
            {
                p.SetValue(target, value, null);
                return;
            }

            FieldInfo f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null)
            {
                f.SetValue(target, value);
                return;
            }

            Failures.Add("member not found: " + t.Name + "." + name);
        }

        #endregion

        private static void Abort()
        {
            if (_createdAssetThisRun)
            {
                AssetDatabase.DeleteAsset(AssetPath);
                _createdAssetThisRun = false;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Paper ribbon VFX generation aborted. Nothing was written.");
            sb.AppendLine();
            sb.AppendLine("Unresolved against this package version (" + Failures.Count + "):");

            foreach (string f in Failures.Distinct())
            {
                sb.AppendLine("  " + f);
            }

            sb.AppendLine();
            sb.AppendLine("Run Tools/Crease/Paper Ribbons/Dump VFX Model API and reconcile the");
            sb.AppendLine("Names region at the top of PaperRibbonVfxGenerator.cs against");
            sb.AppendLine("Temp/VfxApiDump.txt, then run this again.");
            sb.AppendLine();
            sb.AppendLine("To finish by hand instead, create a VFX Graph at " + AssetPath);
            sb.AppendLine("and add these exposed blackboard properties. Nothing downstream");
            sb.AppendLine("depends on the graph's internals, only on these names:");

            foreach (var (name, type, value) in Parameters)
            {
                sb.AppendLine("  " + name.PadRight(22) + type.Name.PadRight(10) + "default " + value);
            }

            Debug.LogError(sb.ToString());
        }
    }
}
