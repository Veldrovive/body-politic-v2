using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;

[CreateAssetMenu(fileName = "AggInterruptBehaviorFactory", menuName = "Body Politic/Aggregate Interrupt Behavior Factory")]
public class AggInterruptBehaviorFactory : ScriptableObject
{
    [Header("Move To Interrupt Behavior")]
    public MoveToBehaviorFactory MoveToBehaviorFactory;
    
    [Header("Move and Use Interrupt Behavior")]
    public MoveAndUseBehaviorFactory MoveAndUseBehaviorFactory;
}