using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public class SaveFileData
{
    public string DataFilePath;
    public SaveDataMeta Meta;
    public Texture2D Screenshot;
}

public class SaveFileInterface
{
    public static SaveDataMeta LoadMetaOnly(string saveFilePath)
    {
        if (string.IsNullOrEmpty(saveFilePath))
        {
            Debug.LogError("Save file path is null or empty.");
            return null;
        }

        if (!System.IO.File.Exists(saveFilePath))
        {
            Debug.LogError($"Save file not found at {saveFilePath}.");
            return null;
        }

        try
        {
            string json = System.IO.File.ReadAllText(saveFilePath);
            JsonSerializerSettings jsonSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects,
                Formatting = Formatting.Indented,
            };
            SaveData saveData = JsonConvert.DeserializeObject<SaveData>(json, jsonSettings);
            return saveData.meta;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load save file metadata: {ex.Message}");
            return null;
        }
    }
    
    public static string GetRootSaveDir()
    {
        string path;
#if UNITY_EDITOR
        // For debugging in the editor, create a "Saves" folder in the project root.
        // This makes the save file easily accessible from the project window.
        // Application.dataPath is the /Assets folder. "../" goes up one level to the project root.
        path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Saves"));
#else
        // For a deployed build, use the platform-safe persistent data path.
        path = Application.persistentDataPath;
#endif

        // Ensure the directory exists before trying to create a file there.
        if (!System.IO.Directory.Exists(path))
        {
            System.IO.Directory.CreateDirectory(path);
        }
        
        return path;
    }

    public static string CreateSaveDir()
    {
        string dirName = $"save_{DateTime.Now:yyyyMMdd_HHmmss}";
        string fullPath = System.IO.Path.Combine(GetRootSaveDir(), dirName);
        if (!System.IO.Directory.Exists(fullPath))
        {
            System.IO.Directory.CreateDirectory(fullPath);
            Debug.Log($"Save directory created at {fullPath}");
        }
        return fullPath;
    }
    
    public static List<SaveFileData> GetSaveFiles()
    {
        string rootDir = GetRootSaveDir();
        // Iterate over the dir looking for directories that match the save dir pattern.
        List<SaveFileData> saveFiles = new List<SaveFileData>();
        string[] directories = System.IO.Directory.GetDirectories(rootDir, "save_*");
        foreach (string dir in directories)
        {
            SaveFileData saveData = new SaveFileData();
            // Each directory should have a SaveData.json file.
            string dataFilePath = System.IO.Path.Combine(dir, "SaveData.json");
            saveData.DataFilePath = dataFilePath;
            if (System.IO.File.Exists(dataFilePath))
            {
                // Read the metadata from the save file.
                SaveDataMeta meta = LoadMetaOnly(dataFilePath);
                if (meta != null)
                {
                    saveData.Meta = meta;
                }
                else
                {
                    Debug.LogWarning($"Failed to load metadata from {dataFilePath}. Skipping this save file.");
                }
            }
            
            string screenshotPath = System.IO.Path.Combine(dir, "Screenshot.png");
            Texture2D screenshot = null;
            if (System.IO.File.Exists(screenshotPath))
            {
                byte[] imageData = System.IO.File.ReadAllBytes(screenshotPath);
                screenshot = new Texture2D(2, 2); // Create a temporary texture to load the image data.
                screenshot.LoadImage(imageData);
                saveData.Screenshot = screenshot;
            }
            else
            {
                Debug.LogWarning($"Screenshot not found at {screenshotPath}. This save file will not have a screenshot.");
            }
            
            saveFiles.Add(saveData);
        }
        return saveFiles;
    }
}