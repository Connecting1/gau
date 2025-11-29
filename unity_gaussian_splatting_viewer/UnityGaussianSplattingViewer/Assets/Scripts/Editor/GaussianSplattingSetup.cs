#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using GaussianSplatting.Runtime;

/// <summary>
/// Unity Editor에서 Gaussian Splatting Renderer를 자동으로 설정하는 유틸리티
/// </summary>
public class GaussianSplattingSetup : MonoBehaviour
{
    [MenuItem("Tools/Gaussian Splats/Setup Renderer in Scene")]
    public static void SetupRendererInScene()
    {
        // 1. GameObject 생성
        GameObject splatObject = new GameObject("GaussianSplatRenderer");

        // 2. Renderer 컴포넌트 추가
        GaussianSplatRenderer renderer = splatObject.AddComponent<GaussianSplatRenderer>();

        // 3. 필수 Shader 및 Compute Shader 자동 할당
        renderer.m_ShaderSplats = Shader.Find("GaussianSplatting/RenderGaussianSplats");
        renderer.m_ShaderComposite = Shader.Find("GaussianSplatting/GaussianComposite");
        renderer.m_ShaderDebugPoints = Shader.Find("GaussianSplatting/GaussianDebugRenderPoints");
        renderer.m_ShaderDebugBoxes = Shader.Find("GaussianSplatting/GaussianDebugRenderBoxes");

        // Compute Shader 찾기
        string[] computeShaderGuids = AssetDatabase.FindAssets("SplatUtilities t:ComputeShader");
        if (computeShaderGuids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(computeShaderGuids[0]);
            renderer.m_CSSplatUtilities = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
        }

        // 4. 씬에서 선택
        Selection.activeGameObject = splatObject;

        // 5. 사용자에게 안내
        Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Debug.Log("GaussianSplatRenderer가 씬에 추가되었습니다!");
        Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Debug.Log("");
        Debug.Log("다음 단계:");
        Debug.Log("1. Inspector에서 'm_Asset' 필드에 GaussianSplatAsset을 드래그하세요");
        Debug.Log("2. Tools > Gaussian Splats > Create GaussianSplatAsset으로 PLY 파일을 변환하세요");
        Debug.Log("3. Play 버튼을 눌러 테스트하세요");
        Debug.Log("");
        Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        EditorUtility.DisplayDialog(
            "Gaussian Splatting Setup",
            "GaussianSplatRenderer가 씬에 추가되었습니다!\n\n" +
            "Inspector에서 'm_Asset' 필드에 GaussianSplatAsset을 할당하세요.\n\n" +
            "아직 Asset이 없다면:\n" +
            "Tools > Gaussian Splats > Create GaussianSplatAsset을 사용하여 PLY 파일을 변환하세요.",
            "확인"
        );
    }

    [MenuItem("Tools/Gaussian Splats/Validate Scene Setup")]
    public static void ValidateSceneSetup()
    {
        GaussianSplatRenderer[] renderers = FindObjectsOfType<GaussianSplatRenderer>();

        if (renderers.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "씬 검증",
                "씬에 GaussianSplatRenderer가 없습니다.\n\n" +
                "Tools > Gaussian Splats > Setup Renderer in Scene를 사용하여 추가하세요.",
                "확인"
            );
            return;
        }

        bool allValid = true;
        System.Text.StringBuilder issues = new System.Text.StringBuilder();

        foreach (var renderer in renderers)
        {
            if (renderer.m_Asset == null)
            {
                allValid = false;
                issues.AppendLine($"- {renderer.gameObject.name}: m_Asset가 할당되지 않음");
            }

            if (renderer.m_ShaderSplats == null)
            {
                allValid = false;
                issues.AppendLine($"- {renderer.gameObject.name}: m_ShaderSplats가 할당되지 않음");
            }

            if (renderer.m_CSSplatUtilities == null)
            {
                allValid = false;
                issues.AppendLine($"- {renderer.gameObject.name}: m_CSSplatUtilities가 할당되지 않음");
            }
        }

        if (allValid)
        {
            EditorUtility.DisplayDialog(
                "씬 검증 성공",
                $"모든 {renderers.Length}개의 GaussianSplatRenderer가 올바르게 설정되었습니다!",
                "확인"
            );
            Debug.Log($"✅ 씬 검증 성공: {renderers.Length}개의 Renderer 정상");
        }
        else
        {
            EditorUtility.DisplayDialog(
                "씬 검증 실패",
                $"일부 설정이 누락되었습니다:\n\n{issues}",
                "확인"
            );
            Debug.LogError($"❌ 씬 검증 실패:\n{issues}");
        }
    }

    [MenuItem("Tools/Gaussian Splats/Open Documentation")]
    public static void OpenDocumentation()
    {
        string docPath = Application.dataPath + "/../GAUSSIAN_SPLATTING_SETUP.md";
        if (System.IO.File.Exists(docPath))
        {
            Application.OpenURL("file://" + docPath);
        }
        else
        {
            EditorUtility.DisplayDialog(
                "문서 없음",
                "GAUSSIAN_SPLATTING_SETUP.md 파일을 찾을 수 없습니다.",
                "확인"
            );
        }
    }
}
#endif
