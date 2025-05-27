using UnityEngine;

public abstract class AbstractDoorOpener : MonoBehaviour
{
    protected bool isOpen = false;
    public bool IsOpen => isOpen;
    
    public abstract void Open();
    public abstract void Close();

    public void Toggle()
    {
        if (isOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }
}