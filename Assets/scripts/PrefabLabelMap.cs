using System.Collections.Generic; 
using UnityEngine; 

[System.Serializable]
public class LabelPrefabPair
{
    public string Label;
    public GameObject Prefab;
    public string Advice;
}

public class PrefabLabelMap : MonoBehaviour
{
    public List<LabelPrefabPair> LabelPrefabPairs = new List<LabelPrefabPair>();

    public Dictionary<string, GameObject> labelToPrefab;
    public Dictionary<string, string> labelToAdvice;

    private void Awake()
    {
        labelToPrefab = new Dictionary<string, GameObject>();
        foreach (var pair in LabelPrefabPairs)
        {
            if (!labelToPrefab.ContainsKey(pair.Label))
            {
                labelToPrefab.Add(pair.Label, pair.Prefab);
                labelToAdvice.Add(pair.Label, pair.Advice);
            }
        }
    }
}
