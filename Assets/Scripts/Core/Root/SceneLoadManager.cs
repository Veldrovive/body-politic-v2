using System;
using System.Collections;
using System.Collections.Generic;
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
    LoadingScreen,
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
            StartCoroutine(LoadLevelWithLoadingScreen(sceneMapping));
        }
    }

    /// <summary>
    /// Loads a level scene asynchronously with an intermediate loading screen.
    /// </summary>
    /// <param name="sceneMapping">The scene mapping data for the level.</param>
    /// <param name="levelSceneName">The name of the level scene to load.</param>
    private IEnumerator LoadLevelWithLoadingScreen(SceneMapping sceneMapping)
    {
        string levelScenePath = GetScenePath(sceneMapping.SceneName);
        Debug.Log($"Loading level {sceneMapping.Id} ({sceneMapping.SceneName})");

        if (LoadingScreenSceneName == null)
        {
            Debug.LogError("Loading Screen Scene object (LoadingScreenSceneObj) is not assigned in the SceneLoadManager. Please assign a SceneAsset in the Inspector.");
            yield break;
        }

        // Get the name of the loading screen scene asset.
        string loadingScreenName = GetScenePath(LoadingScreenSceneName);
        if (string.IsNullOrEmpty(loadingScreenName))
        {
            Debug.LogError("Loading screen scene name is empty or invalid. Ensure a valid SceneAsset is assigned for the loading screen and it has a name.");
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

        if (ResourceDataManager.Instance == null)
        {
            Debug.LogWarning($"Level {sceneMapping.Id} loaded, but ResourceDataManager.Instance is null. Ensure ResourceDataManager is initialized before loading scenes.");
        }
        else
        {
            Debug.Log($"Loaded level has {ResourceDataManager.Instance.NumSaveables} saveables.");
        }
    }
}