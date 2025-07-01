using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Sisus.ComponentNames;
using UnityEngine;
using UnityEngine.Serialization;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using Unity.VisualScripting;
using Object = UnityEngine.Object;
using Unity.Behavior.Serialization.Json;

[Serializable]
public class BehaviorAgentContext
{
    public SaveableBehaviorGraphAgent Agent;

    public bool IsRuntimeAdded;  // Will be removed on exit
    public bool SaveContext = false;
}


public class AgentContextSaveableData
{
    public string SerializedAgentData;  // Saves the full state of the agent including the entire behavior graph.
    public bool enabled;
    
    public string AgentId;
    public string DisplayName;
    public float Priority;
    
    public bool IsRuntimeAdded;
    public bool SaveContext;
}

public class BehaviorControllerSaveableData : SaveableData
{
    public bool IdleOnExit;
    public bool IsInRoutine;
    public AgentContextSaveableData routineAgentContext;
    
    public List<AgentContextSaveableData> queuedAgentContexts;
    public AgentContextSaveableData currentAgentContext;
}


/// <summary>
/// On interrupt finished:
///     HandleBehaviorFinished on finished interrupt
///         Clear currentBehaviorContext
///         If IsRuntimeAdded, destroy the BehaviorGraphAgent.
///     If next in queue is routine and IdleOnExit is true, then do nothing.
///     Else pop next in queue, set currentBehaviorContext, and call StartBehavior with it.
///
/// On interrupt added (param InterruptBehaviorDefinition: BehaviorGraph, BlackboardData):
///     Add new BehaviorGraphAgent component.
///     Set load the blackboard data and manually init as described here https://docs.unity3d.com/Packages/com.unity.behavior@1.0/api/Unity.Behavior.BehaviorGraphAgent.html?q=Init
///     If that all succeeds, TryInterrupt the current behavior. This will null the currentBehaviorContext, stop all execution, and 
///     If that succeeds, StartBehavior the new BehaviorGraphAgent 
/// </summary>

public class BehaviorController : SaveableGOConsumer
{
    [Tooltip("The behavior graph to start with and return to on queue empty")] [SerializeField]
    SaveableBehaviorGraphAgent routineBehavior;

    [Tooltip(
        "If true, when the queue empties, the controller will go to idle state instead of the to routine state graph")]
    public bool IdleOnExit = false;

    [SerializeField] private InterruptFinishedEvent interruptFinishedEvent;

    private BehaviorAgentContext currentBehaviorContext;
    [SerializeField] private List<BehaviorAgentContext> executionDequeue = new List<BehaviorAgentContext>();
    public bool IsIdle => IdleOnExit && currentBehaviorContext == null;
    public bool IsInRoutine => currentBehaviorContext != null && currentBehaviorContext.Agent == routineBehavior;
    public SaveableBehaviorGraphAgent CurrentBehaviorAgent => currentBehaviorContext?.Agent;

    // Tracks all behavior graphs to ensure that only one is ever enabled at a time.
    private List<SaveableBehaviorGraphAgent> existingBehaviorGraphAgents;
    [SerializeField] private bool pauseOnInterrupt = false;
    
    private UnityObjectResolver unityObjectResolver = new();
    private RuntimeSerializationUtility.JsonBehaviorSerializer behaviorSerializer = new();

    public override SaveableData GetSaveData()
    {
        BehaviorControllerSaveableData data = new()
        {
            IdleOnExit = IdleOnExit,
            IsInRoutine = IsInRoutine
        };

        AgentContextSaveableData GetSaveableBehaviorContext(BehaviorAgentContext context)
        {
            // I THINKG BUG IN UNITY BEHAVIOR: If a gameobject has been destroyed, trying to serialize it will cause a serialization error.
            foreach (var bvar in context.Agent.BlackboardReference.Blackboard.Variables)
            {
                if (bvar.ObjectValue == null)
                {
                    // yes this may seem like it is doing nothing, but that's where you'd be wrong.
                    // If this is a GameObject variable and it has been destroyed, this will set it to real null.
                    bvar.ObjectValue = null;
                    continue;
                }
                
                if (typeof(GameObject).IsAssignableFrom(bvar.ObjectValue.GetType()))
                {
                    GameObject go = (GameObject)bvar.ObjectValue;
                    if (go == null)
                    {
                        // Then we run into the problem. We need to set the variable value to be actually null
                        bvar.ObjectValue = null;
                    }
                }
            }
            return new AgentContextSaveableData()
            {
                SerializedAgentData = context.Agent.Serialize(behaviorSerializer, unityObjectResolver),
                enabled = context.Agent.enabled,

                AgentId = context.Agent.AgentId,
                DisplayName = context.Agent.DisplayName,
                Priority = context.Agent.Priority,

                IsRuntimeAdded = context.IsRuntimeAdded,
                SaveContext = context.SaveContext
            };
        }

        data.routineAgentContext = GetSaveableBehaviorContext(new BehaviorAgentContext
        {
            Agent = routineBehavior,
            IsRuntimeAdded = false, // Routine behavior is not added at runtime
            SaveContext = false // Routine behavior does not save context
        });

        data.queuedAgentContexts = executionDequeue.Select(GetSaveableBehaviorContext).ToList();
        data.currentAgentContext = currentBehaviorContext != null
            ? GetSaveableBehaviorContext(currentBehaviorContext)
            : null;
        
        return data;
    }

    public override void LoadSaveData(SaveableData data, bool blankLoad)
    {
        if (blankLoad)
        {
            if (currentBehaviorContext != null)
            {
                Debug.LogError("BehaviorController: blankLoad called but currentBehaviorContext is not null. This indicates a state error.", this);
            }
            // On a fresh start with no save data, just begin the routine behavior.
            if (!IdleOnExit)
            {
                BeginRoutine();
            }
            // else we start with no behavior enabled
            return;
        }

        var saveData = data as BehaviorControllerSaveableData;
        if (saveData == null)
        {
            Debug.LogError($"BehaviorController: Could not cast SaveableData to BehaviorControllerSaveableData. Data type is {data?.GetType().Name}. Performing blank load instead.", this);
            BeginRoutine();
            return;
        }

        // Step 1: Fill idle on exit
        this.IdleOnExit = saveData.IdleOnExit;

        BehaviorAgentContext DeserializeAgentContext(AgentContextSaveableData data)
        {
            // First, we check if the agent with the given ID already exists in the scene.
            string SavedAgentId = data.AgentId;
            var existingAgent = existingBehaviorGraphAgents.FirstOrDefault(a => a.AgentId == SavedAgentId);

            SaveableBehaviorGraphAgent agent;
            if (existingAgent == null)
            {
                // Then we need to construct a new agent component.
                agent = gameObject.AddComponent<SaveableBehaviorGraphAgent>();
                if (data.IsRuntimeAdded == false)
                {
                    Debug.LogWarning($"BehaviorController: saved agent with ID '{SavedAgentId}' is not runtime added, but no existing agent was found. This may indicate a state mismatch.", this);
                }
            }
            else
            {
                agent = existingAgent;
                if (data.IsRuntimeAdded == true)
                {
                    Debug.LogWarning($"BehaviorController: saved agent with ID '{SavedAgentId}' is runtime added, but an existing agent was found. This may indicate a state mismatch.", this);
                }
            }
            
            // Then we can load the data into the agent.
            agent.enabled = data.enabled; // Restore enabled state
            agent.SetId(SavedAgentId);
            agent.SetPriority(data.Priority);
            agent.SetDisplayData(data.DisplayName, ""); // Description is not saved in the context
            // Manually initialize the agent before deserializing its state.
            agent.Init();
            
            // Restore its blackboard and execution state from the serialized string.
            agent.Deserialize(data.SerializedAgentData, behaviorSerializer, unityObjectResolver);
            
            // Wrap the agent in a context and return it.
            return new BehaviorAgentContext
            {
                Agent = agent,
                IsRuntimeAdded = data.IsRuntimeAdded,
                SaveContext = data.SaveContext
            };
        }

        // Step 2: Restore routine behavior's internal state
        if (saveData.routineAgentContext != null)
        {
            // Sanity check to ensure the referenced routine agent hasn't changed drastically.
            if (routineBehavior.AgentId != saveData.routineAgentContext.AgentId)
            {
                Debug.LogWarning($"BehaviorController: Saved routine agent ID '{saveData.routineAgentContext.AgentId}' does not match scene routine agent ID '{routineBehavior.AgentId}'. State may be loaded into the wrong agent.", this);
            }
            // routineBehavior.Deserialize(saveData.routineAgentContext.SerializedAgentData, behaviorSerializer, unityObjectResolver);
            DeserializeAgentContext(saveData.routineAgentContext);
        }
        else
        {
            Debug.LogWarning("BehaviorController: Save data does not contain a routineAgentContext. Routine state will not be restored.", this);
        }

        // Step 3: Fill the execution dequeue with the saved contexts.
        // First, clear any existing runtime-added agents and the queue before loading.
        foreach (var context in executionDequeue)
        {
            if (context.IsRuntimeAdded && context.Agent != null)
            {
                Destroy(context.Agent);
            }
        }
        executionDequeue.Clear();

        if (saveData.queuedAgentContexts != null)
        {
            foreach (var savedContext in saveData.queuedAgentContexts)
            {
                if (savedContext == null) continue;

                var behaviorContext = DeserializeAgentContext(savedContext);
                if (behaviorContext.Agent == null)
                {
                    Debug.LogWarning($"BehaviorController: Deserialized agent context for ID '{savedContext.AgentId}' resulted in a null agent. This may indicate a state mismatch.", this);
                    continue; // Skip this context if the agent is null
                }
                executionDequeue.Add(behaviorContext);
            }
        }
        
        // Step 4: Load the current behavior context if it exists.
        if (saveData.currentAgentContext != null)
        {
            // If we are running the routine, there is nothing else to do
            if (!IsInRoutine)
            {
                // But if we are not running the routine, then we need to set the current behavior context
                currentBehaviorContext = DeserializeAgentContext(saveData.currentAgentContext);
            }
        }
        else
        {
            Debug.LogWarning("BehaviorController: Save data does not contain a currentAgentContext. No current behavior will be restored.", this);
            currentBehaviorContext = null; // Reset if no current context is provided
        }

        // Step 4: The controller will now process the restored queue to determine the next behavior.
        HandleProcessQueue();
    }

    private void Awake()
    {
        if (interruptFinishedEvent == null)
        {
            Debug.LogError("BehaviorController: interruptFinishedEvent is not set. Cannot detect when an interrupt finishes.");
            return;
        }

        interruptFinishedEvent.Event += HandleInterruptFinished;

        existingBehaviorGraphAgents = GetComponents<SaveableBehaviorGraphAgent>().ToList();
        // Debug.Log($"BehaviorController: Found {existingBehaviorGraphAgents.Count} existing BehaviorGraphAgents on {gameObject.name}.");
        foreach (var behavior in existingBehaviorGraphAgents)
        {
            behavior.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (interruptFinishedEvent != null)
        {
            interruptFinishedEvent.Event -= HandleInterruptFinished;
        }
    }

    private void Update()
    {
        if (currentBehaviorContext == null && !IdleOnExit)
        {
            BeginRoutine();
        }
    }

    public bool TryInterrupt(InterruptBehaviorDefinition interruptData, bool clearQueue = false)
    {
        // Check if the new priority is greater than or equal to the current behavior's priority
        if (currentBehaviorContext != null && interruptData.Priority < currentBehaviorContext.Agent.Priority)
        {
            return false; // We cannot interrupt with a lower priority
        }

        BehaviorGraph newGraph = interruptData.BehaviorGraph;
        Dictionary<string, object> blackboardData = interruptData.BlackboardData;

        if (newGraph == null)
        {
            Debug.LogError("BehaviorController: TryInterrupt called with null BehaviorGraph.");
            return false;
        }

        // Step 1: Create a new BehaviorGraphAgent
        SaveableBehaviorGraphAgent newAgent = gameObject.AddComponent<SaveableBehaviorGraphAgent>();
        newAgent.enabled = false; // Start disabled so that we can set it up properly
        newAgent.SetId(interruptData.Id);
        newAgent.SetPriority(interruptData.Priority);
        newAgent.SetDisplayData(
            interruptData.DisplayName,
            interruptData.DisplayDescription
        );
        newAgent.Graph = newGraph;

        // And then we can manually initialize the agent
        newAgent.Init();
        
        // Following https://docs.unity3d.com/Packages/com.unity.behavior@1.0/api/Unity.Behavior.BehaviorGraphAgent.html#Unity_Behavior_BehaviorGraphAgent_Graph
        // I think if you dynamically create the agent you have to set the variables after Init.
        foreach (var kvp in blackboardData)
        {
            bool set = newAgent.SetVariableValue(kvp.Key, kvp.Value);
            if (!set)
            {
                Debug.LogError($"BehaviorController: blackboard data for {newAgent.DisplayName} doesn't contain {kvp.Key}.");
            }
        }

        // Step 2: Check if we can interrupt the current behavior
        if (!InterruptCurrentBehavior())
        {
            Debug.LogError("BehaviorController: Failed to interrupt current behavior.");
            Destroy(newAgent); // Clean up the agent we just created
            return false;
        }

        // Step 3: Start the new behavior
        if (!StartBehavior(newAgent))
        {
            Debug.LogError("BehaviorController: Failed to start new behavior.");
            Destroy(newAgent); // Clean up the agent we just created
            return false;
        }

        // Step 4: Set the current behavior context
        currentBehaviorContext = new BehaviorAgentContext
        {
            Agent = newAgent,
            IsRuntimeAdded = true, // This agent was added at runtime
            SaveContext = interruptData.SaveContext
        };

        if (clearQueue)
        {
            // Then we also clear the execution queue and destroy all runtime-added agents
            foreach (var context in executionDequeue)
            {
                if (context.IsRuntimeAdded && context.Agent != null)
                {
                    Destroy(context.Agent);
                }
            }
            
            executionDequeue.Clear();
        }
        
#if UNITY_EDITOR
        if (pauseOnInterrupt)
        {
            // If we are in the editor, we pause the game to see the interrupt in action
            UnityEditor.EditorApplication.isPaused = true;
        }
#endif

        // Debug.Log($"BehaviorController: Successfully interrupted current behavior with {newAgent.name}.");
        return true;
    }

    /// <summary>
    /// Puts a behavior graph into the back of the queue to be processed last.
    /// If idle or running routine, it will start the behavior immediately.
    /// </summary>
    /// <param name="interruptData"></param>
    public void EnqueueInterrupt(InterruptBehaviorDefinition interruptData)
    {
        if (interruptData == null)
        {
            Debug.LogError("BehaviorController: EnqueueInterrupt called with null InterruptBehaviorDefinition.");
            return;
        }

        // Create a new BehaviorGraphAgent for the interrupt
        SaveableBehaviorGraphAgent newAgent = gameObject.AddComponent<SaveableBehaviorGraphAgent>();
        newAgent.enabled = false; // Start disabled so that we can set it up properly
        newAgent.SetId(interruptData.Id);
        newAgent.SetPriority(interruptData.Priority);
        newAgent.SetDisplayData(
            interruptData.DisplayName,
            interruptData.DisplayDescription
        );
        newAgent.Graph = interruptData.BehaviorGraph;

        // Manually initialize the agent
        newAgent.Init();
        
        // Set the blackboard data for the agent
        foreach (var kvp in interruptData.BlackboardData)
        {
            newAgent.SetVariableValue(kvp.Key, kvp.Value);
        }

        // Step 1: Add the agent to the queue
        var context = new BehaviorAgentContext
        {
            Agent = newAgent,
            IsRuntimeAdded = true, // This agent was added at runtime
            SaveContext = interruptData.SaveContext
        };

        executionDequeue.Insert(0, context); // We execute last to first, so we add to the front of the list

        // Debug.Log($"BehaviorController: Enqueued interrupt behavior {newAgent.name} with priority {newAgent.Priority}.");

        // Step 2: If we are idle or running routine, start the behavior immediately
        if (currentBehaviorContext == null || IsInRoutine)
        {
#if UNITY_EDITOR
            if (pauseOnInterrupt)
            {
                // If we are in the editor, we pause the game to see the interrupt in action
                UnityEditor.EditorApplication.isPaused = true;
            }
#endif
            HandleProcessQueue();
        }
        
        // Clean up the queue
        DeduplicateQueue();
    }

    /// <summary>
    /// Interrupts the current behavior if one is running and moves to the next behavior in the queue.
    /// </summary>
    /// <returns></returns>
    public bool TryProceed(float priority = float.MaxValue)
    {
        if (currentBehaviorContext == null)
        {
            // Then all we have to do is move to the next behavior in the queue
            HandleProcessQueue();
            return true;
        }

        if (priority < currentBehaviorContext.Agent.Priority)
        {
            return false; // We cannot proceed with a lower priority
        }

        if (!InterruptCurrentBehavior())
        {
            // Some error occurred while trying to interrupt the current behavior
            return false;
        }

        // Now we can proceed to the next behavior in the queue
        HandleProcessQueue();
        return true;
    }

    /// <summary>
    /// If ever two or more agents in the queue that are adjacent have the same AgentId, we remove the one with lower
    /// priority or the one later in the queue. This naturally has the desired behavior for player interaction.
    /// If the player clicks to move in one place and then clicks in another, it will generate two behaviors with the
    /// same AgentId and priority. By choosing the later one (i.e. earlier in the array), we stop the running one and
    /// move to the new one as the player expects.
    /// 
    /// We include the current context in the queue. If the current context is marked for deduplication then at the end
    /// of the function call we interrupt the current behavior and start the next one in the queue.
    /// </summary>
    private void DeduplicateQueue()
    {
        // If there's nothing to deduplicate (0 or 1 items total), do nothing.
        int totalBehaviors = (currentBehaviorContext == null ? 0 : 1) + executionDequeue.Count;
        if (totalBehaviors <= 1)
        {
            return;
        }
        
        // Step 1: Create a single list representing the full stack of work.
        // The top of the stack (currently executing or next to execute) is at the end of the list.
        var fullStack = new List<BehaviorAgentContext>();
        fullStack.AddRange(executionDequeue);
        if (currentBehaviorContext != null)
        {
            fullStack.Add(currentBehaviorContext);
        }
        
        // Step 2: Iteratively find and mark adjacent duplicates for removal until no more changes occur.
        // This handles chains of duplicates correctly (e.g., A, A, A -> final A).
        var itemsToRemove = new HashSet<BehaviorAgentContext>();
        bool changedInPass;
        do
        {
            changedInPass = false;
            // The list of items to check in this pass are those not already marked for removal.
            var stackToCheck = fullStack.Where(c => !itemsToRemove.Contains(c)).ToList();

            if (stackToCheck.Count <= 1)
            {
                break; // No more adjacent items to check.
            }

            // Iterate from the top of the current logical stack downwards.
            for (int i = stackToCheck.Count - 1; i > 0; i--)
            {
                BehaviorAgentContext itemOnTop = stackToCheck[i];
                BehaviorAgentContext itemBelow = stackToCheck[i - 1];

                if (itemOnTop.Agent.AgentId == itemBelow.Agent.AgentId)
                {
                    // Found adjacent duplicates. Keep the one with higher priority.
                    BehaviorAgentContext itemToDiscard;
                    if (itemOnTop.Agent.Priority > itemBelow.Agent.Priority)
                    {
                        itemToDiscard = itemBelow;
                    }
                    else
                    {
                        itemToDiscard = itemOnTop;
                    }

                    if (itemsToRemove.Add(itemToDiscard))
                    {
                        // A new item was marked for removal, so we must run another pass.
                        changedInPass = true;
                    }
                }
            }
        } while (changedInPass);
        
        if (itemsToRemove.Count == 0)
        {
            return; // No duplicates were found.
        }
        
        // Step 3: Process the removal list.
        bool shouldInterruptCurrent = currentBehaviorContext != null && itemsToRemove.Contains(currentBehaviorContext);

        // Filter the execution dequeue, creating a new list of items to keep.
        var newExecutionDequeue = new List<BehaviorAgentContext>();
        foreach (var context in executionDequeue)
        {
            if (itemsToRemove.Contains(context))
            {
                // This queued item is being removed. If it's a runtime agent, destroy it.
                if (context.IsRuntimeAdded && context.Agent != null)
                {
                    Destroy(context.Agent);
                }
            }
            else
            {
                // Keep this item.
                newExecutionDequeue.Add(context);
            }
        }
        executionDequeue = newExecutionDequeue;
        
        // Step 4: If the current behavior was marked for removal, interrupt it and proceed to the next in the queue.
        if (shouldInterruptCurrent)
        {
            // TryProceed will handle stopping the current behavior, clearing the context,
            // and starting the next item from the now-deduplicated queue.
            // It uses InterruptCurrentBehavior, which will destroy the runtime agent since SaveContext is false.
            TryProceed();
        }
    }
    
    private void HandleInterruptFinished(GameObject interruptedObject)
    {
        if (interruptedObject != gameObject)
        {
            // This is an interrupt for another object, ignore it
            return;
        }

        HandleCurrentInterruptFinished();
        HandleProcessQueue();
    }

    private void HandleCurrentInterruptFinished()
    {
        // Debug.Log($"BehaviorController: Handling current interrupt finished for {gameObject.name}.");
        // Step 1: Stop the behavior so that it does not restart
        BehaviorGraphAgent agent = currentBehaviorContext.Agent;
        StopBehavior(agent);

        // Step 2: Destroy the agent if it was runtime added
        if (currentBehaviorContext.IsRuntimeAdded)
        {
            Destroy(agent);
        }

        // Step 3: Clear the current behavior context
        currentBehaviorContext = null;
    }

    private void BeginRoutine()
    {
        if (currentBehaviorContext != null)
        {
            Debug.LogWarning("BehaviorController: BeginRoutine called while already processing a behavior. Ignoring.");
            return; // We are already processing a behavior
        }

        if (routineBehavior == null)
        {
            Debug.LogError(
                "BehaviorController: BeginRoutine called but routineBehavior is not set. Cannot start routine behavior.");
            return; // We cannot start the routine behavior
        }

        currentBehaviorContext = new BehaviorAgentContext
        {
            Agent = routineBehavior,
            IsRuntimeAdded = false, // This agent is not added at runtime
            SaveContext = false // Routine behavior does not save context
        };
        StartBehavior(routineBehavior);
    }

    private void HandleProcessQueue()
    {
        if (currentBehaviorContext != null && !IsInRoutine)
        {
            Debug.LogWarning(
                "BehaviorController: HandleProcessQueue called while already processing an interrupt. Ignoring.");
            return; // We are already processing a behavior
        }

        if (executionDequeue.Count == 0)
        {
            // No behaviors to process, check if we should go to idle or routine
            if (IdleOnExit)
            {
                // Debug.Log("BehaviorController: Queue is empty, going to idle state.");
                return; // We are idle
            }
            else
            {
                // Debug.Log("BehaviorController: Queue is empty, going to routine state.");
                if (!IsInRoutine)
                {
                    BeginRoutine();
                }
                return; // We are in routine state
            }
        }

        // Otherwise, we grab the next behavior from the queue

        if (IsInRoutine)
        {
            // Then we first need to interrupt the current routine behavior
            if (!InterruptCurrentBehavior())
            {
                Debug.LogError("BehaviorController: Failed to interrupt current routine behavior.");
                return; // We cannot proceed if we failed to interrupt
            }
        }
        
        // var nextContext = executionDequeue.Last.Value;
        // executionDequeue.RemoveLast();
        var nextContext = executionDequeue[^1];
        executionDequeue.RemoveAt(executionDequeue.Count - 1);
        currentBehaviorContext = nextContext;
        // Debug.Log($"BehaviorController: Starting behavior {currentBehaviorContext.Agent.name} with priority {currentBehaviorContext.Agent.Priority}.");
        if (!StartBehavior(currentBehaviorContext.Agent))
        {
            Debug.LogError($"BehaviorController: Failed to start behavior {currentBehaviorContext.Agent.name}.");
            currentBehaviorContext = null; // Reset the context if we failed to start
            return;
        }
    }

    private bool InterruptCurrentBehavior()
    {
        if (currentBehaviorContext == null)
        {
            // We have no current behavior to interrupt.
            return true;
        }

        // Step 1: Stop the currently running behavior
        BehaviorGraphAgent agent = currentBehaviorContext.Agent;
        bool stopped = StopBehavior(agent);
        if (!stopped)
        {
            return false;
        }

        // Step 2: Save the current context if requested
        // This allows us to return to this agent later
        if (currentBehaviorContext.SaveContext)
        {
            // executionDequeue.AddLast(currentBehaviorContext);
            executionDequeue.Add(currentBehaviorContext);
        }
        else if (currentBehaviorContext.IsRuntimeAdded)
        {
            // Otherwise, if this is a runtime added agent, we destroy it
            Destroy(agent);
        }

        // Step 3: Clear the current context
        currentBehaviorContext = null;

        return true;
    }

    private bool StopBehavior(BehaviorGraphAgent agent)
    {
        if (!agent.enabled) return false;

        agent.enabled = false;
        agent.End(); // Ensures that the cleanup logic in the agent is called
        return true;
    }

    private bool StartBehavior(BehaviorGraphAgent agent)
    {
        if (agent.enabled) return false;

        agent.Restart();
        agent.enabled = true;
        return true;
    }

    public bool HasBehaviorInQueue(string agentId, bool includeCurrent = true)
    {
        if (routineBehavior != null && routineBehavior.AgentId == agentId)
        {
            return true; // The routine behavior is always in the queue
        }

        if (includeCurrent && currentBehaviorContext != null && currentBehaviorContext.Agent.AgentId == agentId)
        {
            return true; // The current behavior is in the queue
        }

        foreach (var context in executionDequeue)
        {
            if (context.Agent.AgentId == agentId)
            {
                return true; // The agent is in the queue
            }
        }

        return false; // The agent is not in the queue
    }

    public class UnityObjectResolver : RuntimeSerializationUtility.IUnityObjectResolver<string>
    {
        private static string GOPrefix = "goKey:";
        private static string TransformPrefix = "transformKey:";
        private static string IdentifiableSOPrefix = "identifiableSOKey:";
        private static string BehaviorGraphPrefix = "behaviorGraphKey:";

        public string Map(Object obj)
        {
            Debug.Log($"Serailizing something");
            if (obj == null)
            {
                Debug.Log("Null object");
                return "null";
            }

            Debug.Log($"Not null object {obj}");
            
            Debug.Log($"BeahviorController: Serializing Unity object: {obj?.name} of type {obj?.GetType()}");

            // We can serialize GameObjects, Transforms, SOs that derive from IdentifiableSO, and BehaviorGraphs
            // Using the SaveableDataManager.
            if (SaveableDataManager.Instance == null)
            {
                throw new InvalidOperationException(
                    "UnityObjectResolver: SaveableDataManager is not initialized. Cannot resolve Unity objects.");
            }

            if (obj == null)
            {
                return "null";
            }

            if (typeof(GameObject).IsAssignableFrom(obj.GetType()))
            {
                // Then we are serializing a GameObject.
                GameObject go = (GameObject)obj;
                string goKey = SaveableDataManager.Instance.GetGameObjectKey(go);
                if (goKey == null)
                {
                    Debug.LogError(
                        $"Tried to serialize GameObject '{go.name}' but it has no ID. Make sure it is registered with SaveableDataManager.");
                }

                return $"{GOPrefix}{goKey}";
            }
            else if (typeof(Transform).IsAssignableFrom(obj.GetType()))
            {
                // Then we are serializing a Transform. Since these are unique per GameObject, we can serialize the
                // GameObject instead and get the transform when we deserialize.
                Transform transform = (Transform)obj;
                GameObject go = transform.gameObject;
                string goKey = SaveableDataManager.Instance.GetGameObjectKey(go);
                if (goKey == null)
                {
                    Debug.LogError(
                        $"Tried to serialize Transform '{transform.name}' but its GameObject has no ID. Make sure it is registered with SaveableDataManager.");
                }

                return $"{TransformPrefix}{goKey}";
            }
            else if (typeof(Component).IsAssignableFrom(obj.GetType()))
            {
                // If there is only one of this component on the GameObject, we can serialize the gameobject and
                // get the component when we deserialize.
                Component component = (Component)obj;
                GameObject go = component.gameObject;
                List<Component> components = go.GetComponents(obj.GetType()).ToList();
                if (components.Count != 1)
                {
                    throw new InvalidOperationException(
                        $"UnityObjectResolver: Cannot serialize component of type {obj.GetType()} on GameObject {go.name} because there are {components.Count} instances. Only one instance is supported.");
                }
                // Otherwise we can serialize the GameObject and get the component when we deserialize.
                string goKey = SaveableDataManager.Instance.GetGameObjectKey(go);
                if (goKey == null)
                {
                    Debug.LogError(
                        $"Tried to serialize Component '{component.name}' but its GameObject has no ID. Make sure it is registered with SaveableDataManager.");
                }
                return $"{GOPrefix}{goKey}";
            }
            else if (typeof(IdentifiableSO).IsAssignableFrom(obj.GetType()))
            {
                // Then we are serializing a ScriptableObject that derives from IdentifiableSO.
                IdentifiableSO identifiableSO = (IdentifiableSO)obj;
                string soKey = SaveableDataManager.Instance.GetIdentifiablSOId(identifiableSO);
                if (soKey == null)
                {
                    Debug.LogError(
                        $"Tried to serialize ScriptableObject '{identifiableSO.name}' but it has no ID. Make sure it is registered with SaveableDataManager.");
                }

                return $"{IdentifiableSOPrefix}{soKey}";
            }
            else if (typeof(BehaviorGraph).IsAssignableFrom(obj.GetType()))
            {
                // Then we are serializing a BehaviorGraph.
                BehaviorGraph behaviorGraph = (BehaviorGraph)obj;
                string graphKey = SaveableDataManager.Instance.GetBehaviorGraphKey(behaviorGraph);
                if (graphKey == null)
                {
                    Debug.LogError(
                        $"Tried to serialize BehaviorGraph '{behaviorGraph.name}' but it has no ID. Make sure it is registered with SaveableDataManager.");
                }

                return $"{BehaviorGraphPrefix}{graphKey}";
            }
            else
            {
                Debug.LogError(
                    $"UnityObjectResolver: Cannot resolve Unity object of type {obj.GetType()}. Only GameObjects, Transforms, IdentifiableSOs, and BehaviorGraphs are supported.");
                return null; // We cannot resolve this object
            }
        }

        public TSerializedType Resolve<TSerializedType>(string key)
            where TSerializedType : Object
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning("UnityObjectResolver: Resolve called with null or empty key. Returning null.");
                return null; // We cannot resolve a null or empty key
            }
            
            if (key == "null")
            {
                return null; // We cannot resolve a 'null' key
            }

            if (SaveableDataManager.Instance == null)
            {
                Debug.LogError(
                    "UnityObjectResolver: SaveableDataManager is not initialized. Cannot resolve Unity objects.");
                return null; // We cannot resolve objects without the SaveableDataManager
            }

            if (typeof(GameObject).IsAssignableFrom(typeof(TSerializedType)))
            {
                // Then we are resolving a GameObject.
                if (key.StartsWith(GOPrefix))
                {
                    string goKey = key.Substring(GOPrefix.Length);
                    GameObject go = SaveableDataManager.Instance.GetGameObjectFromKey(goKey);
                    if (go == null)
                    {
                        Debug.LogError(
                            $"UnityObjectResolver: Could not resolve GameObject with key '{goKey}'. It may not be registered with SaveableDataManager.");
                    }

                    return go as TSerializedType;
                }
                else
                {
                    Debug.LogError(
                        $"UnityObjectResolver: Resolve called with invalid GameObject key '{key}'. Expected prefix '{GOPrefix}'.");
                }
            }
            else if (typeof(Transform).IsAssignableFrom(typeof(TSerializedType)))
            {
                // Then we are resolving a Transform.
                if (key.StartsWith(TransformPrefix))
                {
                    string goKey = key.Substring(TransformPrefix.Length);
                    GameObject go = SaveableDataManager.Instance.GetGameObjectFromKey(goKey);
                    if (go == null)
                    {
                        Debug.LogError(
                            $"UnityObjectResolver: Could not resolve Transform with key '{goKey}'. It may not be registered with SaveableDataManager.");
                        return null;
                    }

                    return go.transform as TSerializedType;
                }
                else
                {
                    Debug.LogError(
                        $"UnityObjectResolver: Resolve called with invalid Transform key '{key}'. Expected prefix '{TransformPrefix}'.");
                }
            }
            else if (typeof(Component).IsAssignableFrom(typeof(TSerializedType)))
            {
                // Then we are resolving a Component.
                // To do this we first resolve the GameObject and then get the component from it.
                if (key.StartsWith(GOPrefix))
                {
                    string goKey = key.Substring(GOPrefix.Length);
                    GameObject go = SaveableDataManager.Instance.GetGameObjectFromKey(goKey);
                    if (go == null)
                    {
                        Debug.LogError(
                            $"UnityObjectResolver: Could not resolve Component with key '{goKey}'. It may not be registered with SaveableDataManager.");
                        return null;
                    }

                    // Now we can get the component from the GameObject
                    List<Component> components = go.GetComponents(typeof(TSerializedType)).ToList();
                    if (components.Count != 1)
                    {
                        Debug.LogError(
                            $"UnityObjectResolver: Cannot resolve component of type {typeof(TSerializedType)} on GameObject {go.name} because there are {components.Count} instances. Only one instance is supported.");
                        return null;
                    }

                    return components[0] as TSerializedType;
                }
                else
                {
                    Debug.LogError(
                        $"UnityObjectResolver: Resolve called with invalid Component key '{key}'. Expected prefix '{GOPrefix}'.");
                }
            }
            else if (typeof(IdentifiableSO).IsAssignableFrom(typeof(TSerializedType)))
            {
                // Then we are resolving a ScriptableObject that derives from IdentifiableSO.
                if (key.StartsWith(IdentifiableSOPrefix))
                {
                    string soKey = key.Substring(IdentifiableSOPrefix.Length);
                    IdentifiableSO so = SaveableDataManager.Instance.GetIdentifiableSOFromId(soKey);
                    if (so == null)
                    {
                        Debug.LogError(
                            $"UnityObjectResolver: Could not resolve IdentifiableSO with key '{soKey}'. It may not be registered with SaveableDataManager.");
                        return null;
                    }

                    return so as TSerializedType;
                }
                else
                {
                    Debug.LogError(
                        $"UnityObjectResolver: Resolve called with invalid IdentifiableSO key '{key}'. Expected prefix '{IdentifiableSOPrefix}'.");
                }
            }
            else if (typeof(BehaviorGraph).IsAssignableFrom(typeof(TSerializedType)))
            {
                // Then we are resolving a BehaviorGraph.
                if (key.StartsWith(BehaviorGraphPrefix))
                {
                    string graphKey = key.Substring(BehaviorGraphPrefix.Length);
                    BehaviorGraph graph = SaveableDataManager.Instance.GetBehaviorGraphFromKey(graphKey);
                    if (graph == null)
                    {
                        Debug.LogError(
                            $"UnityObjectResolver: Could not resolve BehaviorGraph with key '{graphKey}'. It may not be registered with SaveableDataManager.");
                        return null;
                    }

                    return graph as TSerializedType;
                }
                else
                {
                    Debug.LogError(
                        $"UnityObjectResolver: Resolve called with invalid BehaviorGraph key '{key}'. Expected prefix '{BehaviorGraphPrefix}'.");
                }
            }
            else
            {
                Debug.LogError(
                    $"UnityObjectResolver: Cannot resolve Unity object of type {typeof(TSerializedType)}. Only GameObjects, Transforms, IdentifiableSOs, and BehaviorGraphs are supported.");
            }
            return null; // We cannot resolve this object
        }
    }

    public void RemoveBehaviorById(string interruptGraphId, bool includeCurrent = true)
    {
        // Step 1: Remove all agents with the given ID from the execution queue
        executionDequeue.RemoveAll(context => context.Agent.AgentId == interruptGraphId);
        
        // Step 2: If the current behavior context has the same ID, proceed
        if (includeCurrent && currentBehaviorContext != null && currentBehaviorContext.Agent.AgentId == interruptGraphId)
        {
            TryProceed();
        }
    }
}