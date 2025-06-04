/// <summary>
/// Does not directly interract with the save system, but provides an interface that says that this object exposes
/// a serializable save data type that can be used by a ISaveable to save and load data from components it manages.
/// </summary>
/// <typeparam name="TDataType"></typeparam>
public interface IConsumesSaveData<TDataType>
    where TDataType : SaveableData
{
    /// <summary>
    /// Gets the save data for this object.
    /// </summary>
    /// <returns>The save data.</returns>
    public TDataType GetSaveData();

    /// <summary>
    /// Sets the save data for this object.
    /// </summary>
    /// <param name="data">The save data to set.</param>
    public void SetSaveData(TDataType data);
}