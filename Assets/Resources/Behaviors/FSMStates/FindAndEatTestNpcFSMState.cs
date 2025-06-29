using System;
using Unity.Behavior;

[BlackboardEnum]
public enum FindAndEatTestNpcFSMState
{
    PickUpConsumable,
	EatConsumable,
	Wait,
	LamentNoFood,
}
