using System;
using System.Collections.Generic;
using System.Text;
using OSGeo.GDAL;

namespace OrthoDB
{
    //用于实际边界提取 
    public class EdgeExtract
    {
        //搜索的八个方向
        private static int[,] Direction = { { -1, 1 }, { 0, 1 }, { 1, 1 }, { 1, 0 }, { 1, -1 }, { 0, -1 }, { -1, -1 }, { -1, 0 } };

        /// <summary>
        /// 搜索边界，得到边界的点集，并抽稀，得到可用于生成Shp文件的点集
        /// </summary>
        /// <param name="band">影像</param> 
        /// <param name="band">影像</param> 
        /// <param name="startx">搜索起点的X像素坐标</param>
        /// <param name="starty">搜索起点的Y像素坐标</param>
        /// <param name="startDirection">搜索起始方向</param>
        /// <param name="bg">用户指定的背影值</param>
        /// <param name="GeoPntsX">搜索完毕，并抽稀后的点集空间X坐标</param>
        /// <param name="GeoPntsY">搜索完毕，并抽稀后的点集空间Y坐标</param>
        /// <param name="borderXLst">搜索完毕，抽稀前的点集像素X坐标</param>
        /// <param name="borderYLst">搜索完毕，抽稀前的点集像素Y坐标</param>
        public static void FindPnt(Band band,double[] geoTrans, int startx, int starty, int startDirection, double bg, out List<double> GeoPntsX, out List<double> GeoPntsY, out List<int> borderXLst, out List<int> borderYLst)
        {
            GeoPntsX = new List<double>();
            GeoPntsY = new List<double>();

            int rasterX = band.XSize;
            int rasterY = band.YSize;

            //System.IO.StreamWriter sw = new System.IO.StreamWriter(System.Windows.Forms.Application.StartupPath + "\\tmp\\aa.txt");

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
                if (findcout >= 8) break;       //如果在一个点上死循环了，就跳出

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

                    findcout++;

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

                    //sw.WriteLine(tmpX + "\t" + tmpY);
                    //sw.Flush();

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
            //sw.Close();

            #endregion

            #region 找到所有边界点后,找出其中的拐点
            #region 使用左右四点作求平均变化率作为拐点判断的标志
            //List<double> rate = new List<double>();
            //for (int i = 1; i < borderXLst.Count - 2; i++)
            //{
            //    #region 向左取4个点,求其平均向量,作为左边界向量
            //    int tmpInt = Math.Max(0, i - 4);
            //    int toSkip = 0;     //有时候会出现孤岛,导致前后两点一致,则应该跳过该点
            //    double leftVectorX = 0, leftVectorY = 0;//左边界向量的X,Y值
            //    for (int j = tmpInt; j < i; j++)
            //    {
            //        double tmpLen = Math.Sqrt((borderXLst[i] - borderXLst[j]) * (borderXLst[i] - borderXLst[j]) +
            //                                  (borderYLst[i] - borderYLst[j]) * (borderYLst[i] - borderYLst[j]));
            //        if (tmpLen == 0) { toSkip++; continue; }
            //        leftVectorX += (borderXLst[i] - borderXLst[j]) / tmpLen;
            //        leftVectorY += (borderYLst[i] - borderYLst[j]) / tmpLen;
            //    }
            //    leftVectorX = leftVectorX / (i - tmpInt);
            //    leftVectorY = leftVectorY / (i - tmpInt);
            //    #endregion

            //    #region 向右取4个点,求其平均向量,作为右边界向量
            //    tmpInt = Math.Min(borderXLst.Count - 1, i + 4);
            //    toSkip = 0;
            //    double rightVectorX = 0, rightVectorY = 0;//右边界向量的X,Y值
            //    for (int j = i + 1; j <= tmpInt; j++)
            //    {
            //        double tmpLen = Math.Sqrt((borderXLst[j] - borderXLst[i]) * (borderXLst[j] - borderXLst[i]) +
            //                                 (borderYLst[j] - borderYLst[i]) * (borderYLst[j] - borderYLst[i]));
            //        if (tmpLen == 0) { toSkip++; continue; }
            //        rightVectorX += (borderXLst[j] - borderXLst[i]) / tmpLen;
            //        rightVectorY += (borderYLst[j] - borderYLst[i]) / tmpLen;
            //    }
            //    rightVectorX = rightVectorX / (tmpInt - i);
            //    rightVectorY = rightVectorY / (tmpInt - i);
            //    #endregion

            //    //根据左右边界向量,计算该点的变化率
            //    rate.Add(Math.Sqrt((rightVectorX - leftVectorX) * (rightVectorX - leftVectorX) +
            //                       (rightVectorY - leftVectorY) * (rightVectorY - leftVectorY)));
            //}
            ////求平均变化率
            //double averRate = 0;
            //for (int i = 0; i < rate.Count; i++)
            //{
            //    averRate += rate[i];
            //}
            //averRate = averRate / rate.Count; 
            #endregion

            

            //与平均变化率判断方法对应的，循环中间的所有点,如果变化率大于平均变化率,则添加到拐点列中
            //for (int i = 0; i < rate.Count; i++)
            //{
            //    if (rate[i] > averRate)
            //    {
            //        GeoPntsX.Add(geoTrans[0] + borderXLst[i + 1] * geoTrans[1] + borderYLst[i + 1] * geoTrans[2]);
            //        GeoPntsY.Add(geoTrans[3] + borderYLst[i + 1] * geoTrans[4] + borderYLst[i + 1] * geoTrans[5]);
            //    }
            //}

            //第二种方法，使用简单的方法抽稀点列
            //第一步：循环判断中间的点，如果前点到该点的向量等于该点到后点的向量，那么该点就不是拐点，跳过该点
            //若不等于，则将该点记录到拐点列中
            List<int> tmpPntsX = new List<int>();
            List<int> tmpPntsY = new List<int>();

            tmpPntsX.Add(borderXLst[0]);
            tmpPntsY.Add(borderYLst[0]);    //先把第一点加到列中

            for (int i = 1; i < borderXLst.Count-1; i++)
            {
                if (borderXLst.Count < 2) break;

                int prevX = borderXLst[i] - borderXLst[i - 1];
                int prevY = borderYLst[i] - borderYLst[i - 1];
                int nextX = borderXLst[i + 1] - borderXLst[i];
                int nextY = borderYLst[i + 1] - borderYLst[i];

                if (prevX == nextX && prevY == nextY)
                    continue;
                else
                { 
                    tmpPntsX.Add(borderXLst[i]);
                    tmpPntsY.Add(borderYLst[i]);
                }
            }

            tmpPntsX.Add(borderXLst[borderXLst.Count-1]);
            tmpPntsY.Add(borderYLst[borderYLst.Count-1]);    //再把最后一点加到列中

            //判断一下，这个提取的边界是否等于影像的完全边界，如果是的话，那么一般来说说明了影像的背景值有错误
            if (tmpPntsX[0] == 0 && tmpPntsY[0] == 0 && tmpPntsX[1] == rasterX - 1 && tmpPntsY[1] == 0 &&
tmpPntsX[2] == rasterX - 1 && tmpPntsY[2] == rasterY - 1 && tmpPntsX[3] == 0 && tmpPntsY[3] == rasterY - 1 && (tmpPntsX.Count == 4 || tmpPntsX.Count == 5))
                throw new Exception("不符合要素的点列表，疑似影像背景值错误");

            List<int> resPntXLst = new List<int>();
            List<int> resPntYLst = new List<int>();

            //抽稀点列
            cx(7.0, tmpPntsX, tmpPntsY, ref resPntXLst, ref resPntYLst);

            //将抽稀后的点列经运算得到实际空间坐标的列
            for (int i = 0; i < resPntXLst.Count; i++)
            {
                GeoPntsX.Add(geoTrans[0] + resPntXLst[i] * geoTrans[1] + resPntYLst[i] * geoTrans[2]);
                GeoPntsY.Add(geoTrans[3] + resPntXLst[i] * geoTrans[4] + resPntYLst[i] * geoTrans[5]);

            }
            ////第二步:抽稀到十分之一
            
            //for (int i = 1; i < tmpPntsX.Count -1; i++)
            //{
            //    if(i%10 == 0)
            //    { 
            //        GeoPntsX.Add(geoTrans[0] + tmpPntsX[i] * geoTrans[1] + tmpPntsY[i] * geoTrans[2]);
            //        GeoPntsY.Add(geoTrans[3] + tmpPntsX[i] * geoTrans[4] + tmpPntsY[i] * geoTrans[5]);
            //    }
            //}

            //for (int i = 1; i < borderXLst.Count-1; i++)
            //{
            //    if (borderXLst.Count < 2) break;

            //    int prevX = borderXLst[i] - borderXLst[i - 1];
            //    int prevY = borderYLst[i] - borderYLst[i - 1];
            //    int nextX = borderXLst[i + 1] - borderXLst[i];
            //    int nextY = borderYLst[i + 1] - borderYLst[i];

            //    if (prevX == nextX && prevY == nextY)
            //        continue;
            //    else
            //    { 
            //        GeoPntsX.Add(geoTrans[0] + borderXLst[i] * geoTrans[1] + borderYLst[i] * geoTrans[2]);
            //        GeoPntsY.Add(geoTrans[3] + borderXLst[i] * geoTrans[4] + borderYLst[i] * geoTrans[5]);
            //    }
            //}

            //将第一点(即左上角点)的空间坐标加入拐点列
            //GeoPntsX.Add(geoTrans[0] + borderXLst[0] * geoTrans[1] + borderYLst[0] * geoTrans[2]);
            //GeoPntsY.Add(geoTrans[3] + borderXLst[0] * geoTrans[4] + borderYLst[0] * geoTrans[5]);



            //将最后一点(其实也是左上角点)的空间坐标加入拐点列
            //GeoPntsX.Add(geoTrans[0] + borderXLst[0] * geoTrans[1] + borderYLst[0] * geoTrans[2]);
            //GeoPntsY.Add(geoTrans[3] + borderXLst[0] * geoTrans[4] + borderYLst[0] * geoTrans[5]);
            #endregion

        }

        /// <summary>
        /// 提取出图像的边界点列表，由于可能图像可能有多个封闭的区域，所以这里使用两级列表存储点
        /// 注：该算法目前只能处理一到两个独立封闭区域的影像，且不包含内部岛屿的情况
        /// </summary>
        /// <param name="ds">影像数据集</param>
        /// <param name="bg">影像背景值</param>
        /// <param name="pntsXLstLst">X坐标列表的列表</param>
        /// <param name="pntsYLstLst">Y坐标列表的列表</param>
        /// <param name="errMsg">错误信息</param>
        /// <returns>提取是否成功</returns>
        public static bool GetEdgePntLst(Dataset ds,int bg,out List<List<double>> pntsXLstLst, out List<List<double>> pntsYLstLst,out string errMsg)
        {
            pntsXLstLst = new List<List<double>>();
            pntsYLstLst = new List<List<double>>();
            errMsg = "";

            int rasterX = ds.RasterXSize;	//影像宽度
            int rasterY = ds.RasterYSize;	//影像高度

            double[] geoTrans = new double[6];
            ds.GetGeoTransform(geoTrans);		//影像坐标变换参数

            Band band = ds.GetRasterBand(1); 
            if (band.DataType != DataType.GDT_Byte)
            {
                errMsg = "暂不支持格式为8位Byte之外的数据";
                return false ;
            }

            //找到的边界点(像素坐标)
            List<int> borderXLst = new List<int>();
            List<int> borderYLst = new List<int>();

            byte[] raw = null;

            //要读取的影像大小(为提高效率，这里一次读取一定范围的影像，而不是在循环中一个一个像素地去读)
            int toReadx = 0;
            int toReady = 0; 

            try
            {
                #region 获取影像实际的左上点，并从该点开始，追踪出边界 
                int leftTopX = 0, leftTopY = 0; //实际左上点的像素坐标

                toReadx = rasterX;
                toReady = Math.Min(rasterY, 20);

                int tmpReaded = 0;      //记录已经读过多少行了

                raw = new byte[toReadx * toReady];
                bool ifFind = false;

                #region 循环读取，直到找到影像的左上点
                while (!ifFind)
                {
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
                                leftTopX = j;
                                leftTopY = i + tmpReaded;
                                break;
                            }
                        }
                        if (ifFind) break;
                    }

                    //找到了就跳出
                    if (ifFind) break;
                    else
                    {
                        tmpReaded += toReady; //没找到,就再读取20行
                        if (tmpReaded == rasterY) break;//已经全部读取完了,而且没有找到,说明图像全是背景值,跳出
                        toReady = Math.Min(20, rasterY - tmpReaded);  //没有全部读取完,就再读20行,如果剩下不到20行了,就读剩下的行 
                    }
                }
                //如果没有找到,说明图像全是背景值,直接报错跳出即可
                if (!ifFind)
                {
                    errMsg = "影像全部为背景值,无法提取边界\n";
                    return false;
                } 
                #endregion

                #region 搜索边界，得到边界的点集，并抽稀，得到最后的空间坐标点集，作为一个子列表，加入到返回值中去
                List<double> tmpPntsx, tmpPntsy;
                List<int> tmpBorderXLst, tmpBorderYLst;
                FindPnt(band,geoTrans,leftTopX, leftTopY, 3, bg, out tmpPntsx, out tmpPntsy, out tmpBorderXLst, out tmpBorderYLst);
                if (tmpPntsx != null && tmpPntsx.Count != 0 && tmpPntsy != null && tmpPntsy.Count != 0)
                {
                    pntsXLstLst.Add(tmpPntsx);
                    pntsYLstLst.Add(tmpPntsy);
                    borderXLst.AddRange(tmpBorderXLst);
                    borderYLst.AddRange(tmpBorderYLst);
                }
                #endregion 

                #endregion

                #region 获取影像实际的右上点，如果该点不包含在之前搜索过的边界上，就从该点开始追踪出边界
                int rightTopX = 0, rightTopY = 0;
                toReadx = Math.Min(rasterX, 20);
                toReady = rasterY;
                tmpReaded = 0;      //记录已经读过多少列了
                raw = new byte[toReadx * toReady];
                ifFind = false;

                #region 循环读取,直到读取到右上角点为止 
                while (!ifFind)
                {
                    //先读20列
                    band.ReadRaster(rasterX - tmpReaded - toReadx, 0, toReadx, toReady, raw, toReadx, toReady, 0, 0);
                    //找右上角点
                    for (int i = toReadx - 1; i >= 0; i--)
                    {
                        for (int j = 0; j < toReady; j++)
                        {
                            if (raw[j * toReadx + i] != bg)
                            {
                                ifFind = true;
                                rightTopX = rasterX - tmpReaded - toReadx + i;
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
                        tmpReaded += toReadx; //没找到,就再读取20行
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

                    #region 如果不在，那么开始追踪边界并抽稀
                    if (!pntInLst)
                    {
                        FindPnt(band,geoTrans, rightTopX, rightTopY, 1, bg, out tmpPntsx, out tmpPntsy, out tmpBorderXLst, out tmpBorderYLst);
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

                #region 获取影像实际的右下点，如果该点不包含在之前搜索过的边界上，就从该点开始追踪出边界
                int rightBottomX = 0, rightBottomY = 0;
                toReadx = rasterX;
                toReady = Math.Min(rasterY, 20);
                tmpReaded = 0;      //记录已经读过多少行了
                raw = new byte[toReadx * toReady];
                ifFind = false;

                #region 循环读取,直到读取到右下角点为止
                while (!ifFind)
                { 
                    //先读20行
                    band.ReadRaster(0, rasterY - toReady - tmpReaded, toReadx, toReady, raw, toReadx, toReady, 0, 0);
                    //找右下角点
                    for (int i = toReady - 1; i >= 0; i--)
                    {
                        for (int j = toReadx - 1; j >= 0; j--)
                        {
                            if (raw[i * toReadx + j] != bg)
                            {
                                ifFind = true;
                                rightBottomX = j;
                                rightBottomY = rasterY - tmpReaded - toReady + i;
                                break;
                            }
                        }
                        if (ifFind) break;
                    }

                    //找到了就跳出
                    if (ifFind) break;
                    else
                    {
                        tmpReaded += toReady; //没找到,就再读取20行
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
                        FindPnt(band,geoTrans,rightBottomX, rightBottomY, 7, bg, out tmpPntsx, out tmpPntsy, out tmpBorderXLst, out tmpBorderYLst);
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

                #region 获取影像实际的左下点，如果该点不包含在之前搜索过的边界上，就从该点开始追踪出边界
                int leftBottomX = 0, leftBottomY = 0;
                toReadx = Math.Min(rasterX, 20);
                toReady = rasterY;
                tmpReaded = 0;      //记录已经读过多少列了
                raw = new byte[toReadx * toReady];
                ifFind = false;

                #region 循环读取,直到读取到左下角点为止 
                while (!ifFind)
                {
                    //先读20行
                    band.ReadRaster(tmpReaded, 0, toReadx, toReady, raw, toReadx, toReady, 0, 0);
                    //找左下角点
                    for (int i = 0; i < toReadx; i++)
                    {
                        for (int j = toReady - 1; j >= 0; j--)
                        {
                            if (raw[j * toReadx + i] != bg)
                            {
                                ifFind = true;
                                leftBottomX = tmpReaded + i;
                                leftBottomY = j;
                                break;
                            }
                        }
                        if (ifFind) break;
                    }

                    if (ifFind) break; //找到了就跳出
                    else
                    {
                        tmpReaded += toReadx; //没找到,就再读取20行
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
                        FindPnt(band,geoTrans, leftBottomX, leftBottomY, 5, bg, out tmpPntsx, out tmpPntsy, out tmpBorderXLst, out tmpBorderYLst);
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
            }
            catch (Exception ex)
            {
                errMsg = ex.Message;
                return false;
            }
            return true;
        }

        /// <summary>
        /// 使用道格拉斯-普克算法进行抽稀
        /// </summary>
        /// <param name="thres">抽稀的阈值，一般用3-5个像素左右</param>
        /// <param name="srcXLst">原X坐标数组</param>
        /// <param name="srcYLst">原Y坐标数组</param>
        /// <param name="resXLst">抽稀后X坐标数组</param>
        /// <param name="resYLst">抽稀后Y坐标数组</param>
        public static void cx(double thres, List<int> srcXLst, List<int> srcYLst, ref List<int> resXLst, ref List<int> resYLst)
        {
            resXLst = new List<int>();
            resYLst = new List<int>();

            //先拷贝一下数组
            for (int i = 0; i < srcXLst.Count; i++)
            {
                resXLst.Add(srcXLst[i]);
                resYLst.Add(srcYLst[i]);
            }

            //由于点列是一个闭合的环，头尾是相等的，所以需要分为两段，进行抽稀
            //抽稀操作中涉及数组元素的移除，为避免弄乱序号，所以从后面开始抽
            //先抽一半到最后
            IteraCX(thres, ref resXLst, ref resYLst, srcXLst.Count / 2, srcXLst.Count - 1);
            IteraCX(thres, ref resXLst, ref resYLst, 0, srcXLst.Count / 2);
        }

        /// <summary>
        /// 迭代抽稀点列,每次被调用时都只操作点列的一个区段，这个区段用开始和结束的序号表示
        /// </summary>
        /// <param name="thres">阈值</param>
        /// <param name="resXLst">待抽稀的X数组</param>
        /// <param name="resYLst">待抽稀的Y数组</param>
        /// <param name="startIndex">区段的起始点（含该点）</param>
        /// <param name="endIndex">区段的结束点（含该点）</param>
        private static void IteraCX(double thres, ref List<int> resXLst, ref List<int> resYLst, int startIndex, int endIndex)
        {
            //起点和终点的XY坐标
            double startx, starty, endx, endy;
            startx = resXLst[startIndex];
            starty = resYLst[startIndex];
            endx = resXLst[endIndex];
            endy = resYLst[endIndex];

            //求过这两点的直线方程Ax+By+C=0;
            //直线的两点式为(y2-y1)x-(x2-x1)y-x1y2+x2y1=0
            double A = endy - starty;
            double B = startx - endx;
            double C = endx * starty - startx * endy;

            //求点列指定区段中各点至该直线的距离，记录最大距离的值以及对应的点序号
            double maxDistance = 0;
            int maxIndex = startIndex;
            for (int i = startIndex; i <= endIndex; i++)
            {
                if (i == startIndex || i == endIndex) continue;

                double tmpDis = 1 / Math.Sqrt(A * A + B * B);
                tmpDis = tmpDis * Math.Abs(A * resXLst[i] + B * resYLst[i] + C);

                if (tmpDis > maxDistance)
                {
                    maxIndex = i;
                    maxDistance = tmpDis;
                }
            }

            //如果最大距离值大于阈值，那么将本区段在最大距离处分为两段，再进行迭代
            if (maxDistance > thres)
            {
                IteraCX(thres, ref resXLst, ref resYLst, maxIndex, endIndex);
                IteraCX(thres, ref resXLst, ref resYLst, startIndex, maxIndex);
            }
            //如果最大距离小于阈值，那么将本区段所有点（除首末两点之外）从点列中去掉
            else
            {
                for (int i = endIndex - 1; i > startIndex; i--)
                {
                    resXLst.RemoveAt(i);
                    resYLst.RemoveAt(i);
                }
            }
        }

    }
}
