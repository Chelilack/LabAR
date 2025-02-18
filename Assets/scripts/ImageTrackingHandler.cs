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
    [SerializeField] private List<objectPrefab> objectPrefabList;
    private Dictionary<string, GameObject> objectToPrefabMap;
    private ARTrackedImageManager _trackedImageManager;
    private detection detectionScript;
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
    public class DetectedObject
    {
        public Vector3 Position;
        public Vector2 Size;
        public string ClassName;
        public float Rotation;
    }

    //void OnEnable()
    //{

    //    if (cameraManager != null)
    //    {
    //        cameraManager.frameReceived += OnCameraFrameReceived;
    //    }
    //    else
    //    {
    //        Debug.LogError("cameraManager is null in Awake");
    //    }
    //}

    //void OnDisable()
    //{

    //    if (cameraManager != null)
    //    {
    //        cameraManager.frameReceived -= OnCameraFrameReceived;
    //    }
    //    else
    //    {
    //        Debug.Log("cameraManager is null in Awake");
    //    }
    //}

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

        objectToPrefabMap = new Dictionary<string, GameObject>();
        if (!yolo || !_trackedImageManager || objectToPrefabMap == null) 
        {
            Debug.Log("яяяяяяяяяяя");
        }
        foreach (var objectPrefab in objectPrefabList)
        {
            objectToPrefabMap[objectPrefab.imageName] = objectPrefab.prefab;
        }
    }

    async void Start()
    {
        await CallDetectEveryFiveSeconds();
    }


    public async Task CallDetectEveryFiveSeconds()
    {

        while (!cts.IsCancellationRequested)
        {
            Debug.Log($"Processing frame for detection. frame status: {frameUpdate.latestImage}");
            //while (!yolo.ready) 
            //{
            //    await Task.Delay(100); // 
            //}
            //if ((latestCpuImage.valid || test) && yolo.ready)
            if (test || (yolo.ready && frameUpdate.latestImage != null))
            {
                float start = Time.realtimeSinceStartup;
                //latestImage = !test ? ConvertImageToTexture2D(latestCpuImage):example;
                latestImage = !test ? frameUpdate.latestImage : example ;
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
                        if (!createdPrefab.Contains(objectToPrefabMap[label[detectedObjects[i].classID]]))
                        {
                            var temp = Instantiate(objectToPrefabMap[label[detectedObjects[i].classID]], detectedObjects[i].place, Quaternion.identity);
                            created.Add(temp);
                            createdPrefab.Add(objectToPrefabMap[label[detectedObjects[i].classID]]);

                        }


                        //detectionScript.PrintDetections(detectedObjects);                      
                    }

                    //detectedObjects = null;
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

    void DrawBoundingBox(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2), _lineTexture); // Верхняя линия
        GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - 2, rect.width, 2), _lineTexture); // Нижняя линия
        GUI.DrawTexture(new Rect(rect.x, rect.y, 2, rect.height), _lineTexture); // Левая линия
        GUI.DrawTexture(new Rect(rect.x + rect.width - 2, rect.y, 2, rect.height), _lineTexture); // Правая линия
    }

    //Texture2D ConvertImageToTexture2D(XRCpuImage image)
    //{
    //    Debug.Log("Converting XRCpuImage to Texture2D");


    //    Texture2D texture = new Texture2D(image.width, image.height, TextureFormat.RGBA32, false);

    //    XRCpuImage.ConversionParams conversionParams = new XRCpuImage.ConversionParams()
    //    {
    //        inputRect = new RectInt(0, 0, image.width, image.height),
    //        outputDimensions = new Vector2Int(image.width, image.height),

    //        outputFormat = TextureFormat.RGBA32,
    //        transformation = XRCpuImage.Transformation.None
    //    };

    //    int dataSize = image.GetConvertedDataSize(conversionParams);

    //    var rawTextureData = new NativeArray<byte>(dataSize, Allocator.Temp);

    //    try
    //    {
    //        image.Convert(conversionParams, rawTextureData);
    //    }
    //    catch (Exception ex)
    //    {
    //        Debug.LogError("Exception during image conversion: " + ex.Message);
    //        image.Dispose();
    //        return null;
    //    }

    //    texture.LoadRawTextureData(rawTextureData);
    //    texture.Apply();

    //    image.Dispose();
    //    rawTextureData.Dispose();
    //    Debug.Log("Conversion to Texture2D completed");
    //    return texture;
    //}


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
