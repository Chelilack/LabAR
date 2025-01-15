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
    public struct ClassificationResult
    {
        public int ClassID;      
        public float Probability; 
    }

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
                if (numClasses == 1)
                {
                    maxClassScore = 1.0f / (1.0f + Mathf.Exp(-cpuTensor[0, 5, i]));
                    bestClassID = 1;
                }
                else
                {
                    float[] logits = new float[numClasses];
                    for (int k = 0; k < numClasses; k++)
                    {
                        logits[k] = cpuTensor[0, 5 + k, i];
                    }
                    var result    =   GetBestClass(logits);
                    bestClassID   =   result.ClassID ;
                    maxClassScore =   result.Probability;

                }

                Debug.Log("max class score: " + maxClassScore);
                bestClassID = (maxClassScore > 0.55f) ? bestClassID : 0;
                Debug.Log($"Object detected with confidence {confidence}: " +
                          $"(x_center: {x_center}, y_center: {y_center}, width: {width}, height: {height}, classID: {bestClassID})");

                Detection detection = new Detection
                {
                    place=cam.ScreenToWorldPoint(new Vector3(x_center, y_center, CalculateDistance(height, 0.12f, cam))),
                    box = new Rect(x_center - width / 2, y_center - height / 2, width, height),
                    classID = bestClassID, 
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
    /// <summary>
    /// 
    /// </summary>
    /// <param name="logits"> вероятность что это {1 класс, 2 класс, ...}</param>
    /// <returns></returns>
    public static ClassificationResult GetBestClass(float[] logits)
    {
        int numClasses = logits.Length;
        float maxProbability = 0.0f;
        int bestClassID = -1;

        // Вычисляем softmax для логитов и находим максимальную вероятность
        float sumExp = 0.0f;
        float[] probabilities = new float[numClasses];

        // Считаем сумму экспонент
        for (int i = 0; i < numClasses; i++)
        {
            probabilities[i] = Mathf.Exp(logits[i]);
            sumExp += probabilities[i];
        }

        // Нормализуем и находим класс с максимальной вероятностью
        for (int i = 0; i < numClasses; i++)
        {
            probabilities[i] /= sumExp;
            if (probabilities[i] > maxProbability)
            {
                maxProbability = probabilities[i];
                bestClassID = i;
            }
        }

        return new ClassificationResult
        {
            ClassID = bestClassID + 1,  
            Probability = maxProbability
        };
    }



}
