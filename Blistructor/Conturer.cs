using System;
using System.Collections.Generic;
using System.Linq;

using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;

namespace Blistructor
{
    public static class Conturer
    {
        public static List<List<int[]>> getContours(string pathToImage, double tolerance)
        {
            Image<Gray, Byte> img = new Image<Gray, byte>(pathToImage);
            UMat uimage = new UMat();
            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
            CvInvoke.FindContours(img, contours, null, RetrType.List, ChainApproxMethod.LinkRuns);
            List<List<int[]>> allPoints = new List<List<int[]>>();
            int count = contours.Size;
            int nContours = count;
            for (int i = 0; i < count; i++)
            {
                using (VectorOfPoint contour = contours[i])
                using (VectorOfPoint approxContour = new VectorOfPoint())
                {
                    CvInvoke.ApproxPolyDP(contour, approxContour, CvInvoke.ArcLength(contour, true) * tolerance, true);
                    System.Drawing.Point[] pts = approxContour.ToArray();
                    List<int[]> contPoints = new List<int[]>();
                    for (int k = 0; k < pts.Length; k++)
                    {
                        int[] pointsCord = new int[2];
                        pointsCord[0] = pts[k].X;
                        pointsCord[1] = pts[k].Y;
                        contPoints.Add(pointsCord);
                    }
                    allPoints.Add(contPoints);
                }
            }
            return allPoints;
        }
    }
}
