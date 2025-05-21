using System;
using UnityEngine;

public enum CursorType
{
    Default,
    Selection
}

public class CursorManager : MonoBehaviour
{
    [SerializeField] private Texture2D defaultCursor;
    [SerializeField] private Vector2 defaultCursorHotSpot;
    
    [SerializeField] private Texture2D selectionCursor;
    [SerializeField] private Vector2 selectionCursorHotSpot;
    
    private void Start()
    {
        // Set the default cursor at the start
        SetCursor(CursorType.Default);
    }
    
    public void SetCursor(CursorType cursorType)
    {
        Texture2D desiredCursor;
        Vector2 desiredHotSpot;
        switch (cursorType)
        {
            case CursorType.Default:
                desiredCursor = defaultCursor;
                desiredHotSpot = defaultCursorHotSpot;
                break;
            case CursorType.Selection:
                desiredCursor = selectionCursor;
                desiredHotSpot = selectionCursorHotSpot;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(cursorType), cursorType, null);
        }

        if (desiredCursor == null)
        {
            Debug.LogWarning("Cursor texture is null. Using defaults.", this);
            desiredCursor = defaultCursor;
            desiredHotSpot = defaultCursorHotSpot;
        }
        
        Cursor.SetCursor(desiredCursor, desiredHotSpot, CursorMode.Auto);
    }
}