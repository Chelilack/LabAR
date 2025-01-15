using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;
using System.Collections;
using Unity.Collections;
using TMPro;
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
        objectToPrefabMap = new Dictionary<string, GameObject>();
        cameraManager = GetComponentInChildren<ARCameraManager>();
        if (!yolo || !_trackedImageManager || objectToPrefabMap == null || !cameraManager) 
        {
            Debug.Log("€€€€€€€€€€€");
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
                // ѕреобразование изображени€ в формат, подход€щий дл€ tflite модели
                //latestImage = !test ? ConvertImageToTexture2D(latestCpuImage):example;
                latestImage = !test ? ConvertImageToTexture2D(latestCpuImage):example;
                if (latestImage.height < latestImage.width && !test) latestImage = Rotate90(latestImage);
                Debug.Log($"height: {latestImage.height} width: {latestImage.width}");

                //latestImage=ResizeWithBlit(latestImage,640,640);
                //SaveTextureAsPNG(latestImage,"yolo2.png");

                //SaveTextureAsRaw(latestImage,"yolo2.png");
                // —охранение изображени€
                //SaveTextureAsPNG(latestImage, "LatestImage.png");

                // ¬ыполнение обнаружени€ объектов
                //var detectedObjects = detectionScript.Detect(latestImage);
                var detectedObjects = yolo.Detect(latestImage);
                Debug.Log("uuuuaaaaaaaa: " + detectedObjects);
                while (!yolo.ready)
                {
                    yield return null;
                }
                Debug.Log(yolo.ready);
                Debug.Log("uuuuuuuuuuu");
                if (detectedObjects != null && detectedObjects.Length != 0)
                {
                    Instantiate(objectToPrefabMap["first"], detectedObjects[0].place+new Vector3(0f,0f,0.1f), Quaternion.identity);
                    //detectionScript.PrintDetections(detectedObjects);
                    Debug.Log("distance: "+ detectedObjects[0].place.z);
                    Debug.Log($"Detected {detectedObjects.Length} objects");
                    detectedObjects = null;
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

    Texture2D ResizeWithBlit(Texture2D source, int width, int height)
    {
        RenderTexture rt = new RenderTexture(width, height, 32);
        RenderTexture.active = rt;

        Graphics.Blit(source, rt);

        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();

        RenderTexture.active = null;
        rt.Release();
        return result;
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
    void SaveTextureAsRaw(Texture2D texture, string fileNamePrefix)
    {
        // ѕолучить сырые байты текстуры
        byte[] rawBytes = texture.GetRawTextureData();

        // —оздать уникальное им€ файла
        string timestamp = DateTime.Now.ToString("HHmmss");
        string uniqueFileName = $"{fileNamePrefix}_{timestamp}.raw";
        string filePath = Path.Combine(Application.persistentDataPath, uniqueFileName);

        // —охранить сырые байты в файл
        File.WriteAllBytes(filePath, rawBytes);

        Debug.Log($"Raw texture saved to: {filePath}");
    }
    void OnImageChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (ARTrackedImage addedImage in eventArgs.added)
        {
            // —оздайте экземпл€р объекта на месте добавленного изображени€
            InstantiateObjectAtImage(addedImage);
        }

        foreach (ARTrackedImage removedImage in eventArgs.removed)
        {
            // ѕри необходимости удалите или скройте объект, если изображение больше не отслеживаетс€
            HandleRemovedImage(removedImage);
        }
    }

    void InstantiateObjectAtImage(ARTrackedImage trackedImage)
    {
        if (objectToPrefabMap.TryGetValue(trackedImage.referenceImage.name, out GameObject prefab))
        {
            GameObject instantiatedObject = Instantiate(prefab, trackedImage.transform.position, trackedImage.transform.rotation);

            // ѕрикрепл€ем объект к изображению и устанавливаем localPosition и localRotation в ноль
            instantiatedObject.transform.SetParent(trackedImage.transform, false);
            instantiatedObject.transform.localPosition = Vector3.zero;
            instantiatedObject.transform.localRotation = Quaternion.identity;
        }
    }

    void UpdateObjectAtImage(ARTrackedImage trackedImage)
    {
        Transform existingObject = trackedImage.transform.GetChild(0);
        existingObject.position = trackedImage.transform.position;
        existingObject.rotation = trackedImage.transform.rotation;
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
}
