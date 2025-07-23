using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Reflection;
using System.Windows.Forms;
using System.IO.Compression;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Registry = Microsoft.Win32.Registry;
using RegistryKey = Microsoft.Win32.RegistryKey;
using PaintDotNet;
using PaintDotNet.AppModel;
using PaintDotNet.Direct2D1;
using PaintDotNet.DirectWrite;
using PaintDotNet.Effects;
using PaintDotNet.Clipboard;
using PaintDotNet.Imaging;
using PaintDotNet.IndirectUI;
using PaintDotNet.Collections;
using PaintDotNet.PropertySystem;
using PaintDotNet.Rendering;
using ColorWheelControl = PaintDotNet.Imaging.ManagedColor;
using AngleControl = System.Double;
using PanSliderControl = PaintDotNet.Rendering.Vector2Double;
using FolderControl = System.String;
using FilenameControl = System.String;
using ReseedButtonControl = System.Byte;
using RollControl = PaintDotNet.Rendering.Vector3Double;
using IntSliderControl = System.Int32;
using CheckboxControl = System.Boolean;
using TextboxControl = System.String;
using DoubleSliderControl = System.Double;
using ListBoxControl = System.Byte;
using RadioButtonControl = System.Byte;
using MultiLineTextboxControl = System.String;
using LabelComment = System.String;
using FontFamily = System.String;
using LayerControl = System.Int32;
using System.Runtime.CompilerServices;

[assembly: AssemblyTitle("PhotoColorsAndLevelsCorrection plugin for Paint.NET")]
[assembly: AssemblyDescription("Temperature, saturation, level and gamma correction")]
[assembly: AssemblyConfiguration("color temperature saturation level gamma")]
[assembly: AssemblyCompany("Rémy LUCAS")]
[assembly: AssemblyProduct("PhotoColorsAndLevelsCorrection")]
[assembly: AssemblyCopyright("Copyright ©2025 by Rémy LUCAS")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("2.0.*")]
[assembly: AssemblyMetadata("BuiltByCodeLab", "Version=6.13.9087.35650")]
[assembly: SupportedOSPlatform("Windows")]

namespace PhotoColorsAndLevelsCorrectionEffect
{
    public class PhotoColorsAndLevelsCorrectionSupportInfo : IPluginSupportInfo
    {
        public string Author => base.GetType().Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>().Copyright;
        public string Copyright => base.GetType().Assembly.GetCustomAttribute<AssemblyDescriptionAttribute>().Description;
        public string DisplayName => base.GetType().Assembly.GetCustomAttribute<AssemblyProductAttribute>().Product;
        public Version Version => base.GetType().Assembly.GetName().Version;
        public Uri WebsiteUri => new Uri("https://www.getpaint.net/redirect/plugins.html");
    }

    [PluginSupportInfo<PhotoColorsAndLevelsCorrectionSupportInfo>(DisplayName = "Photo colors and levels correction")]
    [EffectCategory(EffectCategory.Adjustment)]
    public class PhotoColorsAndLevelsCorrectionEffectPlugin : PropertyBasedBitmapEffect
    {
        public static string StaticName => "Photo colors and levels correction";
        public static System.Drawing.Image StaticIcon => null;
        public static string SubmenuName => null;

        public PhotoColorsAndLevelsCorrectionEffectPlugin()
            : base(StaticName, StaticIcon, SubmenuName, BitmapEffectOptions.Create() with { IsConfigurable = true })
        {
        }

        public enum PropertyNames
        {
            TemperatureFact,
            SaturationFact,
            CorrLowLevels,
            CorrHighLevels,
            IndRGBlow,
            IndRGBhigh,
            MidtonesParam
        }

        #region Random Number Support
        private readonly uint RandomNumberInstanceSeed;
        private uint RandomNumberRenderSeed = 0;

        internal static class RandomNumber
        {
            public static uint InitializeSeed(uint iSeed, float x, float y)
            {
                return CombineHashCodes(
                    iSeed,
                    CombineHashCodes(
                        Hash(Unsafe.As<float, uint>(ref x)),
                        Hash(Unsafe.As<float, uint>(ref y))));
            }

            public static uint InitializeSeed(uint instSeed, Point2Int32 scenePos)
            {
                return CombineHashCodes(
                    instSeed,
                    CombineHashCodes(
                        Hash(unchecked((uint)scenePos.X)),
                        Hash(unchecked((uint)scenePos.Y))));
            }

            public static uint Hash(uint input)
            {
                uint state = input * 747796405u + 2891336453u;
                uint word = ((state >> (int)((state >> 28) + 4)) ^ state) * 277803737u;
                return (word >> 22) ^ word;
            }

            public static float NextFloat(ref uint seed)
            {
                seed = Hash(seed);
                return (seed >> 8) * 5.96046448E-08f;
            }

            public static int NextInt32(ref uint seed)
            {
                seed = Hash(seed);
                return unchecked((int)seed);
            }

            public static int NextInt32(ref uint seed, int maxValue)
            {
                seed = Hash(seed);
                return unchecked((int)(seed & 0x80000000) % maxValue);
            }

            public static int Next(ref uint seed)
            {
                seed = Hash(seed);
                return unchecked((int)seed);
            }

            public static int Next(ref uint seed, int maxValue)
            {
                seed = Hash(seed);
                return unchecked((int)(seed & 0x80000000) % maxValue);
            }

            public static byte NextByte(ref uint seed)
            {
                seed = Hash(seed);
                return (byte)(seed & 0xFF);
            }

            private static uint CombineHashCodes(uint hash1, uint hash2)
            {
                uint result = hash1;
                result = ((result << 5) + result) ^ hash2;
                return result;
            }
        }
        #endregion


        protected override PropertyCollection OnCreatePropertyCollection()
        {
            List<Property> props = new List<Property>();

            props.Add(new Int32Property(PropertyNames.TemperatureFact, 0, -20, 20));
            props.Add(new Int32Property(PropertyNames.SaturationFact, 0, -100, 100));
            props.Add(new Int32Property(PropertyNames.CorrLowLevels, 100, 0, 150));
            props.Add(new Int32Property(PropertyNames.CorrHighLevels, 100, 0, 150));
            props.Add(new Int32Property(PropertyNames.IndRGBlow, 0, 0, 100));
            props.Add(new Int32Property(PropertyNames.IndRGBhigh, 0, 0, 100));
            props.Add(new Int32Property(PropertyNames.MidtonesParam, 128, 0, 255));

            return new PropertyCollection(props);
        }

        protected override ControlInfo OnCreateConfigUI(PropertyCollection props)
        {
            ControlInfo configUI = CreateDefaultConfigUI(props);

            configUI.SetPropertyControlValue(PropertyNames.TemperatureFact, ControlInfoPropertyNames.DisplayName, "Color temperature correction");
            configUI.SetPropertyControlValue(PropertyNames.TemperatureFact, ControlInfoPropertyNames.ControlColors, new ColorBgra[] { ColorBgra.Cyan, ColorBgra.White, ColorBgra.Orange });
            configUI.SetPropertyControlValue(PropertyNames.TemperatureFact, ControlInfoPropertyNames.ShowHeaderLine, false);

            configUI.SetPropertyControlValue(PropertyNames.SaturationFact, ControlInfoPropertyNames.DisplayName, "Color saturation adjustement");
            configUI.SetPropertyControlValue(PropertyNames.SaturationFact, ControlInfoPropertyNames.ControlStyle, SliderControlStyle.SaturationHue);
            configUI.SetPropertyControlValue(PropertyNames.SaturationFact, ControlInfoPropertyNames.ShowHeaderLine, false);
            
            configUI.SetPropertyControlValue(PropertyNames.CorrLowLevels, ControlInfoPropertyNames.DisplayName, "Low levels to 0 output factor");
            configUI.SetPropertyControlValue(PropertyNames.CorrLowLevels, ControlInfoPropertyNames.ControlColors, new ColorBgra[] { ColorBgra.Gray, ColorBgra.Black });
            configUI.SetPropertyControlValue(PropertyNames.CorrLowLevels, ControlInfoPropertyNames.ShowHeaderLine, false);
            
            configUI.SetPropertyControlValue(PropertyNames.CorrHighLevels, ControlInfoPropertyNames.DisplayName, "High levels to 255 output factor");
            configUI.SetPropertyControlValue(PropertyNames.CorrHighLevels, ControlInfoPropertyNames.ControlColors, new ColorBgra[] { ColorBgra.Gray, ColorBgra.White });
            configUI.SetPropertyControlValue(PropertyNames.CorrHighLevels, ControlInfoPropertyNames.ShowHeaderLine, false);
            
            configUI.SetPropertyControlValue(PropertyNames.IndRGBlow, ControlInfoPropertyNames.DisplayName, "Low levels RGB independence factor");
            //configUI.SetPropertyControlValue(PropertyNames.IndRGBlow, ControlInfoPropertyNames.ControlColors, new ColorBgra[] { ColorBgra.Cyan, ColorBgra.White, ColorBgra.Orange });
            configUI.SetPropertyControlValue(PropertyNames.IndRGBlow, ControlInfoPropertyNames.ShowHeaderLine, false);
            
            configUI.SetPropertyControlValue(PropertyNames.IndRGBhigh, ControlInfoPropertyNames.DisplayName, "High levels RGB independence factor");
            //configUI.SetPropertyControlValue(PropertyNames.IndRGBhigh, ControlInfoPropertyNames.ControlColors, new ColorBgra[] { ColorBgra.Cyan, ColorBgra.White, ColorBgra.Orange });
            configUI.SetPropertyControlValue(PropertyNames.IndRGBhigh, ControlInfoPropertyNames.ShowHeaderLine, false);
            
            configUI.SetPropertyControlValue(PropertyNames.MidtonesParam, ControlInfoPropertyNames.DisplayName, "Gamma correction (mid tones)");
            configUI.SetPropertyControlValue(PropertyNames.MidtonesParam, ControlInfoPropertyNames.ControlColors, new ColorBgra[] { ColorBgra.Black, ColorBgra.White });
            configUI.SetPropertyControlValue(PropertyNames.MidtonesParam, ControlInfoPropertyNames.ShowHeaderLine, false);

            return configUI;
        }

        protected override void OnCustomizeConfigUIWindowProperties(PropertyCollection props)
        {
            // Change the effect's window title
            props[ControlInfoPropertyNames.WindowTitle].Value = "Photo colors and levels correction";
            // Add help button to effect UI
            props[ControlInfoPropertyNames.WindowHelpContentType].Value = WindowHelpContentType.PlainText;
            props[ControlInfoPropertyNames.WindowHelpContent].Value = "Photo colors and levels correction v3,0\nCopyright ©2025 by Rémy LUCAS\nAll rights reserved.";
            base.OnCustomizeConfigUIWindowProperties(props);
        }

        /*
        protected override void OnInitializeRenderInfo(IBitmapEffectRenderInfo renderInfo)
        {
            base.OnInitializeRenderInfo(renderInfo);
        }
        */

        // This function is called each time UI changes (like old PreRender() function
        protected override void OnSetToken(PropertyBasedEffectConfigToken newToken)
        {
            TemperatureFact = newToken.GetProperty<Int32Property>(PropertyNames.TemperatureFact).Value;
            SaturationFact = newToken.GetProperty<Int32Property>(PropertyNames.SaturationFact).Value;
            CorrLowLevels = newToken.GetProperty<Int32Property>(PropertyNames.CorrLowLevels).Value;
            CorrHighLevels = newToken.GetProperty<Int32Property>(PropertyNames.CorrHighLevels).Value;
            IndRGBlow = newToken.GetProperty<Int32Property>(PropertyNames.IndRGBlow).Value;
            IndRGBhigh = newToken.GetProperty<Int32Property>(PropertyNames.IndRGBhigh).Value;
            MidtonesParam = newToken.GetProperty<Int32Property>(PropertyNames.MidtonesParam).Value;

            base.OnSetToken(newToken);

            update_parameters_from_UI();
        }

        #region User Entered Code
        // Name: Photo colors and levels correction
        // Submenu:
        // Author: Rémy LUCAS
        // Title: Photo colors and levels correction
        // Version: 2.0
        // Desc: Temperature, saturation, level and gamma correction
        // Keywords: Color Temperature Saturation Level Gamma
        // URL:
        // Help:

        // For help writing a Bitmap plugin: https://boltbait.com/pdn/CodeLab/help/tutorial/bitmap/

        #region UICode
        IntSliderControl TemperatureFact = 0; // [-20,20,9] Color temperature correction
        IntSliderControl SaturationFact = 0; // [-100,100,3] Color saturation adjustement
        IntSliderControl CorrLowLevels = 100; // [0,150] Low levels to 0 output factor
        IntSliderControl CorrHighLevels = 100; // [0,150] High levels to 255 output factor
        IntSliderControl IndRGBlow = 0; // [0,100] Low levels RGB independence factor
        IntSliderControl IndRGBhigh = 0; // [0,100] High levels RGB independence factor
        IntSliderControl MidtonesParam = 128; // [0,255,5] Gamma correction (mid tones)
        #endregion

        byte scan_minR;
        byte scan_maxR;
        byte scan_minG;
        byte scan_maxG;
        byte scan_minB;
        byte scan_maxB;

        byte minR;
        byte maxR;
        byte minG;
        byte maxG;
        byte minB;
        byte maxB;
        double KR,KB,KG;
        bool scanoriginalOK = false;

        double GammaCorrection;
        double SaturationCorrection;

        int Midtones;

        // OnInitializeRenderInfo function is NOT like the old PreRender() function
        // OnInitializeRenderInfo is called ONE time
        //                        is NOT called after the UI changes
        protected override void OnInitializeRenderInfo(IBitmapEffectRenderInfo renderInfo) {
            base.OnInitializeRenderInfo(renderInfo);

            scan_whole_original_picture();
            update_parameters_from_UI();
        }

        void scan_whole_original_picture() {
            byte R,G,B;

            using IEffectInputBitmap<ColorBgra32> sourceBitmap = Environment.GetSourceBitmapBgra32();
            using IBitmapLock<ColorBgra32> sourceLock = sourceBitmap.Lock(new RectInt32(0, 0, sourceBitmap.Size));
            RegionPtr<ColorBgra32> sourceRegion = sourceLock.AsRegionPtr();

            int TX = sourceBitmap.Size.Width;
            int TY = sourceBitmap.Size.Height;

            scan_minR = 255;
            scan_minG = 255;
            scan_minB = 255;
            scan_maxR = 0;
            scan_maxG = 0;
            scan_maxB = 0;

            for (int y = 0; y < TY; ++y)
            {
                if (IsCancelRequested) return;

                for (int x = 0; x < TX; ++x)
                {
                    // Get your source pixel
                    ColorBgra32 sourcePixel = sourceRegion[x,y];

                    // Find min and max value for each RGB value :
                    B = sourcePixel.B;
                    G = sourcePixel.G;
                    R = sourcePixel.R;
                    if (scan_minR > R) scan_minR = R;
                    if (scan_minG > G) scan_minG = G;
                    if (scan_minB > B) scan_minB = B;
                    if (scan_maxR < R) scan_maxR = R;
                    if (scan_maxG < G) scan_maxG = G;
                    if (scan_maxB < B) scan_maxB = B;
                 }
            }

        }

        void update_parameters_from_UI() {
            SaturationCorrection = 1+0.01*SaturationFact;

            double Rmini,Gmini,Bmini,Rmaxi,Gmaxi,Bmaxi;

            Rmini = (double)scan_minR * CorrLowLevels / 100;
            Gmini = (double)scan_minG * CorrLowLevels / 100;
            Bmini = (double)scan_minB * CorrLowLevels / 100;

            Rmaxi = (double)(255-scan_maxR) * CorrHighLevels / 100;
            Gmaxi = (double)(255-scan_maxG) * CorrHighLevels / 100;
            Bmaxi = (double)(255-scan_maxB) * CorrHighLevels / 100;

            double mini, maxi;

            mini = Rmini;
            if (mini>Gmini) mini = Gmini;
            if (mini>Bmini) mini = Bmini;

            Rmini = mini + (Rmini-mini) * IndRGBlow / 100;
            Gmini = mini + (Gmini-mini) * IndRGBlow / 100;
            Bmini = mini + (Bmini-mini) * IndRGBlow / 100;

            maxi = Rmaxi;
            if (maxi>Gmaxi) maxi = Gmaxi;
            if (maxi>Bmaxi) maxi = Bmaxi;

            Rmaxi = maxi + (Rmaxi-maxi) * IndRGBhigh / 100;
            Gmaxi = maxi + (Gmaxi-maxi) * IndRGBhigh / 100;
            Bmaxi = maxi + (Bmaxi-maxi) * IndRGBhigh / 100;

            Rmaxi = 255 - Rmaxi;
            Gmaxi = 255 - Gmaxi;
            Bmaxi = 255 - Bmaxi;

            if (Rmaxi > 255) Rmaxi = 255;
            if (Gmaxi > 255) Gmaxi = 255;
            if (Bmaxi > 255) Bmaxi = 255;
            if (Rmaxi < 0) Rmaxi = 0;
            if (Gmaxi < 0) Gmaxi = 0;
            if (Bmaxi < 0) Bmaxi = 0;
            if (Rmini > 255) Rmini = 255;
            if (Gmini > 255) Gmini = 255;
            if (Bmini > 255) Bmini = 255;
            if (Rmini < 0) Rmini = 0;
            if (Gmini < 0) Gmini = 0;
            if (Bmini < 0) Bmini = 0;


            maxR = (byte)Rmaxi;
            maxG = (byte)Gmaxi;
            maxB = (byte)Bmaxi;
            minR = (byte)Rmini;
            minG = (byte)Gmini;
            minB = (byte)Bmini;


            if (maxR-minR>0) {
                KR = (double)255 / (double)(maxR - minR);
            } else {
                KR=255;
            }
            if (maxG-minG>0) {
                KG = (double)255 / (double)(maxG - minG);
            } else {
                KG=255;
            }
            if (maxB-minB>0) {
                KB = (double)255 / (double)(maxB - minB);
            } else {
                KB=255;
            }

            double Gamma;
            double MidtoneNormal;
            Gamma = 1;
            Midtones = 255 - MidtonesParam;
            MidtoneNormal = (double)Midtones / 255;
            if (Midtones < 128) {
                MidtoneNormal = MidtoneNormal * 2;
                Gamma = 1 + ( 9 * ( 1 - MidtoneNormal ) );
                if (Gamma>9.99) Gamma=9.99;
            } else if (Midtones > 128) {
                MidtoneNormal = ( MidtoneNormal * 2 ) - 1;
                Gamma = 1 - MidtoneNormal;
                if (Gamma<0.01) Gamma=0.01;
            }
            GammaCorrection = 1 / Gamma;
        }


        protected override void OnRender(IBitmapEffectOutput output) {
            using IEffectInputBitmap<ColorBgra32> sourceBitmap = Environment.GetSourceBitmapBgra32();
            using IBitmapLock<ColorBgra32> sourceLock = sourceBitmap.Lock(new RectInt32(0, 0, sourceBitmap.Size));
            RegionPtr<ColorBgra32> sourceRegion = sourceLock.AsRegionPtr();

            RectInt32 outputBounds = output.Bounds;
            using IBitmapLock<ColorBgra32> outputLock = output.LockBgra32();
            RegionPtr<ColorBgra32> outputSubRegion = outputLock.AsRegionPtr();
            var outputRegion = outputSubRegion.OffsetView(-outputBounds.Location);
            //uint seed = RandomNumber.InitializeSeed(RandomNumberRenderSeed, outputBounds.Location);

            // Delete any of these lines you don't need
            /*
            ColorBgra32 primaryColor = Environment.PrimaryColor.GetBgra32(sourceBitmap.ColorContext);
            ColorBgra32 secondaryColor = Environment.SecondaryColor.GetBgra32(sourceBitmap.ColorContext);
            int canvasCenterX = Environment.Document.Size.Width / 2;
            int canvasCenterY = Environment.Document.Size.Height / 2;
            var selection = Environment.Selection.RenderBounds;
            int selectionCenterX = (selection.Right - selection.Left) / 2 + selection.Left;
            int selectionCenterY = (selection.Bottom - selection.Top) / 2 + selection.Top;
            */

            double R, G, B;
            double hue, saturation; //, value;
            int max, min;
            double v,p,q,t;
            double f;
            int hi;
            byte RED, GREEN, BLUE;

            //update_parameters_from_UI();

            // Loop through the output canvas tile
            for (int y = outputBounds.Top; y < outputBounds.Bottom; ++y)
            {
                if (IsCancelRequested) return;

                for (int x = outputBounds.Left; x < outputBounds.Right; ++x)
                {
                    // Get your source pixel
                    ColorBgra32 sourcePixel = sourceRegion[x,y];

                    RED = sourcePixel.R;
                    GREEN = sourcePixel.G;
                    BLUE = sourcePixel.B;

                    // Temperature correction ================================================
                    if (TemperatureFact!=0) {
                        // thanks BoltBait
                        RED = Clamp2Byte(RED + TemperatureFact);
                        BLUE = Clamp2Byte(BLUE - TemperatureFact);
                    }

                    // Saturation correction =================================================
                    if (SaturationFact!=0) {
                        // RGB => HSV
                        max = Math.Max(RED, Math.Max(GREEN, BLUE));
                        min = Math.Min(RED, Math.Min(GREEN, BLUE));
                        hue = System.Drawing.Color.FromArgb(RED,GREEN,BLUE).GetHue();
                        saturation = (max == 0) ? 0 : 1d - (1d * min / max);

                        saturation = saturation * SaturationCorrection;

                        // HSV => RGB
                        hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
                        f = hue / 60 - Math.Floor(hue / 60);
                        v = max;
                        p = max * (1 - saturation);
                        q = max * (1 - f * saturation);
                        t = max * (1 - (1 - f) * saturation);

                        if (hi == 0) {
                            R = v;
                            G = t;
                            B = p;
                            //color = Color.FromArgb(255, v, t, p);
                        } else if (hi == 1) {
                            R = q;
                            G = v;
                            B = p;
                            //color = Color.FromArgb(255, q, v, p);
                        } else if (hi == 2) {
                            R = p;
                            G = v;
                            B = t;
                            //color = Color.FromArgb(255, p, v, t);
                        }else if (hi == 3) {
                            R = p;
                            G = q;
                            B = v;
                            //color = Color.FromArgb(255, p, q, v);
                        }else if (hi == 4) {
                            R = t;
                            G = p;
                            B = v;
                            //color = Color.FromArgb(255, t, p, v);
                        }else {
                            R = v;
                            G = p;
                            B = q;
                            //color = Color.FromArgb(255, v, p, q);
                        }
                    } else {
                        R = RED;
                        G = GREEN;
                        B = BLUE;
                    }

                    // Low and High level correction =========================================
                    R = KR * (R - minR);
                    G = KG * (G - minG);
                    B = KB * (B - minB);

                    // Gamma correction ======================================================
                    if (Midtones!=128) {
                        R = 255 * Math.Pow(R / 255, GammaCorrection);
                        G = 255 * Math.Pow(G / 255, GammaCorrection);
                        B = 255 * Math.Pow(B / 255, GammaCorrection);
                    }

                    // =======================================================================

                    sourcePixel.R = (byte)R;
                    sourcePixel.G = (byte)G;
                    sourcePixel.B = (byte)B;

                    // Save your pixel to the output canvas
                    outputRegion[x,y] = sourcePixel;
                 }
            }
        }


        private byte Clamp2Byte(int iValue) // thanks BoltBait
        {
            if (iValue < 0) return 0;
            if (iValue > 255) return 255;
            return (byte)iValue;
        }
        #endregion
    }
}
