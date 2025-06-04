using UnityEngine;
using UnityEngine.UIElements;

public class MainMenuUIManager : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private VisualElement root;
    private VisualElement SampleLevelButton;
    private VisualElement AirportLevelButton;
    
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
    }
}