using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro;

public class touchObject : MonoBehaviour
{
    [SerializeField] private Camera arCamera;
    private ARRaycastManager arRaycastManager;
    [SerializeField] private TMP_Text advice;
    [SerializeField] private PrefabLabelMap prefabLabelMap;

    private void Awake()
    {
        arRaycastManager = FindObjectOfType<ARRaycastManager>();

        if (arCamera == null)
        {
            arCamera = Camera.main;
        }
        if (prefabLabelMap == null)
        {
            prefabLabelMap = FindObjectOfType<PrefabLabelMap>();
        }
    }

    private void Update()
    {
        if (Input.touchCount == 0)
            return;

        Touch touch = Input.GetTouch(0);
        if (touch.phase != TouchPhase.Began)
            return;

        Ray ray = arCamera.ScreenPointToRay(touch.position);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            GameObject hitObject = hit.transform.gameObject;
            string hitName = hitObject.name; 

            foreach (var pair in prefabLabelMap.LabelPrefabPairs)
            {
                if (hitName.StartsWith(pair.Prefab.name)) 
                {
                    advice.text = pair.Advice; 
                    return;
                }
            }
            // NEW ↓ Если не найдено — текст по умолчанию
            advice.text = $"Нет совета для {hitName}";
        }
    }
}
