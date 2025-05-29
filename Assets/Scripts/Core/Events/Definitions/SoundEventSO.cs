using System;
using UnityEngine;
using UnityEngine.Serialization;

public enum SoundLoudness
{
    Quiet,
    Normal,
    Loud
}

[Serializable]
public class SoundData
{
    public string SoundInstanceId = Guid.NewGuid().ToString();  // Can be used to end the sound instance later if needed
    public AudioClip Clip;
    public int Suspiciousness;
    public SoundLoudness Loudness;
    public Vector3 EmanationPoint;
    public bool CausesReactions;
    public GameObject CreatorObject = null;
}

[CreateAssetMenu(fileName = "NewSoundEvent", menuName = "Events/Sound Event SO")]
public class SoundEventSO : GameEventSO<SoundData> {
    
}