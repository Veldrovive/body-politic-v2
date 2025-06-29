using System;
using Unity.Behavior;
using UnityEngine;
using Unity.Properties;

#if UNITY_EDITOR
[CreateAssetMenu(menuName = "Behavior/Event Channels/Test")]
#endif
[Serializable, GeneratePropertyBag]
[EventChannelDescription(name: "Test", message: "[asdf] [sdf]", category: "Events", id: "6ce77fe17a0bf0cc39e680a517a5c412")]
public sealed partial class Test : EventChannel<BehaviorGraph, SaveableBehaviorGraphAgent> { }

