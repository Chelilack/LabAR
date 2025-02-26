using System.Collections.Generic; 
using UnityEngine; 

[System.Serializable]
public class LabelPrefabPair
{
    public string Label;
    public GameObject Prefab;
}

public class PrefabLabelMap : MonoBehaviour
{
    public List<LabelPrefabPair> LabelPrefabPairs = new List<LabelPrefabPair>();

    public Dictionary<string, GameObject> labelToPrefab;

    private void Awake()
    {
        labelToPrefab = new Dictionary<string, GameObject>();
        foreach (var pair in LabelPrefabPairs)
        {
            if (!labelToPrefab.ContainsKey(pair.Label))
                labelToPrefab.Add(pair.Label, pair.Prefab);
        }
    }
}
