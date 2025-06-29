using System;
using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class MoveAndUseBehaviorParameters : BehaviorParameters
{
    [FormerlySerializedAs("initiator")] public GameObject initiatorGO;
    public Transform standPoint;
    public InteractionDefinitionSO interactionDefinition;
    public GameObject Interactable;
    public MovementSpeed desiredSpeed = MovementSpeed.NpcSpeed;
    public bool ExactPosition = true;
    public bool FinalAlignment = true;
    public float MoveAcceptanceRadius = 1f;
}

[CreateAssetMenu(fileName = "MoveAndUseBehaviorFactory", menuName = "Body Politic/Interrupt Factories/Move And Use Behavior Factory")]
public class MoveAndUseBehaviorFactory : InterruptBehaviorFactory<MoveAndUseBehaviorParameters>
{
    [SerializeField] private BehaviorGraph graph;
    [SerializeField] private string displayName = "Move and Use";
    [SerializeField] private string displayDescription = "Moves to the specified position and uses an item.";
    [SerializeField] private string genericErrorMessage = "Something went wrong.";
    [SerializeField] private string doorRoleFailedMessage = "This door won't open for me.";
    [SerializeField] private string interactionRoleFailedMessage = "I'm not authorized for this.";
    [SerializeField] private string proximityFailedMessage = "Looks like I need to be closer.";

    public override InterruptBehaviorDefinition GetInterruptDefinition(MoveAndUseBehaviorParameters interruptParameters)
    {
        if (graph == null) return null;
        
        return new InterruptBehaviorDefinition(interruptParameters)
        {
            BehaviorGraph = graph,
            BlackboardData = new Dictionary<string, object>
            {
                { "StandPoint", interruptParameters.standPoint },
                { "Interaction Definition", interruptParameters.interactionDefinition },
                { "Interactable", interruptParameters.Interactable },
                { "ExactPosition", interruptParameters.ExactPosition },
                { "FinalAlignment", interruptParameters.FinalAlignment },
                { "MoveAcceptanceRadius", interruptParameters.MoveAcceptanceRadius },
                { "Speed", interruptParameters.desiredSpeed },
                { "Generic Error Message", genericErrorMessage},
                { "Door Role Failed Message", doorRoleFailedMessage },
                { "Interaction Role Failed Message", interactionRoleFailedMessage },
                { "Proximity Failed Message", proximityFailedMessage }
            },
            
            DisplayName = displayName,
            DisplayDescription = displayDescription
        };
    }
}