using BepInEx;
using HarmonyLib;
using KSP.UI.Binding;
using SpaceWarp;
using SpaceWarp.API.Assets;
using SpaceWarp.API.Mods;
using SpaceWarp.API.UI;
using SpaceWarp.API.UI.Appbar;
using UnityEngine;

namespace LuxOABExtensions;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(SpaceWarpPlugin.ModGuid, SpaceWarpPlugin.ModVer)]
public class LuxOABExtensions : BaseSpaceWarpPlugin
{
    // These are useful in case some other mod wants to add a dependency to this one
    public const string ModGuid = MyPluginInfo.PLUGIN_GUID;
    public const string ModName = MyPluginInfo.PLUGIN_NAME;
    public const string ModVer = MyPluginInfo.PLUGIN_VERSION;
    
    private bool _isWindowOpen;
    private Rect _windowRect;

    private const string ToolbarOABButtonID = "BTN-LuxOABExtensions";

    public static readonly int[] RegularSymmetryModes = { 0,1,2, 3, 4, 6, 8, 10, 12, 16, 20, 24, 32, 64 };

    public static LuxOABExtensions Instance { get; set; }

    /// <summary>
    /// Runs when the mod is first initialized.
    /// </summary>
    public override void OnInitialized()
    {
        base.OnInitialized();

        Instance = this;

        Appbar.RegisterOABAppButton(
            "Editor Extensions",
            ToolbarOABButtonID,
            AssetManager.GetAsset<Texture2D>($"{SpaceWarpMetadata.ModID}/images/icon.png"),
            isOpen =>
            {
                _isWindowOpen = isOpen;
                GameObject.Find(ToolbarOABButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(isOpen);
            }
        );

        Harmony.CreateAndPatchAll(typeof(LuxOABExtensions).Assembly);
        Sizes.Sort();
    }

    public override void OnPostInitialized()
    {
    }

    /// <summary>
    /// Draws a simple UI window when <code>this._isWindowOpen</code> is set to <code>true</code>.
    /// </summary>
    private void OnGUI()
    {
        // Set the UI
        GUI.skin = Skins.ConsoleSkin;

        if (_isWindowOpen)
        {
            _windowRect = GUILayout.Window(
                GUIUtility.GetControlID(FocusType.Passive),
                _windowRect,
                FillWindow,
                "OAB Extensions",
                GUILayout.Height(350),
                GUILayout.Width(350)
            );
        }
    }

    public static int SymmetryValue = 2;
    private static bool overrideSymmetry = false;

    /// <summary>
    /// Defines the content of the UI window drawn in the <code>OnGui</code> method.
    /// </summary>
    /// <param name="windowID"></param>
    private static void FillWindow(int windowID)
    {
        GUILayout.Label("Editor Extensions - Extensions for editor");
        GUI.DragWindow(new Rect(0, 0, 10000, 40));
        overrideSymmetry = GUILayout.Toggle(overrideSymmetry, "Override");

        if (overrideSymmetry)
        {
            GUILayout.Label($"{SymmetryValue}x symmetry");
            SymmetryValue = (int)Math.Round(GUILayout.HorizontalSlider(SymmetryValue, 2, 256));
        }
    }

    //ObjectAssemblyCategoryButton

    public static readonly List<OABSize> StockSizes = new()
    {
        new()
        {
            ID = 0,
            AbbreviatedName = "XS",
            FullName = "XSMALL",
            Diameter = 0.6250f,
            SortingOrder = 10,
            TagColor = (false, new Color(1, 0.333f, 0.333f))
        },
        new()
        {
            ID = 1,
            AbbreviatedName = "SM",
            FullName = "SMALL",
            Diameter = 1.2500f,
            SortingOrder = 20,
            TagColor = (false, new Color(1, 0.733f, 0f))
        },
        new()
        {
            ID = 2,
            AbbreviatedName = "MD",
            FullName = "MEDIUM",
            Diameter = 2.5000f,
            SortingOrder = 30,
            TagColor = (false, new Color(0.659f, 1, 0))
        },
        new()
        {
            ID = 3,
            AbbreviatedName = "LG",
            FullName = "LARGE",
            Diameter = 3.7500f,
            SortingOrder = 40,
            TagColor = (false, new Color(0f, 1, 0.749f))
        },
        new()
        {
            ID = 4,
            AbbreviatedName = "XL",
            FullName = "XLARGE",
            Diameter = 5.0000f,
            SortingOrder = 50,
            TagColor = (false, new Color(0.333f, 0.765f, 1))
        },
        new()
        {
            ID = 5,
            AbbreviatedName = "2XL",
            FullName = "2XLARGE",
            Diameter = 10.0000f,
            SortingOrder = 60,
            TagColor = (false, new Color(0.757f, 0.537f, 0.973f))
        },
        new()
        {
            ID = 6,
            AbbreviatedName = "3XL",
            FullName = "3XLARGE",
            Diameter = 20.0000f,
            SortingOrder = 70,
            TagColor = (false, new Color(0.553f, 0.196f, 0.416f))
        },
        new()
        {
            ID = 7,
            AbbreviatedName = "4XL",
            FullName = "4XLARGE",
            Diameter = 40.0000f,
            SortingOrder = 80
        },
        new()
        {
            ID = 8,
            AbbreviatedName = "5XL",
            FullName = "5XLARGE",
            Diameter = 80.0000f,
            SortingOrder = 90
        },
        new()
        {
            ID = 9,
            AbbreviatedName = "6XL",
            FullName = "6XLARGE",
            Diameter = 160.0000f,
            SortingOrder = 100
        },
    };

    public static List<OABSize> CustomSizes = new()
    {
        new()
        {
            ID = 12,
            AbbreviatedName = "2XS",
            FullName = "2XSMALL",
            Diameter = 0.3125f,
            SortingOrder = 5,
            TagColor = (true, Color.magenta)
        },
        new()
        {
            ID = 13,
            AbbreviatedName = "XS+",
            FullName = "XSMALL+",
            Diameter = 0.9375f,
            SortingOrder = 15,
            TagColor = (true, Color.Lerp(GetByName("XS").Color, GetByName("SM").Color, .5f))
        },
        new()
        {
            ID = 14,
            AbbreviatedName = "SM+",
            FullName = "SMALL+",
            Diameter = 1.8750f,
            SortingOrder = 25,
            TagColor = (true, Color.Lerp(GetByName("SM").Color, GetByName("MD").Color, .5f))
        },
        new()
        {
            ID = 15,
            AbbreviatedName = "MD+",
            FullName = "MEDIUM+",
            Diameter = 3.1250f,
            SortingOrder = 35,
            TagColor = (true, Color.Lerp(GetByName("MD").Color, GetByName("LG").Color, .5f))
        },
        new()
        {
            ID = 16,
            AbbreviatedName = "LG+",
            FullName = "LARGE+",
            Diameter = 4.3750f,
            SortingOrder = 45,
            TagColor = (true, Color.Lerp(GetByName("LG").Color, GetByName("XL").Color, .5f))
        },
        new()
        {
            ID = 17,
            AbbreviatedName = "XL+",
            FullName = "XLARGE+",
            Diameter = 6.2500f,
            SortingOrder = 55,
            TagColor = (true, Color.Lerp(GetByName("XL").Color, GetByName("2XL").Color, 1/4f))
        },
        new()
        {
            ID = 18,
            AbbreviatedName = "XL++",
            FullName = "XLARGE++",
            Diameter = 7.5000f,
            SortingOrder = 65,
            TagColor = (true, Color.Lerp(GetByName("XL").Color, GetByName("2XL").Color, 2/4f))
        },
        new()
        {
            ID = 19,
            AbbreviatedName = "XL+++",
            FullName = "XLARGE+++",
            Diameter = 8.7500f,
            SortingOrder = 75,
            TagColor = (true, Color.Lerp(GetByName("XL").Color, GetByName("2XL").Color, 3/4f))
        },
        new()
        {
            ID = 100,
            AbbreviatedName = "Mk2",
            FullName = "MARK2",
            Diameter = 2.500f,
            SortingOrder = 31,
            TagColor = (true, new Color(54/255f,39/255f,245/255f))
        },
        new()
        {
            ID = 101,
            AbbreviatedName = "Mk3",
            FullName = "MARK3",
            Diameter = 3.7500f,
            SortingOrder = 41,
            TagColor = (true, new Color(39/255f,234/255f,245/255f))
        },
    };

    public static OABSize GetByName(string Name) => Sizes.First(a => a.AbbreviatedName == Name);
    public static OABSize GetByID(int ID) => Sizes.First(a => a.ID == ID);

    public static List<OABSize> Sizes
    {
        get
        {
            List<OABSize> list = new List<OABSize>(StockSizes);
            if (CustomSizes is not null)
                list.AddRange(CustomSizes);
            list.Sort();
            return list;
        }
    }

    public static List<OABSize> SizesByID
    {
        get
        {
            List<OABSize> list = new(Sizes);
            list.Sort(delegate (OABSize x, OABSize y)
            {
                return x.ID.CompareTo(y.ID);
            });
            return list;
        }
    }

    public static Dictionary<string, OABSize> overrides = new()
    {
        {"Milrin-07", GetByName("XS+") },
        { "Reiptor-12", GetByName("SM+") }
    };
}

public struct OABSize : IComparable<OABSize>
{
    /// <summary>
    /// ID on the sizeCategory value in json.
    /// </summary>
    public int ID;
    public float Diameter;
    public string AbbreviatedName;
    public string FullName;
    public int SortingOrder;
    public (bool useColor ,Color tagColor) TagColor;
    public Color Color => TagColor.tagColor;

    public int CompareTo(OABSize other)
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
        if (obj is OABSize)
        {
            OABSize other = (OABSize)obj;

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