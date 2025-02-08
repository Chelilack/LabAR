using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;
using TMPro;
using TensorFlowLite;
using UnityEngine.Networking;

public struct Detection
{
    public Vector3 place;
    public Rect box;        // Ограничивающий прямоугольник убрать как доделаю!!!!!!!!!!!!!!!!!!
    public int classID;     // Класс обнаруженного объекта
    public float score;     // Уверенность (вероятность) для объекта
}

public class detection : MonoBehaviour
{
    [SerializeField] private string modelName = "unityDetection";
    [SerializeField] private string labelmapName = "unityLabelmap";
    public bool ready = true;
    public Texture2D example;
    private string modelPath;
    private string labelPath;
    private byte[] model =null;
    private Color[] originalPixels;
    public Dictionary<int, string> labelDict;
    private Interpreter interpreter;
    [SerializeField] private InterpreterOptions interpreterOption;

    Rect currBox = new Rect();
    Rect photoRect = new Rect();
    bool showBox = true;
    public Camera cam;
    public GameObject box;
    public bool worldPosition;
    public float objectHeight=0.06f;
    private Texture2D currentImage;

    [SerializeField] private TextMeshProUGUI detectionText; // Поле для TextMeshProUGUI

    void Awake()
    {
        StartCoroutine(LoadPathsAndInitialize());
    }

    IEnumerator LoadPathsAndInitialize()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        labelPath = Path.Combine(Application.streamingAssetsPath, labelmapName + ".txt");
        modelPath = Path.Combine(Application.streamingAssetsPath, modelName + ".tflite");

        yield return StartCoroutine(LoadTextFileAndroid(labelPath, (result) => {
            labelDict = LoadLabelsFromText(result);
            Debug.Log("Loaded labels: " + result);
        }));

        yield return StartCoroutine(LoadBinaryFileAndroid(modelPath, (result) => {
            InitializeInterpreter(result);
            model=result;
            if (model !=null) Debug.Log("NICEEE");
        }));
#else
        labelPath = Path.Combine(Application.streamingAssetsPath, labelmapName + ".txt");
        modelPath = Path.Combine(Application.streamingAssetsPath, modelName + ".tflite");

        Debug.Log("Label Path: " + labelPath);
        Debug.Log("Model Path: " + modelPath);

        if (File.Exists(labelPath))
        {
            string labelText = File.ReadAllText(labelPath);
            labelDict = LoadLabelsFromText(labelText);
            Debug.Log("Loaded labels: " + labelText);
        }
        else
        {
            Debug.LogError("Label file does not exist at path: " + labelPath);
            yield break;
        }

        if (File.Exists(modelPath))
        {
            model = File.ReadAllBytes(modelPath);
            Debug.Log("Loaded model data");

            InitializeInterpreter(model);
        }
        else
        {
            Debug.LogError("Model file does not exist at path: " + modelPath);
            yield break;
        }
#endif
    }

    IEnumerator LoadTextFileAndroid(string path, System.Action<string> callback)
    {
        using (UnityWebRequest www = UnityWebRequest.Get(path))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error loading file: " + www.error);
                callback(null);
            }
            else
            {
                callback(www.downloadHandler.text);
            }
        }
    }

    IEnumerator LoadBinaryFileAndroid(string path, System.Action<byte[]> callback)
    {
        using (UnityWebRequest www = UnityWebRequest.Get(path))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error loading file: " + www.error);
                callback(null);
            }
            else
            {
                callback(www.downloadHandler.data);
            }
        }
    }

    private void InitializeInterpreter(byte[] model)
    {
        if (model == null)
        {
            Debug.LogError("Failed to load model data");
            return;
        }

        var options = new InterpreterOptions()
        {
            threads = 8,
            useNNAPI = true,
        };

        interpreter = new Interpreter(model, options);
        interpreter.AllocateTensors();
        Debug.Log("Model and labels are loaded and interpreter is initialized.");
    }
    private Interpreter InitializeNewInterpreter(byte[] model)
    {
        if (model == null)
        {
            Debug.LogError("Failed to load model data");
            return null;
        }

        var options = new InterpreterOptions()
        {
            threads = 8,
            useNNAPI = true,
        };

        Interpreter newInterpreter = new Interpreter(model, options);
        newInterpreter.AllocateTensors();
        Debug.Log("Model and labels are loaded and interpreter is initialized.");
        return newInterpreter;
    }
    private Dictionary<int, string> LoadLabelsFromText(string text)
    {
        Dictionary<int, string> labels = new Dictionary<int, string>();
        string[] lines = text.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            labels[i] = lines[i].Trim();
        }

        return labels;
    }

    public Detection[] Detect(Texture2D image) // менять
    {
        ready = false;
        currentImage = image;
        Interpreter localInterpreter;
        if (model != null)
        {
            localInterpreter = InitializeNewInterpreter(model);
        }
        else 
        {
           Debug.Log("try = interpreter");
           localInterpreter = interpreter;
        }
        if (localInterpreter == null) Debug.LogError("localInterpreter is null (((( ");
        localInterpreter.AllocateTensors();
        //byte[,,,] inputTensor = TransformInput(image,320,320); 
        float[,,,] inputTensor = TransformInputFloat(image,320,320);
        localInterpreter.SetInputTensorData(0, inputTensor);
        localInterpreter.Invoke();
       
        float[,,] boxes = new float[1, 10, 4];
        float[,] classes = new float[1, 10];
        float[,] scores = new float[1, 10];
        float[] numDetections = new float[1];
        if (modelName!="detection")
        {
            Debug.Log("not detection tflite");
            localInterpreter.GetOutputTensorData(1, boxes);
            localInterpreter.GetOutputTensorData(3, classes);
            localInterpreter.GetOutputTensorData(0, scores);
            localInterpreter.GetOutputTensorData(2, numDetections);
        }
        else
        {
            Debug.Log("is detection tflite");
            localInterpreter.GetOutputTensorData(0, boxes);
            localInterpreter.GetOutputTensorData(1, classes);
            localInterpreter.GetOutputTensorData(2, scores);
            localInterpreter.GetOutputTensorData(3, numDetections); 
        }

        int detectedCount = (int)numDetections[0];
        Detection[] detections = new Detection[detectedCount];
        Debug.Log("scores: " + scores[0, 0]);
        Debug.Log("found: " + numDetections[0]);
        Debug.Log("classes: " + classes[0, 0]);
        for (int i = 0; i < detectedCount && scores[0, i] > 0.6f; i++)
        {

            float ymin = boxes[0, i, 0] * image.height;
            float xmin = boxes[0, i, 1] * image.width;
            float ymax = boxes[0, i, 2] * image.height;
            float xmax = boxes[0, i, 3] * image.width;

            detections[i] = new Detection
            {
                box = new Rect(xmin, image.height - ymax, xmax - xmin, ymax - ymin),
                classID = (int)classes[0, i],
                score = scores[0, i]
            };
        }
        

        //interpreter = null;
        //InitializeInterpreter(model);

        Debug.Log("end DETECT: "+ detections.Length );
        localInterpreter.Dispose();
        ready = true;
        return detections;
    }
    public void PrintDetections(Detection[] detections) //менять
    {
        string detectedObjectsText = "^+^";
        float maxScore = 0;
        Detection bestDetection =new Detection();
        if (detections == null) return;
        foreach (var detection in detections)
        {
            if (detection.score != 0 && detection.score > maxScore)
            {
                string detectedObjectInfo = $"Object detected: \n" +
                           $"Class ID: {labelDict[detection.classID]} \n" +
                           $"Score: {detection.score} \n" +
                           $"Box Coordinates: ({detection.box.xMin}, {detection.box.yMin}, {detection.box.width}, {detection.box.height})";
                Debug.Log(detectedObjectInfo);

                detectedObjectsText = detectedObjectInfo;

                bestDetection = detection;
            }
            if (bestDetection.box != null)
            { 
                //DrawBox(bestDetection.box, 0.06f * 1f / bestDetection.box.width); 
                DrawBox(bestDetection.box, CalculateDistance(bestDetection.box.height,objectHeight,cam)); 
                Debug.Log("GG: " + CalculateDistance(bestDetection.box.height, objectHeight, cam)+"H: "+(float)currentImage.height +" W: "+(float)currentImage.width); 
            }
            //(realSizeMeters * focusDistance) / screenSizePixels
        }

        if (detectionText != null)
        {
            detectionText.text = detectedObjectsText;
        }
    }
    
    byte[,,,] TransformInput(Texture2D image , int newWidth = 300 , int newHeight = 300)
    {
        Texture2D resizedImage = ResizeTexture(image, newWidth, newHeight);

        byte[,,,] inputTensor = new byte[1, newWidth, newHeight, 3];
        for (int y = 0; y < resizedImage.height; y++)
        {
            for (int x = 0; x < resizedImage.width; x++)
            {
                Color pixel = resizedImage.GetPixel(x, y);
                inputTensor[0, y, x, 0] = (byte)(pixel.r * 255);
                inputTensor[0, y, x, 1] = (byte)(pixel.g * 255);
                inputTensor[0, y, x, 2] = (byte)(pixel.b * 255);
            }
        }
        return inputTensor;
    }
    float[,,,] TransformInputFloat(Texture2D image , int newWidth = 640 , int newHeight = 640) // менять
    {
        Texture2D resizedImage = ResizeTexture(image, newWidth, newHeight);
        SaveTextureAsPNG(resizedImage, "resizedImage.png");
        float[,,,] inputTensor = new float[1, newWidth, newHeight, 3];
        for (int y = 0; y < resizedImage.height; y++)
        {
            for (int x = 0; x < resizedImage.width; x++)
            {
                Color pixel = resizedImage.GetPixel(x, y);
                inputTensor[0, y, x, 0] = (float)(pixel.r);
                inputTensor[0, y, x, 1] = (float)(pixel.g);
                inputTensor[0, y, x, 2] = (float)(pixel.b);
            }
        }
        return inputTensor;
    }



    Texture2D ResizeTexture(Texture2D source, int newWidth, int newHeight)
    {
        RenderTexture rt = new RenderTexture(newWidth, newHeight, 24);
        RenderTexture.active = rt;
        Graphics.Blit(source, rt);
        Texture2D result = new Texture2D(newWidth, newHeight, source.format, false);
        result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
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

    public void DrawBox(Rect rect,float depth)
    {
        Vector3 leftBottom, leftTop, rightBottom, rightTop, center;
        //convert to world point, then screen space
        //verts[i] = cam.WorldToScreenPoint(transform.TransformPoint(verts[i]));
        if (worldPosition)
        {
            leftBottom = cam.ScreenToWorldPoint(new Vector3(rect.xMin, rect.yMin, cam.nearClipPlane));
            leftTop = cam.ScreenToWorldPoint(new Vector3(rect.xMin, rect.yMax, cam.nearClipPlane));
            rightBottom = cam.ScreenToWorldPoint(new Vector3(rect.xMax, rect.yMin, cam.nearClipPlane));
            rightTop = cam.ScreenToWorldPoint(new Vector3(rect.xMax, rect.yMax, cam.nearClipPlane));
            center = cam.ScreenToWorldPoint(new Vector3(rect.center.x*(float)Screen.width/currentImage.width, rect.center.y * (float)Screen.height / currentImage.height, depth));
            Debug.Log($"BB centerY: {rect.center.y} minY: {rect.yMin} maxY: {rect.yMax}" );
            Instantiate(box, center, Quaternion.identity);
            
        }
        else
        {
            Instantiate(box, new Vector3(rect.center.x, rect.center.y, 1f), Quaternion.identity);
        }
    }
    float CalculateDistance(float screenSizePixels, float realSizeMeters, Camera camera)
    {
       
        float verticalFOV = camera.fieldOfView * Mathf.Deg2Rad;       
        float screenHeightAtOneMeter = 2.0f * Mathf.Tan(verticalFOV / 2.0f);
        float relativeScreenHeight = screenSizePixels / (float)currentImage.height;
        float distance = realSizeMeters / (relativeScreenHeight * screenHeightAtOneMeter);
        return distance;
    }

    void OnDestroy()
    {
        interpreter?.Dispose();
    }
}
