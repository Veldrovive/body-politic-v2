using System;
using UnityEngine;
using System.Collections.Generic;


public class PrefabPrinter : AbstractInteractionReactor
{
    #region Serialized Fields

    [Header("Production Parameters")]
    [Tooltip("The prefab to print.")]
    [SerializeField] private GameObject prefabToPrint;
    
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

    #region Unity Lifecycle

    private void OnValidate()
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
        SafelyRegisterInteractionLifecycleCallback(InteractionLifecycleEvent.OnEnd, printInteractionDefinition,
            TryPrint);
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
        GameObject printedObject = Instantiate(prefabToPrint, printLocation.position, printLocation.rotation);
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
            Debug.LogWarning($"Prefab '{prefabToPrint.name}' is not a consumable, but infection was requested. Infection will not be applied.", this);
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
