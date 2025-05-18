using UnityEngine;
using System;

[Serializable]
public class GameObjectReference : AbstractReference<GameObject, GameObjectVariableSO>
{
    public GameObjectReference() : base() { }
    public GameObjectReference(GameObject value) : base(value) { }
    public GameObjectReference(GameObjectVariableSO variable) : base(variable) { }
}