using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(MansionStalker))]
public sealed class MansionStalkerInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();var enemy=(MansionStalker)target;
        if(Application.isPlaying)
        {
            EditorGUILayout.Space();EditorGUILayout.LabelField("Live behavior",EditorStyles.boldLabel);
            EditorGUILayout.LabelField("State",enemy.State.ToString());
            EditorGUILayout.LabelField("Alert range",enemy.CurrentAlertDistance.ToString("0.0")+" m");
            EditorGUILayout.LabelField("Committed stair travel",enemy.IsTraversingStairs.ToString());
            EditorGUILayout.LabelField("Chase recognition range",enemy.CurrentChaseDistance.ToString("0.0")+" m");
            EditorGUILayout.LabelField("Player visible",enemy.HasVisualContact.ToString());
            using(new EditorGUI.DisabledScope(true))EditorGUILayout.Vector3Field("Last known position",enemy.LastKnownPosition);
            Repaint();
        }
    }
}
