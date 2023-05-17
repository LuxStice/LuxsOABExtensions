using UnityEngine;

namespace LuxsOABExtensions;

public struct LOABESize : IComparable<LOABESize>
{
    /// <summary>
    /// ID on the sizeCategory value in json.
    /// </summary>
    public int ID;
    public float Diameter;
    public string AbbreviatedName;
    public string FullName;
    public int SortingOrder;
    public (bool useColor ,Color tagColor) TagColor; //Replace with a check to see if the size is a default one
    public Color Color => TagColor.tagColor;

    public int CompareTo(LOABESize other)
    {
        if (SortingOrder > other.SortingOrder)
            return 1;
        else if (SortingOrder < other.SortingOrder)
            return -1;
        else
            return 0;
    }

    public override bool Equals(object obj)
    {
        if (obj is LOABESize)
        {
            LOABESize other = (LOABESize)obj;

            return other.ID == ID;
        }
        else
            return false;
    }

    public override int GetHashCode()
    {
        return ID;
    }
    public override string ToString()
    {
        return $"LuxOABExtensions.OABSize: #{ID}, {FullName} ({AbbreviatedName}), {Diameter}";
    }
}