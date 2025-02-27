using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections;
public struct ImageCameraTransform
{
    public Texture2D image;
    public Transform cameraTransform;
}
public class FrameUpdate : MonoBehaviour
{
    [SerializeField] private ARCameraManager cameraManager;
    private Texture2D latestImage;
    public ImageCameraTransform currentImage;
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
            Destroy(latestImage); // Удаляем старое изображение

        latestImage = ConvertImageToTexture2D(cpuImage);
        currentImage = new ImageCameraTransform { image = latestImage, cameraTransform = Camera.main.transform };
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
}
