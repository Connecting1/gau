#if UNITY_EDITOR
using UnityEditor;
using System.IO;

public class BuildAssetBundles
{
    [MenuItem("Assets/Build AssetBundles (Android)")]
    static void BuildBundles()
    {
        string assetBundleDirectory = "Assets/AssetBundles";
        if (!Directory.Exists(assetBundleDirectory))
        {
            Directory.CreateDirectory(assetBundleDirectory);
        }

        // 안드로이드용으로 빌드
        BuildPipeline.BuildAssetBundles(assetBundleDirectory,
                                        BuildAssetBundleOptions.None,
                                        BuildTarget.Android);

        EditorUtility.DisplayDialog("완료", "AssetBundle 빌드가 완료되었습니다!\nAssets/AssetBundles 폴더를 확인하세요.", "확인");
    }
}
#endif