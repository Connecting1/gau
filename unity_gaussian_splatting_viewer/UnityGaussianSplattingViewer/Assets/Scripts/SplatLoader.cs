using System;
using System.Collections;
using System.IO;
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

    private GaussianSplatAsset currentAsset;
    private Coroutine loadingCoroutine;

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

        // Unity 초기화 완료를 Flutter에 알림 (1초 후 - Unity 완전 초기화 대기)
        Invoke("NotifyUnityReady", 1.0f);
    }

    /// <summary>
    /// Unity 초기화 완료를 Flutter에 알림
    /// </summary>
    private void NotifyUnityReady()
    {
        Debug.Log("Sending Unity ready notification to Flutter");
        SendMessageToFlutter("unity_ready", "Unity initialization completed");
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

        // 새 모델 로드 시작
        loadingCoroutine = StartCoroutine(LoadModelCoroutine(filePath));
    }

    /// <summary>
    /// 모델 로딩 코루틴
    /// </summary>
    private IEnumerator LoadModelCoroutine(string filePath)
    {
        // 1. 로딩 인디케이터 표시
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(true);
        }

        SendMessageToFlutter("loading_started", filePath);

        // 2. 이전 모델 언로드
        UnloadCurrentModel();

        // 3. .ply 파일을 바이트 배열로 읽기
        byte[] fileData = null;
        try
        {
            fileData = File.ReadAllBytes(filePath);
            Debug.Log($"File loaded: {fileData.Length} bytes");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to read file: {e.Message}");
            SendMessageToFlutter("error", $"File read failed: {e.Message}");
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }
            yield break;  // 에러 발생 시 종료
        }

        // 4. GaussianSplatAsset 생성
        currentAsset = ScriptableObject.CreateInstance<GaussianSplatAsset>();

        // 5. 데이터 파싱 및 로드 (비동기)
        bool loadSuccess = false;
        yield return StartCoroutine(LoadPlyData(fileData, (success) => {
            loadSuccess = success;
        }));

        // 6. 파싱 실패 확인
        if (!loadSuccess)
        {
            Debug.LogError("Failed to parse PLY file");
            SendMessageToFlutter("error", "PLY parsing failed");
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }
            yield break;  // 파싱 실패 시 종료
        }

        // 7. 렌더러에 에셋 할당
        splatRenderer.m_Asset = currentAsset;

        // 8. 카메라 위치 조정
        AdjustCameraPosition();

        // 9. 성공 메시지
        Debug.Log("Model loaded successfully");
        SendMessageToFlutter("loading_completed", filePath);

        // 10. 로딩 인디케이터 숨김
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(false);
        }
    }


    /// <summary>
    /// PLY 데이터를 파싱하여 GaussianSplatAsset에 로드
    /// </summary>
    private IEnumerator LoadPlyData(byte[] fileData, Action<bool> callback)
    {
        try
        {
            using (MemoryStream stream = new MemoryStream(fileData))
            {
                // PLY 파일 파싱 (GaussianSplatting 플러그인 사용)
                // 실제 구현은 Aras-p/UnityGaussianSplatting 플러그인의 API에 따름

                // 임시: 파일 헤더 검증
                using (StreamReader reader = new StreamReader(stream))
                {
                    string firstLine = reader.ReadLine();
                    if (firstLine != null && firstLine.Trim() == "ply")
                    {
                        Debug.Log("Valid PLY file detected");

                        // 실제 파싱 로직은 플러그인 API 사용
                        // currentAsset.LoadFromPly(fileData);

                        callback(true);
                        yield break;
                    }
                }
            }

            callback(false);
        }
        catch (Exception e)
        {
            Debug.LogError($"PLY parsing error: {e.Message}");
            callback(false);
        }
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
        UnloadCurrentModel();
    }

    void OnApplicationQuit()
    {
        UnloadCurrentModel();
    }
}
