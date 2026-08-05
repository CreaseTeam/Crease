using System;
using System.Collections;
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
    /// Dumps the VFX Graph authoring model API to Temp/VfxApiDump.txt, to reconcile
    /// the name constants in PaperRibbonVfxGenerator against. Reflection only.
    /// </summary>
    public static class VfxModelProbe
    {
        private const string EditorAssemblyName = "Unity.VisualEffectGraph.Editor";
        private const string OutputFileName = "VfxApiDump.txt";

        [MenuItem("Tools/Crease/Paper Ribbons/Dump VFX Model API", false, 20)]
        public static void Dump()
        {
            var sb = new StringBuilder();
            sb.AppendLine("VFX Graph model API dump");
            sb.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Unity: " + Application.unityVersion);
            sb.AppendLine();

            Assembly asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == EditorAssemblyName);

            if (asm == null)
            {
                sb.AppendLine("FAIL: assembly '" + EditorAssemblyName + "' is not loaded.");
                sb.AppendLine("The Visual Effect Graph package is missing or failed to compile.");
                sb.AppendLine();
                sb.AppendLine("Loaded assemblies containing 'VisualEffect' or 'VFX':");
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string n = a.GetName().Name;
                    if (n.IndexOf("VisualEffect", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("VFX", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        sb.AppendLine("  " + n);
                    }
                }

                Write(sb, true);
                return;
            }

            sb.AppendLine("Assembly: " + asm.FullName);
            sb.AppendLine();

            DumpTemplates(sb);
            DumpLibrary(sb, asm);
            DumpAttributes(sb, asm);
            DumpModelTypes(sb, asm);

            Write(sb, false);
        }

        private static void DumpTemplates(StringBuilder sb)
        {
            sb.AppendLine("=== TEMPLATE .vfx ASSETS IN THE PACKAGE ===");
            try
            {
                string root = Path.GetFullPath("Packages/com.unity.visualeffectgraph");
                if (!Directory.Exists(root))
                {
                    sb.AppendLine("  package folder not resolvable at Packages/com.unity.visualeffectgraph");
                }
                else
                {
                    var files = Directory.GetFiles(root, "*.vfx", SearchOption.AllDirectories);
                    if (files.Length == 0) sb.AppendLine("  none found");
                    foreach (var f in files)
                    {
                        sb.AppendLine("  " + f.Substring(root.Length).Replace('\\', '/').TrimStart('/'));
                    }
                }
            }
            catch (Exception e)
            {
                sb.AppendLine("  ERROR: " + e.Message);
            }

            sb.AppendLine();
        }

        // The authoritative list of what can actually be instantiated.
        private static void DumpLibrary(StringBuilder sb, Assembly asm)
        {
            sb.AppendLine("=== VFXLibrary DESCRIPTORS (display name -> concrete type) ===");

            Type library = asm.GetType("UnityEditor.VFX.VFXLibrary");
            if (library == null)
            {
                sb.AppendLine("  UnityEditor.VFX.VFXLibrary not found");
                sb.AppendLine();
                return;
            }

            foreach (string getter in new[] { "GetContexts", "GetBlocks", "GetOperators", "GetParameters" })
            {
                sb.AppendLine("-- " + getter + " --");
                try
                {
                    MethodInfo mi = library.GetMethod(getter, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (mi == null)
                    {
                        sb.AppendLine("   method not found");
                        continue;
                    }

                    var result = mi.Invoke(null, null) as IEnumerable;
                    if (result == null)
                    {
                        sb.AppendLine("   returned null / not enumerable");
                        continue;
                    }

                    var rows = new List<string>();
                    foreach (object descriptor in result)
                    {
                        if (descriptor == null) continue;
                        string display = GetMemberString(descriptor, "name");
                        string category = GetMemberString(descriptor, "category");
                        object model = GetMemberValue(descriptor, "model");
                        string typeName = model != null ? model.GetType().FullName : GetMemberString(descriptor, "modelType");
                        rows.Add(string.Format("   {0,-46} {1,-26} {2}", display, category, typeName));
                    }

                    rows.Sort(StringComparer.Ordinal);
                    foreach (string r in rows) sb.AppendLine(r);
                    sb.AppendLine("   (" + rows.Count + " entries)");
                }
                catch (Exception e)
                {
                    sb.AppendLine("   ERROR: " + e.Message);
                }

                sb.AppendLine();
            }
        }

        private static void DumpAttributes(StringBuilder sb, Assembly asm)
        {
            sb.AppendLine("=== BUILT IN PARTICLE ATTRIBUTES ===");

            Type attr = asm.GetType("UnityEditor.VFX.VFXAttribute");
            if (attr == null)
            {
                sb.AppendLine("  UnityEditor.VFX.VFXAttribute not found");
                sb.AppendLine();
                return;
            }

            bool any = false;
            foreach (var f in attr.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (!typeof(IEnumerable).IsAssignableFrom(f.FieldType)) continue;
                if (f.FieldType == typeof(string)) continue;

                try
                {
                    var value = f.GetValue(null) as IEnumerable;
                    if (value == null) continue;

                    var names = new List<string>();
                    foreach (object item in value)
                    {
                        if (item == null) continue;
                        names.Add(item is string ? (string)item : GetMemberString(item, "name"));
                    }

                    if (names.Count == 0) continue;
                    any = true;
                    sb.AppendLine("  " + f.Name + " (" + names.Count + "): " + string.Join(", ", names));
                }
                catch
                {
                    // Static access can fail on partially initialised types.
                }
            }

            if (!any) sb.AppendLine("  no enumerable static attribute collections found");
            sb.AppendLine();
        }

        private static void DumpModelTypes(StringBuilder sb, Assembly asm)
        {
            sb.AppendLine("=== VFXModel TYPES (settings and slots) ===");

            Type modelBase = asm.GetType("UnityEditor.VFX.VFXModel");
            if (modelBase == null)
            {
                sb.AppendLine("  UnityEditor.VFX.VFXModel not found. Check the namespace.");
                sb.AppendLine();
                return;
            }

            Type settingAttr = asm.GetType("UnityEditor.VFX.VFXSettingAttribute");
            Type slotContainer = asm.GetType("UnityEditor.VFX.IVFXSlotContainer");

            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).ToArray();
                sb.AppendLine("  (partial type load: " + e.LoaderExceptions.Length + " loader errors)");
            }

            var models = types
                .Where(t => t != null && !t.IsAbstract && modelBase.IsAssignableFrom(t))
                .OrderBy(t => t.FullName, StringComparer.Ordinal)
                .ToArray();

            sb.AppendLine("  (" + models.Length + " concrete model types)");
            sb.AppendLine();

            foreach (Type t in models)
            {
                sb.AppendLine(t.FullName + (t.IsPublic ? "   [public]" : "   [internal]"));

                DumpVfxInfo(sb, t);
                DumpSettings(sb, t, settingAttr);
                DumpSlots(sb, t, slotContainer);

                sb.AppendLine();
            }
        }

        private static void DumpVfxInfo(StringBuilder sb, Type t)
        {
            foreach (object a in t.GetCustomAttributes(false))
            {
                if (a == null || a.GetType().Name != "VFXInfoAttribute") continue;
                sb.AppendLine("    [VFXInfo] name=\"" + GetMemberString(a, "name") +
                              "\" category=\"" + GetMemberString(a, "category") + "\"");
            }
        }

        private static void DumpSettings(StringBuilder sb, Type t, Type settingAttr)
        {
            if (settingAttr == null) return;

            var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.GetCustomAttributes(settingAttr, true).Length > 0)
                .ToArray();

            foreach (var f in fields)
            {
                string extra = "";
                if (f.FieldType.IsEnum)
                {
                    extra = "  values: " + string.Join("|", Enum.GetNames(f.FieldType));
                }

                sb.AppendLine("    [VFXSetting] " + f.Name + " : " + FriendlyType(f.FieldType) + extra);
            }
        }

        private static void DumpSlots(StringBuilder sb, Type t, Type slotContainer)
        {
            if (slotContainer == null || !slotContainer.IsAssignableFrom(t)) return;

            ScriptableObject instance = null;
            try
            {
                instance = ScriptableObject.CreateInstance(t);
                if (instance == null) return;

                DumpSlotSide(sb, instance, slotContainer, "in");
                DumpSlotSide(sb, instance, slotContainer, "out");
            }
            catch (Exception e)
            {
                sb.AppendLine("    (slot probe failed: " + e.GetType().Name + " " + e.Message + ")");
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void DumpSlotSide(StringBuilder sb, object instance, Type slotContainer, string side)
        {
            string countName = side == "in" ? "GetNbInputSlots" : "GetNbOutputSlots";
            string getName = side == "in" ? "GetInputSlot" : "GetOutputSlot";

            MethodInfo countMethod = slotContainer.GetMethod(countName);
            MethodInfo getMethod = slotContainer.GetMethod(getName);
            if (countMethod == null || getMethod == null) return;

            int count = (int)countMethod.Invoke(instance, null);
            for (int i = 0; i < count; i++)
            {
                object slot = getMethod.Invoke(instance, new object[] { i });
                if (slot == null) continue;

                string slotName = GetMemberString(slot, "name");
                object property = GetMemberValue(slot, "property");
                object propType = property != null ? GetMemberValue(property, "type") : null;
                string typeName = propType is Type ? FriendlyType((Type)propType) : "?";

                sb.AppendLine("    slot[" + side + " " + i + "] " + slotName + " : " + typeName);
            }
        }

        private static string FriendlyType(Type t)
        {
            if (t == null) return "?";
            if (t == typeof(float)) return "float";
            if (t == typeof(int)) return "int";
            if (t == typeof(bool)) return "bool";
            if (t == typeof(string)) return "string";
            return t.Name;
        }

        private static object GetMemberValue(object target, string memberName)
        {
            if (target == null) return null;
            Type t = target.GetType();

            PropertyInfo p = t.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null && p.CanRead)
            {
                try { return p.GetValue(target, null); } catch { return null; }
            }

            FieldInfo f = t.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null)
            {
                try { return f.GetValue(target); } catch { return null; }
            }

            return null;
        }

        private static string GetMemberString(object target, string memberName)
        {
            object v = GetMemberValue(target, memberName);
            return v == null ? "" : v.ToString();
        }

        private static void Write(StringBuilder sb, bool failed)
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Temp");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, OutputFileName);
            File.WriteAllText(path, sb.ToString());

            if (failed)
            {
                Debug.LogError("VFX model probe failed. Details written to " + path);
            }
            else
            {
                Debug.Log("VFX model API dumped to " + path);
            }

            EditorUtility.RevealInFinder(path);
        }
    }
}
