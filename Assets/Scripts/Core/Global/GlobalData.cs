using UnityEngine;

[DefaultExecutionOrder(-100)]
public class GlobalData : MonoBehaviour
{
    [Tooltip("The SO used to store the player roles.")]
    [SerializeField] public PlayerIdentitySO PlayerIdentity;
    
    [Header("Default Events")]
    // TODO: XXX Uncomment when interrupts brought back
    // [Tooltip("The event used to trigger interrupts.")]
    // [SerializeField] public InterruptSelectionDataEventSO InterruptEvent;

    [Tooltip("The event used to trigger infection events.")]
    [SerializeField] public InfectionDataEventSO InfectionEvent;

    [Tooltip("The event used to trigger camera mode changes.")]
    [SerializeField] public CameraModeRequestEventSO CameraModeRequestEvent;

    public static GlobalData Instance { get; private set; }

    void Awake()
    {
        // Debug.Log($"Initializing Global Data: {this}");
        if (Instance != null) {
            Debug.LogError("There is more than one instance!");
            return;
        }

        Instance = this;
    }
}