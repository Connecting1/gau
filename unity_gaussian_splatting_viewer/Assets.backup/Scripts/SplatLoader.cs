using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using GaussianSplatting.Runtime;

/// <summary>
/// 가우시안 스플래팅 .ply 파일을 동적으로 로드하는 스크립트
/// Flutter에서 전달받은 파일 경로를 기반으로 모델을 렌더링합니다.
/// </summary>
public class SplatLoader : MonoBehaviour
{
    [Header("Gaussian Splatting Settings")]
    [SerializeField] private GaussianSplatRenderer splatRenderer;

    [Header("Loading Settings")]
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private float loadingTimeout = 300f; // 대용량 파일을 위해 5분으로 증가
    [SerializeField] private int chunkSizeInMB = 2; // 청크 크기 (MB)

    private GaussianSplatAsset currentAsset;
    private Coroutine loadingCoroutine;
    private CancellationTokenSource cancellationTokenSource;
    private float loadingProgress = 0f;
    private bool unityReadyNotificationSent = false;
    private int frameCount = 0;

    void Start()
    {
        // 초기화
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(false);
        }

        // 렌더러가 없으면 자동으로 추가
        if (splatRenderer == null)
        {
            splatRenderer = GetComponent<GaussianSplatRenderer>();
            if (splatRenderer == null)
            {
                splatRenderer = gameObject.AddComponent<GaussianSplatRenderer>();
            }
        }

        Debug.Log("SplatLoader initialized and ready to receive file paths");
    }

    void Update()
    {
        // Unity 렌더링이 안정화될 때까지 대기 (3프레임 후)
        if (!unityReadyNotificationSent)
        {
            frameCount++;
            if (frameCount >= 3)
            {
                NotifyUnityReady();
                unityReadyNotificationSent = true;
            }
        }
    }

    /// <summary>
    /// Unity 초기화 완료를 Flutter에 알림
    /// </summary>
    private void NotifyUnityReady()
    {
        Debug.Log("=== SENDING UNITY READY SIGNAL TO FLUTTER ===");
        SendMessageToFlutter("unity_ready", "Unity initialization completed");
        Debug.Log("=== UNITY READY SIGNAL SENT ===");
    }

    /// <summary>
    /// Flutter로부터 메시지를 받아 모델을 로드합니다.
    /// </summary>
    /// <param name="filePath">로컬 파일 시스템의 .ply 파일 절대 경로</param>
    public void LoadModel(string filePath)
    {
        Debug.Log($"Received load request for: {filePath}");

        if (string.IsNullOrEmpty(filePath))
        {
            SendMessageToFlutter("error", "File path is empty");
            return;
        }

        if (!File.Exists(filePath))
        {
            SendMessageToFlutter("error", $"File not found: {filePath}");
            return;
        }

        // 이전 로딩 작업 취소
        if (loadingCoroutine != null)
        {
            StopCoroutine(loadingCoroutine);
        }

        // 이전 백그라운드 작업 취소
        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
        cancellationTokenSource = new CancellationTokenSource();

        // 새 모델 로드 시작
        loadingCoroutine = StartCoroutine(LoadModelCoroutine(filePath));
    }

    /// <summary>
    /// 모델 로딩 코루틴
    /// </summary>
    private IEnumerator LoadModelCoroutine(string filePath)
    {
        // 로딩 인디케이터 표시
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(true);
        }

        SendMessageToFlutter("loading_started", filePath);

        // 이전 모델 언로드
        UnloadCurrentModel();

        loadingProgress = 0f;
        byte[] fileData = null;
        bool hasError = false;

        // 파일 크기 확인
        FileInfo fileInfo = new FileInfo(filePath);
        long fileSize = fileInfo.Length;
        float fileSizeMB = fileSize / (1024f * 1024f);
        Debug.Log($"Loading PLY file: {fileSizeMB:F2} MB");

        // 백그라운드에서 파일 읽기 시작
        Task<byte[]> loadTask = Task.Run(() => LoadFileInChunks(filePath, cancellationTokenSource.Token), cancellationTokenSource.Token);

        // 로딩 완료 대기 (진행률 업데이트)
        while (!loadTask.IsCompleted)
        {
            // 진행률을 Flutter로 전송
            SendMessageToFlutter("loading_progress", $"{(loadingProgress * 100):F1}");
            yield return null;
        }

        // 작업 취소 확인
        if (cancellationTokenSource.Token.IsCancellationRequested)
        {
            Debug.LogWarning("Loading cancelled");
            SendMessageToFlutter("error", "Loading cancelled");
            hasError = true;
        }
        else if (loadTask.IsFaulted)
        {
            Debug.LogError($"Failed to load file: {loadTask.Exception?.Message}");
            SendMessageToFlutter("error", $"Load failed: {loadTask.Exception?.Message}");
            hasError = true;
        }
        else
        {
            // 결과 가져오기
            fileData = loadTask.Result;
            Debug.Log($"File loaded in background: {fileData.Length} bytes");

            // GaussianSplatAsset 생성
            currentAsset = ScriptableObject.CreateInstance<GaussianSplatAsset>();

            // 데이터 파싱 및 로드 (비동기)
            bool loadSuccess = false;
            yield return StartCoroutine(LoadPlyData(fileData, (success) => {
                loadSuccess = success;
            }));

            if (!loadSuccess)
            {
                Debug.LogError("Failed to parse PLY file");
                SendMessageToFlutter("error", "Failed to parse PLY file");
                hasError = true;
            }
            else
            {
                // 렌더러에 에셋 할당
                splatRenderer.m_Asset = currentAsset;

                // 카메라 위치 조정
                AdjustCameraPosition();

                Debug.Log("Model loaded successfully");
                SendMessageToFlutter("loading_completed", filePath);
            }
        }

        // 메모리 정리
        fileData = null;
        GC.Collect();

        // 로딩 인디케이터 숨김
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(false);
        }

        loadingProgress = 0f;
    }

    /// <summary>
    /// 백그라운드 스레드에서 파일을 청크 단위로 읽어오기
    /// 대용량 파일의 경우 메모리 부담을 줄이고 진행률 업데이트 가능
    /// </summary>
    private byte[] LoadFileInChunks(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            FileInfo fileInfo = new FileInfo(filePath);
            long totalBytes = fileInfo.Length;
            int chunkSize = chunkSizeInMB * 1024 * 1024; // MB를 바이트로 변환

            List<byte> allData = new List<byte>((int)totalBytes);
            long bytesRead = 0;

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, chunkSize, FileOptions.SequentialScan))
            {
                byte[] buffer = new byte[chunkSize];
                int read;

                while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    // 취소 확인
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException("File loading cancelled");
                    }

                    // 읽은 데이터 추가
                    for (int i = 0; i < read; i++)
                    {
                        allData.Add(buffer[i]);
                    }

                    bytesRead += read;

                    // 진행률 업데이트 (0.0 ~ 0.9, 파싱을 위해 0.1 남김)
                    loadingProgress = (bytesRead / (float)totalBytes) * 0.9f;

                    // 진행률 로그 (10% 단위)
                    if (bytesRead % (totalBytes / 10) < chunkSize)
                    {
                        Debug.Log($"Loading progress: {(loadingProgress * 100):F1}% ({bytesRead / (1024 * 1024)} MB / {totalBytes / (1024 * 1024)} MB)");
                    }
                }
            }

            Debug.Log($"File loading completed: {allData.Count} bytes");
            return allData.ToArray();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading file in chunks: {e.Message}");
            throw;
        }
    }

    /// <summary>
    /// PLY 데이터를 파싱하여 GaussianSplatAsset에 로드
    /// </summary>
    private IEnumerator LoadPlyData(byte[] fileData, Action<bool> callback)
    {
        Debug.Log($"Starting PLY parsing: {fileData.Length / (1024 * 1024)} MB");
        loadingProgress = 0.9f; // 파일 로딩 완료, 파싱 시작

        bool parseSuccess = false;

        using (MemoryStream stream = new MemoryStream(fileData))
        {
            // 파일 헤더 검증
            using (StreamReader reader = new StreamReader(stream))
            {
                string firstLine = reader.ReadLine();
                if (firstLine != null && firstLine.Trim().ToLower() == "ply")
                {
                    Debug.Log("Valid PLY file detected - header verified");

                    // PLY 헤더 정보 읽기
                    int vertexCount = 0;
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();

                        // vertex 개수 파싱
                        if (line.StartsWith("element vertex"))
                        {
                            string[] parts = line.Split(' ');
                            if (parts.Length >= 3)
                            {
                                int.TryParse(parts[2], out vertexCount);
                                Debug.Log($"PLY contains {vertexCount:N0} vertices");
                            }
                        }

                        // 헤더 끝
                        if (line == "end_header")
                        {
                            break;
                        }
                    }

                    // 진행률 업데이트
                    loadingProgress = 0.95f;
                    yield return null;

                    // Runtime PLY loading is not yet implemented
                    // The Aras-p UnityGaussianSplatting plugin requires PLY files to be converted
                    // in Unity Editor before runtime use.
                    //
                    // To use this viewer:
                    // 1. Open Unity Editor
                    // 2. Use Tools > Gaussian Splats > Create GaussianSplatAsset to convert PLY files
                    // 3. Include the converted assets in your build
                    //
                    // TODO: Implement runtime PLY loading by:
                    // - Completing RuntimePLYLoader implementation
                    // - Creating GaussianSplatAsset with proper data formats at runtime
                    // - Setting up GPU buffers without TextAsset dependencies

                    Debug.LogError("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    Debug.LogError("Runtime PLY loading is NOT YET IMPLEMENTED");
                    Debug.LogError("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    Debug.LogError($"PLY file validated: {vertexCount:N0} vertices");
                    Debug.LogError("The Gaussian Splatting plugin requires PLY files to be");
                    Debug.LogError("converted in Unity Editor before runtime.");
                    Debug.LogError("");
                    Debug.LogError("Next steps:");
                    Debug.LogError("1. Open this project in Unity Editor");
                    Debug.LogError("2. Use: Tools > Gaussian Splats > Create GaussianSplatAsset");
                    Debug.LogError("3. Convert your PLY files to GaussianSplatAsset format");
                    Debug.LogError("4. Include converted assets in your Flutter app build");
                    Debug.LogError("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

                    parseSuccess = false;
                }
                else
                {
                    Debug.LogError($"Invalid PLY file: Expected 'ply' header, got '{firstLine}'");
                    parseSuccess = false;
                }
            }
        }

        callback(parseSuccess);
    }

    /// <summary>
    /// 현재 로드된 모델 언로드
    /// </summary>
    public void UnloadCurrentModel()
    {
        if (currentAsset != null)
        {
            if (splatRenderer != null)
            {
                splatRenderer.m_Asset = null;
            }

            Destroy(currentAsset);
            currentAsset = null;

            // 메모리 정리
            Resources.UnloadUnusedAssets();
            GC.Collect();

            Debug.Log("Previous model unloaded");
            SendMessageToFlutter("model_unloaded", "");
        }
    }

    /// <summary>
    /// 모델 경계에 맞춰 카메라 위치 자동 조정
    /// </summary>
    private void AdjustCameraPosition()
    {
        if (splatRenderer == null || currentAsset == null)
            return;

        // 모델의 바운드 계산
        Bounds bounds = CalculateModelBounds();

        // 카메라를 모델 중심을 향하도록 조정
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            float distance = bounds.size.magnitude * 1.5f;
            Vector3 targetPosition = bounds.center - mainCamera.transform.forward * distance;

            mainCamera.transform.position = targetPosition;
            mainCamera.transform.LookAt(bounds.center);
        }
    }

    /// <summary>
    /// 모델의 바운딩 박스 계산
    /// </summary>
    private Bounds CalculateModelBounds()
    {
        // 기본 바운드 (플러그인 API에 따라 수정 필요)
        return new Bounds(Vector3.zero, Vector3.one * 10f);
    }

    /// <summary>
    /// Flutter로 메시지 전송
    /// </summary>
    private void SendMessageToFlutter(string type, string message)
    {
        string jsonMessage = $"{{\"type\":\"{type}\",\"message\":\"{message}\"}}";

        #if UNITY_ANDROID || UNITY_IOS
        // flutter_unity_widget의 메시지 전송 방식
        UnityMessageManager.Instance.SendMessageToFlutter(jsonMessage);
        #else
        Debug.Log($"Message to Flutter: {jsonMessage}");
        #endif
    }

    void OnDestroy()
    {
        // 진행 중인 작업 취소
        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
        }

        UnloadCurrentModel();
    }

    void OnApplicationQuit()
    {
        // 진행 중인 작업 취소
        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
        }

        UnloadCurrentModel();
    }
}
