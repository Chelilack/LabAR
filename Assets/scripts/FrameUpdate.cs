using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections;
public struct ImageWithCamera
{
    public Texture2D image;
    public Camera oldCamera;
}
public class FrameUpdate : MonoBehaviour
{
    [SerializeField] private ARCameraManager cameraManager;
    private Texture2D latestImage;
    public ImageWithCamera currentImage;
    [SerializeField] private TMP_Text shake;

    public event Action<Texture2D> OnNewFrameReady; 

    private void OnEnable()
    {
        if (cameraManager != null)
            cameraManager.frameReceived += OnCameraFrameReceived;
    }

    private void OnDisable()
    {
        if (cameraManager != null)
            cameraManager.frameReceived -= OnCameraFrameReceived;
    }
    public void Start()
    {

    }
    private void Update()
    {
        Vector3 acceleration = Input.acceleration;
        float shakeMagnitude = acceleration.magnitude;
        shake.text = "shake: " + shakeMagnitude.ToString() + "\n" + Camera.main.transform.position.ToString();
        //if (shakeMagnitude > 2.0f) // Порог чувствительности
        //{
             
        //}
    }
    private void OnCameraFrameReceived(ARCameraFrameEventArgs args)
    {
        if (cameraManager == null || !cameraManager.TryAcquireLatestCpuImage(out XRCpuImage cpuImage))
            return;

        if (latestImage != null)
            Destroy(latestImage);

        latestImage = ConvertImageToTexture2D(cpuImage);
        currentImage = new ImageWithCamera { image = latestImage, oldCamera = CloneCamera(Camera.main) };
        cpuImage.Dispose();

        OnNewFrameReady?.Invoke(latestImage);// у него вообще есть подписчики????
    }

    private Texture2D ConvertImageToTexture2D(XRCpuImage image)
    {
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

        rawTextureData.Dispose();
        return texture;
    }
    public Camera CloneCamera(Camera sourceCamera)
    {
        // Копируем параметры

        // Можно скопировать и другие параметры, если надо
        GameObject newCameraObject = new GameObject("ClonedCamera");
        newCameraObject.SetActive(false);
        Camera newCamera = newCameraObject.AddComponent<Camera>();
        newCamera.transform.position = sourceCamera.transform.position;
        newCamera.transform.rotation = sourceCamera.transform.rotation;
        newCamera.fieldOfView = sourceCamera.fieldOfView;
        newCamera.orthographicSize = sourceCamera.orthographicSize;
        newCamera.orthographic = sourceCamera.orthographic;
        newCamera.nearClipPlane = sourceCamera.nearClipPlane;
        newCamera.farClipPlane = sourceCamera.farClipPlane;
        return newCamera;
    }
}
