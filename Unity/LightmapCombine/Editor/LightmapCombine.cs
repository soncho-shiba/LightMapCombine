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
        var objectsWithLightmap = GetObjectsWithLightmap();
        
        foreach (var obj in objectsWithLightmap)
        {   
            var meshRenderer = obj.GetComponent<MeshRenderer>();
            foreach (var originalMaterial in meshRenderer.sharedMaterials)
            {
                EnableSRGBOnMainTex(originalMaterial);
                var tempMaterial = new Material(Shader.Find(SHADER_NAME_LIGHTMAPCOMBINE));
                SetUpMaterial(tempMaterial, meshRenderer, originalMaterial);
                var newTexture = ExportTexture(tempMaterial, originalMaterial);
                CreateMaterialVariant(originalMaterial, newTexture, obj.name);
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
    
    private Texture2D ExportTexture(Material tempMaterial, Material originalMaterial)
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

        var savePath = SaveTextureAsset(texture.EncodeToPNG(), mainTex);
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
    
    private string SaveTextureAsset(byte[] target, Texture2D mainTex)
    {
        var sourcePath = AssetDatabase.GetAssetPath(mainTex);
        var path = Path.GetDirectoryName(sourcePath);
        var ext = "png";

        if (!Directory.Exists(path)) {
            Directory.CreateDirectory(path);
        }

        var fileName = Path.GetFileNameWithoutExtension(sourcePath) + "_lightmapCombined." + ext;
        fileName = Path.GetFileNameWithoutExtension(AssetDatabase.GenerateUniqueAssetPath(Path.Combine(path, fileName)));
        path = EditorUtility.SaveFilePanelInProject("Save Asset", fileName, ext, "", path);

        if (!string.IsNullOrEmpty(path)) {
            File.WriteAllBytes(path, target);
            AssetDatabase.Refresh();
        }

        return path;
    }
    
    private void CreateMaterialVariant(Material originalMaterial, Texture2D newTexture, string objName)
    {
        if (originalMaterial == null || newTexture == null)
        {
            Debug.LogError("Original material or new texture is null.");
            return;
        }

        var originalMaterialPath = AssetDatabase.GetAssetPath(originalMaterial);
        var folderPath = Path.GetDirectoryName(originalMaterialPath);
        var newMaterialName = $"{Path.GetFileNameWithoutExtension(originalMaterialPath)}_{objName}_lightmapCombined.mat";
        var newMaterialPath = Path.Combine(folderPath, newMaterialName);

        var newMaterial = new Material(originalMaterial);
        newMaterial.SetTexture("_MainTex", newTexture);

        AssetDatabase.CreateAsset(newMaterial, newMaterialPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}

#endif
