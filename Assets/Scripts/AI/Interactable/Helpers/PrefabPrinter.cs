using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class PrefabPrinterSaveableData : SaveableData
{
    public List<GameObject> trackedProducedItems = new List<GameObject>();
    public int lastProducedItemCount = -1;
    public bool printDefEnabled = false;
}

public class PrefabPrinter : AbstractInteractionReactor
{
    #region Serialized Fields

    [Header("Production Parameters")]
    [Tooltip("The prefab to print.")]
    [SerializeField] private HoldableType prefabToPrint;
    
    [Tooltip("The point at which the prefab will appear.")]
    [SerializeField] private Transform printLocation;
    
    [Tooltip("A collider used to detect if there is an existing prefab present.")]
    [SerializeField] private Collider printArea;
    
    [Tooltip("Whether to disable the interaction if there is already a prefab present.")]
    [SerializeField] private bool disableIfPresent = true;
    
    [Header("Interaction Definitions")]
    [Tooltip("The print interaction definition used on the associated interactable.")]
    [SerializeField] private InteractionDefinitionSO printInteractionDefinition;
    
    [Header("Infection")]
    [Tooltip("Whether produced consumables should be infected (Only relevant if prefab is a consumable)")]
    [SerializeField] private BoolReference infectConsumable = new (false);

    #endregion

    #region Internal Fields

    /// <summary>
    /// Tracks produced items that remain within the print area. Used to update the enabled status of the print interaction.
    /// </summary>
    private HashSet<GameObject> trackedProducedItems = new HashSet<GameObject>();
    private int lastProducedItemCount = -1;

    private bool printDefEnabled = false;
    
    #endregion

    #region Save-Load

    public override SaveableData GetSaveData()
    {
        return new PrefabPrinterSaveableData()
        {
            trackedProducedItems = trackedProducedItems.ToList(),
            lastProducedItemCount = lastProducedItemCount,
            printDefEnabled = printDefEnabled
        };
    }

    public override void LoadSaveData(SaveableData data)
    {
        if (data is PrefabPrinterSaveableData printerData)
        {
            trackedProducedItems = new HashSet<GameObject>(printerData.trackedProducedItems);
            lastProducedItemCount = printerData.lastProducedItemCount;
            printDefEnabled = printerData.printDefEnabled;
        }
        else
        {
            Debug.LogWarning($"PrefabPrinter received invalid save data: {data.GetType().Name}.", this);
        }
    }

    #endregion

    #region Unity Lifecycle

    private void Initialize()
    {
        // Check to make sure that the print definition is valid
        if (printInteractionDefinition == null)
        {
            Debug.LogWarning($"{name} requires a print interaction definition.", this);
            return;
        }
        else if (!HasInteractionInstanceFor(printInteractionDefinition))
        {
            Debug.LogWarning($"Interactable associated with {name} does not have an interaction instance for {printInteractionDefinition.name}.", this);
            return;
        }
        
        // Otherwise we are good to link up the events
        bool registered = SafelyRegisterInteractionLifecycleCallback(InteractionLifecycleEvent.OnEnd, printInteractionDefinition,
            TryPrint);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        
        Initialize();
    }

    private void Start()
    {
        Initialize();
    }

    private void Update()
    {
        // We want to remove all tracked produced items that are no longer within the print area (Or the print area is null)
        trackedProducedItems.RemoveWhere(
            trackedObj => printArea == null || !printArea.bounds.Contains(trackedObj.transform.position)
        );

        if (lastProducedItemCount != trackedProducedItems.Count)
        {
            // Then we should update the interaction
            HandleInteractionEnable();
            lastProducedItemCount = trackedProducedItems.Count;
        }
    }

    #endregion

    #region Event Handlers

    public void TryPrint(InteractionContext context)
    {
        // Plan: Create the prefab and add it to the tracked produced items. Try cast to Consumable and infect if not null.
        Debug.Log($"Prefab printer got called to print {prefabToPrint} at {printLocation.position}", this);
        // GameObject printedObject = Instantiate(prefabToPrint, printLocation.position, printLocation.rotation);
        GameObject printedObject = SaveableDataManager.Instance.InstantiateHoldable(prefabToPrint, printLocation.position, printLocation.rotation);
        Debug.Log($"Prefab printer created {printedObject.name} at {printLocation.position}", this);
        trackedProducedItems.Add(printedObject);
        
        // Check if the printed object is a consumable and infect it if so
        Consumable consumable = printedObject.GetComponent<Consumable>();
        if (consumable != null)
        {
            consumable.SetInfected(infectConsumable.Value);
        }
        else if (infectConsumable.Value)
        {
            // We wanted to infect, but this isn't a consumable. That's a problem.
            Debug.LogWarning($"Prefab '{prefabToPrint}' is not a consumable, but infection was requested. Infection will not be applied.", this);
        }
    }

    #endregion

    #region Helpers

    private void HandleInteractionEnable()
    {
        bool shouldBeEnabled = true;
        int numTrackedItems = trackedProducedItems.Count;
        if (disableIfPresent && numTrackedItems > 0)
        {
            shouldBeEnabled = false;
        }

        if (printDefEnabled && !shouldBeEnabled)
        {
            // We need to disable the interaction
            Debug.Log($"{gameObject.name} print interaction disabled due to item presence.", this);
            if (SetInteractionEnabled(printInteractionDefinition, false, false, "Item already present."))
            {
                printDefEnabled = false;
            }
            // else: The interaction update failed for some reason.
        }
        else if (!printDefEnabled && shouldBeEnabled)
        {
            // We need to enable the interaction
            if (SetInteractionEnabled(printInteractionDefinition, true))
            {
                printDefEnabled = true;
            }
        }
    }

    #endregion
    
}
