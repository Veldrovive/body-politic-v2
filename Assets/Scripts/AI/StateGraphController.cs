using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Sisus.ComponentNames;
using UnityEngine;
using UnityEngine.Serialization;

// TODO: Implement a system for whether the interrupt is required to be immediate or not. Sometimes we only want the
// interrupt to work if it can be done right now. Other times we want to interrupt even if it means waiting.

[Serializable]
public class ExecutionContext
{
    public StateGraph Graph;
    public string NodeIdToExecute;  // Points to the StateNode that will execute on StateGraph execution
    public bool IsSavable;  // If true, this StateGraph context will be saved on interrupt
    public bool Ephemeral;  // If true, this StateGraph will be Destroyed upon exit or interrupt without save
    [CanBeNull] public AbstractState CurrentState;
    
    public ExecutionContext(StateGraph graph, string nodeIdToExecute, bool isSavable, bool ephemeral, AbstractState currentState = null)
    {
        Graph = graph;
        NodeIdToExecute = nodeIdToExecute;
        IsSavable = isSavable;
        Ephemeral = ephemeral;
        CurrentState = currentState;
    }
}

public class StateGraphController : MonoBehaviour
{
    [Tooltip("The state graph to start with and return to on queue empty")]
    [SerializeField] StateGraph routineStateGraph;
    
    [FormerlySerializedAs("idleOnExit")] [Tooltip("If true, when the queue empties, the controller will go to idle state instead of the to routine state graph")]
    public bool IdleOnExit = false;
    
    private ExecutionContext currentExecutionContext;
    private LinkedList<ExecutionContext> executionDequeue = new LinkedList<ExecutionContext>();
    private ExecutionContext savedRoutineContext;  // Special case where we save even after clear

    private bool shouldInterrupt = false;
    
    public bool IsIdle => IdleOnExit && currentExecutionContext == null;
    public bool IsInRoutine => currentExecutionContext != null && currentExecutionContext.Graph == routineStateGraph;
    public StateGraph CurrentStateGraph => currentExecutionContext?.Graph;

    /// <summary>
    /// Add a StateGraph to be executed.
    /// </summary>
    /// <param name="graphToRun"></param>
    /// <param name="interruptCurrent"></param>
    /// <param name="saveThisGraphIfItGetsInterrupted"></param>
    /// <param name="clearDeque"></param>
    public void EnqueueStateGraph(StateGraph graphToRun, bool interruptCurrent, bool saveThisGraphIfItGetsInterrupted,
        bool clearDeque, bool ephemeral = false)
    {
        if (clearDeque)
        {
            executionDequeue.Clear();
        }
        
        // Find the initial node to run
        StartNode startNode = graphToRun.GetStartNode();
        StateGraphNode postStartNode = graphToRun.GetStartNodeConnection(startNode);
        string initialNodeId = postStartNode.id;

        ExecutionContext newContext = new ExecutionContext(
            graphToRun,
            initialNodeId,
            saveThisGraphIfItGetsInterrupted,
            ephemeral,
            null
        );
        
        // TODO: What should happen if we add to the queue while the routineStateGraph is running? That graph explicitly
        // never exits so adding to the back of the queue in that case is non-sensical. Should we demand that interruptCurrent
        // is true in that case?
        
        // If this is an interrupt, we should put it at the front of the dequeue. Otherwise it is a normal enqueue and
        // should be added to the back
        if (interruptCurrent)
        {
            executionDequeue.AddFirst(newContext);
            shouldInterrupt = true;  // We also mark the controller to interrupt the current graph. This will cause the
            // next ExecutionContext in the queue to be run which will be the new graph
        }
        else
        {
            // Otherwise we add it to the back of the queue
            executionDequeue.AddLast(newContext);
        }
        
        // We also immediately call Update to try to handle the new state graph
        Update();
    }

    /// <summary>
    /// Overload of EnqueueStateGraph to specifically take a StateGraphFactory. This creates the StateGraph component
    /// and then configures it using the factory. These are always marked as ephemeral and so will be destroyed upon
    /// their completion or interruption (if not saved).
    /// </summary>
    /// <param name="stateGraphFactory"></param>
    /// <param name="interruptCurrent"></param>
    /// <param name="saveThisGraphIfItGetsInterrupted"></param>
    /// <param name="clearDeque"></param>
    public void EnqueueStateGraph(AbstractGraphFactory stateGraphFactory, bool interruptCurrent,
        bool saveThisGraphIfItGetsInterrupted,
        bool clearDeque)
    {
        if (stateGraphFactory == null)
        {
            Debug.LogWarning($"{gameObject.name} State Controller tried to add a state graph factory that is null");
            return;
        }

        Component addedComponent = gameObject.AddComponent(typeof(StateGraph));
        StateGraph stateGraph = addedComponent as StateGraph;
        if (stateGraph == null)
        {
            Debug.LogWarning($"{gameObject.name} State Controller tried to add a state graph that is not a state graph");
            return;
        }
        
        // Configure the state graph using the factory
        try
        {
            stateGraphFactory.ConstructGraph(stateGraph);
        }
        catch (Exception e)
        {
            Debug.LogError($"{gameObject.name} State Controller tried to configure a state graph but failed. Exception: {e}");
            Destroy(addedComponent);
            return;
        }
        
        stateGraph.SetName($"Ephemeral State Graph: {stateGraph.id}");
        
        // Now we can add the state graph to the queue
        EnqueueStateGraph(stateGraph, interruptCurrent, saveThisGraphIfItGetsInterrupted, clearDeque, true);
    }
    
    
    /// <summary>
    /// Attempts to interrupt the currently executing state graph immediately and start the specified graph.
    /// If the interruption is successful, the new graph begins execution in the same frame.
    /// If the interruption fails (e.g., the current state cannot be interrupted), this method does nothing and returns false.
    /// </summary>
    /// <param name="graphToRun">The StateGraph to execute if interruption is successful.</param>
    /// <param name="saveThisGraphIfItGetsInterrupted">If the newly started graph is itself later interrupted, should it be saved?</param>
    /// <param name="ephemeral">If true, the graphToRun will be destroyed upon exit or if interrupted without being saved.</param>
    /// <param name="clearDequeOnSuccess">If true and interruption succeeds, the existing execution queue will be cleared.</param>
    /// <returns>True if the current state was successfully interrupted and the new graph is initiated, false otherwise.</returns>
    public bool TryInterrupt(StateGraph graphToRun, bool saveThisGraphIfItGetsInterrupted, bool clearDequeOnSuccess, bool ephemeral = false)
    {
        if (graphToRun == null)
        {
            Debug.LogWarning($"{gameObject.name} State Controller tried to TryInterrupt with a null graphToRun.");
            return false;
        }

        var (didInterrupt, oldGraphContinuationContext) = TryInterruptCurrentState();

        if (didInterrupt)
        {
            // Current graph was successfully interrupted (or was null).
            // currentExecutionContext is now null (or was already null if no graph was running, or became null in TryInterruptCurrentState if non-savable ephemeral graph was destroyed).
            // The CleanupCurrentState within TryInterruptCurrentState has already nulled currentExecutionContext.CurrentState.

            if (clearDequeOnSuccess)
            {
                executionDequeue.Clear();
                savedRoutineContext = null; // Clearing deque should also clear any pending routine context that's not active.
            }

            // Handle the context of the graph that was just interrupted.
            if (oldGraphContinuationContext != null)
            {
                // Ensure CurrentState is null for the continuation context, as it should resume, not re-run.
                if (oldGraphContinuationContext.CurrentState != null)
                {
                    Debug.LogWarning($"{gameObject.name} State Controller: Interrupted context for graph '{oldGraphContinuationContext.Graph.name}' had a CurrentState. Nullifying.");
                    oldGraphContinuationContext.CurrentState = null;
                }

                if (oldGraphContinuationContext.Graph == routineStateGraph)
                {
                    // If the routine graph was interrupted and is savable, its continuation is stored here.
                    savedRoutineContext = oldGraphContinuationContext;
                }
                else
                {
                    // For other savable graphs, add their continuation to the front of the queue.
                    executionDequeue.AddFirst(oldGraphContinuationContext);
                }
            }
            
            // Set the new graph as the current one.
            StartNode startNode = graphToRun.GetStartNode();
            StateGraphNode postStartNode = graphToRun.GetStartNodeConnection(startNode);
            string initialNodeId = postStartNode.id;

            currentExecutionContext = new ExecutionContext(
                graphToRun,
                initialNodeId,
                saveThisGraphIfItGetsInterrupted,
                ephemeral,
                null // CurrentState will be set by ProcessStateTransitions
            );

            // Immediately process transitions to start the new graph's first state.
            ProcessStateTransitions();
            return true;
        }
        else
        {
            // Interruption failed.
            return false;
        }
    }

    /// <summary>
    /// Overload of TryInterrupt to specifically take an AbstractGraphFactory.
    /// This creates the StateGraph component, configures it using the factory, and then attempts an immediate interrupt.
    /// These graphs are always marked as ephemeral.
    /// </summary>
    /// <param name="stateGraphFactory">The factory to construct the StateGraph.</param>
    /// <param name="saveThisGraphIfItGetsInterrupted">If the newly started graph is itself later interrupted, should it be saved?</param>
    /// <param name="clearDequeOnSuccess">If true and interruption succeeds, the existing execution queue will be cleared.</param>
    /// <returns>True if the current state was successfully interrupted and the new graph is initiated, false otherwise.</returns>
    public bool TryInterrupt(AbstractGraphFactory stateGraphFactory, bool saveThisGraphIfItGetsInterrupted, bool clearDequeOnSuccess)
    {
        if (stateGraphFactory == null)
        {
            Debug.LogWarning($"{gameObject.name} State Controller tried to TryInterrupt with a null stateGraphFactory.");
            return false;
        }

        Component addedComponent = gameObject.AddComponent(typeof(StateGraph));
        StateGraph stateGraph = addedComponent as StateGraph;
        if (stateGraph == null)
        {
            // Should not happen if typeof(StateGraph) is correct.
            Debug.LogError($"{gameObject.name} State Controller failed to add StateGraph component from factory.");
            Destroy(addedComponent); // Clean up the incorrectly added component.
            return false;
        }

        try
        {
            stateGraphFactory.ConstructGraph(stateGraph);
        }
        catch (Exception e)
        {
            Debug.LogError($"{gameObject.name} State Controller failed to configure a state graph from factory. Exception: {e}");
            Destroy(addedComponent); // Clean up: destroy the failed graph.
            return false;
        }

        stateGraph.SetName($"Ephemeral State Graph (Immediate Interrupt): {stateGraph.id}");

        bool success = TryInterrupt(stateGraph, saveThisGraphIfItGetsInterrupted, true, clearDequeOnSuccess);

        if (!success)
        {
            // If the interruption failed, the newly created ephemeral graph is not needed and should be destroyed.
            // This handles the case where TryInterruptCurrentState returns false.
            Destroy(stateGraph); 
        }
        
        return success;
    }

    /// <summary>
    /// Interrupts the current state graph if possible and proceeds normally.
    /// </summary>
    /// <returns></returns>
    public bool TryProceed()
    {
        var (didInterrupt, interruptContext) = TryInterruptCurrentState();

        if (didInterrupt)
        {
            // The current state was successfully interrupted.
            // currentExecutionContext.CurrentState is now null.

            if (interruptContext != null)
            {
                // An ExecutionContext to resume the interrupted graph was created.
                // We need to store it appropriately.
                if (interruptContext.Graph == routineStateGraph)
                {
                    savedRoutineContext = interruptContext;
                }
                // Actually, otherwise we should do nothing since we want to "Proceed" and never save. We only save
                // for the routineStateGraph because if we don't it would cause strange behavior.
                // else
                // {
                //     executionDequeue.AddFirst(interruptContext);
                // }
            }
            // If interruptContext is null, the interrupted graph was either not savable,
            // or had no valid interrupt transition. If it was ephemeral and not savable,
            // TryInterruptCurrentState would have destroyed it.

            // Signal that a new graph should be picked from the queue or routine.
            currentExecutionContext = null;

            // Process the next state/graph.
            Update();
            return true;
        }
        else
        {
            // The current state could not be interrupted.
            return false;
        }
    }
    
    /// <summary>
    /// Removes all copies of state graphs that have this id from the queue. Does not interrupt ongoing state graphs.
    /// </summary>
    /// <param name="stateGraphId"></param>
    /// <returns>True if any graphs were removed. False otherwise.</returns>
    public bool RemoveStateGraphById(string stateGraphId)
    {
        bool removed = false;
        // Check the routine state graph
        if (routineStateGraph?.id == stateGraphId)
        {
            routineStateGraph = null;
            removed = true;
        }
        
        // Check the queue
        LinkedListNode<ExecutionContext> node = executionDequeue.First;
        while (node != null)
        {
            if (node.Value.Graph.id == stateGraphId)
            {
                executionDequeue.Remove(node);
                removed = true;
            }
            node = node.Next;
        }

        return removed;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns>The executionContext to add to the queue to return to the interrupted graph if needed</returns>
    private (bool didInterrupt, ExecutionContext interruptContext) TryInterruptCurrentState()
    {
        if (currentExecutionContext == null)
        {
            Debug.LogWarning($"{gameObject.name} State Controller tried to interrupt a state graph but there is no current state graph");
            return (true, null);
        }

        if (currentExecutionContext.CurrentState == null)
        {
            // Similar to last block, this is unexpected, but still counts as an interrupt
            Debug.LogWarning($"{gameObject.name} State Controller tried to interrupt a state graph but there is no current state");
            return (true, null);
        }
        
        // Now we get to actually trying to interrupt the state
        if (currentExecutionContext.CurrentState.Interrupt())
        {
            // First thing we need to do is Clean up the current state (which also nulls the current state)
            CleanupCurrentState();
            
            // The interrupt succeeded. Let's see if we need to construct an interrupt context
            if (currentExecutionContext.IsSavable)
            {
                // We do return to this state after an interrupt
                // Ok, then we need to find the state to return to after the interrupt
                StateGraphNode nextNode = currentExecutionContext.Graph.GetNodeAfterPortConnection(
                    currentExecutionContext.NodeIdToExecute,
                    StateNode.INTERRUPT_PORT_NAME
                );
                if (nextNode == null)
                {
                    // The interrupt is invalid. Log an error and don't save the context
                    Debug.LogWarning($"{gameObject.name} State Controller could not find the node to return to after an interrupt. NodeId: {currentExecutionContext.NodeIdToExecute}");
                    return (true, null);
                }
                // Otherwise we have the next node and we can construct the interrupt context
                ExecutionContext interruptContext = new ExecutionContext(
                    currentExecutionContext.Graph,
                    nextNode.id,
                    currentExecutionContext.IsSavable,
                    currentExecutionContext.Ephemeral,
                    null
                );
                return (true, interruptContext);
            }
            // Otherwise we don't need to save the context. We just return true
            else
            {
                // We don't need to save the context.
                // If the state graph is ephemeral, we should now destroy it
                if (currentExecutionContext.Ephemeral)
                {
                    // This graph is not savable and so is now removed from this controller entirely which means
                    // that since it is also ephemeral we should destroy the underlying graph
                    Destroy(currentExecutionContext.Graph);
                }
                return (true, null);
            }
        }
        // The interrupt failed
        return (false, null);
    }
    
    private void HandleStateExit(AbstractState exitedState, string stateOutcome)
    {
        if (currentExecutionContext.CurrentState != null)
        {
            currentExecutionContext.CurrentState.enabled = false;  // Immediately freezes the state
        }
        CleanupCurrentState();  // This nulled the current state
        StateGraphNode nextNode = currentExecutionContext.Graph.GetNodeAfterPortConnection(
            currentExecutionContext.NodeIdToExecute,
            stateOutcome
        );
        if (nextNode == null)
        {
            // This is an error. We should exit the state graph and not try to continue
            Debug.LogWarning($"{gameObject.name} State Controller tried to exit a state graph but the next node is null. NodeId: {currentExecutionContext.NodeIdToExecute}");
            currentExecutionContext = null;
            return;
        }
        // Otherwise we just need to update the NodeIdToExecute to the next node
        currentExecutionContext.NodeIdToExecute = nextNode.id;
        // Since the current state was nulled, the Update method will pick this up and start the next state
        Update();
    }

    /// <summary>
    /// Detaches event listeners from the currentExecutionContext.CurrentState and destroys the component.
    /// Nulls the currentExecutionContext.CurrentState after destruction.
    /// </summary>
    private void CleanupCurrentState()
    {
        if (currentExecutionContext == null)
        {
            Debug.LogWarning($"{gameObject.name} State Controller tried to cleanup a state graph but there is no current state graph");
            return;
        }
        if (currentExecutionContext.CurrentState == null)
        {
            Debug.LogWarning($"{gameObject.name} State Controller tried to cleanup a state graph but there is no current state");
            return;
        }
        
        // Remove event listeners
        currentExecutionContext.CurrentState.OnExit -= HandleStateExit;
        RemoveStateEventListeners(currentExecutionContext.NodeIdToExecute);

        // Destroy the component
        Destroy(currentExecutionContext.CurrentState);
        
        // Null the current state
        currentExecutionContext.CurrentState = null;
    }

    private void RemoveStateEventListeners(string nodeIdToExecute)
    {
        currentExecutionContext.Graph.RemoveNodeOutgoingEvents(nodeIdToExecute);
    }

    private AbstractState ActivateNewState(StateNode nextStateNode)
    {
        // Check for any existing enabled components that derive from AbstractState
        if (gameObject.GetComponents<AbstractState>().Any(state => state.enabled))
        {
            // Then we have a state that is already active. This is a fatal error
            Debug.LogError($"FATAL ERROR: {gameObject.name} State Controller tried to add a new state but there is already an active state");
            return null;
        }
        
        AbstractStateConfiguration config = nextStateNode.GenericConfiguration;
        Component addedComponent = gameObject.AddComponent(config.AssociatedStateType);  // States start disabled so OnEnable is not called yet
        AbstractState state = addedComponent as AbstractState;
        if (state == null)
        {
            Debug.LogWarning($"{gameObject.name} State Controller tried to add a state that is not a state. NodeId: {nextStateNode.id}");
            return null;
        }

        try
        {
            state.Configure(config);
        } 
        catch (Exception e)
        {
            // It is very important to handle this because if we do not it will create an infinte loop of creating more
            // components. I know from experience.
            Debug.LogError($"{gameObject.name} State Controller tried to configure a state but failed. NodeId: {nextStateNode.id}. Exception: {e}");
            Destroy(addedComponent);
            return null;
        }
        
        // Create event listeners
        state.OnExit += HandleStateExit;

        EnableStateEventListeners(nextStateNode, state);
        
        // Set the current state to be this state
        currentExecutionContext.CurrentState = state;
        
        // Only once events have been connected do we enable the state
        state.enabled = true;

        return state;
    }

    private void EnableStateEventListeners(StateNode nextStateNode, AbstractState state)
    {
        currentExecutionContext.Graph.LinkNodeOutgoingEvents(nextStateNode.id, state);
    }

    /// <summary>
    /// Processes the current state of the controller, advancing to the next state or switching to a new graph
    /// from the queue or routine graph as necessary. This method handles the execution flow based on the
    /// currentExecutionContext and its CurrentState.
    /// </summary>
    private void ProcessStateTransitions()
    {
        if (currentExecutionContext == null)
        {
            // Then we are not executing anything. If there is something in the queue we should start executing it
            // If there isn't we either start the routine state graph keep currentExecutionContext = null which is
            // the idle state
            if (executionDequeue.Count == 0)
            {
                if (!IdleOnExit)
                {
                    // Then we should try to resume the routine state graph.
                    if (savedRoutineContext != null)
                    {
                        // Then we have a place to resume from
                        currentExecutionContext = savedRoutineContext;
                        savedRoutineContext = null; // Consumed the saved context
                    }
                    else
                    {
                        if (routineStateGraph == null)
                        {
                            // Welp, this is an error
                            Debug.LogError($"{gameObject.name} State Controller tried to start the routine state graph but it is null");
                        }
                        else
                        {
                            // Then we are just starting the routine state from scratch
                            StartNode startNode = routineStateGraph.GetStartNode();
                            StateGraphNode postStartNode = routineStateGraph.GetStartNodeConnection(startNode);
                            string initialNodeId = postStartNode.id;
                            currentExecutionContext = new ExecutionContext(
                                routineStateGraph,
                                initialNodeId,
                                true,  // The routine state graph always saves on interrupt
                                false, // The routine state graph is not ephemeral
                                null
                            );
                            // The next block will see that we now have a currentExecutionContext without a currentState
                            // and will begin execution of the next state in the graph
                        }
                    }
                }
                // else: we are idle and should continue to do nothing until the queue is not empty or idleOnExit is false
            }
            else
            {
                // Then we have something in the queue to execute. We remove it from the queue and set it as the current
                // execution context
                currentExecutionContext = executionDequeue.First.Value;
                executionDequeue.RemoveFirst();
                if (currentExecutionContext.CurrentState != null)
                {
                    // This is an error, we should never have a current state when we are starting a new execution context
                    Debug.LogWarning($"{gameObject.name} State Controller tried to start a new execution context but the current state is not null");
                    currentExecutionContext.CurrentState = null;
                }
            }
        }

        if (currentExecutionContext != null && currentExecutionContext.CurrentState == null)
        {
            // Then we are done with the current state and should move to the next one.
            // If the next node is an ExitNode, this signals that we are done with the current state graph
            // By nulling the currentExecutionContext it will tell logic next frame that we should start the next
            // state graph

            // Other systems have set currentExecutionContext.NodeIdToExecute to the next node to execute. Our job is just
            // to read the actual node, check what kind of node it is, and act accordingly.
            StateGraphNode nextNode = currentExecutionContext.Graph.GetNodeById(currentExecutionContext.NodeIdToExecute);
            if (nextNode == null)
            {
                // Whoops, we tried to start a node that doesn't exist. This is a bug in the state graph
                Debug.LogWarning($"{gameObject.name} State Controller tried to start a node that doesn't exist. NodeId: {currentExecutionContext.NodeIdToExecute}");
                // We should just null the currentExecutionContext and move on
                currentExecutionContext = null;
            }
            else if (nextNode is ExitNode)
            {
                // Then we are done with the current state graph. We should null the currentExecutionContext
                if (currentExecutionContext.Graph == routineStateGraph)
                {
                    Debug.LogWarning($"{gameObject.name} State Controller tried to exit the routine state graph. This is not intended. Use a RestartNode to restart the routine state graph");
                }
                // We now have to check if this state graph was ephemeral
                if (currentExecutionContext.Ephemeral)
                {
                    // Then this StateGraph is intended to be removed when it exits... which is now. Say goodbye.
                    Destroy(currentExecutionContext.Graph);
                }
                currentExecutionContext = null;
            }
            else
            {
                // Then our next node should be a StateNode. However, there is a special case
                if (nextNode is RestartNode)
                {
                    // This denotes that we should move back to the start node. We can handle this by setting NodeIdToExecute
                    // to the initial node and then setting nextNode to this node
                    StartNode startNode = currentExecutionContext.Graph.GetStartNode();
                    StateGraphNode postStartNode = currentExecutionContext.Graph.GetStartNodeConnection(startNode);
                    string initialNodeId = postStartNode.id;
                    currentExecutionContext.NodeIdToExecute = initialNodeId;
                    nextNode = currentExecutionContext.Graph.GetNodeById(initialNodeId);
                }

                // Actually there are two special cases now
                if (nextNode is JumpInputNode jumpNode)
                {
                    // This special node allows us to configurably jump to another node. This allows us to separate complex
                    // logic physically in the graph so that the main flow is easier to see
                    StateGraphNode jumpExitNode = currentExecutionContext.Graph.FindJumpExitNode(jumpNode.JumpKey);
                    if (jumpExitNode == null)
                    {
                        // THis is a fatal error. We should never have a jump input node that doesn't have a corresponding exit node
                        Debug.LogError($"{gameObject.name} State Controller tried to start a jump input node but there is no exit node. NodeId: {currentExecutionContext.NodeIdToExecute}");
                        // We should just null the currentExecutionContext and move on
                        currentExecutionContext = null;
                        return; // Return early to avoid processing a null nextNode
                    }
                    string nextNodeId = jumpExitNode.id; // This should be the node *after* the JumpOutputNode
                    currentExecutionContext.NodeIdToExecute = nextNodeId;
                    nextNode = jumpExitNode;
                }

                if (nextNode is StateNode nextStateNode)
                {
                    ActivateNewState(nextStateNode);
                }
                else if (nextNode != null) // If nextNode became null due to jump logic failure, this prevents further error
                {
                    // This is a bug in the state graph or an unhandled node type after a special node.
                    Debug.LogWarning($"{gameObject.name} State Controller tried to start a node that is not a state node or node traversal failed. NodeId: {currentExecutionContext.NodeIdToExecute}, Actual Node Type: {nextNode.GetType().Name}");
                    currentExecutionContext = null;
                }
                // If nextNode is null at this point (e.g. from failed jump), currentExecutionContext will be nulled or already is.
            }
        }
    }
    
    private void Update()
    {
        // If this is set, we will add it to the queue at the end of Update. This is useful for only adding the
        // interrupt context after the interrupting state has been dequeued
        ExecutionContext toAddInterruptContext = null;
        if (shouldInterrupt)
        {
            // Then we should try to interrupt the current state graph
            var (didInterrupt, interruptContext) = TryInterruptCurrentState();
            if (didInterrupt)
            {
                // We interrupted the current state graph
                toAddInterruptContext = interruptContext;  // Set the interrupt context to be set after all other processing completes
                shouldInterrupt = false;  // Reset the interrupt flag
                currentExecutionContext = null;  // We are no longer executing anything. Nulling this tells the
                // logic further down to start the next state graph
            }
            // else: We leave shouldInterrupt true so that we will try again next frame
        }
        
        // if (currentExecutionContext == null)
        // {
        //     // Then we are not executing anything. If there is something in the queue we should start executing it
        //     // If there isn't we either start the routine state graph keep currentExecutionContext = null which is
        //     // the idle state
        //     if (executionDequeue.Count == 0)
        //     {
        //         if (!IdleOnExit)
        //         {
        //             // Then we should try to resume the routine state graph.
        //             if (savedRoutineContext != null)
        //             {
        //                 // Then we have a place to resume from
        //                 currentExecutionContext = savedRoutineContext;
        //             }
        //             else
        //             {
        //                 if (routineStateGraph == null)
        //                 {
        //                     // Welp, this is an error
        //                     Debug.LogError($"{gameObject.name} State Controller tried to start the routine state graph but it is null");
        //                 }
        //                 else
        //                 {
        //                     // Then we are just starting the routine state from scratch
        //                     StartNode startNode = routineStateGraph.GetStartNode();
        //                     StateGraphNode postStartNode = routineStateGraph.GetStartNodeConnection(startNode);
        //                     string initialNodeId = postStartNode.id;
        //                     currentExecutionContext = new ExecutionContext(
        //                         routineStateGraph,
        //                         initialNodeId,
        //                         true,  // The routine state graph always saves on interrupt
        //                         false,  // The routine state graph is not ephemeral
        //                         null
        //                     );
        //                     // The next block will see that we now have a currentExecutionContext without a currentState
        //                     // and will begin execution of the next state in the graph
        //                 }
        //             }
        //         }
        //         // else: we are idle and should continue to do nothing until the queue is not empty or idleOnExit is false
        //     }
        //     else
        //     {
        //         // Then we have something in the queue to execute. We remove it from the queue and set it as the current
        //         // execution context
        //         currentExecutionContext = executionDequeue.First.Value;
        //         executionDequeue.RemoveFirst();
        //         if (currentExecutionContext.CurrentState != null)
        //         {
        //             // This is an error, we should never have a current state when we are starting a new execution context
        //             Debug.LogWarning($"{gameObject.name} State Controller tried to start a new execution context but the current state is not null");
        //             currentExecutionContext.CurrentState = null;
        //         }
        //     }
        // }
        //
        // if (currentExecutionContext != null && currentExecutionContext.CurrentState == null)
        // {
        //     // Then we are done with the current state and should move to the next one.
        //     // If the next node is an ExitNode, this signals that we are done with the current state graph
        //     // By nulling the currentExecutionContext it will tell logic next frame that we should start the next
        //     // state graph
        //     
        //     // Other systems have set currentExecutionContext.NodeIdToExecute to the next node to execute. Our job is just
        //     // to read the actual node, check what kind of node it is, and act accordingly.
        //     StateGraphNode nextNode = currentExecutionContext.Graph.GetNodeById(currentExecutionContext.NodeIdToExecute);
        //     if (nextNode == null)
        //     {
        //         // Whoops, we tried to start a node that doesn't exist. This is a bug in the state graph
        //         Debug.LogWarning($"{gameObject.name} State Controller tried to start a node that doesn't exist. NodeId: {currentExecutionContext.NodeIdToExecute}");
        //         // We should just null the currentExecutionContext and move on
        //         currentExecutionContext = null;
        //     }
        //     else if (nextNode is ExitNode)
        //     {
        //         // Then we are done with the current state graph. We should null the currentExecutionContext
        //         if (currentExecutionContext.Graph == routineStateGraph)
        //         {
        //             Debug.LogWarning($"{gameObject.name} State Controller tried to exit the routine state graph. This is not intended. Use a RestartNode to restart the routine state graph");
        //         }
        //         // We now have to check if this state graph was ephemeral
        //         if (currentExecutionContext.Ephemeral)
        //         {
        //             // Then this StateGraph is intended to be removed when it exits... which is now. Say goodbye.
        //             Destroy(currentExecutionContext.Graph);
        //         }
        //         currentExecutionContext = null;
        //     }
        //     else
        //     {
        //         // Then our next node should be a StateNode. However, there is a special case
        //         if (nextNode is RestartNode)
        //         {
        //             // This denotes that we should move back to the start node. We can handle this by setting NodeIdToExecute
        //             // to the initial node and then setting nextNode to this node
        //             StartNode startNode = currentExecutionContext.Graph.GetStartNode();
        //             StateGraphNode postStartNode = currentExecutionContext.Graph.GetStartNodeConnection(startNode);
        //             string initialNodeId = postStartNode.id;
        //             currentExecutionContext.NodeIdToExecute = initialNodeId;
        //             nextNode = currentExecutionContext.Graph.GetNodeById(initialNodeId);
        //         }
        //         
        //         // Actually there are two special cases now
        //         if (nextNode is JumpInputNode jumpNode)
        //         {
        //             // This special node allows us to configurably jump to another node. This allows us to separate complex
        //             // logic physically in the graph so that the main flow is easier to see
        //             StateGraphNode jumpExitNode = currentExecutionContext.Graph.FindJumpExitNode(jumpNode.JumpKey);
        //             if (jumpExitNode == null)
        //             {
        //                 // THis is a fatal error. We should never have a jump input node that doesn't have a corresponding exit node
        //                 Debug.LogError($"{gameObject.name} State Controller tried to start a jump input node but there is no exit node. NodeId: {currentExecutionContext.NodeIdToExecute}");
        //                 // We should just null the currentExecutionContext and move on
        //                 currentExecutionContext = null;
        //                 return;
        //             }
        //             string nextNodeId = jumpExitNode.id;
        //             currentExecutionContext.NodeIdToExecute = nextNodeId;
        //             nextNode = currentExecutionContext.Graph.GetNodeById(nextNodeId);
        //         }
        //
        //         if (nextNode is StateNode nextStateNode)
        //         {
        //             ActivateNewState(nextStateNode);
        //         }
        //         else
        //         {
        //             // This is a bug in the state graph. We should never restart to something that is not a state node
        //             Debug.LogWarning($"{gameObject.name} State Controller tried to start a node that is not a state node. NodeId: {currentExecutionContext.NodeIdToExecute}");
        //             currentExecutionContext = null;
        //         }
        //     }
        // }
        ProcessStateTransitions();

        if (toAddInterruptContext != null)
        {
            // We had an interrupt and saved context.
            // There is a special case if the interrupted state graph was the routine state graph since we never 
            // want to actually clear that execution context as we will always return to it
            if (toAddInterruptContext.CurrentState != null)
            {
                // This is wrong. It points to a bug in the creation of the interrupt context
                Debug.LogWarning($"{gameObject.name} State Controller tried to add an interrupt context but the current state is not null");
                // We just null it
                toAddInterruptContext.CurrentState = null;
            }
            if (toAddInterruptContext.Graph == routineStateGraph)
            {
                // Then we set the savedRoutineContext to be the interrupt context
                savedRoutineContext = toAddInterruptContext;
            }
            else
            {
                // Otherwise we add the interrupt context to the queue to be executed next
                executionDequeue.AddFirst(toAddInterruptContext);
            }
        }
    }

    /// <summary>
    /// Checks whether any graph in the StateGraph queue has the given GUID. This includes the routineStateGraph always.
    /// Conditionally also includes the current state graph if includeCurrent is true. Otherwise it only checks the
    /// the graphs actually in the queue.
    /// </summary>
    /// <param name="stateGraphGuid"></param>
    /// <param name="includeCurrent"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public bool HasGraphInQueue(string stateGraphGuid, bool includeCurrent = true)
    {
        if (routineStateGraph != null && routineStateGraph.id == stateGraphGuid)
        {
            return true;
        }
        
        if (includeCurrent && currentExecutionContext != null && currentExecutionContext.Graph.id == stateGraphGuid)
        {
            return true;
        }

        foreach (var executionContext in executionDequeue)
        {
            if (executionContext.Graph.id == stateGraphGuid)
            {
                return true;
            }
        }

        return false;
    }
}