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
            Debug.LogError("File not found: " + filePath);
            SendMessageToFlutter("error", "File not found");
            yield break;
        }

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
            Debug.LogError("Failed to load AssetBundle");
            SendMessageToFlutter("error", "Failed to load AssetBundle");
            yield break;
        }

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
            splatRenderer.m_Asset = currentAsset;
            splatRenderer.gameObject.SetActive(true);

            Debug.Log("Model Loaded Successfully!");
            SendMessageToFlutter("loading_completed", "Success");

            // 4. 카메라 위치 자동 조정 (누락되었던 부분 추가!)
            Invoke("ResetCamera", 0.1f);
        }
        else
        {
            Debug.LogError("No GaussianSplatAsset found in bundle");
            SendMessageToFlutter("error", "Invalid Bundle Format");
        }
    }

    // ▼▼▼ 카메라를 모델 중심으로 설정 ▼▼▼
    void ResetCamera()
    {
        var orbitCam = FindObjectOfType<OrbitCamera>();
        if (orbitCam == null)
        {
            Debug.LogWarning("OrbitCamera not found!");
            return;
        }

        if (currentAsset == null || splatRenderer == null)
        {
            Debug.LogWarning("No asset loaded!");
            return;
        }

        // 1. 모델의 bounds 계산
        Vector3 boundsMin = currentAsset.boundsMin;
        Vector3 boundsMax = currentAsset.boundsMax;
        Vector3 center = (boundsMin + boundsMax) * 0.5f;
        Vector3 size = boundsMax - boundsMin;
        float maxSize = Mathf.Max(size.x, size.y, size.z);

        // 2. 모델 Transform 적용 (모델이 회전/이동되었을 경우 대비)
        Transform modelTransform = splatRenderer.transform;
        Vector3 worldCenter = modelTransform.TransformPoint(center);

        // 3. 카메라 거리 계산 (모델 크기의 1.5배)
        float distance = maxSize * 1.5f;

        Debug.Log($"[SplatLoader] Model bounds: min={boundsMin}, max={boundsMax}");
        Debug.Log($"[SplatLoader] Model center: {worldCenter}, size: {size}, distance: {distance}");

        // 4. OrbitCamera 타겟을 모델 중심으로 설정
        orbitCam.SetTarget(modelTransform, center);
        orbitCam.ResetCamera(worldCenter, distance);

        Debug.Log($"[SplatLoader] Camera reset to model center: {worldCenter}");
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