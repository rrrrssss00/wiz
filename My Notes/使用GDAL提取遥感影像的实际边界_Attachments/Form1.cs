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
    //todo:目前只支持8位Byte类型的影像,而且只支持一个闭合区域的影像
    //todo:目前判断轮廓还有很多多余的点,还需要试试能否进一步减小点的数量
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
           
        }

        private void browseBtn_Click(object sender, EventArgs e)
        {
            OpenFileDialog mdlg = new OpenFileDialog();
            mdlg.Filter = "影像文件(*.img *.pix *.tif *.tiff)|*.img;*.pix;*.tif;*.tiff";
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
            #region 一些检查工作 
            if (pathBox.Text == "")
            {
                MessageBox.Show("请选择待提取的影像文件");
                return;
            }

            double bg = -1;
            if (bgBox.Text == "" || !double.TryParse(bgBox.Text.Trim(),out bg)) 
            {
                MessageBox.Show("背景色,必须是数字");
                return;
            }

            string tmpext = Path.GetExtension(pathBox.Text);
            string destShpFile = pathBox.Text.Replace(tmpext,".shp");
            if(File.Exists(destShpFile))
            {
                MessageBox.Show(destShpFile + "文件已经存在");
                return;
            } 
            #endregion

            logBox.Text = "";

            #region 打开源影像
            Gdal.AllRegister();
            OSGeo.OGR.Ogr.RegisterAll();
            Gdal.SetConfigOption("GDAL_FILENAME_IS_UTF8", "YES");
            //Gdal.SetConfigOption("GDAL_DATA", Application.StartupPath + "\\gdaldata");
            
            //打开源影像
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
                    logBox.Text += "打开影像失败,路径请不要过长或包含过多中文字符\n";
                    return;
                } 
            }

            rasterX = dsin.RasterXSize;	//影像宽度
            rasterY = dsin.RasterYSize;	//影像高度
            bandCount = dsin.RasterCount;	//波段数
            
            dsin.GetGeoTransform(geoTrans);		//影像坐标变换参数
            string proj = dsin.GetProjection();		//影像坐标系信息（WKT格式字符串）
            OSGeo.OSR.SpatialReference projRef = new OSGeo.OSR.SpatialReference(proj);
            #endregion

            int BeginDirect;

            //找到的边界点
            List<int> borderXLst = new List<int>();
            List<int> borderYLst = new List<int>();

            band = dsin.GetRasterBand(1);      //只对一个波段进行处理
            if(band.DataType != DataType.GDT_Byte)
            {
                MessageBox.Show("暂不支持格式为8位Byte之外的数据");
                return;
            }
            

            try
            {
                int leftTopX = 0, leftTopY = 0;
                #region 首先读取最左上方的边界点 
                byte[] raw = null;

                //要读取的影像大小
                int toReadx = rasterX;
                int toReady = Math.Min(rasterY, 20);

                int tmpReaded = 0;      //记录已经读过多少行了

                raw = new byte[toReadx * toReady];
                bool ifFind = false;
                //循环读取,直到读取到左上角点为止
                while (!ifFind)
                {
                    //if (tmpReaded + toReady > rasterY) break;
                    //先读20行
                    band.ReadRaster(0, tmpReaded, toReadx, toReady, raw, toReadx, toReady, 0, 0);
                    //找左上角点
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

                    //找到了就跳出
                    if (ifFind)
                        break;
                    else
                    {
                        //没找到,就再读取20行
                        tmpReaded += toReady;
                        if (tmpReaded == rasterY) break;//已经全部读取完了,而且没有找到,说明图像全是背景值,跳出
                        toReady = Math.Min(20, rasterY - tmpReaded);  //没有全部读取完,就再读20行,如果剩下不到20行了,就读剩下的行 
                    }
                }

                //如果没有找到,说明图像全是背景值,直接报错跳出即可
                if (!ifFind)
                {
                    logBox.Text += "影像全部为背景值,无法提取边界\n";
                    dsin.Dispose();
                    dsin = null;
                    return;
                }
                #endregion

                List<List<double>> pntsXLstLst = new List<List<double>>();
                List<List<double>> pntsYLstLst = new List<List<double>>();
                #region 搜索边界，得到边界的点集，并抽稀，得到可用于生成Shp文件的点集
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

                #region 再尝试从其它方向查找边界点，并搜索边界
                #region 右上方                
                int rightTopX = 0, rightTopY = 0;
                toReadx = Math.Min(rasterX, 20);
                toReady = rasterY;
                tmpReaded = 0;      //记录已经读过多少列了
                raw = new byte[toReadx * toReady];
                ifFind = false;

                #region 循环读取,直到读取到右上角点为止

                while (!ifFind)
                {
                    //if (tmpReaded + toReadx > rasterX) break;
                    //先读20行
                    band.ReadRaster(rasterX - tmpReaded - toReadx, 0, toReadx, toReady, raw, toReadx, toReady, 0, 0);
                    //找右上角点
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

                    //找到了就跳出
                    if (ifFind)
                        break;
                    else
                    {
                        //没找到,就再读取20行
                        tmpReaded += toReadx;
                        if (tmpReaded == rasterX) break;//已经全部读取完了,而且没有找到,说明图像全是背景值,跳出
                        toReadx = Math.Min(20, rasterX - tmpReaded);  //没有全部读取完,就再读20列,如果剩下不到20列了,就读剩下的列
                    }
                }
                #endregion

                if (ifFind)
                {
                    #region 查找一下该点是否在已经搜索过的边界上，如果在，就不要再搜索了                    
                    bool pntInLst = false;
                    for (int i = 0; i < borderXLst.Count; i++)
                    {
                        if (borderXLst[i] == rightTopX && borderYLst[i] == rightTopY)
                        { pntInLst = true; break; }
                    }
                    #endregion

                    #region 搜索边界并抽稀                    
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

                #region 右下方
                int rightBottomX = 0, rightBottomY = 0;
                toReadx = rasterX;
                toReady = Math.Min(rasterY, 20);
                tmpReaded = 0;      //记录已经读过多少列了
                raw = new byte[toReadx * toReady];
                ifFind = false;

                #region 循环读取,直到读取到右下角点为止
                while (!ifFind)
                {
                    //if (tmpReaded + toReady > rasterY) break;

                    //先读20行
                    band.ReadRaster(0, rasterY - toReady - tmpReaded, toReadx, toReady, raw, toReadx, toReady, 0, 0);
                    //找左上角点
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

                    //找到了就跳出
                    if (ifFind)
                        break;
                    else
                    {
                        //没找到,就再读取20行
                        tmpReaded += toReady;
                        if (tmpReaded == rasterY) break;//已经全部读取完了,而且没有找到,说明图像全是背景值,跳出
                        toReady = Math.Min(20, rasterY - tmpReaded);  //没有全部读取完,就再读20行,如果剩下不到20行了,就读剩下的行 
                    }
                }
                #endregion

                if (ifFind)
                {
                    #region 查找一下该点是否在已经搜索过的边界上，如果在，就不要再搜索了
                    bool pntInLst = false;
                    for (int i = 0; i < borderXLst.Count; i++)
                    {
                        if (borderXLst[i] == rightBottomX && borderYLst[i] == rightBottomY)
                        { pntInLst = true; break; }
                    }
                    #endregion

                    #region 搜索边界并抽稀
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

                #region 左下方
                int leftBottomX = 0, leftBottomY = 0;
                toReadx = Math.Min(rasterX, 20);
                toReady = rasterY;
                tmpReaded = 0;      //记录已经读过多少列了
                raw = new byte[toReadx * toReady];
                ifFind = false;

                #region 循环读取,直到读取到左下角点为止

                while (!ifFind)
                {
                    //if (tmpReaded + toReadx > rasterX) break;
                    //先读20行
                    band.ReadRaster(tmpReaded, 0, toReadx, toReady, raw, toReadx, toReady, 0, 0);
                    //找左下角点
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

                    //找到了就跳出
                    if (ifFind)
                        break;
                    else
                    {
                        //没找到,就再读取20行
                        tmpReaded += toReadx;
                        if (tmpReaded == rasterX) break;//已经全部读取完了,而且没有找到,说明图像全是背景值,跳出
                        toReadx = Math.Min(20, rasterX - tmpReaded);  //没有全部读取完,就再读20列,如果剩下不到20列了,就读剩下的列
                    }
                }
                #endregion

                if (ifFind)
                {
                    #region 查找一下该点是否在已经搜索过的边界上，如果在，就不要再搜索了
                    bool pntInLst = false;
                    for (int i = 0; i < borderXLst.Count; i++)
                    {
                        if (borderXLst[i] == leftBottomX && borderYLst[i] == leftBottomY)
                        { pntInLst = true; break; }
                    }
                    #endregion

                    #region 搜索边界并抽稀
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

                #region 生成Shp文件
                OSGeo.OGR.Driver poDriver = OSGeo.OGR.Ogr.GetDriverByName("ESRI Shapefile");
                if (poDriver == null)
                    throw new Exception("操作失败:生成Shp文件错误");

                //用此Driver创建Shape文件
                OSGeo.OGR.DataSource poDS;
                poDS = poDriver.CreateDataSource(destShpFile, null);
                if (poDS == null)
                    throw new Exception("操作失败:生成Shp文件错误");

                OSGeo.OGR.Layer poLayer;
                poLayer = poDS.CreateLayer("lineLry", projRef, OSGeo.OGR.wkbGeometryType.wkbPolygon, null);
                if (poLayer == null)
                    throw new Exception("操作失败:生成Shp文件错误");

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
                logBox.Text += "操作失败:" + ex.Message + "\n";
                dsin.Dispose();
                dsin = null;
                return;
            }

            MessageBox.Show("提取完成");
        }

        /// <summary>
        /// 搜索边界，得到边界的点集，并抽稀，得到可用于生成Shp文件的点集
        /// </summary>
        /// <param name="startx">搜索起点的X像素坐标</param>
        /// <param name="starty">搜索起点的Y像素坐标</param>
        /// <param name="startDirection">搜索起始方向</param>
        /// <param name="bg">用户指定的背影值</param>
        /// <param name="GeoPntsX">搜索完毕，并抽稀后的点集空间X坐标</param>
        /// <param name="GeoPntsY">搜索完毕，并抽稀后的点集空间Y坐标</param>
        /// <param name="borderXLst">搜索完毕，抽稀前的点集像素X坐标</param>
        /// <param name="borderYLst">搜索完毕，抽稀前的点集像素Y坐标</param>
        private void FindPnt(int startx, int starty, int startDirection, double bg, out List<double> GeoPntsX, out List<double> GeoPntsY, out List<int> borderXLst, out List<int> borderYLst)
        {
            GeoPntsX = new List<double>();
            GeoPntsY = new List<double>();

            #region 开始搜索边界,搜索到的边界保存在borderXLst&borderYLst中
            borderXLst = new List<int>();
            borderYLst = new List<int>();
            borderXLst.Add(startx);
            borderYLst.Add(starty);

            #region 一些变量定义
            int currentDirection = startDirection;   //最开始的时候搜索方向（左上方为3）
            int currentX = borderXLst[0];   //开始搜索时的点X坐标
            int currentY = borderYLst[0];   //开始搜索时的点Y坐标

            bool finished = false;      //是否已经完成了
            //为提高运算效率,避免频繁重复读取影像,会预先读取一部分图像进行运算,
            //下面四个值分别代表这部分图像的左侧图像坐标,上端的图像坐标,宽和高
            int rawLeft = 0;// borderXLst[0];
            int rawTop = 0;// borderYLst[0];
            int rawWidth = 0;// Math.Min(1000, rasterX - rawLeft);
            int rawHeight = 0;// Math.Min(1000, rasterY - rawTop);
            byte[] raw = null;
            //读取这部分的影像
            //raw = new byte[rawWidth * rawHeight];
            // band.ReadRaster(rawLeft, rawTop, rawWidth, rawHeight, raw, rawWidth, rawHeight, 0, 0);
            #endregion

            int findcout = 0;
            while (!finished)
            {
                if (findcout == 8) break;       //如果在一个点上死循环了，就跳出

                //下一个扫描的点
                int tmpX = currentX + Direction[currentDirection, 0];
                int tmpY = currentY + Direction[currentDirection, 1];

                //这个扫描点是否已经超出影像边界,若超出,则顺时针转动一下
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

                //检查一下扫描点是否在预读的数据范围内,如果在,则直接运算,不在,则重新读取一块影像
                if (raw == null || tmpX < rawLeft || tmpY < rawTop || tmpX >= rawLeft + rawWidth || tmpY >= rawTop + rawHeight)
                {
                    #region 重新读取,以当前点为中心,左右上下各读500
                    rawLeft = Math.Max(0, tmpX - 500);
                    rawTop = Math.Max(0, tmpY - 500);
                    rawWidth = Math.Min(tmpX + 500, rasterX) - rawLeft;
                    rawHeight = Math.Min(tmpY + 500, rasterY) - rawTop;
                    raw = new byte[rawWidth * rawHeight];
                    band.ReadRaster(rawLeft, rawTop, rawWidth, rawHeight, raw, rawWidth, rawHeight, 0, 0);
                    #endregion
                }

                //下一个扫描点在数组中的位置
                int tmpIndex = (tmpY - rawTop) * rawWidth + (tmpX - rawLeft);

                if (raw[tmpIndex] == bg)
                {
                    //如果下一个扫描点是背景,那么将扫描方向顺时针转动一下继续扫描                        
                    currentDirection--;
                    if (currentDirection == -1)
                    {
                        currentDirection = 7;
                        findcout++;
                    }
                }
                else
                {
                    #region 如果下一个扫描点不是背景,则这个点就是下一个边界点,将这个点记录到列表中

                    //如果不是,则将其记录到列表中
                    borderXLst.Add(tmpX);
                    borderYLst.Add(tmpY);

                    //并设置为当前点
                    currentX = tmpX;
                    currentY = tmpY;

                    findcout = 0;

                    //如果回到起点了,说明已经查找完毕,跳出即可
                    if (tmpX == borderXLst[0] && tmpY == borderYLst[0])
                        break;

                    //将扫描方向逆时针转两下
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

            #region 找到所有边界点后,找出其中的拐点
            List<double> rate = new List<double>();
            for (int i = 1; i < borderXLst.Count - 2; i++)
            {
                #region 向左取4个点,求其平均向量,作为左边界向量
                int tmpInt = Math.Max(0, i - 4);
                int toSkip = 0;     //有时候会出现孤岛,导致前后两点一致,则应该跳过该点
                double leftVectorX = 0, leftVectorY = 0;//左边界向量的X,Y值
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

                #region 向右取4个点,求其平均向量,作为右边界向量
                tmpInt = Math.Min(borderXLst.Count - 1, i + 4);
                toSkip = 0;
                double rightVectorX = 0, rightVectorY = 0;//右边界向量的X,Y值
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

                //根据左右边界向量,计算该点的变化率
                rate.Add(Math.Sqrt((rightVectorX - leftVectorX) * (rightVectorX - leftVectorX) +
                                   (rightVectorY - leftVectorY) * (rightVectorY - leftVectorY)));
            }
            //求平均变化率
            double averRate = 0;
            for (int i = 0; i < rate.Count; i++)
            {
                averRate += rate[i];
            }
            averRate = averRate / rate.Count;

            //拐点的空间坐标
            //List<double> pntsX = new List<double>();
            //List<double> pntsY = new List<double>();
            //将第一点(即左上角点)的空间坐标加入拐点列
            GeoPntsX.Add(geoTrans[0] + borderXLst[0] * geoTrans[1] + borderYLst[0] * geoTrans[2]);
            GeoPntsY.Add(geoTrans[3] + borderYLst[0] * geoTrans[4] + borderYLst[0] * geoTrans[5]);
            //中间的所有点,如果变化率大于平均变化率,则添加到拐点列中
            for (int i = 0; i < rate.Count; i++)
            {
                if (rate[i] > averRate)
                {
                    GeoPntsX.Add(geoTrans[0] + borderXLst[i + 1] * geoTrans[1] + borderYLst[i + 1] * geoTrans[2]);
                    GeoPntsY.Add(geoTrans[3] + borderYLst[i + 1] * geoTrans[4] + borderYLst[i + 1] * geoTrans[5]);
                }
            }
            //将最后一点(其实也是左上角点)的空间坐标加入拐点列
            GeoPntsX.Add(geoTrans[0] + borderXLst[0] * geoTrans[1] + borderYLst[0] * geoTrans[2]);
            GeoPntsY.Add(geoTrans[3] + borderYLst[0] * geoTrans[4] + borderYLst[0] * geoTrans[5]);
            #endregion

        }
    }
}