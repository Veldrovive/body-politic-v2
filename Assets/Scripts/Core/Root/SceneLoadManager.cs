using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework.Constraints;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SceneType
{
    Internal,  // Used for things like loading screens. Cannot be directly loaded.
    Menu,
    Level
}

public enum SceneId
{
    MainMenu,
    Loading,
    SampleLevel,
    AirportLevel,
}

[Serializable]
public class SceneMapping
{
    public SceneId Id;
    public SceneType Type;
    public string SceneName; // The name in the scene folder
    public string PublicName; // The name used in the game, e.g., "Sample Level"
}

public class SceneLoadManager : MonoBehaviour
{
    [SerializeField] string SceneFolderPath = "Assets/Scenes/"; // The folder where scenes are stored. Used for editor scripts, not runtime.
    // Ensure SceneObj in each mapping is a SceneAsset and the corresponding scene is added to Build Settings.
    [SerializeField] private List<SceneMapping> SceneMap;
    // This should be assigned a SceneAsset for the loading screen in the Inspector.
    // The loading screen scene must also be added to Build Settings.
    [SerializeField] string LoadingScreenSceneName;
    
    [SerializeField] float minLoadingScreenDuration = 1f; // Minimum time to show the loading screen, in seconds.
    
    /// <summary>
    /// The singleton instance of the SceneLoadManager.
    /// </summary>
    public static SceneLoadManager Instance { get; private set; }

    private void Awake()
    {
        // Ensure that there is only one instance of SceneLoadManager
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Optional: if you want this manager to persist across scene loads.
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // When we start, we check whether we are in a level or in the main menu.
        // If we are already in a level, we immediately trigger a blank load to ensure everything is set up correctly.
        // Otherwise, we load the main menu which will mean that
        // Get the scene ID of the currently active scene.
        SceneId currentSceneId = GetCurrentScenedId();
        if (currentSceneId == SceneId.MainMenu)
        {
            // Debug.Log("Current scene is Main Menu. No action needed.");
        }
        else
        {
            // Debug.Log($"Current scene is {currentSceneId}. Performing blank load.");
            SaveableDataManager.Instance.BlankLoad();
        }
    }
    
    public string GetScenePath(string name)
    {
        // Ensure the SceneObj is assigned and has a valid name.
        if (string.IsNullOrEmpty(name))
        {
            Debug.LogError($"Scene object for {name} is not assigned or has an invalid name. Please assign a SceneAsset in the Inspector.");
            return string.Empty;
        }

        // Construct the full path to the scene asset.
        return $"{SceneFolderPath}{name}.unity";
    }

    /// <summary>
    /// Loads the specified scene based on its SceneId.
    /// Handles different loading procedures based on SceneType.
    /// </summary>
    /// <param name="sceneId">The Id of the scene to load.</param>
    public void LoadScene(SceneId sceneId)
    {
        // Find the scene mapping for the given sceneId
        SceneMapping sceneMapping = SceneMap.Find(scene => scene.Id == sceneId);

        if (sceneMapping == null)
        {
            Debug.LogError($"SceneMapping not found for SceneId: {sceneId}. Please check SceneMap configuration.");
            return;
        }

        if (string.IsNullOrEmpty(sceneMapping.SceneName))
        {
            Debug.LogError($"Scene object for {sceneId} (mapped to {sceneMapping.Id}) is not assigned in SceneMap. Please assign a SceneAsset in the Inspector.");
            return;
        }

        if (string.IsNullOrEmpty(sceneMapping.SceneName))
        {
            Debug.LogError($"Scene name for {sceneId} (mapped to {sceneMapping.Id}) is empty or invalid. Ensure a valid SceneAsset is assigned and it has a name.");
            return;
        }

        if (sceneMapping.Type == SceneType.Internal)
        {
            // you cannot load internal scenes directly
            Debug.LogError($"Cannot load internal scene: {sceneId} ({sceneMapping.SceneName}). Please use a different scene type.");
            return;
        }
        else if (sceneMapping.Type == SceneType.Menu)
        {
            // Moving to a menu scene. Directly load. These are light.
            // SceneManager.LoadScene uses the scene name to find it in the Build Settings.
            SceneManager.LoadScene(GetScenePath(sceneMapping.SceneName), LoadSceneMode.Single);
        }
        else if (sceneMapping.Type == SceneType.Level)
        {
            // Levels are heavy and require a loading scene to visually progress while the async load happens.
            // Start the coroutine to load the level with a loading screen.
            StartCoroutine(LoadLevelWithLoadingScreen(sceneMapping, startWithBlankLoad: true));
        }
    }

    /// <summary>
    /// Loads a level scene asynchronously with an intermediate loading screen.
    /// </summary>
    /// <param name="sceneMapping">The scene mapping data for the level.</param>
    /// <param name="levelSceneName">The name of the level scene to load.</param>
    private IEnumerator LoadLevelWithLoadingScreen(SceneMapping sceneMapping, bool startWithBlankLoad = false, Action<bool> callback = null)
    {
        // // Unload the currently active scene if it's not the main menu.
        // Scene activeScene = SceneManager.GetActiveScene();
        // if (activeScene.name != "MainMenu" && activeScene.isLoaded)
        // {
        //     Debug.Log($"Unloading current scene: {activeScene.name}");
        //     AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(activeScene);
        //     if (unloadOperation == null)
        //     {
        //         Debug.LogError($"Failed to start unloading the current scene: {activeScene.name}. Cannot proceed with loading a new level.");
        //         callback?.Invoke(false);
        //         yield break; // Critical error, can't proceed.
        //     }
        //     yield return unloadOperation; // Wait for the unload to complete.
        // }
        
        string levelScenePath = GetScenePath(sceneMapping.SceneName);
        Debug.Log($"Loading level {sceneMapping.Id} ({sceneMapping.SceneName})");

        if (LoadingScreenSceneName == null)
        {
            Debug.LogError("Loading Screen Scene object (LoadingScreenSceneObj) is not assigned in the SceneLoadManager. Please assign a SceneAsset in the Inspector.");
            callback?.Invoke(false);
            yield break;
        }

        // Get the name of the loading screen scene asset.
        string loadingScreenName = GetScenePath(LoadingScreenSceneName);
        if (string.IsNullOrEmpty(loadingScreenName))
        {
            Debug.LogError("Loading screen scene name is empty or invalid. Ensure a valid SceneAsset is assigned for the loading screen and it has a name.");
            callback?.Invoke(false);
            yield break;
        }
        
        // Load the loading screen first. This scene must also be in Build Settings.
        SceneManager.LoadScene(loadingScreenName, LoadSceneMode.Single);

        // Wait a frame to ensure the loading screen is fully loaded and its UI components can be found.
        yield return null; 

        LevelLoadUIManager levelLoadUIManager = LevelLoadUIManager.Instance;
        // Attempt to find the LevelLoadUIManager instance.
        // This loop is a safeguard, but ideally, LevelLoadUIManager.Instance should be available soon after the loading scene loads.
        float timeWaited = 0f;
        while (levelLoadUIManager == null && timeWaited < 5f) // Add a timeout to prevent infinite loop
        {
            levelLoadUIManager = LevelLoadUIManager.Instance;
            yield return null; // Wait for the next frame
            timeWaited += Time.deltaTime;
        }

        if (levelLoadUIManager == null)
        {
            Debug.LogError("LevelLoadUIManager.Instance not found after loading the loading screen. Ensure the loading screen has a LevelLoadUIManager and it sets its Instance correctly.");
            // Potentially try to load the level anyway, or handle error differently
            // For now, we'll attempt to load the level without UI updates if manager is missing.
        }
        
        // Set the level name in the loading screen UI, if the UI manager was found.
        if (levelLoadUIManager != null)
        {
            levelLoadUIManager.SetLevelName(sceneMapping.PublicName);
        }
        
        // Start the async load of the actual level scene. LoadSceneMode.Additive keeps the loading screen active.
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(levelScenePath, LoadSceneMode.Additive);
        if (asyncLoad == null)
        {
            Debug.LogError($"Failed to start async load for scene {sceneMapping.Id} ({sceneMapping.SceneName}).");
            callback?.Invoke(false);
            yield break; // Critical error, can't proceed.
        }
        // Prevent the scene from activating immediately.
        asyncLoad.allowSceneActivation = false;
        float loadingStartTime = Time.time;
        
        // Unity's async loading completes at 0.9 progress. allowSceneActivation = true (default) will make it jump to 1.0 when ready.
        // We divide by 0.9f to normalize the progress to a 0-1 range for UI display.
        while (asyncLoad.progress < 0.9 && Time.time - loadingStartTime < minLoadingScreenDuration)
        {
            float trueProgress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            float maxProgress = (Time.time - loadingStartTime) / minLoadingScreenDuration;
            
            float shownProgress = Mathf.Min(trueProgress, maxProgress);
            
            if (levelLoadUIManager != null)
            {
                // Update the progress bar in the loading screen UI.
                levelLoadUIManager.SetProgress(shownProgress);
            }
            yield return null;
        }
        asyncLoad.allowSceneActivation = true;
        
        // Optional: Wait an extra frame to ensure the loaded level initializes before unloading the loading screen.
        yield return null;
        if (startWithBlankLoad)
        {
            SaveableDataManager.Instance.BlankLoad();
        }

        // After the level is loaded (asyncLoad.isDone is true), we can find the loaded scene.
        Scene loadedLevelScene = SceneManager.GetSceneByName(sceneMapping.SceneName);
        if (loadedLevelScene.IsValid())
        {
            // Set the newly loaded level scene as the active scene.
            // This is important for lighting, NavMesh, and other scene-specific settings to work correctly.
            SceneManager.SetActiveScene(loadedLevelScene);
        }
        else
        {
            Debug.LogWarning($"Could not find loaded scene by name {sceneMapping.SceneName} to set it active. This might happen if the scene name is incorrect or loading failed silently.");
        }

        // Unload the loading screen scene. This uses the scene name.
        SceneManager.UnloadSceneAsync(loadingScreenName);
        
        yield return null;
        callback?.Invoke(true);
    }

    public void LoadSave(string saveFilePath)
    {
        // Start the coroutine to load the save file with a loading screen.
        StartCoroutine(LoadSaveWithLoadingScreen(saveFilePath));
    }
    
    private IEnumerator LoadSaveWithLoadingScreen(string saveFilePath, Action<bool> callback = null)
    {
        // Ensure that this save file actually exists.
        if (string.IsNullOrEmpty(saveFilePath) || !System.IO.File.Exists(saveFilePath))
        {
            Debug.LogError($"Save file not found at {saveFilePath}. Cannot load save.");
            callback?.Invoke(false);
            yield break; // Critical error, can't proceed.
        }
        
        // Get the metadata so we can find which scene to load.
        SaveDataMeta meta = SaveFileInterface.LoadMetaOnly(saveFilePath);
        SceneId saveSceneId = meta.ActiveSceneId;
        
        // Find the scene mapping for the save's active scene.
        SceneMapping sceneMapping = SceneMap.Find(scene => scene.Id == saveSceneId);
        if (sceneMapping == null || string.IsNullOrEmpty(sceneMapping.SceneName))
        {
            Debug.LogError($"No SceneMapping found for save's active scene: {saveSceneId}. Cannot load save.");
            callback?.Invoke(false);
            yield break; // Critical error, can't proceed.
        }

        bool? success = null;
        // We are ready to load the scene. We can use LoadLevelWithLoadingScreen as a helper
        yield return LoadLevelWithLoadingScreen(sceneMapping, false, res => success = res);

        // Wait until the scene is loaded and the success flag is set.
        while (!success.HasValue)
        {
            yield return null;
        }

        if (success.Value)
        {
            // The new scene is loaded and ready. Now we can actually load the save data.
            DeserializedSaveData saveData = SaveableDataManager.Instance.LoadSaveFile(saveFilePath);
            // We just use saveData as an indicator of success. If it was null, the load failed.
            if (saveData == null)
            {
                Debug.LogError($"Failed to load save data from {saveFilePath}. The scene is loaded, but the save data could not be retrieved.");
                callback?.Invoke(false);
                yield break; // Critical error, can't proceed.
            }
        
            callback?.Invoke(true);
#if UNITY_EDITOR
            EditorApplication.isPaused = true;
#endif
        }
        else
        {
            Debug.LogError($"Failed to load scene for save file {saveFilePath}. The scene was not loaded successfully.");
            callback?.Invoke(false);
            yield break; // Critical error, can't proceed.
        }
    }

    private void LoadMainMenu()
    {
        // Load the main menu scene. This is a menu scene, so we can load it directly.
        SceneMapping mainMenuMapping = SceneMap.Find(scene => scene.Id == SceneId.MainMenu);
        if (mainMenuMapping == null || string.IsNullOrEmpty(mainMenuMapping.SceneName))
        {
            Debug.LogError("No SceneMapping found for MainMenu. Cannot load main menu.");
            return; // Critical error, can't proceed.
        }
        
        Debug.Log($"Loading Main Menu: {mainMenuMapping.SceneName}");
        SceneManager.LoadScene(GetScenePath(mainMenuMapping.SceneName), LoadSceneMode.Single);
    }
    
    public SceneId GetCurrentScenedId()
    {
        // Get the currently active scene and return its SceneId.
        Scene activeScene = SceneManager.GetActiveScene();
        SceneMapping sceneMapping = SceneMap.Find(scene => scene.SceneName == activeScene.name);
        
        if (sceneMapping != null)
        {
            return sceneMapping.Id;
        }
        
        Debug.LogWarning($"No SceneMapping found for active scene: {activeScene.name}. Returning default SceneId.MainMenu.");
        return SceneId.MainMenu; // Default or fallback value if no mapping is found.
    }

    public void QuickLoad()
    {
        // Selects the last save file and loads it.
        List<SaveFileData> saveFiles = SaveFileInterface.GetSaveFiles();
        if (saveFiles.Count > 0)
        {
            // Load the last save file.
            SaveFileData lastSaveFile = saveFiles[^1]; // Get the last save file in the list.
            Debug.Log($"Quick loading last save file: {lastSaveFile.DataFilePath}");
            LoadSave(lastSaveFile.DataFilePath);
        }
        else
        {
            Debug.LogWarning("No save files found. Cannot quick load.");
            LoadMainMenu(); // Optionally, load the main menu if no saves are available.
        }
    }
}