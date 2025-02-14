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
    [SerializeField] private ARCameraManager cameraManager;
    private XRCpuImage latestCpuImage;
    private Texture2D latestImage;
    [SerializeField] private TextMeshProUGUI DebugText;
    public ARSession arSession;
    public Yolo yolo;
    [SerializeField] private bool test;
    [SerializeField] private Texture2D example;
    private ARRaycastManager raycastManager; // !!!!!!!!!!!!!
    private GameObject[] created; // для добавленных на сцену элементов, чтобы еще раз не добавлять
    private Detection[] detectedObjects;

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

    private bool isProcessing = false;

    void OnEnable()
    {
        if (_trackedImageManager != null)
        {
            _trackedImageManager.trackedImagesChanged += OnImageChanged;
        }
        else
        {
            Debug.LogError("_trackedImageManager is null in OnEnable");
        }

        if (cameraManager != null)
        {
            cameraManager.frameReceived += OnCameraFrameReceived;
        }
        else
        {
            Debug.LogError("cameraManager is null in Awake");
        }
    }

    void OnDisable()
    {
        if (_trackedImageManager != null)
        {
            _trackedImageManager.trackedImagesChanged -= OnImageChanged;
        }
        else
        {
            Debug.LogError("_trackedImageManager is null in OnDisable");
        }

        if (cameraManager != null)
        {
            cameraManager.frameReceived -= OnCameraFrameReceived;
        }
        else
        {
            Debug.Log("cameraManager is null in Awake");
        }
    }

    void Awake()
    {
        //detectionScript = gameObject.GetComponent<detection>();
        yolo = gameObject.GetComponent<Yolo>();
        _trackedImageManager = GetComponent<ARTrackedImageManager>();
        raycastManager = GetComponent<ARRaycastManager>(); // Инициализация ARRaycastManager !!!!!!!!!!!

        _lineTexture = new Texture2D(1, 1);
        _lineTexture.SetPixel(0, 0, boxColor);
        _lineTexture.Apply();

        objectToPrefabMap = new Dictionary<string, GameObject>();
        cameraManager = GetComponentInChildren<ARCameraManager>();
        if (!yolo || !_trackedImageManager || objectToPrefabMap == null || !cameraManager) 
        {
            Debug.Log("яяяяяяяяяяя");
        }
        foreach (var objectPrefab in objectPrefabList)
        {
            objectToPrefabMap[objectPrefab.imageName] = objectPrefab.prefab;
        }
    }

    void Start()
    {
#if UNITY_ANDROID
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
        }
#endif

        if (arSession == null || !arSession.enabled)
        {
            Debug.LogError("AR Session is not enabled or not found");
            return;
        }

        if (cameraManager == null || !cameraManager.enabled)
        {
            Debug.LogError("AR Camera Manager is not enabled or not found");
            return;
        }

        var cameraSubsystem = cameraManager.subsystem;
        if (cameraSubsystem != null)
        {
            Debug.Log("Device supports CPU images");
        }
        else
        {
            Debug.LogError("Device does not support CPU images");
        }

        StartCoroutine(CallDetectEveryFiveSeconds());
    }

    void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        if (isProcessing)
        {
            return;
        }

        if (cameraManager == null)
        {
            Debug.Log("cameraManager is null");
            return;
        }

        if (!cameraManager.enabled)
        {
            Debug.Log("cameraManager is not enabled");
            return;
        }

        if (cameraManager.subsystem == null)
        {
            Debug.Log("cameraManager subsystem is null");
            return;
        }

        if (latestCpuImage.valid)
        {
            latestCpuImage.Dispose();
        }

        if (!cameraManager.TryAcquireLatestCpuImage(out latestCpuImage))
        {
            Debug.Log("Failed to acquire latest CPU image");
            return;
        }

        Debug.Log("Successfully acquired latest CPU image");
    }
    IEnumerator CallDetectEveryFiveSeconds()
    {

        while (true)
        {
            Debug.Log("Processing frame for detection");
            
            //if ((latestCpuImage.valid || test) && detectionScript.ready)
            if ((latestCpuImage.valid || test) && yolo.ready)
            {

                isProcessing = true;
                latestImage = !test ? ConvertImageToTexture2D(latestCpuImage):example;
                if (latestImage.height < latestImage.width && !test) latestImage = Rotate90(latestImage);
                Debug.Log($"height: {latestImage.height} width: {latestImage.width}");
                //SaveTextureAsPNG(latestImage,"check");
                detectedObjects = yolo.Detect(latestImage);
                while (!yolo.ready)
                {
                    yield return null;
                }
                Debug.Log(yolo.ready);
                //created = detectedObjects.Length != 0 ? new GameObject[detectedObjects.Length] : null ;
                if (detectedObjects != null && detectedObjects.Length != 0)// && detectedObjects.Length != created.Length) // надо будет исправить это для теста 
                {
                    Debug.Log($"Detected {detectedObjects.Length} objects");
                    string[] label = objectToPrefabMap.Keys.ToArray();
                    for (int i=0; i < detectedObjects.Length;i++) 
                    {
                        //created[i] = 
                        //Instantiate(objectToPrefabMap[label[detectedObjects[i].classID]], detectedObjects[i].place, Quaternion.identity);
                        

                        //detectionScript.PrintDetections(detectedObjects);
                        Debug.Log("distance: " + detectedObjects[0].place.z);
                    }
                    
                    //detectedObjects = null;
                }
                latestCpuImage.Dispose();
                isProcessing = false;
            }
            else
            {
                Debug.Log("Skipping detection, latestImage or detectionScript not ready");
            }

            yield return new WaitForSeconds(3f);
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

    Texture2D ConvertImageToTexture2D(XRCpuImage image)
    {
        Debug.Log("Converting XRCpuImage to Texture2D");


        Texture2D texture = new Texture2D(image.width, image.height, TextureFormat.RGBA32, false);

        XRCpuImage.ConversionParams conversionParams = new XRCpuImage.ConversionParams()
        {
            inputRect = new RectInt(0, 0, image.width, image.height),
            outputDimensions = new Vector2Int(image.width, image.height),

            outputFormat = TextureFormat.RGBA32,
            transformation = XRCpuImage.Transformation.None
        };

        int dataSize = image.GetConvertedDataSize(conversionParams);

        var rawTextureData = new NativeArray<byte>(dataSize, Allocator.Temp);

        try
        {
            image.Convert(conversionParams, rawTextureData);
        }
        catch (Exception ex)
        {
            Debug.LogError("Exception during image conversion: " + ex.Message);
            image.Dispose();
            return null;
        }

        texture.LoadRawTextureData(rawTextureData);
        texture.Apply();

        image.Dispose();
        rawTextureData.Dispose();
        Debug.Log("Conversion to Texture2D completed");
        return texture;
    }

    void OnImageChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (ARTrackedImage addedImage in eventArgs.added)
        {
            // Создайте экземпляр объекта на месте добавленного изображения
            InstantiateObjectAtImage(addedImage);
        }

        foreach (ARTrackedImage removedImage in eventArgs.removed)
        {
            // При необходимости удалите или скройте объект, если изображение больше не отслеживается
            HandleRemovedImage(removedImage);
        }
    }

    void InstantiateObjectAtImage(ARTrackedImage trackedImage)
    {
        if (objectToPrefabMap.TryGetValue(trackedImage.referenceImage.name, out GameObject prefab))
        {
            GameObject instantiatedObject = Instantiate(prefab, trackedImage.transform.position, trackedImage.transform.rotation);

            // Прикрепляем объект к изображению и устанавливаем localPosition и localRotation в ноль
            instantiatedObject.transform.SetParent(trackedImage.transform, false);
            instantiatedObject.transform.localPosition = Vector3.zero;
            instantiatedObject.transform.localRotation = Quaternion.identity;
        }
    }

    void HandleRemovedImage(ARTrackedImage trackedImage)
    {
        if (trackedImage.transform.childCount > 0)
        {
            Transform existingObject = trackedImage.transform.GetChild(0);
            Destroy(existingObject.gameObject);
        }
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
}
