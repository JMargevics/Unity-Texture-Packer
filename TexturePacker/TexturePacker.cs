using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using System.IO;
using System.Collections;
using System.Collections.Generic;

public class TexturePacker : EditorWindow
{
    VisualElement root;

    TextureSettings outputTextureSettings = new TextureSettings();
    Label ToolLabel;
    readonly string classQ = "texture-item-container";

    Hashtable channelModes = new Hashtable()
                            {
                                {"ChannelR", "texture" },
                                {"ChannelG", "texture" },
                                {"ChannelB", "texture" },
                                {"ChannelA", "texture" }
                            };

    Hashtable InvertStates = new Hashtable()
                        {
                            {"ChannelR", false },
                            {"ChannelG", false },
                            {"ChannelB", false },
                            {"ChannelA", false }
                        };


    enum Channels { R, G, B, A }
    struct TextureSettings
    {
        public int resolution;
        public string name;
    }
    struct Slot
    {
        public Slot(Texture2D _tex, Channels _chan)
        {
            texture = _tex;
            channelDestination = _chan;
            invert = false;
        }
        public Slot(Texture2D _tex, Channels _chan, bool _inv)
        {
            texture = _tex;
            channelDestination = _chan;
            invert = _inv;
        }
        public Texture2D texture { get; }
        public Channels channelDestination { get; }
        public bool invert { get; }
    }

    [MenuItem("Window/Texture Packer")]
    public static void ShowWindow()
    {
        var window = GetWindow<TexturePacker>();
        window.titleContent = new GUIContent("Texture Packer");
        window.maxSize = new Vector2(400, 190);
        window.minSize = new Vector2(400, 190);
    }

    private void OnEnable()
    {
        root = rootVisualElement;
        root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/TexturePacker/UI/style.uss"));
        var UXMLTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/TexturePacker/UI/index.uxml");
        UXMLTree.CloneTree(root);

        outputTextureSettings.name = "TextureName";
        
        outputTextureSettings.resolution = 1024;

        //Set ObjectField filters to Texture2D. Need to move this to UXML
        root.Query<ObjectField>().ForEach((ObjectField a) => a.objectType = typeof(Texture2D));


        //TODO interactively add resolutions
        var ResToolbar = root.Q<ToolbarMenu>();
        ResToolbar.Add(ToolLabel = new Label() { text = outputTextureSettings.resolution.ToString() });
        ResToolbar.menu.AppendAction("256", (DropdownMenuAction a) => { ToolLabel.text = a.name; outputTextureSettings.resolution = 256; });
        ResToolbar.menu.AppendAction("512", (DropdownMenuAction a) => { ToolLabel.text = a.name; outputTextureSettings.resolution = 512; });
        ResToolbar.menu.AppendAction("1024", (DropdownMenuAction a) => { ToolLabel.text = a.name; outputTextureSettings.resolution = 1024; });
        ResToolbar.menu.AppendAction("2048", (DropdownMenuAction a) => { ToolLabel.text = a.name; outputTextureSettings.resolution = 2048; });
        ResToolbar.menu.AppendAction("4096", (DropdownMenuAction a) => { ToolLabel.text = a.name; outputTextureSettings.resolution = 4096; });
        //ResToolbar.menu.AppendAction("8192", (DropdownMenuAction a) => { ToolLabel.text = a.name; outputTextureSettings.resolution = 8192; });

        root.Query<ToolbarMenu>("ToolbarMode")
            .ForEach((ToolbarMenu t) =>
            {
                t.menu.AppendAction("Texture", (DropdownMenuAction a) =>
                {
                    channelModes[t.parent.parent.name] = "texture";
                    t.parent.parent.Q<ColorField>().AddToClassList("hidden");
                    t.parent.parent.Q<TemplateContainer>("TextureInputObject").RemoveFromClassList("hidden");
                    t.parent.parent.Q<TemplateContainer>("TextureChannels").RemoveFromClassList("hidden");
                });
                t.menu.AppendAction("Color", (DropdownMenuAction a) =>
                {
                    channelModes[t.parent.parent.name] = "color";
                    t.parent.parent.Q<ColorField>().RemoveFromClassList("hidden");
                    t.parent.parent.Q<TemplateContainer>("TextureInputObject").AddToClassList("hidden");
                    t.parent.parent.Q<TemplateContainer>("TextureChannels").AddToClassList("hidden");
                });
            });

        root.Query<ToolbarMenu>("SelectChannelMenu")
            .ForEach((ToolbarMenu t) =>
            {
                Label SelectChannelLabel;
                t.Add(SelectChannelLabel = new Label() { text = "R", name = "ChannelLabel" });
                t.menu.AppendAction("R", (DropdownMenuAction a) => { SelectChannelLabel.text = a.name; });
                t.menu.AppendAction("G", (DropdownMenuAction a) => { SelectChannelLabel.text = a.name; });
                t.menu.AppendAction("B", (DropdownMenuAction a) => { SelectChannelLabel.text = a.name; });
                t.menu.AppendAction("A", (DropdownMenuAction a) => { SelectChannelLabel.text = a.name; });
            });

        root.Query<ToolbarToggle>().ForEach((ToolbarToggle t) => {
            t.RegisterCallback<ChangeEvent<bool>>((ChangeEvent<bool> e) =>
            {
                InvertStates[t.parent.Q<TemplateContainer>().name] = e.newValue;

            });
        });

        root.Q<Button>("SaveTexture").clickable.clicked += () => SaveTexture();
        root.Q<Button>("ResetInputs").clickable.clicked += () => ResetUI();
    }

    private void ResetUI()
    {
        //Reset name and res  
        outputTextureSettings.name = "TextureName";
        outputTextureSettings.resolution = 1024;
        ToolLabel.text = outputTextureSettings.resolution.ToString();

        //Set label back to R, reset textures
        root.Query<Label>("ChannelLabel").ForEach((Label a) => a.text = "R");
        root.Query<ObjectField>().ForEach((ObjectField o) => o.value = null);

        //Update modes, show texture ObjectFields
        channelModes = new Hashtable()
                            {
                                {"ChannelR", "texture" },
                                {"ChannelG", "texture" },
                                {"ChannelB", "texture" },
                                {"ChannelA", "texture" }
                            };

        //Reset invert states and ToolbarToggle values
        InvertStates = new Hashtable()
                        {
                            {"ChannelR", false },
                            {"ChannelG", false },
                            {"ChannelB", false },
                            {"ChannelA", false }
                        };

        root.Query<ToolbarToggle>().ForEach((ToolbarToggle t) => { t.value = false; });
        root.Query<ColorField>().ForEach((ColorField c) => { c.value = new Color(0f, 0f, 0f, 1f); c.AddToClassList("hidden"); });
        root.Query<TemplateContainer>("TextureInputObject").ForEach((TemplateContainer o) => o.RemoveFromClassList("hidden"));
        root.Query<TemplateContainer>("TextureChannels").ForEach((TemplateContainer o) => o.RemoveFromClassList("hidden"));
    }

    private Texture2D CreateSolidColorTex(Color color)
    {
        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }

    private void SaveTexture()
    {   
        List<Slot> slots = new();

        //Iterate texture-item-container elements.
        root.Query<VisualElement>(className: classQ).ForEach(
            (VisualElement ve) =>
            {
                //Get texture from the ObjectField
                var _tex = ve.Q<ObjectField>().value as Texture2D;
                Texture2D slotTexture = new Texture2D(outputTextureSettings.resolution, outputTextureSettings.resolution, TextureFormat.RGBA32, true);

                //Fallback if can't read the texture
                if (!_tex && (string)channelModes[ve.parent.name] == "texture")
                {
                    slotTexture.Reinitialize(1, 1);
                    slotTexture.SetPixels(CreateSolidColorTex(Color.white).GetPixels());
                    slotTexture.Apply();
                }
                else if (((string)channelModes[ve.parent.name] == "color"))
                {
                    slotTexture.Reinitialize(1, 1);
                    slotTexture.SetPixels(CreateSolidColorTex(ve.parent.Q<ColorField>().value).GetPixels());
                    slotTexture.Apply();
                }
                else
                {
                    slotTexture.Reinitialize(_tex.height, _tex.width);
                    slotTexture.SetPixels(_tex.GetPixels());
                    slotTexture.Apply();
                }

                //Get the value from channel select dropdown menu, parse to enumerator and save to mode variable
                Enum.TryParse(ve.Q<Label>("ChannelLabel").text, out Channels channelDest);

                //populate with source texture and their channel destinations
                slots.Add(new Slot(slotTexture, channelDest, ve.parent.parent.Q<ToolbarToggle>().value));
            }
        );

        Material shuffleTexMat = new Material(Shader.Find("Hidden/TexShuffle"));
        shuffleTexMat.hideFlags = HideFlags.DontUnloadUnusedAsset;

        shuffleTexMat.SetTexture("_Slot0Tex", slots[0].texture);
        shuffleTexMat.SetFloat("_Slot0Channel", (float)slots[0].channelDestination);

        shuffleTexMat.SetTexture("_Slot1Tex", slots[1].texture);
        shuffleTexMat.SetFloat("_Slot1Channel", (float)slots[1].channelDestination);

        shuffleTexMat.SetTexture("_Slot2Tex", slots[2].texture);
        shuffleTexMat.SetFloat("_Slot2Channel", (float)slots[2].channelDestination);

        shuffleTexMat.SetTexture("_Slot3Tex", slots[3].texture);
        shuffleTexMat.SetFloat("_Slot3Channel", (float)slots[3].channelDestination);

        shuffleTexMat.SetVector("_Inverts", new Vector4(Convert.ToSingle(slots[0].invert),
                                                        Convert.ToSingle(slots[1].invert),
                                                        Convert.ToSingle(slots[2].invert),
                                                        Convert.ToSingle(slots[3].invert)));

        Texture2D outputTexture = new Texture2D(outputTextureSettings.resolution, outputTextureSettings.resolution, TextureFormat.RGBA32, true);
        outputTexture.hideFlags = HideFlags.DontUnloadUnusedAsset;

        RenderTexture outputRT = new RenderTexture(outputTextureSettings.resolution, outputTextureSettings.resolution, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(Texture2D.whiteTexture, outputRT, shuffleTexMat);

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture.active = outputRT;
        outputTexture.ReadPixels(new Rect(0, 0, outputTextureSettings.resolution, outputTextureSettings.resolution), 0, 0, false);
        outputTexture.Apply();
        RenderTexture.active = previousActive;
         
        byte[] texBytes = new byte[0];
        var path = EditorUtility.SaveFilePanel(
            "Save texture as PNG",
            Application.dataPath,
            outputTextureSettings.name + ".png",
            "png");

        if (path.Length != 0)
        {
            texBytes = outputTexture.EncodeToPNG();
            if (texBytes != null)
                File.WriteAllBytes(path, texBytes);
        }
    }
}