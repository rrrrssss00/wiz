using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms; 
using System.IO;
using OSGeo.GDAL;
using OSGeo.OGR;

namespace TestEdgeExtract
{
    //todo:Ŀǰֻ֧��8λByte���͵�Ӱ��,����ֻ֧��һ���պ������Ӱ��
    //todo:Ŀǰ�ж��������кܶ����ĵ�,����Ҫ�����ܷ��һ����С�������
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
           
        }

        private void browseBtn_Click(object sender, EventArgs e)
        {
            OpenFileDialog mdlg = new OpenFileDialog();
            mdlg.Filter = "Ӱ���ļ�(*.img *.pix *.tif *.tiff)|*.img;*.pix;*.tif;*.tiff";
            if (mdlg.ShowDialog() != DialogResult.OK)
                return;

            this.pathBox.Text = mdlg.FileName; 
        }

        private void exitBtn_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private int[,] Direction ={ { -1, 1 }, { 0, 1 }, { 1, 1 }, { 1, 0 }, { 1, -1 }, { 0, -1 }, { -1, -1 }, { -1, 0 } };
        private int rasterX, rasterY, bandCount;
        private Band band = null;
        private double[] geoTrans = new double[6];


        private void startBtn_Click(object sender, EventArgs e)
        {
            #region һЩ��鹤�� 
            if (pathBox.Text == "")
            {
                MessageBox.Show("��ѡ�����ȡ��Ӱ���ļ�");
                return;
            }

            double bg = -1;
            if (bgBox.Text == "" || !double.TryParse(bgBox.Text.Trim(),out bg)) 
            {
                MessageBox.Show("����ɫ,����������");
                return;
            }

            string tmpext = Path.GetExtension(pathBox.Text);
            string destShpFile = pathBox.Text.Replace(tmpext,".shp");
            if(File.Exists(destShpFile))
            {
                MessageBox.Show(destShpFile + "�ļ��Ѿ�����");
                return;
            } 
            #endregion

            logBox.Text = "";

            #region ��ԴӰ��
            Gdal.AllRegister();
            OSGeo.OGR.Ogr.RegisterAll();
            Gdal.SetConfigOption("GDAL_FILENAME_IS_UTF8", "YES");
            //Gdal.SetConfigOption("GDAL_DATA", Application.StartupPath + "\\gdaldata");
            
            //��ԴӰ��
            Dataset dsin = null;
            try
            {
                dsin = Gdal.Open(pathBox.Text, Access.GA_ReadOnly);
                if (dsin == null)
                    throw new Exception();
            }
            catch (Exception ex)
            {
                try
                {
                    dsin = Gdal.Open(pathBox.Text, Access.GA_Update);
                    if (dsin == null)
                    {
                        throw new Exception();
                    }
                }
                catch (Exception ex2)
                {
                    logBox.Text += "��Ӱ��ʧ��,·���벻Ҫ������������������ַ�\n";
                    return;
                } 
            }

            rasterX = dsin.RasterXSize;	//Ӱ����
            rasterY = dsin.RasterYSize;	//Ӱ��߶�
            bandCount = dsin.RasterCount;	//������
            
            dsin.GetGeoTransform(geoTrans);		//Ӱ������任����
            string proj = dsin.GetProjection();		//Ӱ������ϵ��Ϣ��WKT��ʽ�ַ�����
            OSGeo.OSR.SpatialReference projRef = new OSGeo.OSR.SpatialReference(proj);
            #endregion

            int BeginDirect;

            //�ҵ��ı߽��
            List<int> borderXLst = new List<int>();
            List<int> borderYLst = new List<int>();

            band = dsin.GetRasterBand(1);      //ֻ��һ�����ν��д���
            if(band.DataType != DataType.GDT_Byte)
            {
                MessageBox.Show("�ݲ�֧�ָ�ʽΪ8λByte֮�������");
                return;
            }
            

            try
            {
                int leftTopX = 0, leftTopY = 0;
                #region ���ȶ�ȡ�����Ϸ��ı߽�� 
                byte[] raw = null;

                //Ҫ��ȡ��Ӱ���С
                int toReadx = rasterX;
                int toReady = Math.Min(rasterY, 20);

                int tmpReaded = 0;      //��¼�Ѿ�������������

                raw = new byte[toReadx * toReady];
                bool ifFind = false;
                //ѭ����ȡ,ֱ����ȡ�����Ͻǵ�Ϊֹ
                while (!ifFind)
                {
                    //if (tmpReaded + toReady > rasterY) break;
                    //�ȶ�20��
                    band.ReadRaster(0, tmpReaded, toReadx, toReady, raw, toReadx, toReady, 0, 0);
                    //�����Ͻǵ�
                    for (int i = 0; i < toReady; i++)
                    {
                        for (int j = 0; j < toReadx; j++)
                        {
                            if (raw[i * toReadx + j] != bg)
                            {
                                ifFind = true;
                                //borderXLst.Add(j);
                                //borderYLst.Add(i);
                                leftTopX = j;
                                leftTopY = i+tmpReaded;
                                break;
                            }
                        }
                        if (ifFind) break; 
                    }

                    //�ҵ��˾�����
                    if (ifFind)
                        break;
                    else
                    {
                        //û�ҵ�,���ٶ�ȡ20��
                        tmpReaded += toReady;
                        if (tmpReaded == rasterY) break;//�Ѿ�ȫ����ȡ����,����û���ҵ�,˵��ͼ��ȫ�Ǳ���ֵ,����
                        toReady = Math.Min(20, rasterY - tmpReaded);  //û��ȫ����ȡ��,���ٶ�20��,���ʣ�²���20����,�Ͷ�ʣ�µ��� 
                    }
                }

                //���û���ҵ�,˵��ͼ��ȫ�Ǳ���ֵ,ֱ�ӱ�����������
                if (!ifFind)
                {
                    logBox.Text += "Ӱ��ȫ��Ϊ����ֵ,�޷���ȡ�߽�\n";
                    dsin.Dispose();
                    dsin = null;
                    return;
                }
                #endregion

                List<List<double>> pntsXLstLst = new List<List<double>>();
                List<List<double>> pntsYLstLst = new List<List<double>>();
                #region �����߽磬�õ��߽�ĵ㼯������ϡ���õ�����������Shp�ļ��ĵ㼯
                List<double> tmpPntsx, tmpPntsy;
                List<int> tmpBorderXLst, tmpBorderYLst;
                FindPnt(leftTopX, leftTopY, 3, bg, out tmpPntsx, out tmpPntsy, out tmpBorderXLst, out tmpBorderYLst);
                if (tmpPntsx != null && tmpPntsx.Count != 0 && tmpPntsy != null && tmpPntsy.Count != 0)
                {
                    pntsXLstLst.Add(tmpPntsx);
                    pntsYLstLst.Add(tmpPntsy);
                    borderXLst.AddRange(tmpBorderXLst);
                    borderYLst.AddRange(tmpBorderYLst);
                }
                #endregion

                #region �ٳ��Դ�����������ұ߽�㣬�������߽�
                #region ���Ϸ�                
                int rightTopX = 0, rightTopY = 0;
                toReadx = Math.Min(rasterX, 20);
                toReady = rasterY;
                tmpReaded = 0;      //��¼�Ѿ�������������
                raw = new byte[toReadx * toReady];
                ifFind = false;

                #region ѭ����ȡ,ֱ����ȡ�����Ͻǵ�Ϊֹ

                while (!ifFind)
                {
                    //if (tmpReaded + toReadx > rasterX) break;
                    //�ȶ�20��
                    band.ReadRaster(rasterX - tmpReaded - toReadx, 0, toReadx, toReady, raw, toReadx, toReady, 0, 0);
                    //�����Ͻǵ�
                    for (int i = toReadx -1; i >=0; i--)
                    {
                        for (int j = 0; j < toReady; j++)
                        {
                            if (raw[j * toReadx + i] != bg)
                            {
                                ifFind = true;
                                //borderXLst.Add(j);
                                //borderYLst.Add(i);
                                rightTopX = rasterX - tmpReaded -toReadx + i;
                                rightTopY = j;
                                break;
                            }
                        }
                        if (ifFind) break;
                    }

                    //�ҵ��˾�����
                    if (ifFind)
                        break;
                    else
                    {
                        //û�ҵ�,���ٶ�ȡ20��
                        tmpReaded += toReadx;
                        if (tmpReaded == rasterX) break;//�Ѿ�ȫ����ȡ����,����û���ҵ�,˵��ͼ��ȫ�Ǳ���ֵ,����
                        toReadx = Math.Min(20, rasterX - tmpReaded);  //û��ȫ����ȡ��,���ٶ�20��,���ʣ�²���20����,�Ͷ�ʣ�µ���
                    }
                }
                #endregion

                if (ifFind)
                {
                    #region ����һ�¸õ��Ƿ����Ѿ��������ı߽��ϣ�����ڣ��Ͳ�Ҫ��������                    
                    bool pntInLst = false;
                    for (int i = 0; i < borderXLst.Count; i++)
                    {
                        if (borderXLst[i] == rightTopX && borderYLst[i] == rightTopY)
                        { pntInLst = true; break; }
                    }
                    #endregion

                    #region �����߽粢��ϡ                    
                    if (!pntInLst)
                    {
                        FindPnt(rightTopX, rightTopY, 1, bg, out tmpPntsx, out tmpPntsy, out tmpBorderXLst, out tmpBorderYLst);
                        if (tmpPntsx != null && tmpPntsx.Count != 0 && tmpPntsy != null && tmpPntsy.Count != 0)
                        {
                            pntsXLstLst.Add(tmpPntsx);
                            pntsYLstLst.Add(tmpPntsy);
                            borderXLst.AddRange(tmpBorderXLst);
                            borderYLst.AddRange(tmpBorderYLst);
                        }
                    }
                    #endregion
                }
                #endregion

                #region ���·�
                int rightBottomX = 0, rightBottomY = 0;
                toReadx = rasterX;
                toReady = Math.Min(rasterY, 20);
                tmpReaded = 0;      //��¼�Ѿ�������������
                raw = new byte[toReadx * toReady];
                ifFind = false;

                #region ѭ����ȡ,ֱ����ȡ�����½ǵ�Ϊֹ
                while (!ifFind)
                {
                    //if (tmpReaded + toReady > rasterY) break;

                    //�ȶ�20��
                    band.ReadRaster(0, rasterY - toReady - tmpReaded, toReadx, toReady, raw, toReadx, toReady, 0, 0);
                    //�����Ͻǵ�
                    for (int i = toReady - 1; i >= 0; i--)
                    {
                        for (int j = toReadx - 1; j >=0 ; j--)
                        {
                            if (raw[i * toReadx + j] != bg)
                            {
                                ifFind = true;
                                //borderXLst.Add(j);
                                //borderYLst.Add(i);
                                rightBottomX = j;
                                rightBottomY = rasterY - tmpReaded - toReady + i;
                                break;
                            }
                        }
                        if (ifFind) break;
                    }

                    //�ҵ��˾�����
                    if (ifFind)
                        break;
                    else
                    {
                        //û�ҵ�,���ٶ�ȡ20��
                        tmpReaded += toReady;
                        if (tmpReaded == rasterY) break;//�Ѿ�ȫ����ȡ����,����û���ҵ�,˵��ͼ��ȫ�Ǳ���ֵ,����
                        toReady = Math.Min(20, rasterY - tmpReaded);  //û��ȫ����ȡ��,���ٶ�20��,���ʣ�²���20����,�Ͷ�ʣ�µ��� 
                    }
                }
                #endregion

                if (ifFind)
                {
                    #region ����һ�¸õ��Ƿ����Ѿ��������ı߽��ϣ�����ڣ��Ͳ�Ҫ��������
                    bool pntInLst = false;
                    for (int i = 0; i < borderXLst.Count; i++)
                    {
                        if (borderXLst[i] == rightBottomX && borderYLst[i] == rightBottomY)
                        { pntInLst = true; break; }
                    }
                    #endregion

                    #region �����߽粢��ϡ
                    if (!pntInLst)
                    {
                        FindPnt(rightBottomX, rightBottomY, 7, bg, out tmpPntsx, out tmpPntsy, out tmpBorderXLst, out tmpBorderYLst);
                        if (tmpPntsx != null && tmpPntsx.Count != 0 && tmpPntsy != null && tmpPntsy.Count != 0)
                        {
                            pntsXLstLst.Add(tmpPntsx);
                            pntsYLstLst.Add(tmpPntsy);
                            borderXLst.AddRange(tmpBorderXLst);
                            borderYLst.AddRange(tmpBorderYLst);
                        }
                    }
                    #endregion
                }

                #endregion

                #region ���·�
                int leftBottomX = 0, leftBottomY = 0;
                toReadx = Math.Min(rasterX, 20);
                toReady = rasterY;
                tmpReaded = 0;      //��¼�Ѿ�������������
                raw = new byte[toReadx * toReady];
                ifFind = false;

                #region ѭ����ȡ,ֱ����ȡ�����½ǵ�Ϊֹ

                while (!ifFind)
                {
                    //if (tmpReaded + toReadx > rasterX) break;
                    //�ȶ�20��
                    band.ReadRaster(tmpReaded, 0, toReadx, toReady, raw, toReadx, toReady, 0, 0);
                    //�����½ǵ�
                    for (int i = 0; i < toReadx; i++)
                    {
                        for (int j = toReady - 1; j >=0; j--)
                        {
                            if (raw[j * toReadx + i] != bg)
                            {
                                ifFind = true;
                                //borderXLst.Add(j);
                                //borderYLst.Add(i);
                                leftBottomX = tmpReaded + i;
                                leftBottomY = j;
                                break;
                            }
                        }
                        if (ifFind) break;
                    }

                    //�ҵ��˾�����
                    if (ifFind)
                        break;
                    else
                    {
                        //û�ҵ�,���ٶ�ȡ20��
                        tmpReaded += toReadx;
                        if (tmpReaded == rasterX) break;//�Ѿ�ȫ����ȡ����,����û���ҵ�,˵��ͼ��ȫ�Ǳ���ֵ,����
                        toReadx = Math.Min(20, rasterX - tmpReaded);  //û��ȫ����ȡ��,���ٶ�20��,���ʣ�²���20����,�Ͷ�ʣ�µ���
                    }
                }
                #endregion

                if (ifFind)
                {
                    #region ����һ�¸õ��Ƿ����Ѿ��������ı߽��ϣ�����ڣ��Ͳ�Ҫ��������
                    bool pntInLst = false;
                    for (int i = 0; i < borderXLst.Count; i++)
                    {
                        if (borderXLst[i] == leftBottomX && borderYLst[i] == leftBottomY)
                        { pntInLst = true; break; }
                    }
                    #endregion

                    #region �����߽粢��ϡ
                    if (!pntInLst)
                    {
                        FindPnt(leftBottomX, leftBottomY, 5, bg, out tmpPntsx, out tmpPntsy, out tmpBorderXLst, out tmpBorderYLst);
                        if (tmpPntsx != null && tmpPntsx.Count != 0 && tmpPntsy != null && tmpPntsy.Count != 0)
                        {
                            pntsXLstLst.Add(tmpPntsx);
                            pntsYLstLst.Add(tmpPntsy);
                            borderXLst.AddRange(tmpBorderXLst);
                            borderYLst.AddRange(tmpBorderYLst);
                        }
                    }
                    #endregion
                }
                #endregion

                #endregion

                #region ����Shp�ļ�
                OSGeo.OGR.Driver poDriver = OSGeo.OGR.Ogr.GetDriverByName("ESRI Shapefile");
                if (poDriver == null)
                    throw new Exception("����ʧ��:����Shp�ļ�����");

                //�ô�Driver����Shape�ļ�
                OSGeo.OGR.DataSource poDS;
                poDS = poDriver.CreateDataSource(destShpFile, null);
                if (poDS == null)
                    throw new Exception("����ʧ��:����Shp�ļ�����");

                OSGeo.OGR.Layer poLayer;
                poLayer = poDS.CreateLayer("lineLry", projRef, OSGeo.OGR.wkbGeometryType.wkbPolygon, null);
                if (poLayer == null)
                    throw new Exception("����ʧ��:����Shp�ļ�����");

                OSGeo.OGR.Feature poFeature = new Feature(poLayer.GetLayerDefn());
                for (int j = 0; j < pntsXLstLst.Count; j++)
                {
                    OSGeo.OGR.Geometry polygon = new Geometry(OSGeo.OGR.wkbGeometryType.wkbPolygon);
                    OSGeo.OGR.Geometry outring = new Geometry(OSGeo.OGR.wkbGeometryType.wkbLinearRing);
                    for (int i = 0; i < pntsXLstLst[j].Count; i++)
                    {
                        outring.AddPoint_2D(pntsXLstLst[j][i], pntsYLstLst[j][i]);
                    }
                    outring.CloseRings();

                    polygon.AddGeometry(outring);
                    poFeature.SetGeometryDirectly(polygon);
                    int res = poLayer.CreateFeature(poFeature);
                }
                
                poDS.SyncToDisk();
                poDS.Dispose();
                #endregion
            }
            catch (Exception ex)
            {
                logBox.Text += "����ʧ��:" + ex.Message + "\n";
                dsin.Dispose();
                dsin = null;
                return;
            }

            MessageBox.Show("��ȡ���");
        }

        /// <summary>
        /// �����߽磬�õ��߽�ĵ㼯������ϡ���õ�����������Shp�ļ��ĵ㼯
        /// </summary>
        /// <param name="startx">��������X��������</param>
        /// <param name="starty">��������Y��������</param>
        /// <param name="startDirection">������ʼ����</param>
        /// <param name="bg">�û�ָ���ı�Ӱֵ</param>
        /// <param name="GeoPntsX">������ϣ�����ϡ��ĵ㼯�ռ�X����</param>
        /// <param name="GeoPntsY">������ϣ�����ϡ��ĵ㼯�ռ�Y����</param>
        /// <param name="borderXLst">������ϣ���ϡǰ�ĵ㼯����X����</param>
        /// <param name="borderYLst">������ϣ���ϡǰ�ĵ㼯����Y����</param>
        private void FindPnt(int startx, int starty, int startDirection, double bg, out List<double> GeoPntsX, out List<double> GeoPntsY, out List<int> borderXLst, out List<int> borderYLst)
        {
            GeoPntsX = new List<double>();
            GeoPntsY = new List<double>();

            #region ��ʼ�����߽�,�������ı߽籣����borderXLst&borderYLst��
            borderXLst = new List<int>();
            borderYLst = new List<int>();
            borderXLst.Add(startx);
            borderYLst.Add(starty);

            #region һЩ��������
            int currentDirection = startDirection;   //�ʼ��ʱ�������������Ϸ�Ϊ3��
            int currentX = borderXLst[0];   //��ʼ����ʱ�ĵ�X����
            int currentY = borderYLst[0];   //��ʼ����ʱ�ĵ�Y����

            bool finished = false;      //�Ƿ��Ѿ������
            //Ϊ�������Ч��,����Ƶ���ظ���ȡӰ��,��Ԥ�ȶ�ȡһ����ͼ���������,
            //�����ĸ�ֵ�ֱ�����ⲿ��ͼ������ͼ������,�϶˵�ͼ������,��͸�
            int rawLeft = 0;// borderXLst[0];
            int rawTop = 0;// borderYLst[0];
            int rawWidth = 0;// Math.Min(1000, rasterX - rawLeft);
            int rawHeight = 0;// Math.Min(1000, rasterY - rawTop);
            byte[] raw = null;
            //��ȡ�ⲿ�ֵ�Ӱ��
            //raw = new byte[rawWidth * rawHeight];
            // band.ReadRaster(rawLeft, rawTop, rawWidth, rawHeight, raw, rawWidth, rawHeight, 0, 0);
            #endregion

            int findcout = 0;
            while (!finished)
            {
                if (findcout == 8) break;       //�����һ��������ѭ���ˣ�������

                //��һ��ɨ��ĵ�
                int tmpX = currentX + Direction[currentDirection, 0];
                int tmpY = currentY + Direction[currentDirection, 1];

                //���ɨ����Ƿ��Ѿ�����Ӱ��߽�,������,��˳ʱ��ת��һ��
                while (tmpX < 0 || tmpX >= rasterX || tmpY < 0 || tmpY >= rasterY)
                {
                    currentDirection--;
                    if (currentDirection == -1)
                    {
                        currentDirection = 7;
                    }

                    tmpX = currentX + Direction[currentDirection, 0];
                    tmpY = currentY + Direction[currentDirection, 1];
                }

                //���һ��ɨ����Ƿ���Ԥ�������ݷ�Χ��,�����,��ֱ������,����,�����¶�ȡһ��Ӱ��
                if (raw == null || tmpX < rawLeft || tmpY < rawTop || tmpX >= rawLeft + rawWidth || tmpY >= rawTop + rawHeight)
                {
                    #region ���¶�ȡ,�Ե�ǰ��Ϊ����,�������¸���500
                    rawLeft = Math.Max(0, tmpX - 500);
                    rawTop = Math.Max(0, tmpY - 500);
                    rawWidth = Math.Min(tmpX + 500, rasterX) - rawLeft;
                    rawHeight = Math.Min(tmpY + 500, rasterY) - rawTop;
                    raw = new byte[rawWidth * rawHeight];
                    band.ReadRaster(rawLeft, rawTop, rawWidth, rawHeight, raw, rawWidth, rawHeight, 0, 0);
                    #endregion
                }

                //��һ��ɨ����������е�λ��
                int tmpIndex = (tmpY - rawTop) * rawWidth + (tmpX - rawLeft);

                if (raw[tmpIndex] == bg)
                {
                    //�����һ��ɨ����Ǳ���,��ô��ɨ�跽��˳ʱ��ת��һ�¼���ɨ��                        
                    currentDirection--;
                    if (currentDirection == -1)
                    {
                        currentDirection = 7;
                        findcout++;
                    }
                }
                else
                {
                    #region �����һ��ɨ��㲻�Ǳ���,������������һ���߽��,��������¼���б���

                    //�������,�����¼���б���
                    borderXLst.Add(tmpX);
                    borderYLst.Add(tmpY);

                    //������Ϊ��ǰ��
                    currentX = tmpX;
                    currentY = tmpY;

                    findcout = 0;

                    //����ص������,˵���Ѿ��������,��������
                    if (tmpX == borderXLst[0] && tmpY == borderYLst[0])
                        break;

                    //��ɨ�跽����ʱ��ת����
                    currentDirection++;
                    if (currentDirection == 8)
                        currentDirection = 0;
                    currentDirection++;
                    if (currentDirection == 8)
                        currentDirection = 0;
                    #endregion
                }
            }

            #endregion

            #region �ҵ����б߽���,�ҳ����еĹյ�
            List<double> rate = new List<double>();
            for (int i = 1; i < borderXLst.Count - 2; i++)
            {
                #region ����ȡ4����,����ƽ������,��Ϊ��߽�����
                int tmpInt = Math.Max(0, i - 4);
                int toSkip = 0;     //��ʱ�����ֹµ�,����ǰ������һ��,��Ӧ�������õ�
                double leftVectorX = 0, leftVectorY = 0;//��߽�������X,Yֵ
                for (int j = tmpInt; j < i; j++)
                {
                    double tmpLen = Math.Sqrt((borderXLst[i] - borderXLst[j]) * (borderXLst[i] - borderXLst[j]) +
                                              (borderYLst[i] - borderYLst[j]) * (borderYLst[i] - borderYLst[j]));
                    if (tmpLen == 0) { toSkip++; continue; }
                    leftVectorX += (borderXLst[i] - borderXLst[j]) / tmpLen;
                    leftVectorY += (borderYLst[i] - borderYLst[j]) / tmpLen;
                }
                leftVectorX = leftVectorX / (i - tmpInt);
                leftVectorY = leftVectorY / (i - tmpInt);
                #endregion

                #region ����ȡ4����,����ƽ������,��Ϊ�ұ߽�����
                tmpInt = Math.Min(borderXLst.Count - 1, i + 4);
                toSkip = 0;
                double rightVectorX = 0, rightVectorY = 0;//�ұ߽�������X,Yֵ
                for (int j = i + 1; j <= tmpInt; j++)
                {
                    double tmpLen = Math.Sqrt((borderXLst[j] - borderXLst[i]) * (borderXLst[j] - borderXLst[i]) +
                                             (borderYLst[j] - borderYLst[i]) * (borderYLst[j] - borderYLst[i]));
                    if (tmpLen == 0) { toSkip++; continue; }
                    rightVectorX += (borderXLst[j] - borderXLst[i]) / tmpLen;
                    rightVectorY += (borderYLst[j] - borderYLst[i]) / tmpLen;
                }
                rightVectorX = rightVectorX / (tmpInt - i);
                rightVectorY = rightVectorY / (tmpInt - i);
                #endregion

                //�������ұ߽�����,����õ�ı仯��
                rate.Add(Math.Sqrt((rightVectorX - leftVectorX) * (rightVectorX - leftVectorX) +
                                   (rightVectorY - leftVectorY) * (rightVectorY - leftVectorY)));
            }
            //��ƽ���仯��
            double averRate = 0;
            for (int i = 0; i < rate.Count; i++)
            {
                averRate += rate[i];
            }
            averRate = averRate / rate.Count;

            //�յ�Ŀռ�����
            //List<double> pntsX = new List<double>();
            //List<double> pntsY = new List<double>();
            //����һ��(�����Ͻǵ�)�Ŀռ��������յ���
            GeoPntsX.Add(geoTrans[0] + borderXLst[0] * geoTrans[1] + borderYLst[0] * geoTrans[2]);
            GeoPntsY.Add(geoTrans[3] + borderYLst[0] * geoTrans[4] + borderYLst[0] * geoTrans[5]);
            //�м�����е�,����仯�ʴ���ƽ���仯��,����ӵ��յ�����
            for (int i = 0; i < rate.Count; i++)
            {
                if (rate[i] > averRate)
                {
                    GeoPntsX.Add(geoTrans[0] + borderXLst[i + 1] * geoTrans[1] + borderYLst[i + 1] * geoTrans[2]);
                    GeoPntsY.Add(geoTrans[3] + borderYLst[i + 1] * geoTrans[4] + borderYLst[i + 1] * geoTrans[5]);
                }
            }
            //�����һ��(��ʵҲ�����Ͻǵ�)�Ŀռ��������յ���
            GeoPntsX.Add(geoTrans[0] + borderXLst[0] * geoTrans[1] + borderYLst[0] * geoTrans[2]);
            GeoPntsY.Add(geoTrans[3] + borderYLst[0] * geoTrans[4] + borderYLst[0] * geoTrans[5]);
            #endregion

        }
    }
}