using System;
using System.Collections.Generic;
using System.ComponentModel;
//using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using OSGeo.GDAL;
using System.IO;
using System.Management;

namespace TestGdal
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        #region 影像切割
        private bool cut_loadInfo = false;

        private void cut_browserBtn_Click(object sender, EventArgs e)
        {
            OpenFileDialog mDlg = new OpenFileDialog();
            //mDlg.Filter = "影像文件(*.img)|*.img";
            mDlg.Filter = "影像文件(*.img *.pix *.tif)|*.img;*.pix;*.tif";
            if (mDlg.ShowDialog() != DialogResult.OK)
                return;

            this.cut_pathBox.Text = mDlg.FileName;

            ClearInfo();
        }

        private void ClearInfo()
        {
            this.cut_rasterWidthBox.Text = "";
            this.cut_rasterHeightBox.Text = "";
            this.cut_bandBox.Text = "";
            this.cut_geoTransBox.Text = "";
            this.cut_projBox.Text = "";
            cut_loadInfo = false;
        }

        private void cut_cancelBtn_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private Dataset ds = null;

        private void cut_readRasterInfoBtn_Click(object sender, EventArgs e)
        {
            if (cut_pathBox.Text == "")
            {
                MessageBox.Show("请选择要处理的影像");
                return;
            }

            
            //注册并声明环境变量
            Gdal.SetConfigOption("GDAL_FILENAME_IS_UTF8", "NO");            
            Gdal.SetConfigOption("GDAL_DATA", Application.StartupPath + "\\gdaldata");
            //Gdal.SetConfigOption("GDAL_FORCE_CACHING", "YES");
            Gdal.AllRegister();

            try
            {
                //先打开影像文件
                ds = Gdal.Open(cut_pathBox.Text, Access.GA_ReadOnly);
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开影像失败,详细信息：" + ex.Message);
                return;
            }

            //影像长宽
            this.cut_rasterWidthBox.Text = ds.RasterXSize.ToString();
            this.cut_rasterHeightBox.Text = ds.RasterYSize.ToString();
            //影像波段数
            this.cut_bandBox.Text = ds.RasterCount.ToString();
            //影像变换参数
            double[] tmpD = new double[6];
            ds.GetGeoTransform(tmpD);
            string geoTrans = tmpD[0] + "," + tmpD[1] + "," + tmpD[2] + "," + tmpD[3] + "," + tmpD[4] + "," + tmpD[5];
            this.cut_geoTransBox.Text = geoTrans;
            //影像坐标系
            this.cut_projBox.Text = ds.GetProjection();

            if (ds.GetRasterBand(1).DataType != DataType.GDT_Byte)
            {
                MessageBox.Show("影像值类型非BTYE类型,不支持对该影像进行处理");
                return;
            }            
            cut_loadInfo = true;

            int blockx, blocky;
            ds.GetRasterBand(1).GetBlockSize(out blockx, out blocky);

        }

        private void cut_savePathBtn_Click(object sender, EventArgs e)
        {
            SaveFileDialog mDlg = new SaveFileDialog();
            mDlg.Filter = "影像文件(*.img *.pix,*.tif)|*.img;*.pix;*.tif";
            //mDlg.Filter = "影像文件(*.img)|*.img";
            if (mDlg.ShowDialog() != DialogResult.OK)
                return;

            this.cut_savePathBox.Text = mDlg.FileName;
        }

        private void cut_bandSelBtn_Click(object sender, EventArgs e)
        {
            string[] bands = this.cut_bandSelBox.Text.Split(new char[] { ',' });
            List<int> bands2 = new List<int>();
            if (this.cut_bandSelBox.Text != "")
                for (int i = 0; i < bands.Length; i++)
                {
                    bands2.Add(int.Parse(bands[i].Trim()));
                }

            List<int> tmpStr2 = new List<int>();
            for (int i = 1; i <= int.Parse(this.cut_bandBox.Text); i++)
            {
                tmpStr2.Add(i);
            }

            BandSelForm mDlg = new BandSelForm();
            mDlg.allBands = tmpStr2;
            mDlg.selBands = bands2;
            mDlg.ShowDialog();
            if (mDlg.suc)
            {
                string tmpStr = "";
                for (int i = 0; i < mDlg.selBands.Count; i++)
                {
                    tmpStr += mDlg.selBands[i] + ",";
                }
                if (tmpStr != "")
                    tmpStr = tmpStr.Substring(0, tmpStr.Length - 1);
                this.cut_bandSelBox.Text = tmpStr;
            }
        }

        private void cut_okBtn_Click(object sender, EventArgs e)
        {
            #region 数据检验
            if (!cut_loadInfo)
            {
                MessageBox.Show("请先读取影像信息");
                return;
            }

            int x = -1;
            if (cut_xBox.Text.Trim() == "" ||
                !int.TryParse(cut_xBox.Text.Trim(), out x))
            {
                MessageBox.Show("左上角点X格式不正确");
                return;
            }
            if (x < 0 || x >= int.Parse(cut_rasterWidthBox.Text))
            {
                MessageBox.Show("左上角点X越界");
                return;
            }

            int y = -1;
            if (cut_yBox.Text.Trim() == "" ||
                !int.TryParse(cut_yBox.Text.Trim(), out y))
            {
                MessageBox.Show("左上角点Y格式不正确");
                return;
            }
            if (y < 0 || y >= int.Parse(cut_rasterHeightBox.Text))
            {
                MessageBox.Show("左上角点Y越界");
                return;
            }

            int width = -1;
            if (cut_cutWidthBox.Text.Trim() == "" ||
                !int.TryParse(cut_cutWidthBox.Text, out width))
            {
                MessageBox.Show("切割宽度格式不正确");
                return;
            }
            if (width < 0 || width + x > int.Parse(cut_rasterWidthBox.Text))
            {
                MessageBox.Show("切割宽度越界");
                return;
            }

            int height = -1;
            if (cut_cutHeightBox.Text.Trim() == "" ||
                !int.TryParse(cut_cutHeightBox.Text, out height))
            {
                MessageBox.Show("切割高度格式不正确");
                return;
            }
            if (height < 0 || height + y > int.Parse(cut_rasterHeightBox.Text))
            {
                MessageBox.Show("切割高度越界");
                return;
            }

            if (cut_bandSelBox.Text == "")
            {
                MessageBox.Show("选择波段不能为空");
                return;
            }

            if (cut_savePathBox.Text == "")
            {
                MessageBox.Show("请选择保存文件位置");
                return;
            }
            if (File.Exists(cut_savePathBox.Text))
            {
                MessageBox.Show("\"" + cut_savePathBox.Text + "\"已经存在");
                return;
            }

            #endregion

            //获取波段
            string[] bands = this.cut_bandSelBox.Text.Split(new char[] { ',' });
            List<int> selBands = new List<int>();
            for (int i = 0; i < bands.Length; i++)
            {
                selBands.Add(int.Parse(bands[i].Trim()));
            }
            int[] sourceBands = selBands.ToArray();
            int[] desBands = new int[selBands.Count];
            for (int j = 0; j < selBands.Count; j++)
            {
                desBands[j] = j + 1;
            }

            for (int i = 0; i < selBands.Count; i++)
            {
                Band tmpBand = ds.GetRasterBand(selBands[i]);
                if (tmpBand.DataType != DataType.GDT_Byte)
                {
                    MessageBox.Show("源影像的第" + selBands[i]+"个波段并非BYTE类型，无法处理");
                    return;
                }
            }

            //获取坐标偏移参数
            double[] argin = new double[6];
            ds.GetGeoTransform(argin);

           
            //获取影像驱动,生成影像
            Driver d = null;
            if (this.cut_savePathBox.Text.EndsWith(".img") ||
                this.cut_savePathBox.Text.EndsWith(".IMG"))
                    d = Gdal.GetDriverByName("HFA");
            else if (this.cut_savePathBox.Text.EndsWith(".pix") ||
                this.cut_savePathBox.Text.EndsWith(".PIX"))
                    d = Gdal.GetDriverByName("PCIDSK");
            else if (this.cut_savePathBox.Text.EndsWith(".tif") ||
                    this.cut_savePathBox.Text.EndsWith(".TIF"))
                    d = Gdal.GetDriverByName("GTiff");
            else
            {
                MessageBox.Show("非法的影像格式");
                return;
            }
            Gdal.SetCacheMax(300*1024*1024);

            DateTime tmpTime1 = DateTime.Now;

            Dataset dsout;
            if (this.cut_savePathBox.Text.ToUpper().EndsWith(".IMG") ||
                this.cut_savePathBox.Text.ToUpper().EndsWith(".TIF"))
            {
                //获取影像的统计值，如果最小值小于0，那么表明这个影像是SignedByte
                //double max, min, mean, stddev;
                //ds.GetRasterBand(1).GetStatistics(1, 1, out min, out max, out mean, out stddev);
                
                //判断是否是SIGNEDBTYE型的波段
                string[] aa = ds.GetRasterBand(1).GetMetadata("IMAGE_STRUCTURE");
                bool signedByte = false;
                for (int i = 0; i < aa.Length; i++)
                {
                    if (aa[i] == "PIXELTYPE=SIGNEDBYTE")
                    {
                        signedByte = true; break;
                    }
                }
                if (signedByte)
                    dsout = d.Create(this.cut_savePathBox.Text, width, height, bands.Length, DataType.GDT_Byte, new string[] { "PIXELTYPE=SIGNEDBYTE" });
                else
                    dsout = d.Create(this.cut_savePathBox.Text, width, height, bands.Length, DataType.GDT_Byte, null);
            }
            else
                dsout = d.Create(this.cut_savePathBox.Text, width, height, bands.Length, DataType.GDT_Byte, null);
            

            
            //dsout.Dispose();
            //dsout = Gdal.Open(this.cut_savePathBox.Text, Access.GA_Update);

            //根据输入的参数,进行影像切割
            //int currentOutBand = 1;     //当前输出到的波段
            StreamWriter sw = new StreamWriter("log.txt", false);

            #region 能够分波段，逐波段读取10000*10000的块
            //for (int i = 1; i <= int.Parse(this.cut_bandBox.Text); i++)
            //{
            //    if (!selBands.Contains(i))
            //        continue;

            //    sw.WriteLine("band:" + i);
                
            //    if (width*height <= 10000*10000)
            //    {
            //        #region 当截取的影像范围比较小时

            //        //先指定波段,并将该波段中指定范围的数据读取到Byte数组中
            //        byte[] rawData = new byte[width * height];
            //        Band bnd = ds.GetRasterBand(i);
            //        bnd.ReadRaster(x, y, width, height, rawData, width, height, 0, 0);

            //        //将读取出来的数据写到输出的影像中
            //        int[] tmp = new int[1];
            //        tmp[0] = selBands.IndexOf(i) + 1;
            //        dsout.WriteRaster(0, 0, width, height, rawData, width, height, 1, tmp, 0, 0, 0);

            //        #endregion
            //    }
            //    else
            //    {
                   
            //        #region 当截取的影像范围比较大时,分块对影像进行读取和写入
            //        int tmpx = width / 10000;
            //        int tmpy = height / 10000;
            //        byte[] rawData = new byte[10000 * 10000];
            //        for (int j = 0; j < tmpx+1; j++)
            //        {
            //            for (int k = 0; k < tmpy+1; k++)
            //            {
            //                //GC.Collect();
            //                DateTime tmpTime3 = DateTime.Now;

            //                int tmpWidth = Math.Min(10000, width - j * 10000);
            //                int tmpHeight = Math.Min(10000, height - k * 10000);

            //                if (tmpWidth * tmpHeight != rawData.Length)
            //                    rawData = new byte[tmpWidth * tmpHeight];
            //                Band bnd = ds.GetRasterBand(i);
            //                bnd.ReadRaster(x+j*10000, y+k*10000, tmpWidth, tmpHeight, rawData, tmpWidth, tmpHeight, 0, 0);

            //                //将读取出来的数据写到输出的影像中
            //                int[] tmp = new int[1];
            //                tmp[0] = selBands.IndexOf(i) + 1;
            //                dsout.WriteRaster(j * 10000, k * 10000, tmpWidth, tmpHeight, rawData, tmpWidth, tmpHeight, 1, tmp, 0, 0, 0);
                                                       
            //                //if ((float)Gdal.GetCacheUsed() / (float)Gdal.GetCacheMax() > 0.95)
            //                //{
            //                //    //ds.FlushCache();
            //                //    //dsout.FlushCache();
            //                //    //sw.WriteLine("After FlushCache " + Gdal.GetCacheUsed() + " // " + Gdal.GetCacheMax());
            //                //    //sw.Flush();


            //                //    DateTime tmpTime5 = DateTime.Now;
            //                //    dsout.Dispose();
            //                //    dsout = Gdal.Open(this.cut_savePathBox.Text, Access.GA_Update);
            //                //    TimeSpan tmpTime6 = tmpTime5.Subtract(DateTime.Now);
            //                //    sw.WriteLine("After Close " + Gdal.GetCacheUsed() + " // " + Gdal.GetCacheMax()+" "+tmpTime6.TotalSeconds);
            //                //    sw.Flush();
            //                //}

            //                ds.FlushCache();
            //                dsout.FlushCache();                            

            //                TimeSpan tmpTime4 = tmpTime3.Subtract(DateTime.Now);
            //                sw.WriteLine(j + " " + k + " " + tmpTime4.TotalSeconds + " " + Gdal.GetCacheUsed());
            //                sw.Flush();

                            
            //            }
            //        }
            //        #endregion
                    
            //    }
            //    //currentOutBand++;
            //}
            #endregion

            #region 波段统一读，分10000*10000的块，这种方法比上一种稍快
            //int tmpx = width / 10000;
            //int tmpy = height / 10000;
            //byte[] rawData = new byte[3 * 10000 * 10000];
            //for (int j = 0; j < tmpx + 1; j++)
            //{
            //    for (int k = 0; k < tmpy + 1; k++)
            //    {
            //        //GC.Collect();
            //        DateTime tmpTime3 = DateTime.Now;

            //        int tmpWidth = Math.Min(10000, width - j * 10000);
            //        int tmpHeight = Math.Min(10000, height - k * 10000);

            //        if (3 * tmpWidth * tmpHeight != rawData.Length)
            //        {
            //            rawData = null;
            //            GC.Collect();
            //            rawData = new byte[3 * tmpWidth * tmpHeight];
            //        }
            //        //Band bnd = ds.GetRasterBand(i);
            //        //bnd.ReadRaster(x + j * 10000, y + k * 10000, tmpWidth, tmpHeight, rawData, tmpWidth, tmpHeight, 0, 0);

            //        int[] tmp = new int[3];
            //        tmp[0] = 1;
            //        tmp[1] = 2;
            //        tmp[2] = 3;

            //        ds.ReadRaster(x + j * 10000, y + k * 10000, tmpWidth, tmpHeight, rawData, tmpWidth, tmpHeight, 3, tmp, 0, 0, 0);
            //        //将读取出来的数据写到输出的影像中                    
            //        dsout.WriteRaster(j * 10000, k * 10000, tmpWidth, tmpHeight, rawData, tmpWidth, tmpHeight, 3, tmp, 0, 0, 0);

            //        ds.FlushCache();
            //        dsout.FlushCache();

            //        TimeSpan tmpTime4 = tmpTime3.Subtract(DateTime.Now);
            //        sw.WriteLine(j + " " + k + " " + tmpTime4.TotalSeconds + " " + Gdal.GetCacheUsed());
            //        sw.Flush();

            //    }
            //}
            #endregion

            #region 波段统一读，按行分块
            //int cols = 10000 * 10000 / width;   //按整行读取，这里是一次读取的行数
            //int blocks = height / cols;         //读取的次数
            //if (height % cols != 0) blocks++;   //如果不能整除，还需要多读一次
            //int tmpHeight = cols;
            //byte[] rawData = new byte[bands.Length * width * cols];
            //for (int i = 0; i < blocks; i++)
            //{
            //    DateTime tmpTime3 = DateTime.Now;
            //    if (i == blocks - 1 && height % cols != 0)
            //    {
            //        rawData = null;
            //        GC.Collect();
            //        rawData = new byte[bands.Length * width * (height % cols)];
            //        tmpHeight = height % cols;
            //    }

            //    int[] sourceBands = selBands.ToArray(); 
            //    int [] desBands = new int[selBands.Count];
            //    for (int j = 0; j < selBands.Count; j++)
            //    {
            //        desBands[j] = j + 1;
            //    }

            //    ds.ReadRaster(x, y + i * cols, width, tmpHeight, rawData, width, tmpHeight, selBands.Count, sourceBands, 0, 0, 0);
            //    //将读取出来的数据写到输出的影像中                    
            //    dsout.WriteRaster(0, i * cols, width, tmpHeight, rawData, width, tmpHeight, selBands.Count, desBands, 0, 0, 0);

            //    ds.FlushCache();
            //    dsout.FlushCache();

            //    TimeSpan tmpTime4 = tmpTime3.Subtract(DateTime.Now);
            //    sw.WriteLine(i + " " + tmpTime4.TotalSeconds + " " + Gdal.GetCacheUsed());
            //    sw.Flush();
            //}
            #endregion

            #region 按少量行读，分小块,能够控制波段
            int cols = 10000 * 200 / width;   //按整行读取，这里是一次读取的行数
            int blocks = height / cols;         //读取的次数
            if (height % cols != 0) blocks++;   //如果不能整除，还需要多读一次
            int tmpHeight = cols;
            byte[] rawData = new byte[bands.Length * width * cols];
            for (int i = 0; i < blocks; i++)
            {
                DateTime tmpTime3 = DateTime.Now;
                if (i == blocks - 1 && height % cols != 0)
                {
                    rawData = null;
                    GC.Collect();
                    rawData = new byte[bands.Length * width * (height % cols)];
                    tmpHeight = height % cols;
                }

                ds.ReadRaster(x, y + i * cols, width, tmpHeight, rawData, width, tmpHeight, selBands.Count, sourceBands, 0, 0, 0);
                //将读取出来的数据写到输出的影像中                    
                dsout.WriteRaster(0, i * cols, width, tmpHeight, rawData, width, tmpHeight, selBands.Count, desBands, 0, 0, 0);

                ds.FlushCache();
                dsout.FlushCache();

                TimeSpan tmpTime4 = tmpTime3.Subtract(DateTime.Now);
                sw.WriteLine(i + " " + tmpTime4.TotalSeconds + " " + Gdal.GetCacheUsed());
                sw.Flush();
            }
            #endregion

            #region 能够分波段，逐波段读取Block大小的块
            //for (int i = 1; i <= int.Parse(this.cut_bandBox.Text); i++)
            //{
            //    if (!selBands.Contains(i))
            //        continue;

            //    sw.WriteLine("band:" + i);

            //    int blockx, blocky;
            //    ds.GetRasterBand(i).GetBlockSize(out blockx, out blocky);

            //    int tmpx = width / blockx;
            //    int tmpy = height / blocky;
            //    byte[] rawData = new byte[blockx * blocky];
            //    for (int j = 0; j < tmpx + 1; j++)
            //    {
            //        for (int k = 0; k < tmpy + 1; k++)
            //        {
            //            //DateTime tmpTime3 = DateTime.Now;

            //            int tmpWidth = Math.Min(blockx, width - j * blockx);
            //            int tmpHeight = Math.Min(blocky, height - k * blocky);

            //            if (tmpWidth * tmpHeight != rawData.Length)
            //                rawData = new byte[tmpWidth * tmpHeight];
            //            if (tmpWidth == 0 && tmpHeight == 0)
            //                continue;
            //            Band bnd = ds.GetRasterBand(i);
            //            bnd.ReadRaster(x + j * blockx, y + k * blocky, tmpWidth, tmpHeight, rawData, tmpWidth, tmpHeight, 0, 0);

            //            //将读取出来的数据写到输出的影像中
            //            int[] tmp = new int[1];
            //            tmp[0] = selBands.IndexOf(i) + 1;
            //            dsout.WriteRaster(j * blockx, k * blocky, tmpWidth, tmpHeight, rawData, tmpWidth, tmpHeight, 1, tmp, 0, 0, 0);
            //            ;


            //            ds.FlushCache();
            //            dsout.FlushCache();

            //            //TimeSpan tmpTime4 = tmpTime3.Subtract(DateTime.Now);
            //            //sw.WriteLine(j + " " + k + " " + tmpTime4.TotalSeconds + " " + Gdal.GetCacheUsed());
            //            //sw.Flush();


            //        }
            //    }
            //}
            #endregion


            //计算切割后影像的偏移参数
            //图像上(P,L)处点的实际坐标为
            //Xp = padfTransform[0] + P * padfTransform[1] + L * padfTransform[2];
            //Yp = padfTransform[3] + P * padfTransform[4] + L * padfTransform[5];
            argin[0] = argin[0] + x * argin[1] + y * argin[2];
            argin[3] = argin[3] + x * argin[4] + y * argin[5];

            //将计算得到的偏移信息写入到新影像中
            dsout.SetGeoTransform(argin);

            //赋予新影像坐标信息
            OSGeo.OSR.SpatialReference sr = new OSGeo.OSR.SpatialReference(cut_projBox.Text);
            string tmpProj;
            sr.ExportToPrettyWkt(out tmpProj, 0);
            dsout.SetProjection(tmpProj);
            //dsout.SetProjection(ds.GetProjectionRef());
            dsout.Dispose();

            TimeSpan tmpTime2 = tmpTime1.Subtract(DateTime.Now);
            MessageBox.Show("影像切割过程完成,耗时："+tmpTime2.TotalSeconds);

            sw.Close();
            this.Close();
        }
        #endregion

        #region 金字塔
        private void pyra_browseBtn_Click(object sender, EventArgs e)
        {
            OpenFileDialog mDlg = new OpenFileDialog();
            mDlg.Filter = "影像文件(*.img *.pix)|*.img;*.pix";
            if (mDlg.ShowDialog() != DialogResult.OK)
                return;

            this.pyra_pathBox.Text = mDlg.FileName;
        }


        private void pyra_cancelBtn_Click(object sender, EventArgs e)
        {
            this.Close();
        }


        private void pyra_okBtn_Click(object sender, EventArgs e)
        {
            if (pyra_pathBox.Text == "")
            {
                MessageBox.Show("请选择要生成金字塔的影像文件");
                return;
            }

            if (File.Exists(pyra_pathBox.Text.Substring(0, pyra_pathBox.Text.Length - 3) + "rrd"))
            {
                MessageBox.Show("已经存在该影像文件对应的金字塔");
                return;
            }


            
            Gdal.AllRegister();
            //注册并声明环境变量
            Gdal.SetConfigOption("GDAL_FILENAME_IS_UTF8", "YES");
            Gdal.SetConfigOption("GDAL_DATA", Application.StartupPath + "\\gdaldata");
            //声明对IMG文件,生成RRD格式的外部金字塔
            //(IMG格式还支持在.img文件内部生成金字塔,要使用这种生成方式的话,注释掉下边这一边即可)
            Gdal.SetConfigOption("HFA_USE_RRD", "YES");
            Gdal.SetConfigOption("USE_RRD", "YES");
            
            

            //打开影像文件
            Dataset ds = null;
            try
            {
                ds = Gdal.Open(pyra_pathBox.Text, Access.GA_Update);
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开影像文件失败,详细信息:" + ex.Message);
                return;
            }

            //计算要生成金字塔的级数,以及各级数对应的值
            int iWidth = ds.RasterXSize;
            int iHeigh = ds.RasterYSize;
            int iPixelNum = iWidth * iHeigh; //原图像中的总像元个数 
            int iTopNum = 4096; //顶层金字塔大小，64*64 
            int iCurNum = iPixelNum / 4;    //当前生成到金字塔的像元数
            int[] anLevels = new int[100];     //各级金字塔对应的值
            int nLevelCount = 0; //金字塔级数 

            //计算需要的级数
            do
            {
                anLevels[nLevelCount] = (int)Math.Pow(2, nLevelCount + 2);
                nLevelCount++;
                iCurNum /= 4;
            } while (iCurNum > iTopNum);

            Array.Resize(ref anLevels, nLevelCount);
            try
            {
                ds.BuildOverviews("nearest", anLevels);
            }
            catch (Exception ex)
            {
                MessageBox.Show("生成金字塔失败,详细信息:" + ex.Message);
                return;
            }
            MessageBox.Show("金字塔生成成功");
            this.Close();
        }
        
        
        #endregion

        private void button1_Click(object sender, EventArgs e)
        {

            //注册并声明环境变量
            Gdal.SetConfigOption("GDAL_FILENAME_IS_UTF8", "YES");
            Gdal.SetConfigOption("GDAL_DATA", Application.StartupPath + "\\gdaldata");
            Gdal.AllRegister();
            int ddfaf = Gdal.GetCacheMax();
            int wfe = 1;

            Driver d = null;
            if (this.cut_savePathBox.Text.EndsWith(".img") ||
                this.cut_savePathBox.Text.EndsWith(".IMG"))
                d = Gdal.GetDriverByName("HFA");
            else if (this.cut_savePathBox.Text.EndsWith(".pix") ||
                this.cut_savePathBox.Text.EndsWith(".PIX"))
                d = Gdal.GetDriverByName("PCIDSK");
            else
            {
                MessageBox.Show("非法的影像格式");
                return;
            }

            //long men = GetPhisicalMemory();
            //Gdal.SetCacheMax((int)(men/3));
            Gdal.SetCacheMax(300 * 1024 * 1024);

            DateTime tmpTime1 = DateTime.Now;
            d.CreateCopy(cut_savePathBox.Text, ds, 1, null, null, null);
            TimeSpan tmpTime2 = tmpTime1.Subtract(DateTime.Now);
            MessageBox.Show("耗时：" + tmpTime2.TotalSeconds + "s");
        }


        private long GetPhisicalMemory()
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher();   //用于查询一些如系统信息的管理对象 
            searcher.Query = new SelectQuery("Win32_PhysicalMemory ", "", new string[] { "Capacity" });//设置查询条件 
            ManagementObjectCollection collection = searcher.Get();   //获取内存容量 
            ManagementObjectCollection.ManagementObjectEnumerator em = collection.GetEnumerator();

            long capacity = 0;
            while (em.MoveNext())
            {
                ManagementBaseObject baseObj = em.Current;
                if (baseObj.Properties["Capacity"].Value != null)
                {
                    try
                    {
                        capacity += long.Parse(baseObj.Properties["Capacity"].Value.ToString());
                    }
                    catch
                    {
                        return 0;
                    }
                }
            }
            return capacity ;
        } 
    }
}