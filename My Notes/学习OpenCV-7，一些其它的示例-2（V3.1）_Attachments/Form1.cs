using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.UI;
using Emgu.Util;
using Emgu.CV.Structure;
using Emgu.CV.Util;

namespace Test1
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string tmpPath = @"C:\Users\win\Desktop\test.png";
            Bitmap tmpBitmap = (Bitmap)Image.FromFile(tmpPath);
            tmpBitmap.MakeTransparent(Color.FromArgb(128, 253, 253, 251));
            tmpBitmap.Save(@"C:\Users\win\Desktop\test2.png");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Mat tmpMat = new Mat(new Size(400, 400), Emgu.CV.CvEnum.DepthType.Cv8U, 3);
            tmpMat.SetTo(new Emgu.CV.Structure.MCvScalar(0, 255, 255));
            imageBox1.Image = tmpMat;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            Mat srcImg = new Mat("行人1.jpg");
            Mat mbImg = new Mat("模板2.png");

            imageBox1.Image = srcImg;
            MessageBox.Show("原始");

            int result_cols = srcImg.Cols - mbImg.Cols + 1;
            int result_rows = srcImg.Rows - mbImg.Rows + 1;

            Mat resImg = new Mat(new Size(result_cols, result_rows), Emgu.CV.CvEnum.DepthType.Cv32F, 1);

            CvInvoke.MatchTemplate(srcImg, mbImg, resImg, Emgu.CV.CvEnum.TemplateMatchingType.CcoeffNormed);
            CvInvoke.Normalize(resImg, resImg, 255, 0, Emgu.CV.CvEnum.NormType.MinMax);

            imageBox1.Image = resImg.ToImage<Gray,byte>();
            
        }

        private void button4_Click(object sender, EventArgs e)
        {
            Mat srcImg = new Mat("行人1.jpg"); 
            //imageBox1.Image = srcImg;

            Mat srcGray = new Mat();
            CvInvoke.CvtColor(srcImg, srcGray, Emgu.CV.CvEnum.ColorConversion.Bgr2Gray);
            imageBox1.Image = srcGray;
            CvInvoke.Blur(srcGray, srcGray, new Size(3, 3), new Point(-1, -1));
            MessageBox.Show("原图");

            Mat cannyRes = new Mat();
            CvInvoke.Canny(srcGray, cannyRes, 100, 200, 3);

            imageBox1.Image = cannyRes;
            MessageBox.Show("canny");

            Mat hierar = new Mat();
            VectorOfVectorOfPoint contourRes = new VectorOfVectorOfPoint();
            CvInvoke.FindContours(cannyRes, contourRes, hierar, Emgu.CV.CvEnum.RetrType.Tree, Emgu.CV.CvEnum.ChainApproxMethod.ChainApproxSimple);

            imageBox1.Image = hierar;
            MessageBox.Show("hierar");

            Random tmpRandom = new Random();
            Mat resultMat = new Mat(srcImg.Size, Emgu.CV.CvEnum.DepthType.Cv8U, 3);
            for (int i = 0; i < contourRes.Size; i++)
            {
                CvInvoke.DrawContours(resultMat, contourRes, i, new MCvScalar(tmpRandom.Next(255), tmpRandom.Next(255), tmpRandom.Next(255)));
            }

            imageBox1.Image = resultMat;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            Mat srcImg = new Mat("行人1.jpg");
            //imageBox1.Image = srcImg;

            Mat srcGray = new Mat();
            CvInvoke.CvtColor(srcImg, srcGray, Emgu.CV.CvEnum.ColorConversion.Bgr2Gray);
            imageBox1.Image = srcGray;
            CvInvoke.Blur(srcGray, srcGray, new Size(3, 3), new Point(-1, -1));
            MessageBox.Show("原图");

            Mat cannyRes = new Mat();
            CvInvoke.Canny(srcGray, cannyRes, 100, 200, 3);

            imageBox1.Image = cannyRes;
            MessageBox.Show("canny");

            Mat hierar = new Mat();
            VectorOfVectorOfPoint contourRes = new VectorOfVectorOfPoint();
            CvInvoke.FindContours(cannyRes, contourRes, hierar, Emgu.CV.CvEnum.RetrType.Tree, Emgu.CV.CvEnum.ChainApproxMethod.ChainApproxSimple);

            VectorOfVectorOfPoint hullRes = new VectorOfVectorOfPoint(contourRes.Size);
            imageBox1.Image = hierar;
            MessageBox.Show("hierar");

            Random tmpRandom = new Random();
            Mat resultMat = new Mat(srcImg.Size, Emgu.CV.CvEnum.DepthType.Cv8U, 3);
            for (int i = 0; i < contourRes.Size; i++)
            {
                CvInvoke.ConvexHull(contourRes[i], hullRes[i], false);
                MCvScalar tmpColor =  new MCvScalar(tmpRandom.Next(255), tmpRandom.Next(255), tmpRandom.Next(255));
                CvInvoke.DrawContours(resultMat, contourRes, i, tmpColor);
                CvInvoke.DrawContours(resultMat, hullRes, i, tmpColor);

            }

            imageBox1.Image = resultMat;
            MessageBox.Show("hull");


        }

        private void button6_Click(object sender, EventArgs e)
        {
            Mat srcImg = new Mat("行人1.jpg");

            Mat srcGray = new Mat();
            CvInvoke.CvtColor(srcImg, srcGray, Emgu.CV.CvEnum.ColorConversion.Bgr2Gray);
            imageBox1.Image = srcGray;
            MessageBox.Show("原图");

            Mat srcThres = new Mat();
            CvInvoke.Threshold(srcGray, srcThres, 150, 255, Emgu.CV.CvEnum.ThresholdType.Binary);
            imageBox1.Image = srcThres;
            MessageBox.Show("阈值");

            Mat hierar = new Mat();
            VectorOfVectorOfPoint contourRes = new VectorOfVectorOfPoint();
            CvInvoke.FindContours(srcThres, contourRes, hierar, Emgu.CV.CvEnum.RetrType.Tree, Emgu.CV.CvEnum.ChainApproxMethod.ChainApproxSimple);


            Random tmpRandom = new Random();
            Mat resultMat = new Mat(srcImg.Size, Emgu.CV.CvEnum.DepthType.Cv8U, 3);
            //vectorofvec
            for (int i = 0; i < contourRes.Size; i++)
            { 
                MCvScalar tmpColor = new MCvScalar(tmpRandom.Next(255), tmpRandom.Next(255), tmpRandom.Next(255));
                CvInvoke.DrawContours(resultMat, contourRes, i, tmpColor);

                //Rectangle Rect = CvInvoke.BoundingRectangle(contourRes[i]);
                //CvInvoke.Rectangle(resultMat, Rect, tmpColor);

                //VectorOfPoint tmpLine = new VectorOfPoint();
                //CvInvoke.MinEnclosingTriangle(contourRes[i], tmpLine);
                //CvInvoke.Polylines(resultMat, tmpLine, true, tmpColor);

                CircleF tmpCir = CvInvoke.MinEnclosingCircle(contourRes[i]);
                CvInvoke.Circle(resultMat, Point.Ceiling( tmpCir.Center), (int)tmpCir.Radius, tmpColor);

            }
            imageBox1.Image = resultMat;
            MessageBox.Show("contour");


        }

        Emgu.CV.VideoCapture capture = null;
        private void button7_Click(object sender, EventArgs e)
        {
             capture = new VideoCapture();
             Application.Idle += new EventHandler(Application_Idle);
        }

        void Application_Idle(object sender, EventArgs e)
        {
            if (capture != null)
            {
                imageBox1.Image = capture.QueryFrame();
            }
        }

        private void button8_Click(object sender, EventArgs e)
        {
            VideoCapture tmpVideo = new VideoCapture("test.mp4");
            double fCount = tmpVideo.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameCount);
            double fWidth = tmpVideo.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameWidth);

            double tmpframe = tmpVideo.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.PosFrames);
            double tmptime = tmpVideo.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.PosMsec);
            double fps = tmpVideo.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.Fps);

            imageBox1.Image = tmpVideo.QueryFrame();

             tmpframe = tmpVideo.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.PosFrames);
             tmptime = tmpVideo.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.PosMsec); 
        }

        VideoWriter writer = null;
        Timer tmptimer = null;
        private void button9_Click(object sender, EventArgs e)
        {
            capture = new VideoCapture();
            int frameWidth = (int)capture.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameWidth);
            int frameHeight = (int)capture.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameHeight);
            writer = new VideoWriter("res.mp4", 10, new System.Drawing.Size(frameWidth, frameHeight), true);

            tmptimer = new Timer();
            tmptimer.Interval = 100;
            tmptimer.Tick += new EventHandler(tmptimer_Tick);
            tmptimer.Start();
        }

        int frames = 0;
        void tmptimer_Tick(object sender, EventArgs e)
        {
            writer.Write(capture.QueryFrame());
            frames++;
            if (frames >= 200)
            {
                tmptimer.Stop();
                capture.Dispose();
                writer.Dispose();
                MessageBox.Show("done");
            }
        }

        private void button10_Click(object sender, EventArgs e)
        {
            Mat srcImg = new Mat("行人1.jpg");

            Mat srcGray = new Mat();
            CvInvoke.CvtColor(srcImg, srcGray, Emgu.CV.CvEnum.ColorConversion.Bgr2Gray);
            imageBox1.Image = srcGray;
            MessageBox.Show("原图");

            Mat destImg = new Mat(srcGray.Size, Emgu.CV.CvEnum.DepthType.Cv32F,1);
            CvInvoke.CornerHarris(srcGray, destImg, 2);

            CvInvoke.Normalize(destImg, destImg, 0, 255, Emgu.CV.CvEnum.NormType.MinMax);
            Mat scaleImg = new Mat();
            CvInvoke.ConvertScaleAbs(destImg, scaleImg, 1, 0);
            imageBox1.Image = scaleImg;
            MessageBox.Show("Harris角点检测");
        }

        private void button11_Click(object sender, EventArgs e)
        {
            Mat srcImg = new Mat("行人1.jpg");

            Mat srcGray = new Mat();
            CvInvoke.CvtColor(srcImg, srcGray, Emgu.CV.CvEnum.ColorConversion.Bgr2Gray);
            imageBox1.Image = srcGray;
            MessageBox.Show("原图");

            Mat shiTomasiImg = new Mat();
            //srcGray.ToImage<Gray,byte>().
            Emgu.CV.Features2D.GFTTDetector detector = new Emgu.CV.Features2D.GFTTDetector(100, 0.01, 1, 3, false, 0.04);
            MKeyPoint[] pnts = detector.Detect(srcGray);

            for (int i = 0; i < pnts.Length; i++)
            {
                CvInvoke.Circle(srcGray, Point.Ceiling(pnts[i].Point), (int)pnts[i].Size, new MCvScalar(255));
            }
            imageBox1.Image = srcGray;
            MessageBox.Show("detector");
        }

        private void button12_Click(object sender, EventArgs e)
        {
            Mat srcImg = new Mat("行人1.jpg");

            Mat srcGray = new Mat();
            CvInvoke.CvtColor(srcImg, srcGray, Emgu.CV.CvEnum.ColorConversion.Bgr2Gray);
            imageBox1.Image = srcGray;
            MessageBox.Show("原图");

            Emgu.CV.XFeatures2D.SURF surf = new Emgu.CV.XFeatures2D.SURF(400);
            MKeyPoint[] pnts = surf.Detect(srcGray);
            VectorOfKeyPoint pntVector = new VectorOfKeyPoint(pnts);

            Emgu.CV.Features2D.Features2DToolbox.DrawKeypoints(srcGray, pntVector, srcGray, new Bgr(255, 255, 255));
            //for (int i = 0; i < pnts.Length; i++)
            //{
            //    CvInvoke.Circle(srcGray, Point.Ceiling(pnts[i].Point), 3, new MCvScalar(255));
            //}
            imageBox1.Image = srcGray;
            MessageBox.Show("detector");
        }


    }
}
