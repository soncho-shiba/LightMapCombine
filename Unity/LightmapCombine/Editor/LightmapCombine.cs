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
        CheckColorSpace();
        
        var objectsWithLightmap = GetObjectsWithLightmap();
        if (objectsWithLightmap.Count == 0)
        {
            EditorUtility.DisplayDialog("Lightmap Combine Error", "No objects with lightmap found in the scene.", "OK");
            return;
        }

        // 「LightMapCombine」GameObjectの名前を決定
        var lightmapCombineName = GetUniqueLightMapCombineName();

        // 新しい「LightMapCombine」GameObjectの作成
        var lightmapCombineParent = new GameObject(lightmapCombineName);
        
        foreach (var obj in objectsWithLightmap)
        {   
            var meshRenderer = obj.GetComponent<MeshRenderer>();
            foreach (var originalMaterial in meshRenderer.sharedMaterials)
            {
                
                EnableSRGBOnMainTex(originalMaterial);
                
                // RenderTextureに焼きこむ用の一時的なマテリアルを作成
                var tempMaterial = new Material(Shader.Find(SHADER_NAME_LIGHTMAPCOMBINE));
                SetUpMaterial(tempMaterial, meshRenderer, originalMaterial);
                
                var newTexture = ExportTexture(tempMaterial, originalMaterial, $"{lightmapCombineName}_{obj.name}");
                var newMaterialVariant = CreateMaterialVariant(originalMaterial, newTexture, $"{lightmapCombineName}_{obj.name}");

                // オブジェクトの複製を作成し、'LightMapCombine'の子として配置
                var duplicateObj = Instantiate(obj, lightmapCombineParent.transform);
                duplicateObj.name = obj.name;

                // 新しいマテリアルを複製したオブジェクトに適用
                var duplicateMeshRenderer = duplicateObj.GetComponent<MeshRenderer>();
                if (duplicateMeshRenderer != null)
                {
                    duplicateMeshRenderer.sharedMaterials = new Material[] { newMaterialVariant };
                }
            }
            obj.SetActive(false);
        }
    }
    
    private void CheckColorSpace()
    {
        if (PlayerSettings.colorSpace != ColorSpace.Linear)
        {
            EditorUtility.DisplayDialog("Color Space Warning",
                "The project is not set to use Linear color space. " +
                "This tool requires a Linear workflow.",
                "OK");
        }
    }
    
    private string GetUniqueLightMapCombineName()
    {
        int index = 0;
        string baseName = "LightMapCombine";
        string currentName = baseName;

        // 名前が重複していないか確認
        while (GameObject.Find(currentName) != null)
        {
            index++;
            currentName = baseName + "_" + index.ToString("D2");
        }

        return currentName;
    }
    private static List<GameObject> GetObjectsWithLightmap()
    {
        var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>(true); // 非アクティブなオブジェクトも含める
        var objectsWithLightmap = new List<GameObject>();

        foreach (var obj in allObjects)
        {
            // 「LightMapCombine」を含む名前のオブジェクトは除外
            if (obj.name.Contains("LightMapCombine")) 
            {
                continue;
            }

            var meshRenderer = obj.GetComponent<MeshRenderer>();
            if (meshRenderer != null && meshRenderer.lightmapIndex >= 0)
            {
                // オブジェクトをアクティブにする
                obj.SetActive(true);

                // リストに追加
                objectsWithLightmap.Add(obj);
            }
        }
        return objectsWithLightmap;
    }

    

    private void EnableSRGBOnMainTex(Material material)
    {
        if (material == null)
        {
            Debug.LogError("Material is null.");
            return;
        }

        if (!material.HasProperty("_MainTex"))
        {
            Debug.LogError("Material does not have a _MainTex property.");
            return;
        }

        var mainTex = material.GetTexture("_MainTex") as Texture2D;
        if (mainTex == null)
        {
            Debug.LogError("_MainTex is not assigned in the material.");
            return;
        }

        var texturePath = AssetDatabase.GetAssetPath(mainTex);
        var textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (textureImporter == null)
        {
            Debug.LogError("Failed to get TextureImporter for the main texture.");
            return;
        }

        if (!textureImporter.sRGBTexture)
        {
            textureImporter.sRGBTexture = true;
            textureImporter.SaveAndReimport();
        }
    }

    private void SetUpMaterial(Material tempMaterial, MeshRenderer meshRenderer, Material originalMaterial)
    {
        tempMaterial.SetFloat("_ScaleU", meshRenderer.lightmapScaleOffset.x);
        tempMaterial.SetFloat("_ScaleV", meshRenderer.lightmapScaleOffset.y);
        tempMaterial.SetFloat("_OffsetU", meshRenderer.lightmapScaleOffset.z);
        tempMaterial.SetFloat("_OffsetV", meshRenderer.lightmapScaleOffset.w);

        if (LightmapSettings.lightmaps.Length > meshRenderer.lightmapIndex)
        {
            tempMaterial.SetTexture("_Lightmap", LightmapSettings.lightmaps[meshRenderer.lightmapIndex].lightmapColor);
        }

        if (originalMaterial.HasProperty("_MainTex"))
        {
            tempMaterial.SetTexture("_MainTex", originalMaterial.GetTexture("_MainTex"));
        }
    }

    private Texture2D ExportTexture(Material tempMaterial, Material originalMaterial, string prefix)
    {
        var mainTex = tempMaterial.GetTexture("_MainTex") as Texture2D;
        if (mainTex == null)
        {
            Debug.LogError("MainTex is not assigned in the material.");
            return null;
        }

        var renderTexture = RenderTexture.GetTemporary(mainTex.width, mainTex.height, 0, RenderTextureFormat.ARGBFloat);
        Graphics.Blit(mainTex, renderTexture, tempMaterial, 0);
    
        var currentRT = RenderTexture.active;
        RenderTexture.active = renderTexture;
        var texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBAFloat, false);
        texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        texture.Apply();
        RenderTexture.active = currentRT;

        var savePath = SaveTextureAsset(texture.EncodeToPNG(), mainTex, prefix);
        RenderTexture.ReleaseTemporary(renderTexture);

        if (!string.IsNullOrEmpty(savePath)) {
            AssetDatabase.ImportAsset(savePath);
            ApplyTextureImportSettings(originalMaterial.GetTexture("_MainTex") as Texture2D, savePath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(savePath);
        }

        return null;
    }

    private void ApplyTextureImportSettings(Texture2D sourceTexture, string newTexturePath)
    {
        if (sourceTexture == null || string.IsNullOrEmpty(newTexturePath))
        {
            Debug.LogError("Source texture or new texture path is invalid.");
            return;
        }

        var sourceTextureImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sourceTexture)) as TextureImporter;
        var newTextureImporter = AssetImporter.GetAtPath(newTexturePath) as TextureImporter;
        if (sourceTextureImporter == null || newTextureImporter == null)
        {
            Debug.LogError("Failed to get TextureImporter.");
            return;
        }
        
        newTextureImporter.sRGBTexture = sourceTextureImporter.sRGBTexture;
        newTextureImporter.alphaSource     = sourceTextureImporter.alphaSource;
        newTextureImporter.isReadable      = sourceTextureImporter.isReadable ;
        newTextureImporter.mipmapEnabled   = sourceTextureImporter.mipmapEnabled;
        newTextureImporter.wrapMode        = sourceTextureImporter.wrapMode;

        newTextureImporter.SaveAndReimport();
    }
    
    private string SaveTextureAsset(byte[] target, Texture2D mainTex, string prefix)
    {
        var sourcePath = AssetDatabase.GetAssetPath(mainTex);
        var directoryPath = Path.GetDirectoryName(sourcePath);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(sourcePath);
        var newFileName = $"{prefix}_{fileNameWithoutExtension}.png";
        var newPath = Path.Combine(directoryPath, newFileName);

        // 同名ファイルが存在する場合、上書き
        if (File.Exists(newPath))
        {
            File.Delete(newPath);
        }

        // テクスチャを保存
        File.WriteAllBytes(newPath, target);
        AssetDatabase.Refresh();

        return newPath;
    }
    
    private  Material CreateMaterialVariant(Material originalMaterial, Texture2D newTexture, string prefix)
    {
        if (originalMaterial == null || newTexture == null)
        {
            Debug.LogError("Original material or new texture is null.");
            return　null;
        }

        var originalMaterialPath = AssetDatabase.GetAssetPath(originalMaterial);
        var folderPath = Path.GetDirectoryName(originalMaterialPath);
        var newMaterialName = $"{prefix}_{Path.GetFileNameWithoutExtension(originalMaterialPath)}.mat";
        var newMaterialPath = Path.Combine(folderPath, newMaterialName);

        var newMaterial = new Material(originalMaterial);
        newMaterial.SetTexture("_MainTex", newTexture);
        newMaterial.parent = originalMaterial;
        AssetDatabase.CreateAsset(newMaterial, newMaterialPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return newMaterial;
    }
}

#endif
