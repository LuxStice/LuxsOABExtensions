
using LuxsOABExtensions;
using UnityEngine;

public struct SizeConfigData
{
    //public string author;
    public List<LOABESizeData> Sizes;
}

public struct PartsConfigData
{
    public List<LOABEPartData> Parts;
}

public struct LOABEPartDataCollection
{
    public string mod;
    public List<LOABEPartData> LOABEPartData;
}

public struct LOABEPartData
{
    public string LOABESizeID;
    public List<string> partNames;

    public bool TryGetSize(out LOABESize size)
    {
        size = default;

        if(IsID(out int ID))
        {
            size = LuxsOABExtensions.LuxsOABExtensions.GetByID(ID);
            return true;
        }
        if (IsAbbreviation(out string abbrv))
        {
            size = LuxsOABExtensions.LuxsOABExtensions.GetByAbbreviation(abbrv);
            return true;
        }
        if (IsFullName(out string fullName))
        {
            size = LuxsOABExtensions.LuxsOABExtensions.GetByFullName(fullName);
            return true;
        }

        return false;
    }

    public bool IsAbbreviation(out string abbreviation)
    {
        abbreviation = null;
        if (LOABESizeID.StartsWith("[") && LOABESizeID.EndsWith("]"))
        {
            abbreviation = LOABESizeID.Replace("[", string.Empty).Replace("]", string.Empty);
            return true;
        }
        return false;
    }
    public bool IsFullName(out string fullName)
    {
        fullName = null;
        if (!IsAbbreviation(out string _))
        {
            fullName = LOABESizeID;
            return true;
        }
        return false;
    }
    public bool IsID(out int ID)
    {
        ID = -1;
        if (int.TryParse(LOABESizeID, out int @int))
        {
            ID = @int;
            return true;
        }
        return false;
    }
}

public struct LOABESizeData
{
    public int ID;
    public float Diameter;
    public string AbbreviatedName;
    public string FullName;
    public int SortingOrder;
    public Color32 TagColor;

    public LOABESizeData(LOABESize a) : this()
    {
        ID = a.ID;
        Diameter = a.Diameter;
        AbbreviatedName = a.AbbreviatedName;
        FullName = a.FullName;
        SortingOrder = a.SortingOrder;
        TagColor = a.Color;
    }

    public LOABESize ToLOABESize()
    {
        return new()
        {
            ID = ID,
            Diameter = Diameter,
            AbbreviatedName = AbbreviatedName,
            FullName = FullName,
            SortingOrder = SortingOrder,
            TagColor = new(true, TagColor)
        };
    }
}

public static class Extensions
{

}