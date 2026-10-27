using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    [CustomEditor(typeof(PlayerSpawnPointMarker))]
    internal sealed class PlayerPickupRangeEditor : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            var marker = (PlayerSpawnPointMarker)target;
            var radius = serializedObject.FindProperty("pickupRadius");
            Vector3 center = marker.RangeCenter;
            float value = Mathf.Max(0f, radius.floatValue);
            Vector3 handlePosition = center + Vector3.right * value;
            Handles.color = new Color(1f, .78f, .22f, 1f);
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider(handlePosition, Vector3.right,
                HandleUtility.GetHandleSize(handlePosition) * .09f, Handles.CubeHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.Update();
                radius.floatValue = Mathf.Max(0f, moved.x - center.x);
                serializedObject.ApplyModifiedProperties();
                SceneView.RepaintAll();
            }
            Handles.Label(center + Vector3.up * Mathf.Max(.2f, value), $"직접 획득 {value:0.##}");
        }
    }
}
