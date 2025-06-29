using System;
using Unity.Behavior;
using UnityEngine;
using Unity.Properties;

#if UNITY_EDITOR
[CreateAssetMenu(menuName = "Behavior/Event Channels/InterruptFinishedEvent")]
#endif
[Serializable, GeneratePropertyBag]
[EventChannelDescription(name: "InterruptFinishedEvent", message: "Interrupt finished on [Self]", category: "Events", id: "1c993274f3d79a7e6236a6a6063059e4")]
public sealed partial class InterruptFinishedEvent : EventChannel<GameObject> { }

