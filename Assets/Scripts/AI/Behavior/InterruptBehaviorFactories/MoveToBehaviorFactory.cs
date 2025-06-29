using System;
using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class MoveToBehaviorParameters : BehaviorParameters
{
    public Vector3 targetPosition;
    public MovementSpeed desiredSpeed = MovementSpeed.NpcSpeed;
    public bool exactPosition = true;
    public bool finalAlignment = false;
}

[CreateAssetMenu(fileName = "MoveToBehaviorFactory", menuName = "Body Politic/Interrupt Factories/Move To Behavior Factory")]
public class MoveToBehaviorFactory : InterruptBehaviorFactory<MoveToBehaviorParameters>
{
    [SerializeField] private BehaviorGraph graph;
    [SerializeField] private string displayName = "Move";
    [SerializeField] private string displayDescription = "Moves to the specified position.";
    [SerializeField] private string roleFailedMessage = "This door won't open for me.";
    [SerializeField] private string genericErrorMessage = "I couldn't get there.";

    public override InterruptBehaviorDefinition GetInterruptDefinition(MoveToBehaviorParameters interruptParameters)
    {
        if (graph == null) return null;

        return new InterruptBehaviorDefinition(interruptParameters)
        {
            BehaviorGraph = graph,
            BlackboardData = new Dictionary<string, object>
            {
                { "Target", interruptParameters.targetPosition },
                { "DesiredSpeed", interruptParameters.desiredSpeed },
                { "ExactPosition", interruptParameters.exactPosition },
                { "FinalAlignment", interruptParameters.finalAlignment },
                { "Role Failed Message", roleFailedMessage },
                { "Generic Error Message", genericErrorMessage }
            },
            
            DisplayName = displayName,
            DisplayDescription = displayDescription
        };
    }
}