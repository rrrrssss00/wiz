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

        #region Ӱ���и�
        private bool cut_loadInfo = false;

        private void cut_browserBtn_Click(object sender, EventArgs e)
        {
            OpenFileDialog mDlg = new OpenFileDialog();
            //mDlg.Filter = "Ӱ���ļ�(*.img)|*.img";
            mDlg.Filter = "Ӱ���ļ�(*.img *.pix *.tif)|*.img;*.pix;*.tif";
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
                MessageBox.Show("��ѡ��Ҫ�����Ӱ��");
                return;
            }

            
            //ע�Ტ������������
            Gdal.SetConfigOption("GDAL_FILENAME_IS_UTF8", "NO");            
            Gdal.SetConfigOption("GDAL_DATA", Application.StartupPath + "\\gdaldata");
            //Gdal.SetConfigOption("GDAL_FORCE_CACHING", "YES");
            Gdal.AllRegister();

            try
            {
                //�ȴ�Ӱ���ļ�
                ds = Gdal.Open(cut_pathBox.Text, Access.GA_ReadOnly);
            }
            catch (Exception ex)
            {
                MessageBox.Show("��Ӱ��ʧ��,��ϸ��Ϣ��" + ex.Message);
                return;
            }

            //Ӱ�񳤿�
            this.cut_rasterWidthBox.Text = ds.RasterXSize.ToString();
            this.cut_rasterHeightBox.Text = ds.RasterYSize.ToString();
            //Ӱ�񲨶���
            this.cut_bandBox.Text = ds.RasterCount.ToString();
            //Ӱ��任����
            double[] tmpD = new double[6];
            ds.GetGeoTransform(tmpD);
            string geoTrans = tmpD[0] + "," + tmpD[1] + "," + tmpD[2] + "," + tmpD[3] + "," + tmpD[4] + "," + tmpD[5];
            this.cut_geoTransBox.Text = geoTrans;
            //Ӱ������ϵ
            this.cut_projBox.Text = ds.GetProjection();

            if (ds.GetRasterBand(1).DataType != DataType.GDT_Byte)
            {
                MessageBox.Show("Ӱ��ֵ���ͷ�BTYE����,��֧�ֶԸ�Ӱ����д���");
                return;
            }            
            cut_loadInfo = true;

            int blockx, blocky;
            ds.GetRasterBand(1).GetBlockSize(out blockx, out blocky);

        }

        private void cut_savePathBtn_Click(object sender, EventArgs e)
        {
            SaveFileDialog mDlg = new SaveFileDialog();
            mDlg.Filter = "Ӱ���ļ�(*.img *.pix,*.tif)|*.img;*.pix;*.tif";
            //mDlg.Filter = "Ӱ���ļ�(*.img)|*.img";
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
            #region ���ݼ���
            if (!cut_loadInfo)
            {
                MessageBox.Show("���ȶ�ȡӰ����Ϣ");
                return;
            }

            int x = -1;
            if (cut_xBox.Text.Trim() == "" ||
                !int.TryParse(cut_xBox.Text.Trim(), out x))
            {
                MessageBox.Show("���Ͻǵ�X��ʽ����ȷ");
                return;
            }
            if (x < 0 || x >= int.Parse(cut_rasterWidthBox.Text))
            {
                MessageBox.Show("���Ͻǵ�XԽ��");
                return;
            }

            int y = -1;
            if (cut_yBox.Text.Trim() == "" ||
                !int.TryParse(cut_yBox.Text.Trim(), out y))
            {
                MessageBox.Show("���Ͻǵ�Y��ʽ����ȷ");
                return;
            }
            if (y < 0 || y >= int.Parse(cut_rasterHeightBox.Text))
            {
                MessageBox.Show("���Ͻǵ�YԽ��");
                return;
            }

            int width = -1;
            if (cut_cutWidthBox.Text.Trim() == "" ||
                !int.TryParse(cut_cutWidthBox.Text, out width))
            {
                MessageBox.Show("�и��ȸ�ʽ����ȷ");
                return;
            }
            if (width < 0 || width + x > int.Parse(cut_rasterWidthBox.Text))
            {
                MessageBox.Show("�и���Խ��");
                return;
            }

            int height = -1;
            if (cut_cutHeightBox.Text.Trim() == "" ||
                !int.TryParse(cut_cutHeightBox.Text, out height))
            {
                MessageBox.Show("�и�߶ȸ�ʽ����ȷ");
                return;
            }
            if (height < 0 || height + y > int.Parse(cut_rasterHeightBox.Text))
            {
                MessageBox.Show("�и�߶�Խ��");
                return;
            }

            if (cut_bandSelBox.Text == "")
            {
                MessageBox.Show("ѡ�񲨶β���Ϊ��");
                return;
            }

            if (cut_savePathBox.Text == "")
            {
                MessageBox.Show("��ѡ�񱣴��ļ�λ��");
                return;
            }
            if (File.Exists(cut_savePathBox.Text))
            {
                MessageBox.Show("\"" + cut_savePathBox.Text + "\"�Ѿ�����");
                return;
            }

            #endregion

            //��ȡ����
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
                    MessageBox.Show("ԴӰ��ĵ�" + selBands[i]+"�����β���BYTE���ͣ��޷�����");
                    return;
                }
            }

            //��ȡ����ƫ�Ʋ���
            double[] argin = new double[6];
            ds.GetGeoTransform(argin);

           
            //��ȡӰ������,����Ӱ��
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
                MessageBox.Show("�Ƿ���Ӱ���ʽ");
                return;
            }
            Gdal.SetCacheMax(300*1024*1024);

            DateTime tmpTime1 = DateTime.Now;

            Dataset dsout;
            if (this.cut_savePathBox.Text.ToUpper().EndsWith(".IMG") ||
                this.cut_savePathBox.Text.ToUpper().EndsWith(".TIF"))
            {
                //��ȡӰ���ͳ��ֵ�������СֵС��0����ô�������Ӱ����SignedByte
                //double max, min, mean, stddev;
                //ds.GetRasterBand(1).GetStatistics(1, 1, out min, out max, out mean, out stddev);
                
                //�ж��Ƿ���SIGNEDBTYE�͵Ĳ���
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

            //��������Ĳ���,����Ӱ���и�
            //int currentOutBand = 1;     //��ǰ������Ĳ���
            StreamWriter sw = new StreamWriter("log.txt", false);

            #region �ܹ��ֲ��Σ��𲨶ζ�ȡ10000*10000�Ŀ�
            //for (int i = 1; i <= int.Parse(this.cut_bandBox.Text); i++)
            //{
            //    if (!selBands.Contains(i))
            //        continue;

            //    sw.WriteLine("band:" + i);
                
            //    if (width*height <= 10000*10000)
            //    {
            //        #region ����ȡ��Ӱ��Χ�Ƚ�Сʱ

            //        //��ָ������,�����ò�����ָ����Χ�����ݶ�ȡ��Byte������
            //        byte[] rawData = new byte[width * height];
            //        Band bnd = ds.GetRasterBand(i);
            //        bnd.ReadRaster(x, y, width, height, rawData, width, height, 0, 0);

            //        //����ȡ����������д�������Ӱ����
            //        int[] tmp = new int[1];
            //        tmp[0] = selBands.IndexOf(i) + 1;
            //        dsout.WriteRaster(0, 0, width, height, rawData, width, height, 1, tmp, 0, 0, 0);

            //        #endregion
            //    }
            //    else
            //    {
                   
            //        #region ����ȡ��Ӱ��Χ�Ƚϴ�ʱ,�ֿ��Ӱ����ж�ȡ��д��
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

            //                //����ȡ����������д�������Ӱ����
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

            #region ����ͳһ������10000*10000�Ŀ飬���ַ�������һ���Կ�
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
            //        //����ȡ����������д�������Ӱ����                    
            //        dsout.WriteRaster(j * 10000, k * 10000, tmpWidth, tmpHeight, rawData, tmpWidth, tmpHeight, 3, tmp, 0, 0, 0);

            //        ds.FlushCache();
            //        dsout.FlushCache();

            //        TimeSpan tmpTime4 = tmpTime3.Subtract(DateTime.Now);
            //        sw.WriteLine(j + " " + k + " " + tmpTime4.TotalSeconds + " " + Gdal.GetCacheUsed());
            //        sw.Flush();

            //    }
            //}
            #endregion

            #region ����ͳһ�������зֿ�
            //int cols = 10000 * 10000 / width;   //�����ж�ȡ��������һ�ζ�ȡ������
            //int blocks = height / cols;         //��ȡ�Ĵ���
            //if (height % cols != 0) blocks++;   //�����������������Ҫ���һ��
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
            //    //����ȡ����������д�������Ӱ����                    
            //    dsout.WriteRaster(0, i * cols, width, tmpHeight, rawData, width, tmpHeight, selBands.Count, desBands, 0, 0, 0);

            //    ds.FlushCache();
            //    dsout.FlushCache();

            //    TimeSpan tmpTime4 = tmpTime3.Subtract(DateTime.Now);
            //    sw.WriteLine(i + " " + tmpTime4.TotalSeconds + " " + Gdal.GetCacheUsed());
            //    sw.Flush();
            //}
            #endregion

            #region �������ж�����С��,�ܹ����Ʋ���
            int cols = 10000 * 200 / width;   //�����ж�ȡ��������һ�ζ�ȡ������
            int blocks = height / cols;         //��ȡ�Ĵ���
            if (height % cols != 0) blocks++;   //�����������������Ҫ���һ��
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
                //����ȡ����������д�������Ӱ����                    
                dsout.WriteRaster(0, i * cols, width, tmpHeight, rawData, width, tmpHeight, selBands.Count, desBands, 0, 0, 0);

                ds.FlushCache();
                dsout.FlushCache();

                TimeSpan tmpTime4 = tmpTime3.Subtract(DateTime.Now);
                sw.WriteLine(i + " " + tmpTime4.TotalSeconds + " " + Gdal.GetCacheUsed());
                sw.Flush();
            }
            #endregion

            #region �ܹ��ֲ��Σ��𲨶ζ�ȡBlock��С�Ŀ�
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

            //            //����ȡ����������д�������Ӱ����
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


            //�����и��Ӱ���ƫ�Ʋ���
            //ͼ����(P,L)�����ʵ������Ϊ
            //Xp = padfTransform[0] + P * padfTransform[1] + L * padfTransform[2];
            //Yp = padfTransform[3] + P * padfTransform[4] + L * padfTransform[5];
            argin[0] = argin[0] + x * argin[1] + y * argin[2];
            argin[3] = argin[3] + x * argin[4] + y * argin[5];

            //������õ���ƫ����Ϣд�뵽��Ӱ����
            dsout.SetGeoTransform(argin);

            //������Ӱ��������Ϣ
            OSGeo.OSR.SpatialReference sr = new OSGeo.OSR.SpatialReference(cut_projBox.Text);
            string tmpProj;
            sr.ExportToPrettyWkt(out tmpProj, 0);
            dsout.SetProjection(tmpProj);
            //dsout.SetProjection(ds.GetProjectionRef());
            dsout.Dispose();

            TimeSpan tmpTime2 = tmpTime1.Subtract(DateTime.Now);
            MessageBox.Show("Ӱ���и�������,��ʱ��"+tmpTime2.TotalSeconds);

            sw.Close();
            this.Close();
        }
        #endregion

        #region ������
        private void pyra_browseBtn_Click(object sender, EventArgs e)
        {
            OpenFileDialog mDlg = new OpenFileDialog();
            mDlg.Filter = "Ӱ���ļ�(*.img *.pix)|*.img;*.pix";
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
                MessageBox.Show("��ѡ��Ҫ���ɽ�������Ӱ���ļ�");
                return;
            }

            if (File.Exists(pyra_pathBox.Text.Substring(0, pyra_pathBox.Text.Length - 3) + "rrd"))
            {
                MessageBox.Show("�Ѿ����ڸ�Ӱ���ļ���Ӧ�Ľ�����");
                return;
            }


            
            Gdal.AllRegister();
            //ע�Ტ������������
            Gdal.SetConfigOption("GDAL_FILENAME_IS_UTF8", "YES");
            Gdal.SetConfigOption("GDAL_DATA", Application.StartupPath + "\\gdaldata");
            //������IMG�ļ�,����RRD��ʽ���ⲿ������
            //(IMG��ʽ��֧����.img�ļ��ڲ����ɽ�����,Ҫʹ���������ɷ�ʽ�Ļ�,ע�͵��±���һ�߼���)
            Gdal.SetConfigOption("HFA_USE_RRD", "YES");
            Gdal.SetConfigOption("USE_RRD", "YES");
            
            

            //��Ӱ���ļ�
            Dataset ds = null;
            try
            {
                ds = Gdal.Open(pyra_pathBox.Text, Access.GA_Update);
            }
            catch (Exception ex)
            {
                MessageBox.Show("��Ӱ���ļ�ʧ��,��ϸ��Ϣ:" + ex.Message);
                return;
            }

            //����Ҫ���ɽ������ļ���,�Լ���������Ӧ��ֵ
            int iWidth = ds.RasterXSize;
            int iHeigh = ds.RasterYSize;
            int iPixelNum = iWidth * iHeigh; //ԭͼ���е�����Ԫ���� 
            int iTopNum = 4096; //�����������С��64*64 
            int iCurNum = iPixelNum / 4;    //��ǰ���ɵ�����������Ԫ��
            int[] anLevels = new int[100];     //������������Ӧ��ֵ
            int nLevelCount = 0; //���������� 

            //������Ҫ�ļ���
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
                MessageBox.Show("���ɽ�����ʧ��,��ϸ��Ϣ:" + ex.Message);
                return;
            }
            MessageBox.Show("���������ɳɹ�");
            this.Close();
        }
        
        
        #endregion

        private void button1_Click(object sender, EventArgs e)
        {

            //ע�Ტ������������
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
                MessageBox.Show("�Ƿ���Ӱ���ʽ");
                return;
            }

            //long men = GetPhisicalMemory();
            //Gdal.SetCacheMax((int)(men/3));
            Gdal.SetCacheMax(300 * 1024 * 1024);

            DateTime tmpTime1 = DateTime.Now;
            d.CreateCopy(cut_savePathBox.Text, ds, 1, null, null, null);
            TimeSpan tmpTime2 = tmpTime1.Subtract(DateTime.Now);
            MessageBox.Show("��ʱ��" + tmpTime2.TotalSeconds + "s");
        }


        private long GetPhisicalMemory()
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher();   //���ڲ�ѯһЩ��ϵͳ��Ϣ�Ĺ������ 
            searcher.Query = new SelectQuery("Win32_PhysicalMemory ", "", new string[] { "Capacity" });//���ò�ѯ���� 
            ManagementObjectCollection collection = searcher.Get();   //��ȡ�ڴ����� 
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