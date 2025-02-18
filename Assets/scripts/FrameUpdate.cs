using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections;

public class FrameUpdate : MonoBehaviour
{
    [SerializeField] private ARCameraManager cameraManager;
    public Texture2D latestImage;

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

    private void OnCameraFrameReceived(ARCameraFrameEventArgs args)
    {
        if (cameraManager == null || !cameraManager.TryAcquireLatestCpuImage(out XRCpuImage cpuImage))
            return;

        if (latestImage != null)
            Destroy(latestImage); // Удаляем старое изображение

        // Конвертация CPUImage в Texture2D (работает в главном потоке)
        latestImage = ConvertImageToTexture2D(cpuImage);
        cpuImage.Dispose();

        // Вызываем событие, передавая изображение нейросети
        OnNewFrameReady?.Invoke(latestImage);
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
