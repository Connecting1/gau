using System;
using System.Collections;
using System.IO;
using UnityEngine;
using GaussianSplatting.Runtime;

public class SplatLoader : MonoBehaviour
{
    [SerializeField] private GaussianSplatRenderer splatRenderer;
    private GaussianSplatAsset currentAsset;
    private AssetBundle currentBundle;

    void Start()
    {
        if (splatRenderer == null)
            splatRenderer = GetComponent<GaussianSplatRenderer>();

        // 1. Unity 준비 완료 신호 전송
        StartCoroutine(NotifyFlutterReady());
    }

    IEnumerator NotifyFlutterReady()
    {
        yield return new WaitForSeconds(0.5f);
        SendMessageToFlutter("unity_ready", "Unity Ready");
    }

    // Flutter에서 호출
    public void LoadModel(string filePath)
    {
        StartCoroutine(LoadBundleCoroutine(filePath));
    }

    IEnumerator LoadBundleCoroutine(string filePath)
    {
        Debug.Log("Loading Bundle from: " + filePath);
        SendMessageToFlutter("loading_started", filePath);

        UnloadCurrentModel();

        if (!File.Exists(filePath))
        {
            Debug.LogError($"[SplatLoader] File not found: {filePath}");
            SendMessageToFlutter("error", "File not found");
            yield break;
        }

        // 파일 정보 로그
        FileInfo fileInfo = new FileInfo(filePath);
        Debug.Log($"[SplatLoader] File size: {fileInfo.Length} bytes");
        Debug.Log($"[SplatLoader] File path: {filePath}");

        // 2. AssetBundle 로드
        var bundleRequest = AssetBundle.LoadFromFileAsync(filePath);
        while (!bundleRequest.isDone)
        {
            SendMessageToFlutter("loading_progress", (bundleRequest.progress * 0.5f).ToString());
            yield return null;
        }

        currentBundle = bundleRequest.assetBundle;
        if (currentBundle == null)
        {
            Debug.LogError($"[SplatLoader] Failed to load AssetBundle from: {filePath}");
            Debug.LogError($"[SplatLoader] File exists: {File.Exists(filePath)}");
            Debug.LogError($"[SplatLoader] Possible reasons: corrupted file, wrong platform, or not a valid AssetBundle");
            SendMessageToFlutter("error", "Failed to load AssetBundle");
            yield break;
        }

        Debug.Log($"[SplatLoader] AssetBundle loaded successfully: {currentBundle.name}");

        // 3. 에셋 로드
        var assetRequest = currentBundle.LoadAllAssetsAsync<GaussianSplatAsset>();
        while (!assetRequest.isDone)
        {
            SendMessageToFlutter("loading_progress", (0.5f + assetRequest.progress * 0.5f).ToString());
            yield return null;
        }

        if (assetRequest.allAssets.Length > 0)
        {
            currentAsset = assetRequest.allAssets[0] as GaussianSplatAsset;

            if (currentAsset == null)
            {
                Debug.LogError("Failed to cast asset to GaussianSplatAsset");
                SendMessageToFlutter("error", "Invalid Asset Type");
                yield break;
            }

            // 바운딩 박스 정보 로그 출력
            Debug.Log($"Asset Bounds: Min={currentAsset.boundsMin}, Max={currentAsset.boundsMax}");
            Debug.Log($"Asset Splat Count: {currentAsset.splatCount}");

            splatRenderer.m_Asset = currentAsset;
            splatRenderer.gameObject.SetActive(true);

            // 렌더러가 완전히 준비될 때까지 한 프레임 대기
            yield return new WaitForEndOfFrame();

            Debug.Log("Model Loaded Successfully!");
            SendMessageToFlutter("loading_completed", "Success");

            // 4. 카메라 위치 자동 조정 - 직접 호출 (Invoke 제거)
            ResetCamera();
        }
        else
        {
            Debug.LogError("No GaussianSplatAsset found in bundle");
            SendMessageToFlutter("error", "Invalid Bundle Format");
        }
    }

    // ▼▼▼ [개선된 함수] 모델 바운딩 박스에 맞춰 카메라 자동 조정 ▼▼▼
    void ResetCamera()
    {
        // 씬에 있는 OrbitCamera를 찾음
        var orbitCam = FindObjectOfType<OrbitCamera>();
        if (orbitCam == null)
        {
            Debug.LogWarning("OrbitCamera not found!");
            return;
        }

        // 현재 로딩된 에셋의 바운딩 박스 정보를 사용
        if (currentAsset != null)
        {
            Vector3 boundsMin = currentAsset.boundsMin;
            Vector3 boundsMax = currentAsset.boundsMax;

            // 바운딩 박스 기반으로 카메라 자동 조정
            // padding 값을 조정하여 모델이 화면에 보이는 크기 조절 가능
            orbitCam.AutoFrameBounds(boundsMin, boundsMax, padding: 1.8f);

            Debug.Log($"Camera Auto-Framed: bounds=({boundsMin} ~ {boundsMax})");
        }
        else
        {
            // 에셋이 없으면 기본 리셋
            orbitCam.ResetCamera();
            Debug.Log("Camera Reset to Default.");
        }
    }

    public void UnloadCurrentModel()
    {
        if (splatRenderer != null) splatRenderer.m_Asset = null;

        if (currentAsset != null)
        {
            Resources.UnloadAsset(currentAsset);
            currentAsset = null;
        }

        if (currentBundle != null)
        {
            currentBundle.Unload(true);
            currentBundle = null;
        }

        Resources.UnloadUnusedAssets();
    }

    void SendMessageToFlutter(string type, string msg)
    {
        string json = $"{{\"type\":\"{type}\",\"message\":\"{msg}\"}}";
        // UnityMessageManager가 없어도 에러 안 나게 안전장치 추가
        if (UnityMessageManager.Instance != null)
        {
            UnityMessageManager.Instance.SendMessageToFlutter(json);
        }
    }
}