// Name: Photo colors and levels correction
// Submenu:
// Author: Rémy LUCAS
// Title: Photo colors and levels correction
// Version: 1.0
// Desc: Temperature, saturation, level and gamma correction
// Keywords: Color Temperature Saturation Level Gamma
// URL:
// Help:

/*
// Decorations available  :
IntSliderControl S1 = 0; // [0,100,1] S1
IntSliderControl S2 = 0; // [0,100,2] S2
IntSliderControl S3 = 0; // [0,100,3] S3
IntSliderControl S4 = 0; // [0,100,4] S4
IntSliderControl S5 = 0; // [0,100,5] S5
IntSliderControl S6 = 0; // [0,100,6] S6
IntSliderControl S7 = 0; // [0,100,7] S7
IntSliderControl S8 = 0; // [0,100,8] S8
IntSliderControl S9 = 0; // [0,100,9] S9
IntSliderControl S10 = 0; // [0,100,10] S10
IntSliderControl S11 = 0; // [0,100,11] S11
IntSliderControl S12 = 0; // [0,100,12] S12
*/

#region UICode
IntSliderControl TemperatureFact = 0; // [-20,20,9] Color temperature correction
IntSliderControl SaturationFact = 0; // [-100,100,3] Color saturation factor
IntSliderControl CorrLowLevels = 100; // [0,150] Low levels to 0 output factor
IntSliderControl CorrHighLevels = 100; // [0,150] High levels to 255 output factor
IntSliderControl IndRGBlow = 0; // [0,100] Low levels RGB independance factor
IntSliderControl IndRGBhigh = 0; // [0,100] High levels RGB independance factor
IntSliderControl Midtones = 128; // [0,255,5] Gamma correction (mid tones)
#endregion

// This single-threaded function is called after the UI changes and before the Render function is called
// The purpose is to prepare anything you'll need in the Render function
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

/*
double[] HistoR = new double[256];
double[] HistoG = new double[256];
double[] HistoB = new double[256];
*/

void PreRender(Surface dst, Surface src)
{
    byte R,G,B;

    //if (!scanoriginalOK) {
		scan_minR = 255;
		scan_minG = 255;
		scan_minB = 255;
		scan_maxR = 0;
		scan_maxG = 0;
		scan_maxB = 0;

		/*
		long[] HR = new long[256];
		long[] HG = new long[256];
		long[] HB = new long[256];
		int i;
		for (i=0;i<256;i++) {
			HR[i] = 0;
			HG[i] = 0;
			HB[i] = 0;
		}
		*/

		for (int y = src.Bounds.Top; y < src.Bounds.Bottom; ++y)
		{
			if (IsCancelRequested) return;

			for (int x = src.Bounds.Left; x < src.Bounds.Right; ++x)
			{
				// Get your source pixel
				ColorBgra sourcePixel = src[x,y];

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

				/*
				// Histogram count :
				HR[R]++;
				HG[G]++;
				HB[B]++;
				*/
			}
		}

		
		/*
		long nbpix = (src.Bounds.Bottom- src.Bounds.Top) * (src.Bounds.Right-src.Bounds.Left);
		long HR_max = 0;
		long HG_max = 0;
		long HB_max = 0;
		for (i=0;i<256;i++) {
			if (HR[i] > HR_max) HR_max = HR[i];
			if (HG[i] > HG_max) HG_max = HG[i];
			if (HB[i] > HB_max) HB_max = HB[i];
		}
		double HR_K = 255 / HR_max;
		double HG_K = 255 / HG_max;
		double HB_K = 255 / HB_max;
		for (i=0;i<256;i++) {
			HistoR[i] = HR[i] * HR_K;
			HistoG[i] = HG[i] * HG_K;
			HistoB[i] = HB[i] * HB_K;
		}
		*/
		//scanoriginalOK = true;
   // }

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
    Midtones = 255 - Midtones;
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

// Here is the main multi-threaded render function
// The dst canvas is broken up into rectangles and
// your job is to write to each pixel of that rectangle
void Render(Surface dst, Surface src, Rectangle rect)
{
    // uint seed = RandomNumber.InitializeSeed(RandomNumberRenderSeed, rect.Location);
    double R, G, B;
    double hue, saturation; //, value;
    Color color;
    int max, min;
    double v,p,q,t;
    double f;
    int hi;

    // Step through each row of the current rectangle
    for (int y = rect.Top; y < rect.Bottom; y++)
    {
        if (IsCancelRequested) return;
        // Step through each pixel on the current row of the rectangle
        for (int x = rect.Left; x < rect.Right; x++)
        {
            ColorBgra SrcPixel = src[x,y];
            ColorBgra CurrentPixel = SrcPixel;
            /*
            ColorBgra CurrentPixel = SrcPixel;
            CurrentPixel.B = (byte) ((KB * (double)(SrcPixel.B - minB)));
            CurrentPixel.G = (byte) ((KG * (double)(SrcPixel.G - minG)));
            CurrentPixel.R = (byte) ((KR * (double)(SrcPixel.R - minR)));
            
            if (Midtones!=128) {
                CurrentPixel.R = (byte) (255 * ( Math.Pow( ( (double)CurrentPixel.R / 255 ), GammaCorrection ) ));
                CurrentPixel.G = (byte) (255 * ( Math.Pow( ( (double)CurrentPixel.G / 255 ), GammaCorrection ) ));
                CurrentPixel.B = (byte) (255 * ( Math.Pow( ( (double)CurrentPixel.B / 255 ), GammaCorrection ) ));
            }
            */
            
            // Temperature correction ================================================
            if (TemperatureFact!=0) {
                // thanks BoltBait
                CurrentPixel.R = Clamp2Byte(CurrentPixel.R + TemperatureFact); 
                CurrentPixel.B = Clamp2Byte(CurrentPixel.B - TemperatureFact); 
            }
            
            // Saturation correction =================================================
            if (SaturationFact!=0) {
                // RGB => HSV
                color = Color.FromArgb(CurrentPixel.R,CurrentPixel.G,CurrentPixel.B);
                max = Math.Max(color.R, Math.Max(color.G, color.B));
                min = Math.Min(color.R, Math.Min(color.G, color.B));
                hue = color.GetHue();
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
                R = CurrentPixel.R;
                G = CurrentPixel.G;
                B = CurrentPixel.B;
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

            CurrentPixel.R = (byte)R;
            CurrentPixel.G = (byte)G;
            CurrentPixel.B = (byte)B;
            dst[x,y] = CurrentPixel;
        }
    }
}


private byte Clamp2Byte(int iValue) // thanks BoltBait
{
    if (iValue < 0) return 0;
    if (iValue > 255) return 255;
    return (byte)iValue;
}

public static void ColorToHSV(Color color, out double hue, out double saturation, out double value)
{
    int max = Math.Max(color.R, Math.Max(color.G, color.B));
    int min = Math.Min(color.R, Math.Min(color.G, color.B));

    hue = color.GetHue();
    saturation = (max == 0) ? 0 : 1d - (1d * min / max);
    value = max / 255d;
}

public static Color ColorFromHSV(double hue, double saturation, double value)
{
    int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
    double f = hue / 60 - Math.Floor(hue / 60);

    value = value * 255;
    int v = Convert.ToInt32(value);
    int p = Convert.ToInt32(value * (1 - saturation));
    int q = Convert.ToInt32(value * (1 - f * saturation));
    int t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

    if (hi == 0)
        return Color.FromArgb(255, v, t, p);
    else if (hi == 1)
        return Color.FromArgb(255, q, v, p);
    else if (hi == 2)
        return Color.FromArgb(255, p, v, t);
    else if (hi == 3)
        return Color.FromArgb(255, p, q, v);
    else if (hi == 4)
        return Color.FromArgb(255, t, p, v);
    else
        return Color.FromArgb(255, v, p, q);
}
