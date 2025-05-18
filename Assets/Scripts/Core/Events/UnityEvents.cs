// Purpose: Concrete UnityEvent subclasses for serialization.

using UnityEngine;
using UnityEngine.Events;
using System;  // For System.Serializable

// --- We create one class for each data type we want to pass with events ---
// This is necessary for Unity to serialize the events properly

[Serializable]
public class UnityEventInt : UnityEvent<int> { }

[Serializable]
public class UnityEventFloat : UnityEvent<float> { }

[Serializable]
public class UnityEventString : UnityEvent<string> { }

[Serializable]
public class UnityEventBool : UnityEvent<bool> { }

[Serializable]
public class UnityEventGameObject : UnityEvent<GameObject> { }

[Serializable]
public class UnityEventTargetInfo : UnityEvent<TargetInfoData> { }

[Serializable]
public class UnityEventCameraModeRequest : UnityEvent<CameraModeRequest> { }

[Serializable]
public class UnityEventInfectionData : UnityEvent<InfectionData> { }