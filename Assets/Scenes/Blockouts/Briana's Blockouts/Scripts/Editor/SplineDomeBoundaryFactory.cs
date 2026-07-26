using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace Crease.Flying.Environment.BlockoutHelpers
{
    /// <summary>
    /// Editor utility that creates a pre-configured <see cref="SplineDomeBoundary"/> GameObject with
    /// a default closed elliptical Spline outline. Reshape the Spline's knots in the Scene view to
    /// match the play area you want to contain, then drag the result into a Prefabs folder to reuse it.
    /// </summary>
    public static class SplineDomeBoundaryFactory
    {
        private const string MenuPath = "GameObject/Blockout Helpers/Create Spline Dome Boundary";
        private const float DefaultRadiusX = 90f;
        private const float DefaultRadiusZ = 260f;
        private const int DefaultKnotCount = 8;

        [MenuItem(MenuPath, false, 10)]
        private static void CreateSplineDomeBoundary(MenuCommand menuCommand)
        {
            var go = new GameObject("Spline Dome Boundary");
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);

            SplineContainer container = go.AddComponent<SplineContainer>();
            container.Spline = new Spline(BuildDefaultKnotPositions(), TangentMode.AutoSmooth, closed: true);

            go.AddComponent<MeshCollider>();
            go.AddComponent<SplineDomeBoundary>();

            Undo.RegisterCreatedObjectUndo(go, "Create Spline Dome Boundary");
            Selection.activeGameObject = go;
        }

        private static float3[] BuildDefaultKnotPositions()
        {
            var knots = new float3[DefaultKnotCount];
            for (int i = 0; i < DefaultKnotCount; i++)
            {
                float angle = (i / (float)DefaultKnotCount) * Mathf.PI * 2f;
                knots[i] = new float3(
                    Mathf.Cos(angle) * DefaultRadiusX,
                    0f,
                    Mathf.Sin(angle) * DefaultRadiusZ);
            }
            return knots;
        }
    }
}
