#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class LightmapCombine : ScriptableWizard 
{
    private const string SHADER_NAME_LIGHTMAPCOMBINE = "LightmapCombine";

    [MenuItem("SonTools/LightmapCombine")]
    private static void Open()
    {
        DisplayWizard<LightmapCombine>("Lightmap Combine", "Create");
    }

    private void OnWizardCreate()
    {   
        // シーン内のオブジェクト取得
        var objectsWithLightmap = GetObjectsWithLightmap();
        
        foreach (var obj in objectsWithLightmap)
        {   
            var meshRenderer = obj.GetComponent<MeshRenderer>();
            foreach (var material in meshRenderer.sharedMaterials)
            {
                // 合成用一時マテリアルを作成
                var tempMaterial = new Material(Shader.Find(SHADER_NAME_LIGHTMAPCOMBINE));
                SetUpMaterial(tempMaterial, meshRenderer, material);
                ExportTexture(tempMaterial);
            }
        }
    }

    private static List<GameObject> GetObjectsWithLightmap()
    {
        var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
        var objectsWithLightmap = new List<GameObject>();

        foreach (var obj in allObjects)
        {
            var meshRenderer = obj.GetComponent<MeshRenderer>();
            if (meshRenderer != null && meshRenderer.lightmapIndex >= 0)
            {
                objectsWithLightmap.Add(obj);
            }
        }

        return objectsWithLightmap;
    }
    
    private void SetUpMaterial(Material tempMaterial, MeshRenderer meshRenderer, Material originalMaterial)
    {
        tempMaterial.SetFloat("_ScaleU", meshRenderer.lightmapScaleOffset.x);
        tempMaterial.SetFloat("_ScaleV", meshRenderer.lightmapScaleOffset.y);
        tempMaterial.SetFloat("_OffsetU", meshRenderer.lightmapScaleOffset.z);
        tempMaterial.SetFloat("_OffsetV", meshRenderer.lightmapScaleOffset.w);

        if (LightmapSettings.lightmaps.Length > meshRenderer.lightmapIndex)
        {
            var lmInfo = LightmapSettings.lightmaps[meshRenderer.lightmapIndex];
            tempMaterial.SetTexture("_Lightmap", lmInfo.lightmapColor);
        }

        if (originalMaterial.HasProperty("_MainTex"))
        {
            tempMaterial.SetTexture("_MainTex", originalMaterial.GetTexture("_MainTex"));
        }
    }
    
    private void ExportTexture(Material material)
    {
        var mainTex = material.GetTexture("_MainTex") as Texture2D;
        if (mainTex == null)
        {
            Debug.LogError("MainTex is not assigned in the material.");
            return;
        }
        var lightmap = material.GetTexture("_Lightmap") as Texture2D;
        if (lightmap == null)
        {
            Debug.LogError("lightmap is not assigned in the material.");
            return;
        }

        // RenderTextureに変換後の値を書き込む
        var renderTexture   = RenderTexture.GetTemporary(mainTex.width, mainTex.height, 0, RenderTextureFormat.ARGBFloat);
        Graphics.Blit(lightmap, renderTexture, material, 0);
        
        // RenderTextureの値をTextureに書きこむ
        var currentRT = RenderTexture.active;
        RenderTexture.active = renderTexture;
        var texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBAFloat, false);
        texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        texture.Apply();
        RenderTexture.active = currentRT;

        // エンコードして保存
        var savePath = SaveAsset(texture.EncodeToPNG(), mainTex);

        // テクスチャインポート設定
        if (!string.IsNullOrEmpty(savePath)) {
            AssetDatabase.ImportAsset(savePath);
            var textureImporter             = AssetImporter.GetAtPath(savePath) as TextureImporter;
            textureImporter.alphaSource     = TextureImporterAlphaSource.None;
            textureImporter.isReadable      = false;
            textureImporter.mipmapEnabled   = true;
            textureImporter.wrapMode        = TextureWrapMode.Clamp;
            textureImporter.SaveAndReimport();
        }

        // RenderTextureを解放
        RenderTexture.ReleaseTemporary(renderTexture);
    }
    
    private string SaveAsset(byte[] target, Texture2D mainTex)
    {
        var sourcePath = AssetDatabase.GetAssetPath(mainTex);
        var path = Path.GetDirectoryName(sourcePath);
        var ext = "png";

        // ディレクトリが無ければ作る
        if (!Directory.Exists(path)) {
            Directory.CreateDirectory(path);
        }

        // ファイル保存パネルを表示
        var fileName = Path.GetFileNameWithoutExtension(sourcePath) + "_lightmapCombined." + ext;
        fileName = Path.GetFileNameWithoutExtension(AssetDatabase.GenerateUniqueAssetPath(Path.Combine(path, fileName)));
        path = EditorUtility.SaveFilePanelInProject("Save Asset", fileName, ext, "", path);

        if (!string.IsNullOrEmpty(path)) {
            // ファイルを保存する
            File.WriteAllBytes(path, target);
            AssetDatabase.Refresh();
        }

        return path;
    }
    
    private string RelativeToAbsolutePath(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return "";

        string dataPath = Application.dataPath;
        string projectPath = dataPath.Substring(0, dataPath.Length - 6); // "Assets"フォルダを除去
        return Path.Combine(projectPath, relativePath).Replace('/', '\\');
    }
    
}

#endif