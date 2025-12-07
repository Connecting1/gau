using UnityEngine;
using System.Runtime.InteropServices;

/// <summary>
/// Flutter와 Unity 간 메시지 통신을 관리하는 싱글톤 매니저
/// flutter_unity_widget과 연동
/// </summary>
public class UnityMessageManager : MonoBehaviour
{
    private static UnityMessageManager _instance;
    public static UnityMessageManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("UnityMessageManager");
                _instance = go.AddComponent<UnityMessageManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Flutter로 메시지 전송
    /// </summary>
    public void SendMessageToFlutter(string message)
    {
        Debug.Log($"[Unity -> Flutter] Attempting to send: {message}");

        #if UNITY_ANDROID && !UNITY_EDITOR
        SendMessageToFlutterAndroid(message);
        #elif UNITY_IOS && !UNITY_EDITOR
        SendMessageToFlutterIOS(message);
        #else
        Debug.Log($"[Unity -> Flutter] (Editor mode) {message}");
        #endif
    }

    /// <summary>
    /// Android 플랫폼에서 Flutter로 메시지 전송
    /// </summary>
    private void SendMessageToFlutterAndroid(string message)
    {
        #if UNITY_ANDROID
        try
        {
            Debug.Log("[Unity -> Flutter Android] Sending message via JNI");
            using (AndroidJavaClass jc = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                using (AndroidJavaObject jo = jc.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    jo.Call("onUnityMessage", message);
                    Debug.Log("[Unity -> Flutter Android] Message sent successfully");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Unity -> Flutter Android] Failed to send message: {e.Message}");
        }
        #endif
    }

    /// <summary>
    /// iOS 플랫폼에서 Flutter로 메시지 전송
    /// </summary>
    #if UNITY_IOS
    [DllImport("__Internal")]
    private static extern void onUnityMessage(string message);
    #endif

    private void SendMessageToFlutterIOS(string message)
    {
        #if UNITY_IOS
        onUnityMessage(message);
        #endif
    }

    /// <summary>
    /// Flutter로부터 메시지 수신 (flutter_unity_widget에서 호출)
    /// </summary>
    public void OnFlutterMessage(string message)
    {
        Debug.Log($"[Flutter -> Unity] Received: {message}");

        try
        {
            // JSON 파싱
            FlutterMessage flutterMsg = JsonUtility.FromJson<FlutterMessage>(message);

            // 메시지 타입별 처리
            HandleFlutterMessage(flutterMsg);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to parse Flutter message: {e.Message}");
        }
    }

    /// <summary>
    /// Flutter 메시지 처리
    /// </summary>
    private void HandleFlutterMessage(FlutterMessage message)
    {
        switch (message.type)
        {
            case "load_model":
                HandleLoadModel(message.data);
                break;

            case "unload_model":
                HandleUnloadModel();
                break;

            case "reset_camera":
                HandleResetCamera();
                break;

            case "set_zoom":
                HandleSetZoom(message.data);
                break;

            default:
                Debug.LogWarning($"Unknown message type: {message.type}");
                break;
        }
    }

    /// <summary>
    /// 모델 로드 처리
    /// </summary>
    private void HandleLoadModel(string filePath)
    {
        SplatLoader loader = FindObjectOfType<SplatLoader>();
        if (loader != null)
        {
            loader.LoadModel(filePath);
        }
        else
        {
            Debug.LogError("SplatLoader not found in scene");
        }
    }

    /// <summary>
    /// 모델 언로드 처리
    /// </summary>
    private void HandleUnloadModel()
    {
        SplatLoader loader = FindObjectOfType<SplatLoader>();
        if (loader != null)
        {
            loader.UnloadCurrentModel();
        }
    }

    /// <summary>
    /// 카메라 리셋 처리
    /// </summary>
    private void HandleResetCamera()
    {
        OrbitCamera camera = FindObjectOfType<OrbitCamera>();
        if (camera != null)
        {
            camera.ResetCamera();
        }
    }

    /// <summary>
    /// 줌 레벨 설정 처리
    /// </summary>
    private void HandleSetZoom(string zoomData)
    {
        if (float.TryParse(zoomData, out float zoom))
        {
            OrbitCamera camera = FindObjectOfType<OrbitCamera>();
            if (camera != null)
            {
                camera.SetZoomDistance(zoom);
            }
        }
    }
}

/// <summary>
/// Flutter에서 전송되는 메시지 구조
/// </summary>
[System.Serializable]
public class FlutterMessage
{
    public string type;
    public string data;
}
