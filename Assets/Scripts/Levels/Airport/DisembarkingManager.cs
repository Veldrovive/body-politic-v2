using System;
using System.Linq;
using UnityEngine;

public class DisemarkingManagerSaveableData : SaveableData
{
    public float DisembarkEndTime;
    public float NextDisembarkTime;
    public float MinTimeBetweenDisembarking;
    public float MaxTimeBetweenDisembarking;
}

public class DisembarkingManager : SaveableGOConsumer
{
    [SerializeField] private PooledNpcManager passengerNpcPool;
    
    [SerializeField] private float minTimeBetweenDisembarking = 60;
    [SerializeField] private float maxTimeBetweenDisembarking = 60;

    [Tooltip("The length of time it takes for all passengers to clear the disembarking area.")]
    [SerializeField] private float disembarkLength = 40f;
    [SerializeField] private BoolVariableSO isDisembarking;

    private float disembarkEndTime = float.MinValue;
    private float nextDisembarkTime = 0;

    public override void LoadSaveData(SaveableData data, bool blankLoad)
    {
        if (blankLoad)
        {
            nextDisembarkTime = SaveableDataManager.Instance.time;
        }
        else
        {
            if (data is not DisemarkingManagerSaveableData disembarkingData)
            {
                Debug.LogError("DisembarkingManager received invalid save data.", this);
                return;
            }
            
            disembarkEndTime = disembarkingData.DisembarkEndTime;
            nextDisembarkTime = disembarkingData.NextDisembarkTime;
            minTimeBetweenDisembarking = disembarkingData.MinTimeBetweenDisembarking;
            maxTimeBetweenDisembarking = disembarkingData.MaxTimeBetweenDisembarking;
        }
    }

    public override SaveableData GetSaveData()
    {
        return new DisemarkingManagerSaveableData()
        {
            DisembarkEndTime = disembarkEndTime,
            NextDisembarkTime = nextDisembarkTime,
            MinTimeBetweenDisembarking = minTimeBetweenDisembarking,
            MaxTimeBetweenDisembarking = maxTimeBetweenDisembarking
        };
    }

    private void Update()
    {
        if (SaveableDataManager.Instance.time >= nextDisembarkTime)
        {
            // We can disembark a passenger
            DisembarkPassengers();
            // Set the next disembark time
            nextDisembarkTime = SaveableDataManager.Instance.time + UnityEngine.Random.Range(minTimeBetweenDisembarking, maxTimeBetweenDisembarking);
        }

        if (isDisembarking != null)
        {
            if (isDisembarking.Value && SaveableDataManager.Instance.time >= disembarkEndTime)
            {
                // Disembarking has ended, reset the state
                isDisembarking.Value = false;
                Debug.Log("Disembarking has ended.");
            }
        }
    }

    private void DisembarkPassengers()
    {
        Debug.Log($"DisembarkPassengers (was {nextDisembarkTime} - Time: {SaveableDataManager.Instance.time})");

        int numCanDisembark = passengerNpcPool.NpcPool.Count(npcContext => passengerNpcPool.IsNpcReadyForReset(npcContext));
        if (numCanDisembark != passengerNpcPool.NpcPool.Count())
        {
            Debug.Log("Not all passengers are ready to disembark. Cannot proceed with disembarking.");
            return;
        }
        
        passengerNpcPool.ResetAllNpcs();  // Resets those NPCs that are ready for reset

        if (isDisembarking != null)
        {
            isDisembarking.Value = true;
            disembarkEndTime = SaveableDataManager.Instance.time + disembarkLength;
        }
    }
}