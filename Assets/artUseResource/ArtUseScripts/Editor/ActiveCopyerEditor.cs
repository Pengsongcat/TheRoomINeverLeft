using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ActiveCopyer))]
public class ActiveCopyerEditor : Editor
{
    // 下拉列表可选的数量
    static readonly int[] countOptions = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var copyer = (ActiveCopyer)target;

        string[] labels = new string[countOptions.Length];
        for (int i = 0; i < countOptions.Length; i++)
            labels[i] = countOptions[i].ToString();

        EditorGUI.BeginChangeCheck();
        int newCount = EditorGUILayout.IntPopup("配对数量", copyer.pairCount, labels, countOptions);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(copyer, "Change Pair Count");
            copyer.pairCount = newCount;
            copyer.ResizePairs();
            EditorUtility.SetDirty(copyer);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("配对列表 (follower 跟随 source 的 active)", EditorStyles.boldLabel);

        var pairsProp = serializedObject.FindProperty("pairs");
        for (int i = 0; i < pairsProp.arraySize; i++)
        {
            var element = pairsProp.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"#{i + 1}", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(element.FindPropertyRelative("source"), new GUIContent("Source (源物体)"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("follower"), new GUIContent("Follower (跟随物体)"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("invert"), new GUIContent("Invert (取反)"));
            EditorGUILayout.EndVertical();
        }

        serializedObject.ApplyModifiedProperties();
    }
}
