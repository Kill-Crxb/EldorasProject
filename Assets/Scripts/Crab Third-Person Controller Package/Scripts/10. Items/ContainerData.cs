using UnityEngine;

// ContainerData.cs - For chest/inventory persistence (Online-game friendly)
[System.Serializable]
public class ContainerData
{
    public string containerId;
    public ContainerType type;
    public int gridWidth;
    public int gridHeight;
    public long lastModified;
    public ItemInstance[] items;

    // For network serialization
    public string ToJson()
    {
        return JsonUtility.ToJson(this, true);
    }

    public static ContainerData FromJson(string json)
    {
        return JsonUtility.FromJson<ContainerData>(json);
    }

    // For database storage
    public byte[] ToBinary()
    {
        string json = ToJson();
        return System.Text.Encoding.UTF8.GetBytes(json);
    }

    public static ContainerData FromBinary(byte[] data)
    {
        string json = System.Text.Encoding.UTF8.GetString(data);
        return FromJson(json);
    }
}

public enum ContainerType
{
    PlayerInventory,
    Chest,
    Bank,
    Vendor,
    Mailbox
}