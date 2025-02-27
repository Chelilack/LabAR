using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;
using System.Collections;
using Unity.Collections;
using TMPro;
using System.Linq;
using System.IO;
using Unity.Sentis;
using System.Threading.Tasks;
using System.Threading;

[Serializable]
public class objectPrefab
{
    public string imageName;
    public GameObject prefab;
}

public class ImageTrackingHandler : MonoBehaviour
{
    private Dictionary<string, GameObject> objectToPrefabMap;
    private ARTrackedImageManager _trackedImageManager;

    [SerializeField] private FrameUpdate frameUpdate;
    [SerializeField] private TextMeshProUGUI DebugText;
    public ARSession arSession;
    public Yolo yolo;
    [SerializeField] private bool test;
    [SerializeField] private Texture2D example;
    private Texture2D latestImage;
    private ARRaycastManager raycastManager; // !!!!!!!!!!!!!
    private List <GameObject> created = new List<GameObject> (); // для добавленных на сцену элементов, чтобы еще раз не добавлять
    private List <GameObject> createdPrefab = new List<GameObject> (); // для добавленных на сцену элементов, чтобы еще раз не добавлять
    private Detection[] detectedObjects;

    private CancellationTokenSource cts;

    public Rect boundingBox;
    public Color boxColor = Color.red;
    private Texture2D _lineTexture;

    private Vector3 lastCameraPosition;
    private Quaternion lastCameraRotation;
    public class DetectedObject
    {
        public Vector3 Position;
        public Vector2 Size;
        public string ClassName;
        public float Rotation;
    }

    void Awake()
    {
        //detectionScript = gameObject.GetComponent<detection>();
        yolo = gameObject.GetComponent<Yolo>();
        frameUpdate = gameObject.GetComponent<FrameUpdate>();
        _trackedImageManager = GetComponent<ARTrackedImageManager>();
        raycastManager = GetComponent<ARRaycastManager>(); // Инициализация ARRaycastManager !!!!!!!!!!!

        cts = new CancellationTokenSource();

        _lineTexture = new Texture2D(1, 1);
        _lineTexture.SetPixel(0, 0, boxColor);
        _lineTexture.Apply();

        
    }

    async void Start()
    {
        objectToPrefabMap = FindAnyObjectByType<PrefabLabelMap>().labelToPrefab;
        if (!yolo || !_trackedImageManager || objectToPrefabMap == null)
        {
            Debug.LogError("smth wrong");
        }
        await CallDetectEveryFiveSeconds();
    }


    public async Task CallDetectEveryFiveSeconds()
    {

        while (!cts.IsCancellationRequested)
        {
            Debug.Log($"Processing frame for detection. frame status: {frameUpdate.currentImage}");
            //while (!yolo.ready) 
            //{
            //    await Task.Delay(100); // 
            //}
            //if ((latestCpuImage.valid || test) && yolo.ready)
            ImageCameraTransform currentImage = frameUpdate.currentImage; // чтобы во время обработки изображение не поменялось 
            if (test || (yolo.ready && currentImage.image != null))
            {
                float start = Time.realtimeSinceStartup;
                SaveCameraTransform(currentImage.cameraTransform);
                latestImage = !test ? currentImage.image : example ;
                Debug.Log($"texture2D {(float)Time.realtimeSinceStartup - start} sec");
                if (latestImage.height < latestImage.width && !test) latestImage = Rotate90(latestImage);
                Debug.Log($"height: {latestImage.height} width: {latestImage.width}");
                //SaveTextureAsPNG(latestImage,"check");
                if (created == null) Debug.Log("created=null");

                float startTime = Time.realtimeSinceStartup;

                detectedObjects = await yolo.Detect(latestImage);


                Debug.Log($"Detect time: {Mathf.Abs(startTime - (float)Time.realtimeSinceStartup)}");


                //created = detectedObjects.Length != 0 ? new GameObject[detectedObjects.Length] : null ;
                if (detectedObjects != null && detectedObjects.Length != 0)// && detectedObjects.Length != created.Length) // надо будет исправить это для теста 
                {
                    Debug.Log($"Detected {detectedObjects.Length} objects");
                    string[] label = objectToPrefabMap.Keys.ToArray();
                    for (int i = 0; i < detectedObjects.Length; i++)
                    {
                        if (!createdPrefab.Contains(objectToPrefabMap[label[detectedObjects[i].classID]]) || true)
                        {
                            var temp = Instantiate(objectToPrefabMap[label[detectedObjects[i].classID]], TransformPositionToNewCamera(detectedObjects[i].place, Camera.main.transform), Quaternion.identity);
                            created.Add(temp);
                            createdPrefab.Add(objectToPrefabMap[label[detectedObjects[i].classID]]);

                        }     
                    }
                }
            }
            else
            {
                Debug.Log($"Skipping detection, latestImage or detectionScript not ready( {yolo.ready} )");
                
            }
            await Task.Yield();
            await Task.Delay(1500);
        }
    }
    private void OnGUI()
    {
        if (detectedObjects == null) return;

        foreach (var detection in detectedObjects)
        {
            // Преобразуем координаты, так как (0,0) в Unity GUI находится вверху экрана
            Rect screenRect = new Rect(
                detection.box.x,
                Screen.height - detection.box.y - detection.box.height,
                detection.box.width,
                detection.box.height
            );

            DrawBoundingBox(screenRect, Color.red);
            Debug.Log($"name:{detection}");
            Debug.Log($"Bounding Box: x={screenRect.x}, y={screenRect.y}, width={screenRect.width}, height={screenRect.height}");
            Debug.Log($"Screen: width={Screen.width}, height={Screen.height}");
        }
    }
    public void SaveCameraTransform(Transform cameraTransform)
    {
        lastCameraPosition = cameraTransform.position;
        lastCameraRotation = cameraTransform.rotation;
    }
    public Vector3 TransformPositionToNewCamera(Vector3 originalPosition, Transform currentCameraTransform)
    {
        Vector3 currentCameraPosition = currentCameraTransform.position;
        Quaternion currentCameraRotation = currentCameraTransform.rotation;

        Vector3 relativePosition = Quaternion.Inverse(currentCameraRotation) * (originalPosition - currentCameraPosition);
        return lastCameraRotation * relativePosition + lastCameraPosition;
    }
    void DrawBoundingBox(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2), _lineTexture); // Верхняя линия
        GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - 2, rect.width, 2), _lineTexture); // Нижняя линия
        GUI.DrawTexture(new Rect(rect.x, rect.y, 2, rect.height), _lineTexture); // Левая линия
        GUI.DrawTexture(new Rect(rect.x + rect.width - 2, rect.y, 2, rect.height), _lineTexture); // Правая линия
    }


    Texture2D Rotate90(Texture2D originalTexture)
    {
        int width = originalTexture.width;
        int height = originalTexture.height;

        Texture2D rotatedTexture = new Texture2D(height, width);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int newX = height-1-y;
                int newY = width-1-x;
                rotatedTexture.SetPixel(newX, newY, originalTexture.GetPixel(x, y));
            }
        }


        
       
        rotatedTexture.Apply();

        return rotatedTexture;
    }

    void SaveTextureAsPNG(Texture2D texture, string fileNamePrefix)
    {
        byte[] bytes = texture.EncodeToPNG();
        string timestamp = DateTime.Now.ToString("HHmmss");
        string uniqueFileName = $"{fileNamePrefix}_{timestamp}.png";
        string filePath = Path.Combine(Application.persistentDataPath, uniqueFileName);
        File.WriteAllBytes(filePath, bytes);
        Debug.Log($"Image saved to: {filePath}");
    }

    //void OnDestroy()
    //{
    //    StopAllTasks();
    //}

    void OnApplicationQuit()
    {
        StopAllTasks();
    }
    void StopAllTasks()
    {
        if (cts != null)
        {
            cts.Cancel(); 
            cts.Dispose();
        }
    }
}
