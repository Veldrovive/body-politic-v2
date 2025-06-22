using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PanicGraphConfiguration : AbstractGraphFactoryConfig
{
    public SayStateConfiguration EntrySayConfig;
    public float PanicDuration = 20f;
    [NonSerialized] public Zone PanicZone;
    public List<SayBubbleData> PanicSayConfig = new List<SayBubbleData>();
}

public enum PanicGraphExitConnection
{
    PanicEnded,
    FailedToPanic,  // Used when there is no point in the zone that can be reached
    DoorRoleFailed, // Used when the NPC cannot reach the point due to a door role failure
    Error,         // Used when the NPC cannot reach the point due to an error
}

public class PanicGraphFactory : GenericAbstractGraphFactory<PanicGraphConfiguration, PanicGraphExitConnection>
{
    public PanicGraphFactory(PanicGraphConfiguration configuration, string graphId = null) : base(configuration, graphId)
    {
    }

    protected override void ConstructGraphInternal(StateGraph graph, GraphFactoryConnectionEnd startPoint)
    {
        float startTime = SaveableDataManager.Instance.time;
        float endTime = startTime + config.PanicDuration;
        
        // Steps:
        // 1. Start with a say node to indicate panic (Requires SayStateNode)
        // 2. Pick a random point in the NPC's PanicZone to run to (Requires GetZonePointGateStateNode)
        // 3. Run to the point (Requires MoveToStateNode)
        // 4. Say a panic message (Requires SequentialSayStateNode)
        // 5. Check if time is past the end time. If not, loop back to step 2. If so, exit with PanicEnded.
        //      (Requires TimeCheckGateStateNode)

        #region Node Construction
        // Construct: entry say state node
        SayStateConfiguration entrySayStateConfig = config.EntrySayConfig;
        if (entrySayStateConfig == null)
        {
            entrySayStateConfig = new SayStateConfiguration();
        }
        SayStateNode startSay = new(entrySayStateConfig);

        // We need to create a Vector3VariableSO to store the panic point between states
        // Vector3VariableSO panicPoint = ScriptableObject.CreateInstance<Vector3VariableSO>();
        Vector3VariableSO panicPoint = SaveableDataManager.Instance.CreateInstance<Vector3VariableSO>(InstantiableSOType.Vector3Variable);
        
        // Construct: Get point in panic zone node
        GetZonePointGateStateConfiguration getZonePointConfig = new GetZonePointGateStateConfiguration
        {
            FeasibleZone = config.PanicZone,
            SnapToNavMesh = true,
            RandomPoint = panicPoint
        };
        GetZonePointGateStateNode getZonePointNode = new(getZonePointConfig);
        
        // Construct: Move to point node
        MoveToStateConfiguration moveToConfig = new MoveToStateConfiguration
        {
            TargettingType = MoveToStateTargettingType.Position,
            TargetPosition = new Vector3Reference(panicPoint),
            DesiredSpeed = MovementSpeed.Run,
            RequireExactPosition = true,
            RequireFinalAlignment = false,
        };
        MoveToStateNode moveToNode = new(moveToConfig);
        
        // Construct: Say panic message node
        SequentialSayStateConfiguration sayPanicConfig = new SequentialSayStateConfiguration
        {
            SayBubbleDataList = config.PanicSayConfig
        };
        SequentialSayStateNode sayPanicNode = new(sayPanicConfig);
        
        // Construct: Time check gate node
        TimeCheckGateStateConfiguration timeCheckConfig = new TimeCheckGateStateConfiguration
        {
            EndTime = endTime,
        };
        TimeCheckGateStateNode timeCheckNode = new(timeCheckConfig);
        #endregion

        #region Node Connections

        // Connect start to the first say node
        graph.ConnectStateFlow(
            startPoint.GraphNode, startPoint.PortName,
            startSay, StateNode.IN_PORT_NAME
        );
        
        // Say state Timeout, Interrupt, and LoadIn all lead to the GetZonePoint node
        ConnectStateFlows<SayStateOutcome>(graph, startSay, new() {
            { SayStateOutcome.Timeout, getZonePointNode }
        });
        ConnectStateInterrupt(graph, startSay, getZonePointNode);
        ConnectStateLoadIn(graph, startSay, getZonePointNode);
        
        // GetZonePoint PointFound leads to MoveTo node. Interrupt and LoadIn loop back on itself. PointNotFound
        // leads to FailedToPanic exit.
        ConnectStateFlows<GetZonePointGateStateOutcome>(graph, getZonePointNode, new() {
            { GetZonePointGateStateOutcome.PointFound, moveToNode }
        });
        ConnectStateInterrupt(graph, getZonePointNode, getZonePointNode);
        ConnectStateLoadIn(graph, getZonePointNode, getZonePointNode);
        AddExitConnection(PanicGraphExitConnection.FailedToPanic,
            new(getZonePointNode, nameof(GetZonePointGateStateOutcome.PointNotFound)),
            "Oh Zimborp! There's nowhere to go!"
        );
        
        // MoveToState Arrived leads to SayPanic node. Interrupt and LoadIn loop back to getZonePointNode. Error and 
        // DoorRoleFailed lead to FailedToPanic exit.
        ConnectStateFlows<MoveToStateOutcome>(graph, moveToNode, new() {
            { MoveToStateOutcome.Arrived, sayPanicNode }
        });
        ConnectStateInterrupt(graph, moveToNode, moveToNode);
        ConnectStateLoadIn(graph, moveToNode, moveToNode);
        AddExitConnection(PanicGraphExitConnection.Error,
            new(moveToNode, nameof(MoveToStateOutcome.Error)),
            "Oh Zimborp! I can't get there!"
        );
        AddExitConnection(PanicGraphExitConnection.DoorRoleFailed,
            new(moveToNode, nameof(MoveToStateOutcome.DoorRoleFailed)),
            "Oh Zimborp! I can't get through!"
        );
        
        // SayPanicState Timeout, Interrupt, and LoadIn lead to the TimeCheck node
        ConnectStateFlows<SequentialSayStateOutcome>(graph, sayPanicNode, new() {
            { SequentialSayStateOutcome.Timeout, timeCheckNode }
        });
        ConnectStateInterrupt(graph, moveToNode, moveToNode);
        ConnectStateLoadIn(graph, moveToNode, moveToNode);
        
        // TimeCheckGateState EndTimeReached leads to the end of the graph with PanicEnded exit.
        // Continue leads back to the GetZonePoint node.
        // Interrupt and LoadIn loop back to getZonePointNode.
        ConnectStateFlows<TimeCheckGateStateOutcome>(graph, timeCheckNode, new() {
            { TimeCheckGateStateOutcome.Continue, getZonePointNode }
        });
        ConnectStateInterrupt(graph, moveToNode, moveToNode);
        ConnectStateLoadIn(graph, moveToNode, moveToNode);
        AddExitConnection(PanicGraphExitConnection.PanicEnded,
            new(timeCheckNode, nameof(TimeCheckGateStateOutcome.Timeout)),
            "Phew! I think I'm safe now!"
        );

        #endregion
    }
}