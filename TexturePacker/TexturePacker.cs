using System;
using UnityEditor;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using System.IO;
using System.Collections;
using System.Collections.Generic;

public class TexturePacker : EditorWindow
{
    struct TextureSettings
    {
        public int resolution;
        public string name;
    }

    VisualElement root;

    enum Channels { R, G, B, A }
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

        var ResToolbar = root.Q<ToolbarMenu>();
        ResToolbar.Add(ToolLabel = new Label() { text = "1024" });
        ResToolbar.menu.AppendAction("256", (DropdownMenuAction a) => { ToolLabel.text = a.name; outputTextureSettings.resolution = 256; });
        ResToolbar.menu.AppendAction("512", (DropdownMenuAction a) => { ToolLabel.text = a.name; outputTextureSettings.resolution = 512; });
        ResToolbar.menu.AppendAction("1024", (DropdownMenuAction a) => { ToolLabel.text = a.name; outputTextureSettings.resolution = 1024; });
        ResToolbar.menu.AppendAction("2048", (DropdownMenuAction a) => { ToolLabel.text = a.name; outputTextureSettings.resolution = 2048; });
        ResToolbar.menu.AppendAction("4096", (DropdownMenuAction a) => { ToolLabel.text = a.name; outputTextureSettings.resolution = 4096; });

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

        //Reset stuff
        root.Q<Button>("ResetInputs").clickable.clicked += () =>
        {
            //Reset name and res
            
            outputTextureSettings.name = "Generated/TextureName.png";
            outputTextureSettings.resolution = 1024;
            ToolLabel.text = "1024";

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
        };

    }

    private void SaveTexture()
    {
        List<Color[]> pixelData = new List<Color[]>();
        Texture2D outputTexture = new Texture2D(outputTextureSettings.resolution, outputTextureSettings.resolution, TextureFormat.RGBA32, false);
        Texture2D newTex = new Texture2D(outputTextureSettings.resolution, outputTextureSettings.resolution, TextureFormat.RGBA32, false);
        Texture2D tex = new Texture2D(outputTextureSettings.resolution, outputTextureSettings.resolution, TextureFormat.RGBA32, false);

        //Iterate texture-item-container elements.
        root.Query<VisualElement>(className: classQ).ForEach(
            (VisualElement ve) =>
            {
                //Get texture from the ObjectField
                var _tex = ve.Q<ObjectField>().value as Texture2D;

                if (!_tex && (string)channelModes[ve.parent.name] == "texture")
                {

                    tex = Texture2D.whiteTexture;
                }
                else if (((string)channelModes[ve.parent.name] == "color"))
                {
                    //Generate a 2x2 texture using color value from the element
                    Texture2D newColorTex = new Texture2D(2, 2);
                    newColorTex.SetPixel(1, 1, ve.parent.Q<ColorField>().value);
                    newColorTex.SetPixel(1, 2, ve.parent.Q<ColorField>().value);
                    newColorTex.SetPixel(2, 1, ve.parent.Q<ColorField>().value);
                    newColorTex.SetPixel(2, 2, ve.parent.Q<ColorField>().value);
                    tex.Reinitialize(2, 2);
                    tex.SetPixels(newColorTex.GetPixels());
                }
                else
                {
                    tex.Reinitialize(_tex.height, _tex.width);
                    tex.SetPixels(_tex.GetPixels());
                    tex.Apply();
                }

                //Force square texture
                if (tex.height != outputTextureSettings.resolution ||
                    tex.width != outputTextureSettings.resolution)
                {
                    TextureScale.Bilinear(tex, outputTextureSettings.resolution, outputTextureSettings.resolution);
                }

                //Get the value from channel select dropdown menu, parse to enumerator and save to mode variable
                Enum.TryParse(ve.Q<Label>("ChannelLabel").text, out Channels mode);

                for (int i = 0; i < outputTextureSettings.resolution; i++)
                {
                    for (int j = 0; j < outputTextureSettings.resolution; j++)
                    {
                        Color px = newTex.GetPixel(i, j);

                        float _px = new float();
                        switch (mode)
                        {
                            case Channels.R:                     
                                _px = tex.GetPixel(i, j).r;
                                break;
                            case Channels.G:
                                _px = tex.GetPixel(i, j).g;
                                break;
                            case Channels.B:
                                _px = tex.GetPixel(i, j).b;
                                break;
                            case Channels.A:
                                _px = tex.GetPixel(i, j).a;
                                break;
                        }

                        //We get label's name and parse it into ENUM
                        switch (ve.parent.name)
                        {
                            case "ChannelR":
                                px.r = _px;
                                break;
                            case "ChannelG":
                                px.g = _px;
                                break;
                            case "ChannelB":
                                px.b = _px;
                                break;
                            case "ChannelA":
                                px.a = _px;
                                break;
                        }

                        //Blit final pixel
                        newTex.SetPixel(i, j, px);
                    }
                }
            }
        );

        var ipx = newTex.GetPixels();

        //Invert texture channels if any of the toggles are true. TODO Move code to block above
        if ((bool) InvertStates["ChannelR"] == true ||
            (bool) InvertStates["ChannelG"] == true ||
            (bool) InvertStates["ChannelB"] == true ||
            (bool) InvertStates["ChannelA"] == true )
        {
            for (int i=0; i < ipx.Length; i++)
            {
                if ((bool)InvertStates["ChannelR"] == true) ipx[i].r = 1.0f - ipx[i].r;
                if ((bool)InvertStates["ChannelG"] == true) ipx[i].g = 1.0f - ipx[i].g;
                if ((bool)InvertStates["ChannelB"] == true) ipx[i].b = 1.0f - ipx[i].b;
                if ((bool)InvertStates["ChannelA"] == true) ipx[i].a = 1.0f - ipx[i].a;
            }
        }

        outputTexture.SetPixels(ipx);
        outputTexture.Apply();

        //Save texture to disc
        byte[] texBytes = new byte[0];
        var path = EditorUtility.SaveFilePanel(
            "Save mask map as PNG",
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