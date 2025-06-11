using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class SaveableSO : IdentifiableSO
{
    [SerializeField] private List<SceneId> ActiveSceneIds = new List<SceneId>();  // Will only be saved if the SaveableSO is active in the current scene.
    
    public abstract SaveableData GetSaveData();
    public abstract void LoadSaveData(SaveableData data, bool blankLoad);
}