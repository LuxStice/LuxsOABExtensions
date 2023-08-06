using BepInEx;
using HarmonyLib;
using KSP.OAB;
using KSP.UI.Binding;
using SpaceWarp;
using SpaceWarp.API.Assets;
using SpaceWarp.API.Mods;
using SpaceWarp.API.UI;
using SpaceWarp.API.UI.Appbar;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;
using BepInEx.Configuration;
using KSP.Messages;

namespace LuxsOABExtensions;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(SpaceWarpPlugin.ModGuid, SpaceWarpPlugin.ModVer)]
public class LuxsOABExtensions : BaseSpaceWarpPlugin
{
    // These are useful in case some other mod wants to add a dependency to this one
    public const string ModGuid = MyPluginInfo.PLUGIN_GUID;
    public const string ModName = MyPluginInfo.PLUGIN_NAME;
    public const string ModVer = MyPluginInfo.PLUGIN_VERSION;
    
    private bool _isWindowOpen;
    private Rect _windowRect;

    private const string ToolbarOABButtonID = "BTN-LuxOABExtensions";
    public static readonly int[] StockSymmetryModes = { 0, 1, 2, 3, 4, 6, 8 };

    public static int[] RegularSymmetryModes = StockSymmetryModes.AddRangeToArray(new int[7] {10, 12, 16, 20, 24, 32, 64 });

    public static ObjectAssemblyBuilder CurrentOAB => Game.OAB.Current;

    public static LuxsOABExtensions Instance { get; set; }


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

        ConfigureConfigManager();
        Game.Messages.Subscribe<GameStateLeftMessage>(GameStateLeft);

        Harmony.CreateAndPatchAll(typeof(LuxsOABExtensions).Assembly);

        LoadConfigs();

        //SpaceWarp.API.Loading.Loading.CreateAssetLoadingActionWithExtensions(PluginFolderPath, ImportConfig, "cfg");
    }

    void LoadConfigs()
    {
        var sizesPaths = Directory.GetFiles(Path.Combine(PluginFolderPath, "Sizes"), "*.cfg", SearchOption.AllDirectories);//Sizes are under LuxsOABExtensions/Sizes/

        foreach (string path in sizesPaths)
        {
            File.ReadAllText(path);

            using (StreamReader sr = new(path))
            {
                LOABESizeData loabeSizeData = JsonConvert.DeserializeObject<LOABESizeData>(sr.ReadToEnd());
                LOABESize size = loabeSizeData.ToLOABESize();
                if (!CustomSizes.Contains(size))
                    CustomSizes.Add(size);
                else
                {
                    Logger.LogWarning($"{size.FullName} ({size.AbbreviatedName}) already exists! ({path})");
                }
            }
        }

        var partsPaths = Directory.GetParent(PluginFolderPath).GetFiles("*LOABE.cfg", SearchOption.AllDirectories);//Parts are under ModId/LOABE/

        foreach (var path in partsPaths)
        {
            File.ReadAllText(path.FullName);

            using (StreamReader sr = new(path.FullName))
            {
                LOABEPartDataCollection loabePartDataCollection = JsonConvert.DeserializeObject<LOABEPartDataCollection>(sr.ReadToEnd());
                int errorCount = 0;
                int partsApplied = 0;
                foreach (LOABEPartData loabePartData in loabePartDataCollection.LOABEPartData)
                {
                    if (loabePartData.TryGetSize(out var size))
                        loabePartData.partNames.ForEach(a => { overrides.Add(a, size); partsApplied++; });
                    else
                    {
                        Logger.LogWarning($"{path.FullName} is searching for a size that doesnt exist ({loabePartData.LOABESizeID})");
                        errorCount++;
                    }
                }
                if (errorCount > 0)
                    Logger.LogWarning($"{loabePartDataCollection.mod}'s loading finished with {errorCount} errors");
                Logger.LogInfo($"{loabePartDataCollection.mod} applied LOABEConfigs to {partsApplied} parts");
            }
        }
    }

    private void GameStateLeft(MessageCenterMessage obj)
    {
        GameStateLeftMessage message = obj as GameStateLeftMessage;

        if (message.StateBeingLeft == KSP.Game.GameState.VehicleAssemblyBuilder)
            _isWindowOpen = false;
    }

    internal ConfigEntry<bool> placementConfig;
    internal ConfigEntry<float> translationStepConfig;
    internal ConfigEntry<float> rotationStepConfig;
    internal ConfigEntry<float> partTranslationLimitConfig;

    private void ConfigureConfigManager()
    {
        ConfigDefinition _placementConfig = new("VFX", "ShowSparks");
        ConfigDescription _placementDescription = new("Prevent the sparks from appearing when you place a part (true = appear, false = not appear)", new AcceptableValueList<bool>(true, false));
        placementConfig = Config.Bind(_placementConfig, true, _placementDescription);

        ConfigDefinition _translationStepConfig = new("VesselManipulation", "TranslationStep");
        ConfigDescription _translationStepDescription = new("How much each button press (+ or -) should move the vessel", new AcceptableValueRange<float>(.1f, 10f));
        translationStepConfig = Config.Bind(_translationStepConfig, 1f, _translationStepDescription);

        ConfigDefinition _rotationStepConfig = new("VesselManipulation", "RotationStep");
        ConfigDescription _rotationStepDescription = new("How much each button press (+ or -) should rotate the vessel", new AcceptableValueRange<float>(.1f, 90f));
        rotationStepConfig = Config.Bind(_rotationStepConfig, 5f, _rotationStepDescription);

        ConfigDefinition _partTranslationLimitConfig = new("Part Manipulation", "TranslationLimitMultiplier");
        ConfigDescription _partTranslationLimitDescription = new("Multiplies current translation limit by value", new AcceptableValueRange<float>(1f, 25f));
        partTranslationLimitConfig = Config.Bind(_partTranslationLimitConfig, 10f, _partTranslationLimitDescription);
    }

    public override void OnPostInitialized()
    {
        Sizes.Sort();
        return;//Code below is to save configs automatically
        CustomSizes.ForEach(a =>
        {
            string path = Path.Combine(PluginFolderPath, "output", "sizes", $"{a.ID} - {a.AbbreviatedName}.cfg");

            Directory.CreateDirectory(Path.Combine(PluginFolderPath, "output", "sizes"));
            File.Create(path).Close();

            LOABESizeData sizeData = new LOABESizeData(a);

            using (StreamWriter sw = new(path))
            {
                sw.Write(JsonConvert.SerializeObject(sizeData));
            };
        });

        return;

        LOABEPartDataCollection collection = new() { mod = "SORRY", LOABEPartData = new()};

        foreach (var kvp in overrides)
        {
            string partName = kvp.Key;
            var Size = kvp.Value;

            LOABEPartData partData = new() { LOABESizeID = Size.AbbreviatedName, partNames = new() { partName} };

            collection.LOABEPartData.Add(partData);
        }

        string path = Path.Combine(PluginFolderPath, "output", "parts", $"{collection.mod}.cfg");

        Directory.CreateDirectory(Path.Combine(PluginFolderPath, "output", "parts"));
        File.Create(path).Close();

        using (StreamWriter sw = new(path))
        {
            sw.Write(JsonConvert.SerializeObject(collection));
        };
    }

    public static void UpdateSprite()
    {
        string path ="LuxsOABExtensions".ToLower() + "/images/";
        int toparse = (int)CurrentOAB.Stats.SymmetryMode.GetValue();
        if (toparse < RegularSymmetryModes.Length) {
            int SymmetryValue = RegularSymmetryModes[toparse];
            switch (SymmetryValue)
            {
                case 10:
                case 12:
                case 16:
                case 20:
                case 24:
                case 32:
                case 64:
                    Texture2D tex = AssetManager.GetAsset<Texture2D>(path + $"OAB_Symmetry_{SymmetryValue}.png".ToLower());
                    Sprite sprite = Sprite.Create(tex, new Rect(0.0f, 0.0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 68.0f);
                    GameObject.Find("ICO-Symmetry-Mode").GetComponent<Image>().sprite = sprite;
                    break;
            }
        }
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
                "Lux's OAB Extensions",
                GUILayout.Height(160),
                GUILayout.Width(500)
            );
        }
    }

    public static int CustomSymmetryValue = 1;
    private static int lastSymmetry = 1;

    public static bool LOABESymmetryMode = false;
    public static bool CustomSymmetryMode = false;

    public static float AngleSnap = 15;

    public static float defaultWidth, defaultHeight, defaultDepth;
    public static Vector3 defaultCameraBounds;
    public static bool VABExpanded = false;

    /// <summary>
    /// Defines the content of the UI window drawn in the <code>OnGui</code> method.
    /// </summary>
    /// <param name="windowID"></param>
    private static void FillWindow(int windowID)
    {
        GUIStyle headerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, fontStyle = FontStyle.Bold, fontSize = 16};
        GUIStyle selectedButtonStyle = new(GUI.skin.button) { fontStyle = FontStyle.Bold };

        GUILayout.Label("Xtra flavours");
        if (GUI.Button(new Rect(Instance._windowRect.width - 18, 2, 16, 16), "x"))
        {
            Instance._isWindowOpen = false;
            GUIUtility.ExitGUI();
        }
        GUI.DragWindow(new Rect(0, 0, 10000, 40));

        bool _ShowPlacementVFX = ShowPlacementVFX;
        _ShowPlacementVFX = GUILayout.Toggle(_ShowPlacementVFX, "Show sparks?");
        if (_ShowPlacementVFX != ShowPlacementVFX)
        {
            ShowPlacementVFX = _ShowPlacementVFX;
            Instance.placementConfig.SetSerializedValue(_ShowPlacementVFX.ToString());
            Instance.Config.Save();
        }

        if (false)
        {
            if (VABExpanded)
            {
                if (GUILayout.Button("Retract OAB"))
                {
                    var curEnv = (CurrentOAB.CurrentEnvironment as ObjectAssemblyEnvironment);

                    CurrentOAB.BuilderAssets.cameraPositionLimitCollider.bounds.Expand(-1000);
                    curEnv.BuildAreaBounds().bounds.Expand(-1000);
                    CurrentOAB.CurrentSizeLimits = CurrentOAB.CurrentEnvironment.BuildSizeLimits(OABVariant.VAB);
                    CurrentOAB.CurrentEnvironment.ShowEnvironmentObjects();

                    VABExpanded = false;
                }
            }
            else
            {
                if (GUILayout.Button("Expand OAB"))
                {
                    if (CurrentOAB is not null)
                    {
                        var curEnv = (CurrentOAB.CurrentEnvironment as ObjectAssemblyEnvironment);

                        curEnv.BuildAreaBounds().bounds.Expand(1000);

                        CurrentOAB.BuilderAssets.cameraPositionLimitCollider.bounds.Expand(1000);
                        CurrentOAB.CurrentSizeLimits = new VABSizeLimits(curEnv.gameObject, curEnv.environmentCenter.position, 1000, 500, 500);

                        CurrentOAB.CurrentEnvironment.HideEnvironmentObjects();
                        VABExpanded = true;
                    }
                }
            }
        }//OAB Hiding

        GUILayout.Label("Symmetry", headerStyle);
        bool customSymmetryMode = CustomSymmetryMode;
        customSymmetryMode = GUILayout.Toggle(customSymmetryMode, "Override default symmetry?");
        bool forceCustomValueUpdate = false;

        if(customSymmetryMode != CustomSymmetryMode)
        {
            CustomSymmetryMode = customSymmetryMode;
            forceCustomValueUpdate = true;
        }

        if (customSymmetryMode)
        {
            int SymmetryValue = CustomSymmetryValue;
            GUILayout.Label(SymmetryValue == 0 ? "Mirror symmetry" : $"{SymmetryValue}x symmetry");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<<"))
                SymmetryValue -= 10;
            if (GUILayout.Button("<"))
                SymmetryValue--;
            if (GUILayout.Button(">"))
                SymmetryValue++;
            if (GUILayout.Button(">>"))
                SymmetryValue += 10;
            SymmetryValue = Mathf.Clamp(SymmetryValue, 0, 512);
            GUILayout.EndHorizontal();
            SymmetryValue = (int)Math.Round(GUILayout.HorizontalSlider(SymmetryValue, 0, 512));
            if (SymmetryValue == 512)
                GUILayout.Label("Turning the Kerbol system into a binary star system I see");
            else if (SymmetryValue > 256)
                GUILayout.Label(@"R.I.P. your pc  ¯\_(ツ)_/¯");
            else if (SymmetryValue > 128)
                GUILayout.Label(@"Hmmm... You sure?");
            else if (SymmetryValue > 64)
                GUILayout.Label($"A symmetry value this high will possibly result in freezes");

            if (CustomSymmetryValue != SymmetryValue || forceCustomValueUpdate)
            {
                CustomSymmetryValue = SymmetryValue;

                if (RegularSymmetryModes.Contains(CustomSymmetryValue))
                {
                    UpdateSprite();
                }

                FindObjectOfType<ObjectAssemblyToolbar>().SetSymmetryToolByEnum((BuilderSymmetryMode)SymmetryValue);
            }
            GUILayout.Space(10);

        }


        GUILayout.Label("Angle snap", headerStyle);
        GUILayout.Label($"{AngleSnap}º snap");
        LuxsOABExtensions.AngleSnap = (int)GUILayout.HorizontalSlider(LuxsOABExtensions.AngleSnap, 1, 90);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("01º", AngleSnap == 1 ? selectedButtonStyle : GUI.skin.button))
        {
            LuxsOABExtensions.AngleSnap = 1;
        }
        if (GUILayout.Button("05º", AngleSnap == 5 ? selectedButtonStyle : GUI.skin.button))
        {
            LuxsOABExtensions.AngleSnap = 5;
        }
        if (GUILayout.Button("10º", AngleSnap == 10 ? selectedButtonStyle : GUI.skin.button))
        {
            LuxsOABExtensions.AngleSnap = 10;
        }
        if (GUILayout.Button("15º", AngleSnap == 15 ? selectedButtonStyle : GUI.skin.button))
        {
            LuxsOABExtensions.AngleSnap = 15;
        }
        if (GUILayout.Button("20º", AngleSnap == 20 ? selectedButtonStyle : GUI.skin.button))
        {
            LuxsOABExtensions.AngleSnap = 20;
        }
        if (GUILayout.Button("30º", AngleSnap == 30 ? selectedButtonStyle : GUI.skin.button))
        {
            LuxsOABExtensions.AngleSnap = 30;
        }
        if (GUILayout.Button("45º", AngleSnap == 45 ? selectedButtonStyle : GUI.skin.button))
        {
            LuxsOABExtensions.AngleSnap = 45;
        }
        if (GUILayout.Button("60º", AngleSnap == 60 ? selectedButtonStyle : GUI.skin.button))
        {
            LuxsOABExtensions.AngleSnap = 60;
        }
        if (GUILayout.Button("90º", AngleSnap == 90 ? selectedButtonStyle : GUI.skin.button))
        {
            LuxsOABExtensions.AngleSnap = 90;
        }
        GUILayout.EndHorizontal();



        GUILayout.Label("Assembly position", headerStyle);

        if (CurrentOAB.Stats.mainAssembly is null)
        {
            GUILayout.Label("Please start an assembly!");
        }
        else
        {
            var activeAssembly = CurrentOAB.Stats.MainAssembly;
            Vector3 OABCenter = CurrentOAB.CurrentEnvironment.EnvironmentCenter.position;
            Vector3 assemblyPos = activeAssembly.Anchor.PartTransform.position;

            float translationStep = Instance.translationStepConfig.Value;
            float rotationStep = Instance.rotationStepConfig.Value;

            GUILayout.Label($"Current step: {translationStep}");
            translationStep = (float)Math.Round(GUILayout.HorizontalSlider(translationStep, 0.1f, 10f),1);

            if (translationStep != Instance.translationStepConfig.Value)
            {
                Instance.translationStepConfig.Value = translationStep;
                Instance.Config.Save();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Up", GUILayout.Width(50));
            if (GUILayout.Button("+"))
            {
                activeAssembly.Anchor.PartTransform.position += Vector3.up * translationStep;
            }
            if (GUILayout.Button("0"))
            {
                activeAssembly.Anchor.PartTransform.position = new Vector3(assemblyPos.x, OABCenter.y, assemblyPos.z);
            }
            if (GUILayout.Button("-"))
            {
                activeAssembly.Anchor.PartTransform.position -= Vector3.up * translationStep;
            }
            GUILayout.Label("Down", GUILayout.Width(50));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Right", GUILayout.Width(50));
            if (GUILayout.Button("+"))
            {
                activeAssembly.Anchor.PartTransform.position += Vector3.right * translationStep;
            }
            if (GUILayout.Button("0"))
            {
                activeAssembly.Anchor.PartTransform.position = new Vector3(OABCenter.x, assemblyPos.y, assemblyPos.z);
            }
            if (GUILayout.Button("-"))
            {
                activeAssembly.Anchor.PartTransform.position -= Vector3.right * translationStep;
            }
            GUILayout.Label("Left", GUILayout.Width(50));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Front", GUILayout.Width(50));
            if (GUILayout.Button("+"))
            {
                activeAssembly.Anchor.PartTransform.position += Vector3.forward * translationStep;
            }
            if (GUILayout.Button("0"))
            {
                activeAssembly.Anchor.PartTransform.position = new Vector3(assemblyPos.x, assemblyPos.y, OABCenter.z);
            }
            if (GUILayout.Button("-"))
            {
                activeAssembly.Anchor.PartTransform.position -= Vector3.forward * translationStep;
            }
            GUILayout.Label("Back", GUILayout.Width(50));
            GUILayout.EndHorizontal();
            if (GUILayout.Button("To ground"))
            {
                var distanceToGround = Vector3.up * (activeAssembly.Anchor.CalculateOffsetToGround() + 5);
                distanceToGround.x = assemblyPos.x;
                distanceToGround.z = assemblyPos.z;
                activeAssembly.Anchor.PartTransform.position = distanceToGround;
            }
        }//Translation

    }

    //ObjectAssemblyCategoryButton

    public static readonly List<LOABESize> StockSizes = new()
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


    public static List<LOABESize> CustomSizes = new(); 
    //    = new()
    //{
    //    new()
    //    {
    //        ID = 12,
    //        AbbreviatedName = "2XS",
    //        FullName = "2XSMALL",
    //        Diameter = 0.3125f,
    //        SortingOrder = 5,
    //        TagColor = (true, Color.magenta)
    //    },
    //    new()
    //    {
    //        ID = 13,
    //        AbbreviatedName = "XS+",
    //        FullName = "XSMALL+",
    //        Diameter = 0.9375f,
    //        SortingOrder = 15,
    //        TagColor = (true, Color.Lerp(GetByAbbreviation("XS").Color, GetByAbbreviation("SM").Color, .5f))
    //    },
    //    new()
    //    {
    //        ID = 14,
    //        AbbreviatedName = "SM+",
    //        FullName = "SMALL+",
    //        Diameter = 1.8750f,
    //        SortingOrder = 25,
    //        TagColor = (true, Color.Lerp(GetByAbbreviation("SM").Color, GetByAbbreviation("MD").Color, .5f))
    //    },
    //    new()
    //    {
    //        ID = 15,
    //        AbbreviatedName = "MD+",
    //        FullName = "MEDIUM+",
    //        Diameter = 3.1250f,
    //        SortingOrder = 35,
    //        TagColor = (true, Color.Lerp(GetByAbbreviation("MD").Color, GetByAbbreviation("LG").Color, .5f))
    //    },
    //    new()
    //    {
    //        ID = 16,
    //        AbbreviatedName = "LG+",
    //        FullName = "LARGE+",
    //        Diameter = 4.3750f,
    //        SortingOrder = 45,
    //        TagColor = (true, Color.Lerp(GetByAbbreviation("LG").Color, GetByAbbreviation("XL").Color, .5f))
    //    },
    //    new()
    //    {
    //        ID = 17,
    //        AbbreviatedName = "XL+",
    //        FullName = "XLARGE+",
    //        Diameter = 6.2500f,
    //        SortingOrder = 55,
    //        TagColor = (true, Color.Lerp(GetByAbbreviation("XL").Color, GetByAbbreviation("2XL").Color, 1/4f))
    //    },
    //    new()
    //    {
    //        ID = 18,
    //        AbbreviatedName = "XL++",
    //        FullName = "XLARGE++",
    //        Diameter = 7.5000f,
    //        SortingOrder = 65,
    //        TagColor = (true, Color.Lerp(GetByAbbreviation("XL").Color, GetByAbbreviation("2XL").Color, 2/4f))
    //    },
    //    new()
    //    {
    //        ID = 19,
    //        AbbreviatedName = "XL+++",
    //        FullName = "XLARGE+++",
    //        Diameter = 8.7500f,
    //        SortingOrder = 75,
    //        TagColor = (true, Color.Lerp(GetByAbbreviation("XL").Color, GetByAbbreviation("2XL").Color, 3/4f))
    //    },
    //    new()
    //    {
    //        ID = 100,
    //        AbbreviatedName = "Mk2",
    //        FullName = "MARK2",
    //        Diameter = 2.500f,
    //        SortingOrder = 31,
    //        TagColor = (true, new Color(54/255f,39/255f,245/255f))
    //    },
    //    new()
    //    {
    //        ID = 101,
    //        AbbreviatedName = "Mk3",
    //        FullName = "MARK3",
    //        Diameter = 3.7500f,
    //        SortingOrder = 41,
    //        TagColor = (true, new Color(39/255f,234/255f,245/255f))
    //    },
    //};
    public static LOABESize GetByAbbreviation(string Name) => Sizes.FirstOrDefault(a => a.AbbreviatedName == Name);
    public static LOABESize GetByID(int ID) => Sizes.FirstOrDefault(a => a.ID == ID);

    internal static LOABESize GetByFullName(string fullName) => Sizes.FirstOrDefault(a => a.FullName == fullName);

    public static List<LOABESize> Sizes
    {
        get
        {
            List<LOABESize> list = new List<LOABESize>(StockSizes);
            if (CustomSizes is not null)
                list.AddRange(CustomSizes);
            list.Sort();
            return list;
        }
    }

    public static List<LOABESize> SizesByID
    {
        get
        {
            List<LOABESize> list = new(Sizes);
            list.Sort(delegate (LOABESize x, LOABESize y)
            {
                return x.ID.CompareTo(y.ID);
            });
            return list;
        }
    }

    public static Dictionary<string, LOABESize> overrides = new();
    //    = new()
    //{
    //    {"Milrin-07", GetByAbbreviation("XS+") },
    //    { "Reiptor-12", GetByAbbreviation("SM") },
    //    { "BE-4", GetByAbbreviation("SM+") },
    //    { "GridfinHexS", GetByAbbreviation("SM+") },
    //    { "GridfinHexM", GetByAbbreviation("MD+") }
    //};
    internal static bool listSet;
    internal static bool ShowPlacementVFX = true;
    public const float NODE_CONNECTED_THRESHOLD = 0.001f;

    public static bool TryFindConnectedNode(IObjectAssemblyPart part, IObjectAssemblyPart target, out (IObjectAssemblyPartNode partNode, IObjectAssemblyPartNode targetNode) NodePair)
    {
        foreach (var node in part.Nodes)
        {
            if (node.NodeType == KSP.Sim.AttachNodeType.Surface)
                continue;

            foreach (var targetNode in target.Nodes)
            {
                if (node.NodeType == KSP.Sim.AttachNodeType.Surface)
                    continue;

                float distance = Vector3.Distance(node.WorldPosition, targetNode.WorldPosition);

                if(distance < NODE_CONNECTED_THRESHOLD)
                {
                    NodePair = (node, targetNode);
                    return true;
                }
            }
        }


        NodePair = (null, null);
        return false;
    }
}
