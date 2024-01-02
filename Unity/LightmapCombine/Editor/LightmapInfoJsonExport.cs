using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class LightmapInfoJsonExport : EditorWindow
{
    private List<ObjectLightmapInfo> lightmapInfoList = new List<ObjectLightmapInfo>();
    private List<GameObject> selectedObjects = new List<GameObject>();

    [MenuItem("SonTools/Lightmap Info Json Export")]
    static void OpenWindow()
    {
        LightmapInfoJsonExport window = GetWindow<LightmapInfoJsonExport>();
        window.titleContent = new GUIContent("Lightmap Info Json Export");
        window.Show();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Lightmap UVのスケールとオフセット値を取得したいオブジェクトを選択してください");

        if (GUILayout.Button("Refresh Selected Objects"))
        {
            UpdateSelectedObjects();
        }

        if (GUILayout.Button("Collect and Save Lightmap Data"))
        {
            CollectLightmapInfo();
            SaveLightmapInfoToJson();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Selected Objects:", EditorStyles.boldLabel);

        if (selectedObjects.Count == 0)
        {
            EditorGUILayout.LabelField("No objects selected.");
        }

        foreach (GameObject obj in selectedObjects)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(obj.name);
            if (GUILayout.Button("Remove"))
            {
                selectedObjects.Remove(obj);
                break; // Break to avoid modifying the list while iterating
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
    }

    private string RelativeToAbsolutePath(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return "";

        string dataPath = Application.dataPath;
        string projectPath = dataPath.Substring(0, dataPath.Length - 6); // "Assets"フォルダを除去
        return Path.Combine(projectPath, relativePath).Replace('/', '\\');
    }
    
    private void UpdateSelectedObjects()
    {
        selectedObjects.Clear();
        selectedObjects.AddRange(Selection.gameObjects);
    }

    private void CollectLightmapInfo()
    {
        lightmapInfoList.Clear();
        GameObject[] selectedObjects = Selection.gameObjects;

        foreach (GameObject selectedObject in selectedObjects)
        {
            MeshRenderer meshRenderer = selectedObject.GetComponent<MeshRenderer>();
            if (meshRenderer != null && meshRenderer.lightmapIndex >= 0)
            {
                var lightmapInfo = new ObjectLightmapInfo
                {
                    name = selectedObject.name,
                    lightmapScaleOffset = new LightmapScaleOffset
                    {
                        scaleU = meshRenderer.lightmapScaleOffset.x,
                        scaleV = meshRenderer.lightmapScaleOffset.y,
                        offsetU = meshRenderer.lightmapScaleOffset.z,
                        offsetV = meshRenderer.lightmapScaleOffset.w
                    },
                    materials = new List<MaterialInfo>()
                };

                // Lightmapのパスを取得
                string lightmapPath = "";
                if (LightmapSettings.lightmaps.Length > meshRenderer.lightmapIndex)
                {
                    LightmapData lmInfo = LightmapSettings.lightmaps[meshRenderer.lightmapIndex];
                    string relativePath = AssetDatabase.GetAssetPath(lmInfo.lightmapColor);
                    lightmapPath = RelativeToAbsolutePath(relativePath);
                }
                lightmapInfo.lightmapPath = lightmapPath;
                
                foreach (Material material in meshRenderer.sharedMaterials)
                {
                    if (material != null)
                    {
                        var materialInfo = new MaterialInfo
                        {
                            materialName = material.name,
                            mainTexturePath =  material.HasProperty("_MainTex") ? RelativeToAbsolutePath(AssetDatabase.GetAssetPath(material.GetTexture("_MainTex"))) : ""
                        };

                        lightmapInfo.materials.Add(materialInfo);
                    }
                }

                lightmapInfoList.Add(lightmapInfo);
            }
        }
    }

    private void SaveLightmapInfoToJson()
    {
        string json = JsonUtility.ToJson(new LightmapInfoList { objects = lightmapInfoList }, true);
        
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string defaultFileName = string.IsNullOrEmpty(sceneName) ? "Lightmapinfo" : sceneName;

        string path = EditorUtility.SaveFilePanel("Save lightmap info", "", defaultFileName, "json");
        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllText(path, json);
            EditorUtility.DisplayDialog("Lightmap info Export", "Lightmap info exported successfully!", "OK");
        }
    }
}

[System.Serializable]
public class ObjectLightmapInfo
{
    public string name;
    public LightmapScaleOffset lightmapScaleOffset;
    public List<MaterialInfo> materials;
    public string lightmapPath;
}

[System.Serializable]
public class LightmapScaleOffset
{
    public float scaleU;
    public float scaleV;
    public float offsetU;
    public float offsetV;
}

[System.Serializable]
public class MaterialInfo
{
    public string materialName;
    public string mainTexturePath; 
}

[System.Serializable]
public class LightmapInfoList
{
    public List<ObjectLightmapInfo> objects;
}
