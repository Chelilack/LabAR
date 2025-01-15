using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Sentis;
using System.Collections;
using System.Linq;

public class Yolo : MonoBehaviour
{
    public ModelAsset modelAsset;
    public Texture2D example;
    public bool ready=true;
    public bool finished = false;
    public Detection[] currentResult;
    public Camera cam;
    Model runtimeModel;
    Worker worker;
    Tensor<float> inputTensor;
    //Tensor<float> inputTensor;
    void Awake()
    {
        runtimeModel = ModelLoader.Load(modelAsset);
        worker = new Worker(runtimeModel, BackendType.GPUCompute); 
        var answer = Detect(example);


        //Tensor inputTensor = TransformInputToTensor(example);






        //inputTensor.Dispose();
        //worker.Dispose();


    }
    private IEnumerator RunModelInference(Texture2D texture2D)
    {
        ready = false;
        inputTensor = TextureConverter.ToTensor(texture2D, 640, 640, 3);
        float start = Time.realtimeSinceStartup;
        worker.Schedule(inputTensor);
        Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;

        outputTensor.ReadbackRequest();

        while (!outputTensor.IsReadbackRequestDone())
        {
            yield return null;
        }
        var cpuTensor = outputTensor.ReadbackAndClone();
        Debug.Log("bool: " + outputTensor.IsReadbackRequestDone());
        Debug.Log("000: " + cpuTensor.shape);
        List<Detection> detections = new List<Detection>();
        for (int i = 0; i < 8400; i++)
        {
            float x_center   = cpuTensor[0, 0, i] * (Screen.width   / 640f);
            float y_center   = cpuTensor[0, 1, i] * (Screen.height  / 640f);
            float width      = cpuTensor[0, 2, i] * (Screen.width   / 640f);
            float height     = cpuTensor[0, 3, i] * (Screen.height  / 640f);
            float confidence = cpuTensor[0, 4, i];
            if (confidence > 0.5f)
            {
                int numClasses = cpuTensor.shape[1] - 5;
                int bestClassID = -1;
                float maxClassScore = 0.0f;
                for (int k = 0; k < numClasses; k++)
                {
                    float classScore = cpuTensor[0, 5 + k, i]; // Извлекаем вероятность для класса c
                    if (classScore > maxClassScore)
                    {
                        maxClassScore = classScore;
                        bestClassID = k+1;
                    }
                }
                Debug.Log("Max Class Score: " + maxClassScore);
                bestClassID = (maxClassScore > 0.5f) ? bestClassID : 0;
                Debug.Log($"Object detected with confidence {confidence}: " +
                          $"(x_center: {x_center}, y_center: {y_center}, width: {width}, height: {height}, classID: {bestClassID})");

                Detection detection = new Detection
                {
                    place=cam.ScreenToWorldPoint(new Vector3(x_center, y_center, CalculateDistance(height, 0.12f, cam))),
                    box = new Rect(x_center - width / 2, y_center - height / 2, width, height),
                    classID = bestClassID, // Здесь можно указать ваш classID, если есть данные для его вычисления
                    score = confidence,
                };

                detections.Add(detection);
            }
        }
        if (detections.Count == 0)
        {
            Debug.Log("nu ne to");

        }
        else
        {
            currentResult = detections
             .GroupBy(d => d.classID)
             .Select(g => g.OrderByDescending(d => d.score).First())
             .ToArray();
            Debug.Log("best: " + currentResult[0].score);
        }
        Debug.Log("wasted time: " + (float)(Time.realtimeSinceStartup - start));
        ready = true;
        finished = true;
    }
    public Detection[] Detect(Texture2D texture2D) 
    {
        Coroutine coroutine = ready ? StartCoroutine(RunModelInference(texture2D)): null;
        //while (!ready) {}
        //StopCoroutine(coroutine);

        return currentResult;
    }
    //Texture2D ResizeTexture(Texture2D source, int newWidth, int newHeight)
    //{
    //    RenderTexture rt = new RenderTexture(newWidth, newHeight, 24);
    //    RenderTexture.active = rt;
    //    Graphics.Blit(source, rt);
    //    Texture2D result = new Texture2D(newWidth, newHeight, TextureFormat.RGB24, false);
    //    result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
    //    result.Apply();
    //    RenderTexture.active = null;
    //    rt.Release();
    //    return result;
    //}
    /// <summary>
    /// 
    /// </summary>
    /// <param name="currentImage"></param>
    /// <param name="screenSizePixels">высота объекта на экране в пикселях</param>
    /// <param name="realSizeMeters">высота объекта на экране в метрах </param>
    /// <param name="camera"></param>
    /// <returns></returns>
    float CalculateDistance(float screenSizePixels, float realSizeMeters, Camera camera)
    {

        float verticalFOV = camera.fieldOfView * Mathf.Deg2Rad;
        float screenHeightAtOneMeter = 2.0f * Mathf.Tan(verticalFOV / 2.0f);
        float relativeScreenHeight = screenSizePixels / (float)Screen.height;
        float distance = realSizeMeters / (relativeScreenHeight * screenHeightAtOneMeter);
        return distance;
    }
    //то что внизу потом уберу это не должно тут лежать
    //public void DrawBox(Rect rect, float depth)
    //{
    //    //Vector3 leftBottom, leftTop, rightBottom, rightTop, center;


    //    //leftBottom = cam.ScreenToWorldPoint(new Vector3(rect.xMin, rect.yMin, cam.nearClipPlane));
    //    //leftTop = cam.ScreenToWorldPoint(new Vector3(rect.xMin, rect.yMax, cam.nearClipPlane));
    //    //rightBottom = cam.ScreenToWorldPoint(new Vector3(rect.xMax, rect.yMin, cam.nearClipPlane));
    //    //rightTop = cam.ScreenToWorldPoint(new Vector3(rect.xMax, rect.yMax, cam.nearClipPlane));
    //    Vector3 center = cam.ScreenToWorldPoint(new Vector3(rect.center.x * (float)Screen.width / currentImage.width, rect.center.y * (float)Screen.height / currentImage.height, depth));
    //    Debug.Log($"BB centerY: {rect.center.y} minY: {rect.yMin} maxY: {rect.yMax}");
    //    Instantiate(box, center, Quaternion.identity);


    //}


    //void SaveTextureAsPNG(Texture2D texture, string fileNamePrefix)
    //{
    //    byte[] bytes = texture.EncodeToPNG();
    //    string timestamp = DateTime.Now.ToString("HHmmss");
    //    string uniqueFileName = $"{fileNamePrefix}_{timestamp}.png";
    //    string filePath = Path.Combine(Application.persistentDataPath, uniqueFileName);
    //    File.WriteAllBytes(filePath, bytes);
    //    Debug.Log($"Image saved to: {filePath}");
    //}

}
