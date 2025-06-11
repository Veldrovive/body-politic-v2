using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[NodeInfo("Say String", "Event Listener/Say String")]
public class SayStringListenerNode : EventListenerNode
{
    [SerializeField] private float bubbleDuration = 3f;
    public static string SAY_PORT_NAME = "String";
    [EventInputPort("String")]
    public void SayString(string message)
    {
        // If the message is not empty, create a speech bubble with the message.
        if (!string.IsNullOrEmpty(message) && npcContext != null)
        {
            npcContext.SpeechBubbleManager.ShowBubble(
                message, bubbleDuration    
            );
        }
    }
}