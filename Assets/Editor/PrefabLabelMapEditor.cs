using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(PrefabLabelMap))]
public class PrefabLabelMapEditor : Editor
{
    public TextAsset labelMapAsset;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PrefabLabelMap map = (PrefabLabelMap)target;

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Импорт LabelMap", EditorStyles.boldLabel);
        labelMapAsset = (TextAsset)EditorGUILayout.ObjectField("LabelMap File", labelMapAsset, typeof(TextAsset), false);

        if (GUILayout.Button("import keys from LabelMap"))
        {
            ImportLabels(map);
        }
    }

    void ImportLabels(PrefabLabelMap map)
    {
        if (labelMapAsset == null)
        {
            Debug.LogError("Сначала укажите файл с LabelMap");
            return;
        }

        string[] labels = labelMapAsset.text.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        map.LabelPrefabPairs.Clear();
        foreach (string label in labels)
        {
            map.LabelPrefabPairs.Add(new LabelPrefabPair { Label = label });
        }

        EditorUtility.SetDirty(map);
        Debug.Log("Ключи успешно импортированы!");
    }
}
