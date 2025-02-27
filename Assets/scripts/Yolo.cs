using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Sentis;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

public struct Detection
{
    public Vector3 place;
    public Rect box;
    public int classID;
    public float score;
}

public class Yolo : MonoBehaviour
{
    public ModelAsset modelAsset;
    public Texture2D example;
    public bool ready = false;
    public bool finished = false;
    public Detection[] currentResult;
    public float imgSize;
    public Camera cam;
    Model runtimeModel;
    Worker worker;
    Tensor<float> inputTensor;
    IEnumerator m_Schedule;
    const int k_LayersPerFrame = 4;
    //Tensor<float> inputTensor;
    public struct ClassificationResult
    {
        public int ClassID;
        public float Probability;
    }


    private CancellationTokenSource cts;
    async void Awake()
    {
        Debug.Log($"Main Thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
        runtimeModel = await Task.Run(() =>
        {
            Debug.Log($"Background Thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
            return ModelLoader.Load(modelAsset);
        });
        Debug.Log($"Back to Main Thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
        
        worker = new Worker(runtimeModel, BackendType.GPUCompute);
        ready = true;
        cts = new CancellationTokenSource();

        //var answer = Detect(example);

        //Tensor inputTensor = TransformInputToTensor(example);






        //inputTensor.Dispose();
        //worker.Dispose();


    }
    private async Task RunModelInference(Texture2D texture2D)
    {
        ready = false;
        Debug.Log($"pos begin: {Camera.main.transform.position.ToString()}");
        inputTensor = TextureConverter.ToTensor(texture2D, (int)imgSize, (int)imgSize, 3);
        m_Schedule = worker.ScheduleIterable(inputTensor);
        float start = Time.realtimeSinceStartup;
        //worker.Schedule(inputTensor);

        int it = 0;
        while (m_Schedule.MoveNext()) 
        {
            if (++it % k_LayersPerFrame == 0) 
            {
                await Task.Yield(); 
                it = 0; 
            }
        }

        Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;

        //Tensor<float> cpuTensor = await outputTensor.ReadbackAndCloneAsync();
        Debug.Log($"Before await: {Thread.CurrentThread.ManagedThreadId}");
        Debug.Log("bool2: " + outputTensor.IsReadbackRequestDone());

        Tensor<float> cpuTensor = await outputTensor.ReadbackAndCloneAsync(); // фоном вызывать нельзя
        Debug.Log($" readbackandclone: {(float)Time.realtimeSinceStartup - start} sec");
        //Tensor<float> cpuTensor = await Task.Run(() => outputTensor.ReadbackAndCloneAsync());

        // фоном вызывать нельзя
        Debug.Log($"After await: {Thread.CurrentThread.ManagedThreadId}");

        Debug.Log("bool: " + outputTensor.IsReadbackRequestDone());
        Debug.Log("000: " + cpuTensor.shape);
        int numClasses = cpuTensor.shape[1] - 4;
        int numPrediction = cpuTensor.shape[2];
        List<Detection> detections = new List<Detection>();
        //string output = "class 1 : ";
        Debug.Log($"pos end: {Camera.main.transform.position.ToString()}");
        for (int i = 0; i < numPrediction; i++)
        {
            float x_center   = (Screen.width/2)+Mathf.Sign(cpuTensor[0, 0, i] - imgSize / 2f)*(Mathf.Abs(cpuTensor[0, 0, i]-imgSize/2f) * (Screen.height * ((float)texture2D.width / (float)texture2D.height) / imgSize));
            float y_center   = (imgSize - cpuTensor[0, 1, i]) * (Screen.height  / imgSize); // Yolo (0,0) - левый верхний угол.  Unity (0,0) - левый нижний угол
            float width      = cpuTensor[0, 2, i] * (Screen.height*((float)texture2D.width/(float)texture2D.height)/ imgSize);
            float height     = cpuTensor[0, 3, i] * (Screen.height  / imgSize);


            float confidence = Enumerable.Range(4, numClasses) // 
            .Select(k => cpuTensor[0, k, i])
            .Max();

            //output += $" {cpuTensor[0, 5, i]},";
            //if (cpuTensor[0, 4, i] > 0.5f) Debug.Log("wow" + cpuTensor[0, 4, i] + "i: "+ i);
            //if (cpuTensor[0, 5, i] > 0.5f) Debug.Log("class 1 " + cpuTensor[0, 5, i] + " i: "+ i + " confidence: " + cpuTensor[0, 4, i]);
            // Находим максимальное значение

            if (confidence > 0.9f)
            {
                int bestClassID = Enumerable.Range(4, numClasses)
                .Select(k => cpuTensor[0, k, i])
                .ToList().IndexOf(confidence);
              

                //Debug.Log("max class score: " + confidence);
                //bestClassID = (confidence > 0.5f) ? bestClassID : 0;
                Debug.Log($"Object detected with confidence {confidence}: " +
                          $"(x_center: {x_center}, y_center: {y_center}, width: {width}, height: {height}, classID: {bestClassID})\n " +
                          $"x_center: {cpuTensor[0, 0, i]}, y_center: {cpuTensor[0, 1, i]}, width: {cpuTensor[0, 2, i]}, height: {cpuTensor[0, 3, i]}");
                        Debug.Log($"texture2D width: {texture2D.width} height: {texture2D.height}");


                Detection detection = new Detection
                {
                    place=cam.ScreenToWorldPoint(new Vector3(x_center, y_center, cam.nearClipPlane + CalculateDistance(height, 0.15f, cam))), 
                    box = new Rect(x_center - width / 2, y_center - height / 2, width, height),
                    classID = bestClassID, 
                    score = confidence,
                };

                detections.Add(detection);
            }
        }
        //Debug.Log(output);
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
            for (int i = 0; i < currentResult.Length; i++)
            {
                Debug.Log($"best {i} class: {currentResult[i].score}");
            }
        }
        Debug.Log("wasted time: " + (float)(Time.realtimeSinceStartup - start));
        ready = true;
        finished = true;
    }
    async public Task<Detection[]> Detect(Texture2D texture2D) 
    {
        if (runtimeModel == null) return null;
        if (runtimeModel == null) Debug.Log("still no model :( ");
        if (worker == null) Debug.Log("still no worker :( ");
        if (worker == null) return null;
        if (ready) await RunModelInference(texture2D);
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
