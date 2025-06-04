using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class LevelLoadUIManager : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private VisualElement root;
    private Label LevelNameLabel;
    private VisualElement ProgressBar;
    
    public static LevelLoadUIManager Instance;
    private void Awake()
    {
        // Ensure that there is only one instance of LevelLoadUIManager
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("Duplicated LevelLoadUIManager instance found. Multiple loading scenes are active at the same time?");
        }
        
        if (uiDocument == null)
        {
            if (!TryGetComponent(out uiDocument))
            {
                Debug.LogError("UIDocument is not assigned and could not be found on the GameObject.");
                return;
            }
        }
        
        root = uiDocument.rootVisualElement;
        LevelNameLabel = root.Q<Label>("LevelName");
        LevelNameLabel.text = "";
        ProgressBar = root.Q<VisualElement>("ProgressBar");
        ProgressBar.style.width = Length.Percent(0f);  // Initialize progress bar to 0%
    }

    private void OnDestroy()
    {
        // Clean up the singleton instance when this object is destroyed
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SetLevelName(string levelName)
    {
        if (LevelNameLabel == null)
        {
            Debug.LogError("LevelNameLabel is not assigned in the UI.");
            return;
        }
        
        LevelNameLabel.text = levelName;
    }
    
    public void SetProgress(float progress)
    {
        if (ProgressBar == null)
        {
            Debug.LogError("ProgressBar is not assigned in the UI.");
            return;
        }
        
        // Clamp progress between 0 and 1
        progress = Mathf.Clamp01(progress);
        ProgressBar.style.width = Length.Percent(progress * 100f);
    }
    

    // private void Start()
    // {
    //     StartCoroutine(SimulateProgress());
    // }
    //
    // private IEnumerator SimulateProgress()
    // {
    //     if (ProgressBar == null)
    //     {
    //         yield break;
    //     }
    //
    //     ProgressBar.style.width = Length.Percent(0f);  // 0 to 100
    //     while (true)
    //     {
    //         currentProgress += 0.01f;  // Simulate progress increment
    //         if (currentProgress > 1f)
    //         {
    //             currentProgress = 0f;  // Reset progress for simulation
    //         }
    //         ProgressBar.style.width = Length.Percent(currentProgress * 100f);
    //         yield return new WaitForSeconds(0.1f);  // Wait for a short duration before next increment
    //     }
    // }
}