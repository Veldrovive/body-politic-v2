using UnityEngine;
using UnityEngine.UIElements;

public class MainMenuUIManager : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private VisualElement root;
    private VisualElement SampleLevelButton;
    private VisualElement AirportLevelButton;
    
    private VisualElement LoadButton;
    private Label LoadButtonLabel => LoadButton.Q<Label>("LoadButtonLabel");
    
    private void Awake()
    {
        if (uiDocument == null)
        {
            if (!TryGetComponent(out uiDocument))
            {
                Debug.LogError("UIDocument is not assigned and could not be found on the GameObject.");
                return;
            }
        }
        
        root = uiDocument.rootVisualElement;
        SampleLevelButton = root.Q<VisualElement>("L1");
        SampleLevelButton.RegisterCallback<ClickEvent>(evt =>
        {
            SceneLoadManager.Instance.LoadScene(SceneId.SampleLevel);
        });
        
        AirportLevelButton = root.Q<VisualElement>("L2");
        AirportLevelButton.RegisterCallback<ClickEvent>(evt =>
        {
            SceneLoadManager.Instance.LoadScene(SceneId.AirportLevel);
        });

        LoadButton = root.Q<VisualElement>("Load");
        var saveFiles = SaveFileInterface.GetSaveFiles();
        // We always load the latest save file, which is the last one in the list.
        if (saveFiles.Count > 0)
        {
            LoadButton.RegisterCallback<ClickEvent>(evt =>
            {
                SaveFileData saveFileData = saveFiles[^1];
                SceneLoadManager.Instance.LoadSave(saveFileData.DataFilePath);
            });
            LoadButtonLabel.text = $"Load {saveFiles.Count} Save File(s)";
        }
        else
        {
            LoadButton.style.display = DisplayStyle.None;
        }
    }
}