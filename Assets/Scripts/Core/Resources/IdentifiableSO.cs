#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public abstract class IdentifiableSO : ScriptableObject
{
    // The asset's own GUID is the most reliable ID.
    // It's hidden in the inspector because a designer should never change it.
    [SerializeField]
    private string identifierId;
    public string ID => identifierId;

    // OnValidate is the perfect place for this. It's called when the asset is
    // created, imported, or changed in the inspector.
    protected virtual void OnValidate()
    {
#if UNITY_EDITOR
        // Get the path of the asset
        string path = AssetDatabase.GetAssetPath(this);

        // Get the GUID from the path. If the asset is not saved yet, path will be empty.
        // In that case, we do nothing. The ID will be set when the user saves the asset.
        if (!string.IsNullOrEmpty(path))
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            
            // If the GUID has changed (e.g., asset was duplicated), update it.
            if (identifierId != guid)
            {
                identifierId = guid;
                EditorUtility.SetDirty(this); // Mark the asset to be saved
            }
        }
#endif
    }
}