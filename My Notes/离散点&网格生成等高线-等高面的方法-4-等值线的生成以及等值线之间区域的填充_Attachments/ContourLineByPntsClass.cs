using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using DotSpatial.Topology;

namespace ContourBandByPntsLib
{
    /// <summary>
    /// 等值插值及图片生成类（输入已知参数为离散点及浓度，输出为规则的网格，并按照规则网格进行等值线渲染）
    /// 这个类需依赖ColorMap类，以及DotSpatial的Topology类
    /// </summary>
    public class ContourLineByPntsClass
    {
        #region 第一步：根据输入的离散点数据，通过插值生成规律的网格
        /// <summary>
        /// 根据输入的已知离散点坐标以及对应浓度值，按用户指定的网格参数，插值计算出网格每一个节点处的浓度值
        /// </summary>
        /// <param name="srcCoordX">已知离散点的X坐标数组</param>
        /// <param name="srcCoordY">已知离散点的Y坐标数组</param>
        /// <param name="srcValue">已知离散点的浓度值</param>
        /// <param name="ext">待插值生成网格的空间范围（X及Y坐标的最大最小值）</param>
        /// <param name="gridXCount">待插值生成网格X方向的单元数量</param>
        /// <param name="gridYCount">待插值生成网格Y方向的单元数量</param>
        /// <returns></returns>
        public static List<ContourGridBlock> GenNewGridByIntero(List<double> srcCoordX, List<double> srcCoordY, List<double> srcValue, ContourExtent ext, int gridXCount, int gridYCount, out ContourExtent validExt)
        {
            List<ContourGridBlock> newBlockLst = new List<ContourGridBlock>();

            //原网格单元大小
            double gridCellSizeX = ext.Width / gridXCount;      //网格单元大小--X方向
            double gridCellSizeY = ext.Height / gridYCount;     //网格单元大小--Y方向

            #region 统计一下输入点中不为0部分的范围
            int value_count = 0;
            double minx = 0, maxx = 0, miny = 0, maxy = 0;
            for (int i = 0; i < srcValue.Count; i++)
            {
                if (srcValue[i] == 0) continue;

                if (value_count == 0)
                {
                    minx = srcCoordX[i];
                    maxx = srcCoordX[i];
                    miny = srcCoordY[i];
                    maxy = srcCoordY[i];
                }
                else
                {
                    if (srcCoordX[i] < minx)
                        minx = srcCoordX[i];

                    if (srcCoordX[i] > maxx)
                        maxx = srcCoordX[i];

                    if (srcCoordY[i] < miny)
                        miny = srcCoordY[i];

                    if (srcCoordY[i] > maxy)
                        maxy = srcCoordY[i];
                }
                value_count++;
            } 

            //将有效区域范围外扩1个网格单元大小，避免当有效点数太少的情况下，所有待插值点的值都为0
            minx -= gridCellSizeX;
            maxx += gridCellSizeX;
            miny -= gridCellSizeY;
            maxy += gridCellSizeY;  

            ContourExtent triExt = new ContourExtent();
            triExt.MinX = minx;
            triExt.MaxX = maxx;
            triExt.MinY = miny;
            triExt.MaxY = maxy;

            validExt = triExt;
            #endregion

            //根据刚才得到的范围，筛选出新的输入点集（去除了原来部分为0的值，让程序运行更快）
            List<double> new_coordXLst = new List<double>();
            List<double> new_coordYLst = new List<double>();
            List<double> new_valLst = new List<double>();

            for (int i = 0; i < srcValue.Count; i++)
            {
                if (srcCoordX[i] >= minx && srcCoordX[i] <= maxx && srcCoordY[i] >= miny && srcCoordY[i] <= maxy)
                {
                    new_coordXLst.Add(srcCoordX[i]);
                    new_coordYLst.Add(srcCoordY[i]);
                    new_valLst.Add(srcValue[i]);
                }
            }

            //计算一下新范围与旧范围之间的大小关系，算出缩小的比例（并使其不超过5）
            int beishu = (int)Math.Min(5,Math.Min(ext.Width/triExt.Width,ext.Height/triExt.Height));
            if (beishu < 1) beishu = 1;
            int newGrdXCount = gridXCount * beishu;
            int newGrdYCount = gridYCount * beishu;
            double newGridCellSizeX = gridCellSizeX / beishu;
            double newGridCellSizeY = gridCellSizeY / beishu;


            #region 根据已知点生成三角网，作为插值的依据
            List<DelaunayTriangulator.Point> triPnts = new List<DelaunayTriangulator.Point>();
            for (int i = 0; i < new_coordXLst.Count; i++)
            {
                triPnts.Add(new DelaunayTriangulator.Point(new_coordXLst[i], new_coordYLst[i], new_valLst[i]));
            }
            List<DelaunayTriangulator.Triangle> tris = null;  //三角网生成结果
            try
            {
                tris = DelaunayTriangulator.DelaunayTriangulation2d.Triangulate(triPnts);
            }
            catch (Exception ex)
            {
                //三角形插值不成功的情况下，使用最临近插值代替
            } 
            #endregion

            //由于相邻网格角点之间存在重复，为避免重复计算浪费时间，这里如果计算过了就存一下
            //由于角点数量比网格数量大1（比如一行有10个网格，那么网格角点数量是11），所以这里的数组长度是这样的
            double[,] resVals = new double[newGrdXCount + 1, newGrdYCount + 1];
            for (int i = 0; i < resVals.GetLength(0); i++)
            {
                for (int j = 0; j < resVals.GetLength(1); j++)
                {
                    resVals[i, j] = double.MinValue;
                }
            }

            //按网格的划分，逐个生成网格单元
            
            //for (int i = 0; i < gridXCount; i++)
            Parallel.For(0, newGrdXCount, i =>
            {
                for (int j = 0; j < newGrdYCount; j++)
                {
                    ContourGridBlock tmpBlock = new ContourGridBlock();
                    tmpBlock.minX = ext.MinX + i * newGridCellSizeX;
                    tmpBlock.maxX = ext.MinX + (i + 1) * newGridCellSizeX;
                    tmpBlock.minY = ext.MinY + j * newGridCellSizeY;
                    tmpBlock.maxY = ext.MinY + (j + 1) * newGridCellSizeY;

                    //查找下四个角点之前计算过了没，计算过了的话直接用，没有的话插值计算一下
                    if (resVals[i, j + 1] != double.MinValue) tmpBlock.tlValue = resVals[i, j + 1];
                    else
                    {
                        //tmpBlock.tlValue = GetPntValueByInterp(tmpBlock.minX, tmpBlock.maxY, srcCoordX, srcCoordY, srcValue, gridCellSizeX, gridCellSizeY); 
                        tmpBlock.tlValue = GetPntValueByInterp(tmpBlock.minX, tmpBlock.maxY, tris, triExt);
                        lock (resVals)
                        {
                            resVals[i, j + 1] = tmpBlock.tlValue;
                        }
                    }
                    if (resVals[i, j] != double.MinValue) tmpBlock.blValue = resVals[i, j];
                    else
                    {
                        //tmpBlock.blValue = GetPntValueByInterp(tmpBlock.minX, tmpBlock.minY, srcCoordX, srcCoordY, srcValue, gridCellSizeX, gridCellSizeY);
                        tmpBlock.blValue = GetPntValueByInterp(tmpBlock.minX, tmpBlock.minY, tris, triExt);
                        lock (resVals)
                        {
                            resVals[i, j] = tmpBlock.blValue;
                        }
                    }
                    if (resVals[i + 1, j + 1] != double.MinValue) tmpBlock.trValue = resVals[i + 1, j + 1];
                    else
                    {
                        //tmpBlock.trValue = GetPntValueByInterp(tmpBlock.maxX, tmpBlock.maxY, srcCoordX, srcCoordY, srcValue, gridCellSizeX, gridCellSizeY);
                        tmpBlock.trValue = GetPntValueByInterp(tmpBlock.maxX, tmpBlock.maxY, tris, triExt);
                        lock (resVals)
                        {
                            resVals[i + 1, j + 1] = tmpBlock.trValue;
                        }
                    }
                    if (resVals[i + 1, j] != double.MinValue) tmpBlock.brValue = resVals[i + 1, j];
                    else
                    {
                        //tmpBlock.brValue = GetPntValueByInterp(tmpBlock.maxX, tmpBlock.minY, srcCoordX, srcCoordY, srcValue, gridCellSizeX, gridCellSizeY);
                        tmpBlock.brValue = GetPntValueByInterp(tmpBlock.maxX, tmpBlock.minY, tris, triExt);
                        lock (resVals)
                        {
                            resVals[i + 1, j] = tmpBlock.brValue;
                        }
                    }
                    //tmpBlock.centerValue = GetPntValueByInterp(tmpBlock.minX + gridCellSizeX / 2, tmpBlock.minY + gridCellSizeY / 2, srcCoordX, srcCoordY, srcValue, gridCellSizeX, gridCellSizeY);
                    tmpBlock.centerValue = GetPntValueByInterp(tmpBlock.minX + gridCellSizeX / 2, tmpBlock.minY + gridCellSizeY / 2, tris, triExt);

                    //double tmpMinV = tmpBlock.blValue, tmpMaxV = tmpBlock.blValue;
                    //if (tmpBlock.brValue < tmpMinV) tmpMinV = tmpBlock.brValue;
                    //if (tmpBlock.brValue > tmpMaxV) tmpMaxV = tmpBlock.brValue;
                    //if (tmpBlock.trValue < tmpMinV) tmpMinV = tmpBlock.trValue;
                    //if (tmpBlock.trValue > tmpMaxV) tmpMaxV = tmpBlock.trValue;
                    //if (tmpBlock.tlValue < tmpMinV) tmpMinV = tmpBlock.tlValue;
                    //if (tmpBlock.tlValue > tmpMaxV) tmpMaxV = tmpBlock.tlValue;

                    //if (tmpMaxV > tmpMinV * 3)
                    //{
                    //    for (int u = 0; u < beishu; u++)
                    //    {
                    //        for (int v = 0; v < beishu; v++)
                    //        {
                    //            ContourGridBlock tmpBlock2 = new ContourGridBlock();
                    //            tmpBlock2.minX = ext.MinX + i * gridCellSizeX+ u*gridCellSizeX/beishu;
                    //            tmpBlock2.maxX = ext.MinX + i * gridCellSizeX + (u+1) * gridCellSizeX / beishu; 
                    //            tmpBlock2.minY = ext.MinY + j * gridCellSizeY + v*gridCellSizeY/beishu;
                    //            tmpBlock2.maxY = ext.MinY + j * gridCellSizeY + (v+1)*gridCellSizeY/beishu;

                    //            tmpBlock2.tlValue = GetPntValueByInterp(tmpBlock2.minX, tmpBlock2.maxY, tris, triExt);
                    //            tmpBlock2.trValue = GetPntValueByInterp(tmpBlock2.maxX, tmpBlock2.maxY, tris, triExt);
                    //            tmpBlock2.brValue = GetPntValueByInterp(tmpBlock2.maxX, tmpBlock2.minY, tris, triExt);
                    //            tmpBlock2.blValue = GetPntValueByInterp(tmpBlock2.minX, tmpBlock2.minY, tris, triExt);
                    //            tmpBlock2.centerValue = GetPntValueByInterp((tmpBlock2.minX + tmpBlock2.maxX) / 2, (tmpBlock2.minY + tmpBlock2.maxY) / 2, tris, triExt);

                    //            newBlockLst.Add(tmpBlock2);
                    //        }
                    //    }
                    //}
                    //else
                    lock (newBlockLst)
                    {
                        newBlockLst.Add(tmpBlock);
                    }


                    ////将blocks梳理一下，挑选出内部比较复杂的子块，进行一下拆分
                    //List<ContourGridBlock> newBlocks = new List<ContourGridBlock>();
                    //for (int i = 0; i < blocks.Count; i++)
                    //{
                    //    int minIndex = 0, maxIndex = 0;

                    //    //分别计算四个角点的断点序号，计算其最大的差值，以确定当前Block内部的复杂程序
                    //    for (int j = 1; j < expandBandLevels.Count - 1; j++)
                    //    {
                    //        if (blocks[i].tlValue >= expandBandLevels[j] && blocks[i].tlValue < expandBandLevels[j + 1])
                    //        {
                    //            minIndex = j; maxIndex = j;
                    //            break;
                    //        }
                    //    }

                    //    for (int j = 1; j < expandBandLevels.Count - 1; j++)
                    //    {
                    //        if (blocks[i].trValue >= expandBandLevels[j] && blocks[i].trValue < expandBandLevels[j + 1])
                    //        {
                    //            if (j < minIndex) minIndex = j;
                    //            if (j > maxIndex) maxIndex = j;
                    //            break;
                    //        }
                    //    }
                    //    for (int j = 1; j < expandBandLevels.Count - 1; j++)
                    //    {
                    //        if (blocks[i].blValue >= expandBandLevels[j] && blocks[i].blValue < expandBandLevels[j + 1])
                    //        {
                    //            if (j < minIndex) minIndex = j;
                    //            if (j > maxIndex) maxIndex = j;
                    //            break;
                    //        }
                    //    }


                    //    for (int j = 1; j < expandBandLevels.Count - 1; j++)
                    //    {
                    //        if (blocks[i].brValue >= expandBandLevels[j] && blocks[i].brValue < expandBandLevels[j + 1])
                    //        {
                    //            if (j < minIndex) minIndex = j;
                    //            if (j > maxIndex) maxIndex = j;
                    //            break;
                    //        }
                    //    }

                    //    if (maxIndex - minIndex <= 3)
                    //    {
                    //        newBlocks.Add(blocks[i]);
                    //    }
                    //    else
                    //    {

                    //    }

                    //}
                }
            });

            return newBlockLst;
        }

        public static int beishu  = 4;

        /// <summary>
        /// 根据建立的三角网，插值计算用户指定位置处的浓度值
        /// </summary>
        /// <param name="inputX">待计算位置的X坐标值</param>
        /// <param name="inputY">待计算位置的Y坐标值</param>
        /// <param name="tris">生成的三角网</param>
        /// <param name="triExt">三角网有效区域的范围，该范围之外的浓度为0</param>
        /// <returns>插值结果</returns>
        public static double GetPntValueByInterp(double inputX, double inputY, List<DelaunayTriangulator.Triangle> tris, ContourExtent triExt)
        {
            if (inputX < triExt.MinX || inputX > triExt.MaxX || inputY < triExt.MinY || inputY > triExt.MaxY)
                return 0;

            for (int i = 0; i < tris.Count; i++)
            {
                double u, v;
                if (tris[i].CalInterpoPara(inputX, inputY, out u, out v))
                {
                    //如果在三角形内，则根据重心坐标u,v进行插值
                    double tmpRes = (1 - v - u) * tris[i].Vertex1.Z + u * tris[i].Vertex2.Z + v * tris[i].Vertex3.Z;
                    return tmpRes;
                }
            }
            return 0;
        }

        /// <summary>
        /// 根据输入的已知离散点坐标以及对应的浓度值，插值计算用户指定位置处的浓度值(原版本备份)
        /// </summary>
        /// <param name="inputX">待计算位置的X坐标值</param>
        /// <param name="inputY">待计算位置的Y坐标值</param>
        /// <param name="srcCoordX">已知离散点的X坐标数组</param>
        /// <param name="srcCoordY">已知离散点的Y坐标数组</param>
        /// <param name="srcValue">已知离散点的浓度值</param>
        /// <param name="gridCellSizeX">待插值生成网格X方向的网格单元大小（用于从离散点集中查找相近点的子集，用该子集作为插值计算依据）</param>
        /// <param name="gridCellSizeY">待插值生成网格Y方向的网格单元大小（用于从离散点集中查找相近点的子集，用该子集作为插值计算依据）</param>
        /// <returns></returns>
        public static double GetPntValueByInterp(double inputX, double inputY, List<double> srcCoordX, List<double> srcCoordY, List<double> srcValue, double gridCellSizeX, double gridCellSizeY)
        {
            return 0;

            //double res = 0;
            //double tmpExtentSize = extentSize;         //从用户指定位置向外扩几个网格单元大小，以搜索插值计算的依据
            //List<int> srcIndexs = null;      //搜索到的插值依据点的序号

            //do
            //{
            //    tmpExtentSize+=0.5;

            //    srcIndexs = new List<int>();
            //    //向外扩extentSize个网格大小，并在此范围内找插值源点
            //    double tmpMinX = inputX - tmpExtentSize * gridCellSizeX;
            //    double tmpMaxX = inputX + tmpExtentSize * gridCellSizeX;
            //    double tmpMinY = inputY - tmpExtentSize * gridCellSizeY;
            //    double tmpMaxY = inputY + tmpExtentSize * gridCellSizeY;

            //    for (int i = 0; i < srcCoordX.Count; i++)
            //    {
            //        if (srcCoordX[i] >= tmpMinX && srcCoordX[i] <= tmpMaxX && srcCoordY[i] >= tmpMinY && srcCoordY[i] <= tmpMaxY)
            //            srcIndexs.Add(i);
            //    }

            //    //如果找到的点少于3个，那么再往外扩一个网格大小
            //}
            //while (srcIndexs.Count < 3);

            //List<double> tmpX = new List<double>();
            //List<double> tmpY = new List<double>();
            //List<double> tmpZ = new List<double>();

            ////将找到的插值源点存入一个临时列表中
            //for (int i = 0; i < srcIndexs.Count; i++)
            //{
            //    tmpX.Add(srcCoordX[srcIndexs[i]]);
            //    tmpY.Add(srcCoordY[srcIndexs[i]]);
            //    tmpZ.Add(srcValue[srcIndexs[i]]);
            //}

            ////return Interpolate(tmpX, tmpY, tmpZ, inputX, inputY, 1e-2);
            //return InterpolateTriangle(tmpX, tmpY, tmpZ, inputX, inputY, 1e-2);
        }

        #region 原有的距离反比平方插值
      

        /// <summary>距离平方反比加权插值法(将所有已知点作为插值输入)</summary>
        /// <param name="x">已知点列的X坐标</param>
        /// <param name="y">已知点列的Y坐标</param>
        /// <param name="z">已知点列的值</param>
        /// <param name="X">插值点的X坐标</param>
        /// <param name="Y">插值点的Y坐标</param>
        /// <param name="thres">插值时，距离多少之内就可以算重合，不插值直接返回</param>
        /// <returns>插值结果</returns>
        public static double Interpolate(List<double> x, List<double> y, List<double> z, double X, double Y, double thres)
        {
            if (x.Count < 3 || x.Count != y.Count || x.Count != z.Count)
                return 0;

            int srcCount = x.Count;

            double Z = 0;


            bool find = false;
            List<double> disLst = new List<double>();
            for (int j = 0; j < srcCount; j++)
            {
                double tmpDis = Math.Pow(X - x[j], 2) + Math.Pow(Y - y[j], 2);
                disLst.Add(tmpDis);
                if (tmpDis < Math.Pow(thres, 2))          //重合点
                {
                    Z = z[j];
                    find = true;
                    break;
                }
            }
            if (find) return Z;

            double valsum = 0, dissum = 0;
            for (int j = 0; j < disLst.Count; j++)
            {
                //if (z[j] > 1 && disLst[j] < 4e-4)
                //{
                //    int asfd = 1;
                //    asfd++;
                //}

                valsum += z[j] / disLst[j];
                dissum += 1 / disLst[j];
            }
            Z = valsum / dissum;

            return Z;
        }

        /// <summary>
        /// 最临近算法进行插值，即取离待插值点最近的已知点浓度作为待插值点的浓度
        /// </summary>
        /// <param name="x">已知点列的X坐标</param>
        /// <param name="y">已知点列的Y坐标</param>
        /// <param name="z">已知点列的值</param>
        /// <param name="X">插值点的X坐标</param>
        /// <param name="Y">插值点的Y坐标</param>
        /// <param name="thres">最临近算法中，该参数无用</param>
        /// <returns></returns>
        public static double InterpolateNearestNeighbor(List<double> x, List<double> y, List<double> z, double X, double Y, double thres)
        {
            if (x.Count == 0) return 0;

            if (x.Count == 1) return z[0];

            int tmpIndex = -1;
            double minDist = double.MaxValue;

            for (int i = 0; i < x.Count; i++)
            {
                double tmpDis = Math.Pow(X - x[i], 2) + Math.Pow(Y - y[i], 2);

                if (tmpDis < minDist)
                {
                    minDist = tmpDis;
                    tmpIndex = i;
                }
            }

            return z[tmpIndex];
        }

        /// <summary>使用三角网插值算法进行插值</summary>
        /// <param name="x">已知点列的X坐标</param>
        /// <param name="y">已知点列的Y坐标</param>
        /// <param name="z">已知点列的值</param>
        /// <param name="X">插值点的X坐标</param>
        /// <param name="Y">插值点的Y坐标</param>
        /// <param name="thres">插值时，距离多少之内就可以算重合，不插值直接返回</param>
        /// <returns>插值结果</returns>
        public static double InterpolateTriangle(List<double> x, List<double> y, List<double> z, double X, double Y, double thres)
        {
            if (x.Count < 3 || x.Count != y.Count || x.Count != z.Count)
                return 0;

            bool allsame = true;   //是否输入的值都相同
            double tmpV = z[0]; 
            for (int i = 1; i < z.Count; i++)
            {
                if (z[i] != tmpV)
                {
                    allsame = false;
                    break;
                }
            }
            if (allsame) return tmpV;
            
            #region 找一下有没有与已经点重点的情况，如果有，则直接返回重合已经点的坐标
            for (int j = 0; j < x.Count; j++)
            {
                if (Math.Abs(X - x[j]) < thres && Math.Abs(Y - y[j]) < thres)
                {
                    return z[j];
                }
            }
            #endregion

            #region 生成德劳内三角网
            List<DelaunayTriangulator.Point> triPnts = new List<DelaunayTriangulator.Point>();
            for (int i = 0; i < x.Count; i++)
            {
                triPnts.Add(new DelaunayTriangulator.Point(x[i], y[i], z[i])); 
            }

            List<DelaunayTriangulator.Triangle> tris = null;  //三角网生成结果
            try
            {
                 tris = DelaunayTriangulator.DelaunayTriangulation2d.Triangulate(triPnts);
            }
            catch (Exception ex)
            {
                //三角形插值不成功的情况下，使用最临近插值代替
                return InterpolateNearestNeighbor(x, y, z, X, Y, thres);
            } 
            #endregion

            for (int i = 0; i < tris.Count; i++)
            {
                double u,v;
                if (tris[i].CalInterpoPara(X, Y, out u, out v))
                { 
                    //如果在三角形内，则根据重心坐标u,v进行插值
                    double tmpRes = (1 - v - u) * tris[i].Vertex1.Z + u * tris[i].Vertex2.Z + v * tris[i].Vertex3.Z;
                    return tmpRes;
                }
            }

            //如果三角形插值没有成功，则使用最临近插值代替
            return InterpolateNearestNeighbor(x, y, z, X, Y, thres);
        }
        #endregion

        #endregion

        #region 第二步：根据生成的规律网格，在每个网格的内部生成零碎的等值线
        /// <summary>
        /// 根据输入的网格，生成等值线线段
        /// </summary>
        /// <param name="blks">输入的网格单元列表</param>
        /// <param name="bandLevelBPs">断点值列表</param>
        /// <param name="outExtent">网格范围</param>
        /// <returns>生成的等值线线段列表（Dictionary格式，Key为断点序号，Value为等值线线段列表）</returns>
        public static Dictionary<int,List<ContourLineSegment>> GenSegments(List<ContourGridBlock> blks, List<double> bandLevelBPs, ContourExtent outExtent)
        {
            Dictionary<int, List<ContourLineSegment>> resDic = new Dictionary<int, List<ContourLineSegment>>();

            //这里与等值面不同，等值面是针对每两个断点之间的区间进行生成，等值线是针对每个断点的值进行生成，所以不需要在断点的两边各加一个值了
            for (int i = 0; i < bandLevelBPs.Count; i++)
            {
                List<ContourLineSegment> resSegments = new List<ContourLineSegment>();      //当前断点对应的等值线线段集合

                foreach (ContourGridBlock currentBlk in blks)
                {
                    //超出范围的不渲染
                    if (currentBlk.maxX < outExtent.MinX || currentBlk.minX > outExtent.MaxX || currentBlk.maxY < outExtent.MinY || currentBlk.minY > outExtent.MaxY)
                        continue;

                    if (currentBlk.tlValue == 0 && currentBlk.trValue == 0 && currentBlk.brValue == 0 && currentBlk.blValue == 0)
                        continue;       //全是0的不用处理了

                    List<double> pntVals = new List<double>() { currentBlk.tlValue, currentBlk.trValue, currentBlk.brValue, currentBlk.blValue };

                    double pnt1Val = pntVals[0];
                    double pnt2Val = pntVals[1];
                    double pnt3Val = pntVals[2];
                    double pnt4Val = pntVals[3];

                    //计算四个角点及中心点的Level代码(大于upperValue为2，小于lowerValue为0，两者之间为1)
                    int pnt1Code = CalLevelCode(pnt1Val, bandLevelBPs[i]);
                    int pnt2Code = CalLevelCode(pnt2Val, bandLevelBPs[i]);
                    int pnt3Code = CalLevelCode(pnt3Val, bandLevelBPs[i]);
                    int pnt4Code = CalLevelCode(pnt4Val, bandLevelBPs[i]);
                    int centerCode = CalLevelCode(currentBlk.centerValue, bandLevelBPs[i]);

                    string tmpCornerCode = "" + pnt1Code + pnt2Code + pnt3Code + pnt4Code;      //左上，右上，右下，左下的顺序
                    //List<PointF[]> currentPolygonPntLsts = GetPolygonByCode(tmpCornerCode, centerCode, pntVals, lowerValue, upperValue, currentBlkMinPixelX, currentBlkMinPixelY, currentBlkPixelsInWidth, currentBlkPixelsInHeight);
                    List<ContourLineSegment> segs = GetLineSegByCode(tmpCornerCode, centerCode, i,bandLevelBPs, currentBlk);
                    resSegments.AddRange(segs); 
                }
                resDic.Add(i, resSegments);

            }
            return resDic;
        }

        /// <summary>
        /// 根据输入的值与断点值，判断两者之间的相互关系，并返回相应的代码
        /// </summary>
        /// <param name="inputValue">输入值</param>
        /// <param name="breakValue">断点值</param>
        /// <returns>代码，小于为0，大于为1</returns>
        public static int CalLevelCode(double inputValue, double breakValue)
        {
            if (inputValue < breakValue) return 0;
            else return 1;
        }

        /// <summary>
        /// 根据网格单元的空间位置，以及断点值等参数，根据算法生成出该网格单元内的等值线线段
        /// </summary>
        /// <param name="codeString">四角点的代码</param>
        /// <param name="centerCode">中心点的代码</param>
        /// <param name="level">等级</param>
        /// <param name="bandLevelBPs">断点值列表</param>
        /// <param name="blk">网格单元</param>
        /// <returns>等值线线段或线段列表</returns>
        public static List<ContourLineSegment> GetLineSegByCode(string codeString, int centerCode, int level,List<double> bandLevelBPs, ContourGridBlock blk)
        {
            List<ContourLineSegment> resLst = new List<ContourLineSegment>();
            ContourLineSegment tmpLineSeg = null;

            switch (codeString)
            {
                #region 0000和1111两种情况，没有等值线段
                case "0000":
                    return resLst;
                case "1111":
                    return resLst; 
                #endregion

                #region 12种情况，只有一条等值线线段
                case  "1110":
                    tmpLineSeg = new ContourLineSegment();
                    tmpLineSeg.x1 = blk.minX;
                    tmpLineSeg.y1 = blk.minY + (blk.maxY - blk.minY) * (bandLevelBPs[level] - blk.blValue) / (blk.tlValue - blk.blValue);
                    tmpLineSeg.x2 = blk.minX + (blk.maxX - blk.minX) * (bandLevelBPs[level] - blk.blValue) / (blk.brValue - blk.blValue);
                    tmpLineSeg.y2 = blk.minY;
                    tmpLineSeg.level = level;
                    resLst.Add(tmpLineSeg);
                    return resLst;
                case "1101":
                    tmpLineSeg = new ContourLineSegment();
                    tmpLineSeg.x1 = blk.minX + (blk.maxX - blk.minX) * (bandLevelBPs[level] - blk.blValue) / (blk.brValue - blk.blValue);
                    tmpLineSeg.y1 = blk.minY;
                    tmpLineSeg.x2 = blk.maxX;
                    tmpLineSeg.y2 = blk.minY + (blk.maxY - blk.minY) * (bandLevelBPs[level] - blk.brValue) / (blk.trValue - blk.brValue);
                    tmpLineSeg.level = level;
                    resLst.Add(tmpLineSeg); 
                    return resLst;
                case "1011":
                    tmpLineSeg = new ContourLineSegment();
                    tmpLineSeg.x1 = blk.minX + (blk.maxX - blk.minX) * (bandLevelBPs[level] - blk.tlValue) / (blk.trValue - blk.tlValue);
                    tmpLineSeg.y1 = blk.maxY;
                    tmpLineSeg.x2 = blk.maxX;
                    tmpLineSeg.y2 = blk.minY + (blk.maxY - blk.minY) * (bandLevelBPs[level] - blk.brValue) / (blk.trValue - blk.brValue);
                    tmpLineSeg.level = level;
                    resLst.Add(tmpLineSeg);
                    return resLst;
                case "0111":
                    tmpLineSeg = new ContourLineSegment();
                    tmpLineSeg.x1 = blk.minX;
                    tmpLineSeg.y1 = blk.minY + (blk.maxY - blk.minY) * (bandLevelBPs[level] - blk.blValue) / (blk.tlValue - blk.blValue);
                    tmpLineSeg.x2 = blk.minX + (blk.maxX - blk.minX) * (bandLevelBPs[level] - blk.tlValue) / (blk.trValue - blk.tlValue);
                    tmpLineSeg.y2 = blk.maxY;
                    tmpLineSeg.level = level;
                    resLst.Add(tmpLineSeg);
                    return resLst;
                case "0001":
                    tmpLineSeg = new ContourLineSegment();
                    tmpLineSeg.x1 = blk.minX;
                    tmpLineSeg.y1 = blk.minY + (blk.maxY - blk.minY) * (bandLevelBPs[level] - blk.blValue) / (blk.tlValue - blk.blValue);
                    tmpLineSeg.x2 = blk.minX + (blk.maxX - blk.minX) * (bandLevelBPs[level] - blk.blValue) / (blk.brValue - blk.blValue);
                    tmpLineSeg.y2 = blk.minY;
                    tmpLineSeg.level = level;
                    resLst.Add(tmpLineSeg);
                    return resLst;
                case "0010":
                    tmpLineSeg = new ContourLineSegment();
                    tmpLineSeg.x1 = blk.minX + (blk.maxX - blk.minX) * (bandLevelBPs[level] - blk.blValue) / (blk.brValue - blk.blValue);
                    tmpLineSeg.y1 = blk.minY;
                    tmpLineSeg.x2 = blk.maxX;
                    tmpLineSeg.y2 = blk.minY + (blk.maxY - blk.minY) * (bandLevelBPs[level] - blk.brValue) / (blk.trValue - blk.brValue);
                    tmpLineSeg.level = level;
                    resLst.Add(tmpLineSeg);
                    return resLst;
                case "0100":
                    tmpLineSeg = new ContourLineSegment();
                    tmpLineSeg.x1 = blk.minX + (blk.maxX - blk.minX) * (bandLevelBPs[level] - blk.tlValue) / (blk.trValue - blk.tlValue);
                    tmpLineSeg.y1 = blk.maxY;
                    tmpLineSeg.x2 = blk.maxX;
                    tmpLineSeg.y2 = blk.minY + (blk.maxY - blk.minY) * (bandLevelBPs[level] - blk.brValue) / (blk.trValue - blk.brValue);
                    tmpLineSeg.level = level;
                    resLst.Add(tmpLineSeg);
                    return resLst;
                case "1000":
                    tmpLineSeg = new ContourLineSegment();
                    tmpLineSeg.x1 = blk.minX;
                    tmpLineSeg.y1 = blk.minY + (blk.maxY - blk.minY) * (bandLevelBPs[level] - blk.blValue) / (blk.tlValue - blk.blValue);
                    tmpLineSeg.x2 = blk.minX + (blk.maxX - blk.minX) * (bandLevelBPs[level] - blk.tlValue) / (blk.trValue - blk.tlValue);
                    tmpLineSeg.y2 = blk.maxY;
                    tmpLineSeg.level = level;
                    resLst.Add(tmpLineSeg);
                    return resLst;
                case "1100":
                    tmpLineSeg = new ContourLineSegment();
                    tmpLineSeg.x1 = blk.minX;
                    tmpLineSeg.y1 = blk.minY + (blk.maxY - blk.minY) * (bandLevelBPs[level] - blk.blValue) / (blk.tlValue - blk.blValue);
                    tmpLineSeg.x2 = blk.maxX;
                    tmpLineSeg.y2 = blk.minY + (blk.maxY - blk.minY) * (bandLevelBPs[level] - blk.brValue) / (blk.trValue - blk.brValue);
                    tmpLineSeg.level = level;
                    resLst.Add(tmpLineSeg);
                    return resLst;
                case "1001":
                    tmpLineSeg = new ContourLineSegment();
                    tmpLineSeg.x1 = blk.minX + (blk.maxX - blk.minX) * (bandLevelBPs[level] - blk.blValue) / (blk.brValue - blk.blValue);
                    tmpLineSeg.y1 = blk.minY;
                    tmpLineSeg.x2 = blk.minX + (blk.maxX - blk.minX) * (bandLevelBPs[level] - blk.tlValue) / (blk.trValue - blk.tlValue);
                    tmpLineSeg.y2 = blk.maxY;
                    tmpLineSeg.level = level;
                    resLst.Add(tmpLineSeg);
                    return resLst;
                case "0011":
                    tmpLineSeg = new ContourLineSegment();
                    tmpLineSeg.x1 = blk.minX;
                    tmpLineSeg.y1 = blk.minY + (blk.maxY - blk.minY) * (bandLevelBPs[level] - blk.blValue) / (blk.tlValue - blk.blValue);
                    tmpLineSeg.x2 = blk.maxX;
                    tmpLineSeg.y2 = blk.minY + (blk.maxY - blk.minY) * (bandLevelBPs[level] - blk.brValue) / (blk.trValue - blk.brValue);
                    tmpLineSeg.level = level;
                    resLst.Add(tmpLineSeg);
                    return resLst;
                case "0110":
                    tmpLineSeg = new ContourLineSegment();
                    tmpLineSeg.x1 = blk.minX + (blk.maxX - blk.minX) * (bandLevelBPs[level] - blk.blValue) / (blk.brValue - blk.blValue);
                    tmpLineSeg.y1 = blk.minY;
                    tmpLineSeg.x2 = blk.minX + (blk.maxX - blk.minX) * (bandLevelBPs[level] - blk.tlValue) / (blk.trValue - blk.tlValue);
                    tmpLineSeg.y2 = blk.maxY;
                    tmpLineSeg.level = level;
                    resLst.Add(tmpLineSeg);
                    return resLst; 
                #endregion

                #region 剩余两种情况，有两条等值线
                case "1010":
                    if (centerCode == 0)
                    {
                        tmpLineSeg = new ContourLineSegment();
                        tmpLineSeg.x1 = blk.minX;
                        tmpLineSeg.y1 = blk.minY + (blk.maxY - blk.minY) * (bandLevelBPs[level] - blk.blValue) / (blk.tlValue - blk.blValue);
                        tmpLineSeg.x2 = blk.minX + (blk.maxX - blk.minX) * (bandLevelBPs[level] - blk.tlValue) / (blk.trValue - blk.tlValue);
                        tmpLineSeg.y2 = blk.maxY;
                        tmpLineSeg.level = level;
                        resLst.Add(tmpLineSeg);
                        tmpLineSeg = new ContourLineSegment();
                        tmpLineSeg.x1 = blk.minX + (blk.maxX - blk.minX) * (bandLevelBPs[level] - blk.blValue) / (blk.brValue - blk.blValue);
                        tmpLineSeg.y1 = blk.minY;
                        tmpLineSeg.x2 = blk.maxX;
                        tmpLineSeg.y2 = blk.minY + (blk.maxY - blk.minY) * (bandLevelBPs[level] - blk.brValue) / (blk.trValue - blk.brValue);
                        tmpLineSeg.level = level;
                        resLst.Add(tmpLineSeg);
                        return resLst;
                    }
                    else if (centerCode == 1)
                    {
                        tmpLineSeg = new ContourLineSegment();
                        tmpLineSeg.x1 = blk.minX;
                        tmpLineSeg.y1 = blk.minY + (blk.maxY - blk.minY) * (bandLevelBPs[level] - blk.blValue) / (blk.tlValue - blk.blValue);
                        tmpLineSeg.x2 = blk.minX + (blk.maxX - blk.minX) * (bandLevelBPs[level] - blk.blValue) / (blk.brValue - blk.blValue);
                        tmpLineSeg.y2 = blk.minY;
                        tmpLineSeg.level = level;
                        resLst.Add(tmpLineSeg);
                        tmpLineSeg = new ContourLineSegment();
                        tmpLineSeg.x1 = blk.minX + (blk.maxX - blk.minX) * (bandLevelBPs[level] - blk.tlValue) / (blk.trValue - blk.tlValue);
                        tmpLineSeg.y1 = blk.maxY;
                        tmpLineSeg.x2 = blk.maxX;
                        tmpLineSeg.y2 = blk.minY + (blk.maxY - blk.minY) * (bandLevelBPs[level] - blk.brValue) / (blk.trValue - blk.brValue);
                        tmpLineSeg.level = level;
                        resLst.Add(tmpLineSeg);
                        return resLst;
                    }
                    else return resLst;
                case "0101":
                    if (centerCode == 0)
                    {
                        tmpLineSeg = new ContourLineSegment();
                        tmpLineSeg.x1 = blk.minX;
                        tmpLineSeg.y1 = blk.minY + (blk.maxY - blk.minY) * (bandLevelBPs[level] - blk.blValue) / (blk.tlValue - blk.blValue);
                        tmpLineSeg.x2 = blk.minX + (blk.maxX - blk.minX) * (bandLevelBPs[level] - blk.blValue) / (blk.brValue - blk.blValue);
                        tmpLineSeg.y2 = blk.minY;
                        tmpLineSeg.level = level;
                        resLst.Add(tmpLineSeg);
                        tmpLineSeg = new ContourLineSegment();
                        tmpLineSeg.x1 = blk.minX + (blk.maxX - blk.minX) * (bandLevelBPs[level] - blk.tlValue) / (blk.trValue - blk.tlValue);
                        tmpLineSeg.y1 = blk.maxY;
                        tmpLineSeg.x2 = blk.maxX;
                        tmpLineSeg.y2 = blk.minY + (blk.maxY - blk.minY) * (bandLevelBPs[level] - blk.brValue) / (blk.trValue - blk.brValue);
                        tmpLineSeg.level = level;
                        resLst.Add(tmpLineSeg);
                        return resLst;
                    }
                    else if (centerCode == 1)
                    {
                        tmpLineSeg = new ContourLineSegment();
                        tmpLineSeg.x1 = blk.minX;
                        tmpLineSeg.y1 = blk.minY + (blk.maxY - blk.minY) * (bandLevelBPs[level] - blk.blValue) / (blk.tlValue - blk.blValue);
                        tmpLineSeg.x2 = blk.minX + (blk.maxX - blk.minX) * (bandLevelBPs[level] - blk.tlValue) / (blk.trValue - blk.tlValue);
                        tmpLineSeg.y2 = blk.maxY;
                        tmpLineSeg.level = level;
                        resLst.Add(tmpLineSeg);
                        tmpLineSeg = new ContourLineSegment();
                        tmpLineSeg.x1 = blk.minX + (blk.maxX - blk.minX) * (bandLevelBPs[level] - blk.blValue) / (blk.brValue - blk.blValue);
                        tmpLineSeg.y1 = blk.minY;
                        tmpLineSeg.x2 = blk.maxX;
                        tmpLineSeg.y2 = blk.minY + (blk.maxY - blk.minY) * (bandLevelBPs[level] - blk.brValue) / (blk.trValue - blk.brValue);
                        tmpLineSeg.level = level;
                        resLst.Add(tmpLineSeg);
                        return resLst;
                    }
                    else return resLst; 
                #endregion
            }

            return resLst;
        }
        #endregion

        #region 第三步：将邻接的等值线连接起来,构成一条条闭合或不闭合的等值线
        public static List<ContourLine> GetLinesFromSegs(Dictionary<int, List<ContourLineSegment>> segDic,double thres)
        {
            List<ContourLine> contourLines = new List<ContourLine>();

            foreach (int currentLevel in segDic.Keys)
            {
                List<ContourLineSegment> currentSegs = segDic[currentLevel];    //当前等级的所有线段
                int remainingSegCount = currentSegs.Count;//当前还剩余没有拼接的线段数量

                Dictionary<int, bool> indexIsUsed = new Dictionary<int, bool>();    //检查currentSegs列表里，哪些序号已经连接过了，哪些还没有，Key为列表序号，Value为是否已经连接
                for (int i = 0; i < currentSegs.Count; i++) //先把所有的序号都设置为false
                { indexIsUsed.Add(i, false); }
                
                while (remainingSegCount>0)     //把所有线段都拼到等值线中，循环才结束
                {
                    List<double> leftLstX = new List<double>();
                    List<double> leftLstY = new List<double>();
                    List<double> rightLstX = new List<double>(); 
                    List<double> rightLstY = new List<double>();    
                    //这里使用了两个Lst，分别将第一个线段的左右两个端点存入,并逐个将连接的线段端点坐标附加到列表后方
                    //其实也可以用一个Lst，这里这么用是为了尝试避免List.Insert带来的效率下降

                    for (int i = 0; i < currentSegs.Count; i++)
                    {
                        if (indexIsUsed[i]) continue;   //已经连接过的，跳过

                        if (leftLstX.Count == 0)
                        {
                            #region 第一点，将坐标存到两个列表的头上
                            leftLstX.Add(currentSegs[i].x1);
                            leftLstY.Add(currentSegs[i].y1);
                            rightLstX.Add(currentSegs[i].x2);
                            rightLstY.Add(currentSegs[i].y2);
                            indexIsUsed[i] = true;
                            remainingSegCount--;
                            #endregion
                        }
                        else
                        {
                            #region 如果不是第一点，那么对比当前的起点和终点，看是否有其中一点与leftLst或rightLst的最后一点相同,如果相同则将另一点附加到对应的列表后方
                            //起点是否与leftLst最后一点相同
                            if (Math.Abs(currentSegs[i].x1 - leftLstX[leftLstX.Count - 1]) <= thres &&
                                Math.Abs(currentSegs[i].y1 - leftLstY[leftLstY.Count - 1]) <= thres)
                            //if (currentSegs[i].x1 == leftLstX[leftLstX.Count - 1] &&
                            //    currentSegs[i].y1 == leftLstY[leftLstY.Count - 1])
                            {
                                leftLstX.Add(currentSegs[i].x2);
                                leftLstY.Add(currentSegs[i].y2);
                                indexIsUsed[i] = true;
                                remainingSegCount--;
                                i = 0;      //i设为0是为了处理这种情况：第1点与第3点相交，第3点与第2点相交，如果不设为0的话，第二点就漏过去了
                            }

                            //终点是否与leftLst最后一点相同
                            else if (Math.Abs(currentSegs[i].x2 - leftLstX[leftLstX.Count - 1]) <= thres &&
                               Math.Abs(currentSegs[i].y2 - leftLstY[leftLstY.Count - 1]) <= thres)
                            {
                                leftLstX.Add(currentSegs[i].x1);
                                leftLstY.Add(currentSegs[i].y1);
                                indexIsUsed[i] = true;
                                remainingSegCount--;
                                i = 0;
                            }

                            //起点是否与rightLst最后一点相同
                            else if (Math.Abs(currentSegs[i].x1 - rightLstX[rightLstX.Count - 1]) <= thres &&
                               Math.Abs(currentSegs[i].y1 - rightLstY[rightLstY.Count - 1]) <= thres)
                            {
                                rightLstX.Add(currentSegs[i].x2);
                                rightLstY.Add(currentSegs[i].y2);
                                indexIsUsed[i] = true;
                                remainingSegCount--;
                                i = 0;
                            }

                            //终点是否与rightLst最后一点相同
                            else if (Math.Abs(currentSegs[i].x2 - rightLstX[rightLstX.Count - 1]) <= thres &&
                               Math.Abs(currentSegs[i].y2 - rightLstY[rightLstY.Count - 1]) <= thres)
                            {
                                rightLstX.Add(currentSegs[i].x1);
                                rightLstY.Add(currentSegs[i].y1);
                                indexIsUsed[i] = true;
                                remainingSegCount--;
                                i = 0;
                            }
                            #endregion
                        }
                    }

                    //将两个列表合并到一起
                    rightLstX.Reverse();
                    //leftLstX.AddRange(rightLstX);
                    rightLstX.AddRange(leftLstX);
                    rightLstY.Reverse();
                    //leftLstY.AddRange(rightLstY);
                    rightLstY.AddRange(leftLstY);
                         
                    //根据新列表生成一条等值线
                    ContourLine tmpLine = new ContourLine();
                    tmpLine.level = currentLevel;
                    tmpLine.xCoords = rightLstX;
                    tmpLine.yCoords = rightLstY;
                    contourLines.Add(tmpLine);
                }
            }
            return contourLines;
        }
        #endregion

        #region 第四步，等值线平滑
        /// <summary>
        /// 等值线平滑
        /// </summary>
        /// <param name="inputLines">输入的等值线列表</param>
        /// <param name="ratio">平滑比例（在原等值线的两点之间插入多少个中间点）</param>
        /// <returns>平滑后的等值线列表</returns>
        public static List<ContourLine> SmoothLines(List<ContourLine> inputLines, int ratio = 5)
        {
            #region 等值线平滑
            //List<ContourLine> outputLines = new List<ContourLine>();
            //for (int i = 0; i < inputLines.Count; i++)
            //{
            //    outputLines.Add(Smooth(inputLines[i] , ratio));
            //} 
            for (int i = 0; i < inputLines.Count; i++)
            {
                inputLines[i] = Smooth(inputLines[i], ratio);
            }
            #endregion

            #region 在等值线平滑的同时，先构建一下闭合等值线之间的彼此包含关系（使用树状结构表示），一个是因为平滑前点少，进行空间关系判断时较快，另外一个原因是平滑操作可能会改变等值线围成的多边形之间的拓扑关系，导致关系混乱
            ////先把闭合等值线挑出来
            //List<ContourLine> closedLines = new List<ContourLine>();
            //List<int> closedLineIdx = new List<int>();          //闭合等值线的序号，用于查找
            //for (int i = 0; i < inputLines.Count; i++)
            //{
            //    if (inputLines[i].isClosedRing)
            //    {
            //        closedLines.Add(inputLines[i]);
            //        closedLineIdx.Add(i);
            //    }
            //}

            ////将闭合等值线按照空间包含关系，构建成树状结构（不构建成列表，是因为一个多边形内可能包含多个下一级多边形）
            ////由于可能有多个树装关系，因此这里声明的是List列表
            ////每个列表的元素均表示一个树状结构的根结点，其内部属性中包含其子结点及更深层的节点
            //rootNodeLst = new List<PolygonTreeNode>();

            //for (int i = 0; i < closedLines.Count; i++)
            //{
            //    ContourLine currentLine = closedLines[i];

            //    #region 先构建当前等值线对应的多边形
            //    List<Coordinate> coordList = new List<Coordinate>();
            //    for (int k = 0; k < currentLine.pntCount; k++)
            //    {
            //        coordList.Add(new Coordinate(currentLine.xCoords[k], currentLine.yCoords[k]));
            //    }
            //    Polygon currentPolygon = new Polygon(coordList);
            //    if (!currentPolygon.IsValid)
            //    {
            //        int aaa = 1;
            //        aaa++;
            //    }
            //    #endregion

            //    rootNodeLst = BuildTree(rootNodeLst, currentPolygon, currentLine, outputLines[closedLineIdx[i]]);
            //}
            #endregion
            return inputLines;
        }

        /// <summary>
        /// 等值线平滑
        /// </summary>
        /// <param name="inputLine">输入的等值线</param>
        /// <param name="ratio">平滑比例（在原等值线的两点之间插入多少个中间点）</param>
        /// <returns>平滑后的等值线</returns>
        public static ContourLine Smooth(ContourLine inputLine, int ratio)
        {
            if (inputLine.pntCount < 3)
            {
                for (int i = 0; i < inputLine.pntCount; i++)
                {
                    inputLine.xSmoothCoords.Add(inputLine.xCoords[i]);
                    inputLine.ySmoothCoords.Add(inputLine.yCoords[i]);
                }
                return inputLine;       //小于3个点时，无法平滑，将原有点坐标赋给平滑后坐标
            }

            //ContourLine outputLine = new ContourLine();
            //outputLine.level = inputLine.level;

            //辅助用的两个数组
            List<double> aidedXCoords = new List<double>();
            List<double> aidedYCoords = new List<double>();

            #region 在原有数组的头尾各添加一个辅助点，在抛物线拟合的过程中需要用到，
            if (inputLine.isClosedRing)
            {
                #region 如果是闭合曲线，需要在点集的前方添加第n-2点，后方添加1点，然后进行抛物线平滑
                aidedXCoords.Add(inputLine.xCoords[inputLine.pntCount - 2]);
                aidedYCoords.Add(inputLine.yCoords[inputLine.pntCount - 2]);
                for (int i = 0; i < inputLine.pntCount; i++)
                {
                    aidedXCoords.Add(inputLine.xCoords[i]);
                    aidedYCoords.Add(inputLine.yCoords[i]);
                }
                aidedXCoords.Add(inputLine.xCoords[1]);
                aidedYCoords.Add(inputLine.yCoords[1]);
                #endregion

            }
            else
            {
                #region 如果是非闭合曲线，需要在点集的前方添加第0点，后方添加n-1点，然后进行抛物线平滑
                aidedXCoords.Add(inputLine.xCoords[0]);
                aidedYCoords.Add(inputLine.yCoords[0]);
                for (int i = 0; i < inputLine.pntCount; i++)
                {
                    aidedXCoords.Add(inputLine.xCoords[i]);
                    aidedYCoords.Add(inputLine.yCoords[i]);
                }
                aidedXCoords.Add(inputLine.xCoords[inputLine.pntCount - 1]);
                aidedYCoords.Add(inputLine.yCoords[inputLine.pntCount - 1]);
                #endregion
            }
         

            #endregion

            #region 抛物线拟合
            //从辅助数据的第二个点开始，进行计算
            for (int i = 1; i < aidedXCoords.Count-2; i++)
            {
                //当前点i到下一点i+1之间的抛物线方程为
                //fx(t) = (-4*t^3+4*t^2-t)*x[i-1]+(12*t^3-10*t^2+1)*x[i]+(-12*t^3+8*t^2+t)*x[i+1]+(4*t^3-2*t^2)*x[i+2]
                //fy(t) = (-4*t^3+4*t^2-t)*y[i-1]+(12*t^3-10*t^2+1)*y[i]+(-12*t^3+8*t^2+t)*y[i+1]+(4*t^3-2*t^2)*y[i+2]
                //0<=t<=0.5，t==0时，fx(0) = x[i],fy(0) = y[i],t==0.5时 fx(0.5) = x[i+1],fy(0.5) = y[i+1]
                //基本原理见http://blog.csdn.net/clever101/article/details/771160或http://www.cnblogs.com/ouzi/archive/2008/08/22/1273926.html
 
                //根据ratio的值进行插值
                //先把当前点加入
                //outputLine.xCoords.Add(aidedXCoords[i]);
                //outputLine.yCoords.Add(aidedYCoords[i]);
                inputLine.xSmoothCoords.Add(aidedXCoords[i]);
                inputLine.ySmoothCoords.Add(aidedYCoords[i]);

                //然后根据ratio的值，将0-0.5之间切成ratio份
                double incre = 0.5 / (ratio + 1);
                for (int j = 1; j <= ratio; j++)
                {
                    double t = j * incre;
                    double tmpX = (-4 * t * t * t + 4 * t * t - t) * aidedXCoords[i - 1] + (12 * t * t * t - 10 * t * t + 1) * aidedXCoords[i] + (-12 * t * t * t + 8 * t * t + t) * aidedXCoords[i + 1] + (4 * t * t * t - 2 * t * t) * aidedXCoords[i + 2];
                    double tmpY = (-4 * t * t * t + 4 * t * t - t) * aidedYCoords[i - 1] + (12 * t * t * t - 10 * t * t + 1) * aidedYCoords[i] + (-12 * t * t * t + 8 * t * t + t) * aidedYCoords[i + 1] + (4 * t * t * t - 2 * t * t) * aidedYCoords[i + 2];

                    inputLine.xSmoothCoords.Add(tmpX);
                    inputLine.ySmoothCoords.Add(tmpY);
                } 
            }

            //在上面的拟合过程中，原数据的最后一个点未添加，这里补充添加一下
            inputLine.xSmoothCoords.Add(inputLine.xCoords[inputLine.pntCount - 1]);
            inputLine.ySmoothCoords.Add(inputLine.yCoords[inputLine.pntCount - 1]);

            #endregion

            return inputLine; 
        }
        #endregion

        #region 第五步，获取等值线之间的区域，并填充
        /// <summary>
        /// 获取等值线之间的区域，用于填充（基础算法基于论文《等值线追踪生成等值面过程中的算法策略》）
        /// </summary>
        /// <param name="lines">平滑后的等值线</param>
        /// <param name="bandLevelBPs">浓度值断点</param>
        /// <param name="outExtent">生成区域边界</param>
        /// <returns>生成的区域</returns>
        public static List<ContourPolygonBetweenLines> GenPolygons(List<ContourLine> lines, List<double> bandLevelBPs, ContourExtent outExtent,int tlLevel,int trLevel,int blLevel,int brLevel)
        {
            List<ContourPolygonBetweenLines> resPolygons = new List<ContourPolygonBetweenLines>();

            #region Step5.1 先把等值线分为2部分，第一部分为非闭合等值线，第二部分为闭合的等值线
            List<ContourLine> closedLines = new List<ContourLine>();
            List<ContourLine> unclosedLines = new List<ContourLine>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].isClosedRing) closedLines.Add(lines[i]);
                else unclosedLines.Add(lines[i]);
            }
            #endregion

            #region Step 5.2 先对于非闭合等值线，计算这些等值线与区域边界（一般为矩形）的交点，并生成这些等值线与区域边界围成的多边形
            //由非闭合等值线追踪出来的多边形（本阶段出来的都是外边界，内部岛屿得等到与后续的闭合等值线一起处理时才能生成）
            List<ContourPolygonBetweenLines> polygonsOnEdge = new List<ContourPolygonBetweenLines>();       //非
            List<PntOnEdge> pntsOnEdges = new List<PntOnEdge>();

            #region 先把所有非闭合等值线的两个端点生成PntOnEdge对象，构建一个临时列表，之后再按规则逐个添加到正式列表中
            #region 构建临时列表
            List<PntOnEdge> tmpPnts = new List<PntOnEdge>();
            for (int i = 0; i < unclosedLines.Count; i++)
            {
                PntOnEdge pnt1 = new PntOnEdge();
                pnt1.x = unclosedLines[i].xCoords[0];
                pnt1.y = unclosedLines[i].yCoords[0];
                pnt1.line = unclosedLines[i];

                PntOnEdge pnt2 = new PntOnEdge();
                pnt2.x = unclosedLines[i].xCoords[unclosedLines[i].pntCount - 1];
                pnt2.y = unclosedLines[i].yCoords[unclosedLines[i].pntCount - 1];
                pnt2.line = unclosedLines[i];

                pnt1.otherPnt = pnt2;
                pnt2.otherPnt = pnt1;

                tmpPnts.Add(pnt1);
                tmpPnts.Add(pnt2);
            } 
            #endregion

            #region 构建正式列表
            //step1:将左上角点加入（四个角点为特殊角点），
            //step2:将落在区域边界左边界上的点构建一个临时列表，将列表按点的Y坐标从上到下排序后，加到左上角点后方
            //step3:将左下角点加入
            //step4:将落在区域边界下边界上的点构建一个临时列表，将列表按点的X坐标从左到右排序后，加到左下角点后方
            //step5-8:同理加入右下角点，右边界上的点，右上角点，上边界上的点

            //step1:区域边界左上角点,所有角点的times为1
            PntOnEdge tlPnt = new PntOnEdge();
            tlPnt.x = outExtent.MinX;
            tlPnt.y = outExtent.MaxY;
            //临时构造一个虚拟的等值线，仅用于标识角点的Level
            ContourLine tmpLine = new ContourLine();
            tmpLine.level = tlLevel;
            tlPnt.line = tmpLine;
            tlPnt.otherPnt = null;
            tlPnt.times = 1;
            pntsOnEdges.Add(tlPnt);

            //step2:左边界上的点
            List<PntOnEdge> pntsOnLeftEdge = new List<PntOnEdge>();
            for (int i = 0; i < tmpPnts.Count; i++)
            {
                if (tmpPnts[i].x == outExtent.MinX)
                    pntsOnLeftEdge.Add(tmpPnts[i]); 
            }
            PntOnEdgeComparer cpr = new PntOnEdgeComparer(false, false);
            pntsOnLeftEdge.Sort(cpr);
            pntsOnEdges.AddRange(pntsOnLeftEdge);

            //step3:区域边界左下角点，
            PntOnEdge blPnt = new PntOnEdge();
            blPnt.x = outExtent.MinX;
            blPnt.y = outExtent.MinY;
            //临时构造一个虚拟的等值线，仅用于标识角点的Level
            tmpLine = new ContourLine();
            tmpLine.level = blLevel;
            blPnt.line = tmpLine;
            blPnt.otherPnt = null;
            blPnt.times = 1;
            pntsOnEdges.Add(blPnt);

            //step4:下边界上的点
            List<PntOnEdge> pntsOnBottomEdge = new List<PntOnEdge>();
            for (int i = 0; i < tmpPnts.Count; i++)
            {
                if (tmpPnts[i].y == outExtent.MinY)
                    pntsOnBottomEdge.Add(tmpPnts[i]);
            }
            cpr = new PntOnEdgeComparer(true, true);
            pntsOnBottomEdge.Sort(cpr);
            pntsOnEdges.AddRange(pntsOnBottomEdge);

            //step5:区域边界右下角点
            PntOnEdge brPnt = new PntOnEdge();
            brPnt.x = outExtent.MaxX;
            brPnt.y = outExtent.MinY;
            //临时构造一个虚拟的等值线，仅用于标识角点的Level
            tmpLine = new ContourLine();
            tmpLine.level = brLevel;
            brPnt.line = tmpLine;
            brPnt.otherPnt = null;
            brPnt.times = 1;
            pntsOnEdges.Add(brPnt);

            //step6:右边界上的点
            List<PntOnEdge> pntsOnRightEdge = new List<PntOnEdge>();
            for (int i = 0; i < tmpPnts.Count; i++)
            {
                if (tmpPnts[i].x == outExtent.MaxX)
                    pntsOnRightEdge.Add(tmpPnts[i]);
            }
            cpr = new PntOnEdgeComparer(false, true);
            pntsOnRightEdge.Sort(cpr);
            pntsOnEdges.AddRange(pntsOnRightEdge);

            //step7:区域边界右上角点
            PntOnEdge trPnt = new PntOnEdge();
            trPnt.x = outExtent.MaxX;
            trPnt.y = outExtent.MaxY;
            //临时构造一个虚拟的等值线，仅用于标识角点的Level
            tmpLine = new ContourLine();
            tmpLine.level = trLevel;
            trPnt.line = tmpLine;
            trPnt.otherPnt = null;
            trPnt.times = 1;
            pntsOnEdges.Add(trPnt);

            //step8:上边界上的点
            List<PntOnEdge> pntsOnTopEdge = new List<PntOnEdge>();
            for (int i = 0; i < tmpPnts.Count; i++)
            {
                if (tmpPnts[i].y == outExtent.MaxY)
                    pntsOnTopEdge.Add(tmpPnts[i]);
            }
            cpr = new PntOnEdgeComparer(true, false);
            pntsOnTopEdge.Sort(cpr);
            pntsOnEdges.AddRange(pntsOnTopEdge);
            #endregion
           
            #endregion

            #region 对正式列表进行追踪 
            int finishedPntCount = 0;       //在pntsOnEdges中已经完成追踪（完成追踪的含义是，times属性变为0）的点数量，这个值等于pntsOnEdges.Count时，循环结束
            int currentIndex = 0;           //当前追踪的点序号
            List<PntOnEdge> currentPnts = new List<PntOnEdge>();    //当前追踪经过的点
            while (finishedPntCount<pntsOnEdges.Count)
            {
                //追踪路过的点，如果还没有完成，那么将这个点添加到列表中
                if (pntsOnEdges[currentIndex].times > 0)
                {
                    if (currentPnts.Count != 0 && currentPnts[0] == pntsOnEdges[currentIndex])
                    {
                        #region 如果追踪完一圈了，那么构建一个多边形，并重置一些变量
                        ContourPolygonBetweenLines tmpPolygon = new ContourPolygonBetweenLines();
                        int tmpLevel1 = int.MinValue, tmpLevel2 = int.MinValue;      //多边形两边等值线的Level

                        //将当前追踪经过的点集加到多边形的边界点集中
                        for (int i = 0; i < currentPnts.Count; i++)
                        {
                            //如果需要，更新一下多边形的Level值
                            if (tmpLevel1 == int.MinValue) tmpLevel1 = currentPnts[i].line.level;
                            else if (tmpLevel2 == int.MinValue && tmpLevel1 != currentPnts[i].line.level) tmpLevel2 = currentPnts[i].line.level;

                            if (currentPnts[i].otherPnt == null)
                            {
                                #region 如果是区域边界的角点，只单纯把点的坐标加进去，并将该角点的times减1
                                tmpPolygon.xCoords.Add(currentPnts[i].x);
                                tmpPolygon.yCoords.Add(currentPnts[i].y);
                                tmpPolygon.xSmoothCoords.Add(currentPnts[i].x);
                                tmpPolygon.ySmoothCoords.Add(currentPnts[i].y);

                                currentPnts[i].times -= 1;
                                if (currentPnts[i].times == 0) finishedPntCount++;
                                #endregion
                            }
                            else
                            {
                                #region 如果是非角点(这种情况下，该点及该点后一点应当在同一等值线上)，那么将这个角点所在等值线的点全加进去              
                                if (i < currentPnts.Count - 1 && currentPnts[i].line == currentPnts[i + 1].line)
                                {
                                    if (currentPnts[i].x == currentPnts[i].line.xCoords[0] && currentPnts[i].y == currentPnts[i].line.yCoords[0])
                                    {
                                        //如果该点为等值线的第一点，那么将这个等值线上的点按正序加到多边形边界上
                                        for (int j = 0; j < currentPnts[i].line.pntCount; j++)
                                        {
                                            tmpPolygon.xCoords.Add(currentPnts[i].line.xCoords[j]);
                                            tmpPolygon.yCoords.Add(currentPnts[i].line.yCoords[j]); 
                                        }
                                        for (int j = 0; j < currentPnts[i].line.xSmoothCoords.Count; j++)
                                        {
                                            tmpPolygon.xSmoothCoords.Add(currentPnts[i].line.xSmoothCoords[j]);
                                            tmpPolygon.ySmoothCoords.Add(currentPnts[i].line.ySmoothCoords[j]); 
                                        }
                                    }
                                    else if (currentPnts[i].x == currentPnts[i].line.xCoords[currentPnts[i].line.pntCount - 1] &&
                                            currentPnts[i].y == currentPnts[i].line.yCoords[currentPnts[i].line.pntCount - 1])
                                    {
                                        //如果该点为等值线的最后一点，那么将这个等值线上的点按逆序加到多边形边界上
                                        for (int j = currentPnts[i].line.pntCount - 1; j >= 0; j--)
                                        {
                                            tmpPolygon.xCoords.Add(currentPnts[i].line.xCoords[j]);
                                            tmpPolygon.yCoords.Add(currentPnts[i].line.yCoords[j]);
                                        }
                                        for (int j = 0; j < currentPnts[i].line.xSmoothCoords.Count; j++)
                                        {
                                            tmpPolygon.xSmoothCoords.Add(currentPnts[i].line.xSmoothCoords[j]);
                                            tmpPolygon.ySmoothCoords.Add(currentPnts[i].line.ySmoothCoords[j]);
                                        }
                                    }
                                    else continue;      //这种情况一般不会出现，写上避免意外

                                    currentPnts[i].times -= 1;
                                    if (currentPnts[i].times == 0) finishedPntCount++;
                                    currentPnts[i + 1].times -= 1;
                                    if (currentPnts[i + 1].times == 0) finishedPntCount++;

                                    i++;   //由于本次处理了两个点，直接将下一个点跳过
                                }
                                else if (i == 0)
                                { 
                                    //特殊情况，如果非角点是点集第一点，按角点处理
                                    tmpPolygon.xCoords.Add(currentPnts[i].x);
                                    tmpPolygon.yCoords.Add(currentPnts[i].y);
                                    tmpPolygon.xSmoothCoords.Add(currentPnts[i].x);
                                    tmpPolygon.ySmoothCoords.Add(currentPnts[i].y); 

                                    currentPnts[i].times -= 1;   
                                    if (currentPnts[i].times == 0) finishedPntCount++;
                                }
                                else if (i == currentPnts.Count - 1 && currentPnts[i].line == currentPnts[0].line)
                                { 
                                    //与上一情况相同，非角点是点集第一点，那么点集最后一点应该与第一点在同一条等值线上
                                    if (currentPnts[i].x == currentPnts[i].line.xCoords[0] && currentPnts[i].y == currentPnts[i].line.yCoords[0])
                                    {
                                        //如果该点为等值线的第一点，那么将这个等值线上的点按正序加到多边形边界上(点集第一点不加了)
                                        for (int j = 0; j < currentPnts[i].line.pntCount -1; j++)
                                        {
                                            tmpPolygon.xCoords.Add(currentPnts[i].line.xCoords[j]);
                                            tmpPolygon.yCoords.Add(currentPnts[i].line.yCoords[j]);
                                        }
                                        for (int j = 0; j < currentPnts[i].line.xSmoothCoords.Count-1; j++)
                                        {
                                            tmpPolygon.xSmoothCoords.Add(currentPnts[i].line.xSmoothCoords[j]);
                                            tmpPolygon.ySmoothCoords.Add(currentPnts[i].line.ySmoothCoords[j]);
                                        }
                                    }
                                    else if (currentPnts[i].x == currentPnts[i].line.xCoords[currentPnts[i].line.pntCount - 1] &&
                                            currentPnts[i].y == currentPnts[i].line.yCoords[currentPnts[i].line.pntCount - 1])
                                    {
                                        //如果该点为等值线的最后一点，那么将这个等值线上的点按逆序加到多边形边界上
                                        for (int j = currentPnts[i].line.pntCount - 2; j >= 0; j--)
                                        {
                                            tmpPolygon.xCoords.Add(currentPnts[i].line.xCoords[j]);
                                            tmpPolygon.yCoords.Add(currentPnts[i].line.yCoords[j]);
                                            //tmpPolygon.xSmoothCoords.Add(currentPnts[i].line.xSmoothCoords[j]);
                                            //tmpPolygon.ySmoothCoords.Add(currentPnts[i].line.ySmoothCoords[j]); 
                                        }
                                        for (int j = currentPnts[i].line.xSmoothCoords.Count - 2; j >= 0; j--)
                                        {
                                            tmpPolygon.xSmoothCoords.Add(currentPnts[i].line.xSmoothCoords[j]);
                                            tmpPolygon.ySmoothCoords.Add(currentPnts[i].line.ySmoothCoords[j]);
                                        }
                                    }
                                    else continue;      //这种情况一般不会出现，写上避免意外

                                    currentPnts[i].times -= 1;
                                    if (currentPnts[i].times == 0) finishedPntCount++;
                                }

                                #endregion
                            }
                        }

                        //再将第一点加上，构成闭合的环
                        tmpPolygon.xCoords.Add(currentPnts[0].x);
                        tmpPolygon.yCoords.Add(currentPnts[0].y);
                        tmpPolygon.xSmoothCoords.Add(currentPnts[0].x);
                        tmpPolygon.ySmoothCoords.Add(currentPnts[0].y); 

                        //处理一下Level,并赋值
                        if (tmpLevel2 == int.MinValue) tmpLevel2 = tmpLevel1;       //有时候可能会遇到只有一个Level值的情况，这里处理一下
                        tmpPolygon.upLevel = Math.Max(tmpLevel1, tmpLevel2);
                        tmpPolygon.lowLevel = Math.Min(tmpLevel1, tmpLevel2);

                        polygonsOnEdge.Add(tmpPolygon);         //将当前多边形加入列表中

                        currentPnts = new List<PntOnEdge>();  //重新开始追踪一个新的多边形

                        #endregion
                    }
                    else
                    {
                        currentPnts.Add(pntsOnEdges[currentIndex]);
                        if (currentPnts.Count == 1) { }//如果当前点是点集的第一个点时，也不跳到该点所在等值线的另一端点
                        else if (pntsOnEdges[currentIndex].otherPnt != null && pntsOnEdges[currentIndex].otherPnt != currentPnts[0])    //当前点不是角点，且所在等值线另一端点不是点集第一点时，跳到等值线另一端点处    
                        {
                            //如果当前经过的点不是角点，那么跳到该点所在等值线的另一端点,并另一端点加入点集，从另一端点处开始继续追踪
                            currentPnts.Add(pntsOnEdges[currentIndex].otherPnt);

                            currentIndex = pntsOnEdges.IndexOf(pntsOnEdges[currentIndex].otherPnt);     //将当前点置于另一端点位置
                        }
                        else if (pntsOnEdges[currentIndex].otherPnt != null && pntsOnEdges[currentIndex].otherPnt == currentPnts[0]) //当前点不是角点，且所在等值线另一端点是点集第一点时 ，跳到等值线另一端点处    
                        {
                            //如果当前经过的点不是角点，那么跳到该点所在等值线的另一端点,但另一端点不加入点集（因为一开始加过了），而且不继续追踪了（因为已经回到点集起点，闭合了，可以直接生成Polygon了）
                            currentIndex = pntsOnEdges.IndexOf(pntsOnEdges[currentIndex].otherPnt);
                            continue;
                        }
                    }

                }

                //循环到下一点，如果已经到列表结尾，再从头开始
                currentIndex++;
                if (currentIndex == pntsOnEdges.Count) currentIndex = 0;
            }

            #endregion
            
            #endregion

            #region Step 5.3 再对于闭合等值线，生成这些等值线之间的包含关系，
            //由闭合等值线追踪出来的多边形
            List<ContourPolygonBetweenLines> closedPolygons = new List<ContourPolygonBetweenLines>();

            #region 构建闭合等值线树状关系
            //将闭合等值线按照空间包含关系，构建成树状结构（不构建成列表，是因为一个多边形内可能包含多个下一级多边形）
            //由于可能有多个树装关系，因此这里声明的是List列表
            //每个列表的元素均表示一个树状结构的根结点，其内部属性中包含其子结点及更深层的节点
            List<PolygonTreeNode> rootNodeLst = new List<PolygonTreeNode>();

            for (int i = 0; i < closedLines.Count; i++)
            {
                ContourLine currentLine = closedLines[i];

                #region 先构建当前等值线对应的多边形
                List<Coordinate> coordList = new List<Coordinate>();
                for (int k = 0; k < currentLine.pntCount; k++)
                {
                    coordList.Add(new Coordinate(currentLine.xCoords[k], currentLine.yCoords[k]));
                }
                Polygon currentPolygon = new Polygon(coordList);
                //if (!currentPolygon.IsValid)
                //{
                //    int aaa = 1;
                //    aaa++;
                //}
                #endregion

                rootNodeLst = BuildTree(rootNodeLst, currentPolygon, currentLine);
            }
            #endregion
            
            #endregion

            #region Step 5.4 将非闭合等值线与边界围成的多边形中，包含的闭合等值线多边形给扣掉(因为闭合等值线是不可能包含非闭合等值线的，所以相反的情况不需要考虑)
            for (int i = 0; i < polygonsOnEdge.Count; i++)
            {
                //构建外边界多边形（根据平滑前的坐标）
                List<Coordinate> tmpCoords = new List<Coordinate>();
                for (int j = 0; j < polygonsOnEdge[i].xCoords.Count; j++)
                {
                    tmpCoords.Add(new Coordinate(polygonsOnEdge[i].xCoords[j], polygonsOnEdge[i].yCoords[j]));
                }

                Polygon tmpPolylgon = new Polygon(tmpCoords);

                //看多边形是否包含某个或某几个闭合等值线的多边形(由于根结点一定是一个树状结构中范围最大的那一个，所以只需要判断各个根结点就行了)
                for (int j = 0; j < rootNodeLst.Count; j++)
                {
                    if (tmpPolylgon.Contains(rootNodeLst[j].outLinePolygon))
                    { 
                        //如果包含的话，将这个根结点的平滑后外边界点集添加到这个“非闭合等值线与边界围的多边形”的Holes中
                        int tmpIndex = polygonsOnEdge[i].xCoordsOfHoles.Count;
                        polygonsOnEdge[i].xCoordsOfHoles.Add(new List<double>());
                        polygonsOnEdge[i].yCoordsOfHoles.Add(new List<double>());

                        for (int k = 0; k < rootNodeLst[j].outLine.xSmoothCoords.Count; k++)
                        {
                            polygonsOnEdge[i].xCoordsOfHoles[tmpIndex].Add(rootNodeLst[j].outLine.xSmoothCoords[k]);
                            polygonsOnEdge[i].yCoordsOfHoles[tmpIndex].Add(rootNodeLst[j].outLine.ySmoothCoords[k]);
                        }
                    }
                } 
            }
            #endregion

            #region Step 5.5 根据闭合等值线的树状结构，生成多边形
            for (int i = 0; i < rootNodeLst.Count; i++)
            {
                //递归，生成树状结构中各结点对应的多边形
                closedPolygons.AddRange(GenClosedPolygons(rootNodeLst[i]));
            }
            #endregion
            polygonsOnEdge.AddRange(closedPolygons);        //两个列表合并

            return polygonsOnEdge;
        }

        /// <summary>
        /// 计算输入的浓度值位于断点列表的哪个区间（浓度值大于返回值对应的断点，小于返回值+1对应断点）
        /// </summary>
        /// <param name="bandLevelBPs">断点列表</param>
        /// <param name="tmpVal">输入浓度值</param>
        /// <returns>序号</returns>
        public static int CalLevel(List<double> bandLevelBPs, double tmpVal)
        {
            if (tmpVal < bandLevelBPs[0]) return -1;
            if (tmpVal >bandLevelBPs[bandLevelBPs.Count-1]) return bandLevelBPs.Count;
            for (int i = 0; i < bandLevelBPs.Count-1; i++)
            {
               if (tmpVal > bandLevelBPs[i] && tmpVal <= bandLevelBPs[i + 1])
                    return i;
            }
            return 0;
        }

        /// <summary>
        /// 用于迭代构建树状结构
        /// </summary>
        /// <param name="nodes">原来的树根结点列表</param>
        /// <param name="inputPolygon">待插入的多边形</param>
        /// <param name="inputLine">待插入的等值线（与多边形对应）</param>
        /// <returns>更新后的树装结构</returns>
        public static List<PolygonTreeNode> BuildTree(List<PolygonTreeNode> nodes, Polygon inputPolygon, ContourLine inputLine)
        {
            bool ifBeContained = false;     //是否被当前列表中的某个根结点包含
            for (int j = 0; j < nodes.Count; j++)
            {
                #region 对每一个树进行搜索，判断当前等值线围成的多边形是否属于该树，如果属于某个树，则将该等值线加到这个树对应的结点上
                //由于树状结构中，根结点的多边形范围是最大的，只需要判断当前等值线围成的多边形与根结点之间的关系即可

                //如果当前等值线对应的多边形被任何一个根结点的多边形包含（由于等值线的特性，不会同时被多个等值线树状结构包含），则将当前多边形加到这个根结点对应的树状结构中
                if (nodes[j].outLinePolygon.Contains(inputPolygon))
                {
                    nodes[j].subNodes = BuildTree(nodes[j].subNodes, inputPolygon, inputLine);      //迭代更新结点

                    ifBeContained = true;
                    break;
                }
                #endregion
            }
            if (ifBeContained) return nodes;

            //如果没有被列表中的某个根结点包含，判断一下，当前插入的多边形是否包含了某个或某几个根结点
            List<int> containIndexes = new List<int>();         //包含根结点的序号
            for (int i = 0; i < nodes.Count; i++)
            {
                if (inputPolygon.Contains(nodes[i].outLinePolygon))
                    containIndexes.Add(i);
            }
            //如果至少包含了一个根结点，那么新建一个结点，将这些根结点转为该结点的子结点
            if (containIndexes.Count > 0)
            {
                PolygonTreeNode currentNode = new PolygonTreeNode();
                currentNode.outLine = inputLine;
                currentNode.outLinePolygon = inputPolygon;
                for (int i = 0; i < containIndexes.Count; i++)
                {
                    currentNode.subNodes.Add(nodes[containIndexes[i]]);
                }
                nodes.Add(currentNode);

                //再将原列表中的对应结点删掉
                for (int i = containIndexes.Count - 1; i >= 0; i--)
                {
                    nodes.RemoveAt(containIndexes[i]);
                }
                return nodes;
            }
            else
            {
                //如果既没有被某个根结点包含，也没有包含某个根结点的话，那么就作为一个新的根结点插入
                PolygonTreeNode currentNode = new PolygonTreeNode();
                currentNode.outLine = inputLine;
                currentNode.outLinePolygon = inputPolygon;

                nodes.Add(currentNode);
                return nodes;
            }

        }

        public static List<ContourPolygonBetweenLines> GenClosedPolygons(PolygonTreeNode node)
        {
            List<ContourPolygonBetweenLines> resPolygons = new List<ContourPolygonBetweenLines>();

            int tmpLevel1 = int.MinValue, tmpLevel2 = int.MinValue;
            ContourPolygonBetweenLines currentPolygon = new ContourPolygonBetweenLines();
            currentPolygon.xCoords = node.outLine.xCoords;
            currentPolygon.yCoords = node.outLine.yCoords;
            currentPolygon.xSmoothCoords = node.outLine.xSmoothCoords;
            currentPolygon.ySmoothCoords = node.outLine.ySmoothCoords;
            tmpLevel1 = node.outLine.level;

            for (int i = 0; i < node.subNodes.Count; i++)
            {
                currentPolygon.xCoordsOfHoles.Add(node.subNodes[i].outLine.xSmoothCoords);
                currentPolygon.yCoordsOfHoles.Add(node.subNodes[i].outLine.ySmoothCoords);

                if (tmpLevel2 == int.MinValue && node.subNodes[i].outLine.level != tmpLevel1)
                    tmpLevel2 = node.subNodes[i].outLine.level;
            }

            if (tmpLevel2 == int.MinValue) tmpLevel2 = tmpLevel1;
            currentPolygon.lowLevel = Math.Min(tmpLevel1, tmpLevel2);
            currentPolygon.upLevel = Math.Max(tmpLevel1, tmpLevel2);
            resPolygons.Add(currentPolygon);

            for (int i = 0; i < node.subNodes.Count; i++)
            {
                resPolygons.AddRange(GenClosedPolygons(node.subNodes[i]));
            }
            return resPolygons;
        }
        #endregion

        #region 第六步：等值线渲染
        /// <summary>
        /// 将连接完毕的等值线渲染到图片中
        /// </summary>
        /// <param name="lines">生成的等值线</param>
        /// <param name="polygons">等值线之间围成的多边形</param>
        /// <param name="bandLevelBPs">断点列表</param>
        /// <param name="outExtent">绘制的区域边界</param>
        /// <param name="pixelSize">图片1像素代表的实际距离</param>
        /// <param name="colorMap">颜色表</param>
        /// <returns>生成的图片</returns>
        public static Bitmap GetBitmap(List<ContourLine> lines,List<ContourPolygonBetweenLines> polygons, List<double> bandLevelBPs, ContourExtent outExtent,double pixelSize,float tranparent, string colorMap)
        {
            if (!ColorMap.ColorMaps.ContainsKey(colorMap)) throw new Exception("找不到对应的ColorMap:" + colorMap);

            //生成图片的大小
            int needWidth = (int)(outExtent.Width / pixelSize) + 1;
            int needHeight = (int)(outExtent.Height / pixelSize) + 1; 

            #region 生成的BitMap及相应的Graphic对象

            Bitmap TempBitmap = new Bitmap(needWidth, needHeight);
            Graphics gra = Graphics.FromImage(TempBitmap);
            gra.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
            gra.Clear(Color.Transparent);

            #endregion

            #region 画多边形
            for (int i = 0; i < polygons.Count; i++)
            {
                Color currentColor = ColorMap.ColorMaps[colorMap].CalColor(polygons[i].lowLevel + 1, bandLevelBPs.Count);
                SolidBrush currentBrush = new SolidBrush(currentColor);

                GraphicsPath path = new GraphicsPath();
                PointF[] outlinePnts = new PointF[polygons[i].xSmoothCoords.Count];
                for (int j = 0; j < polygons[i].xSmoothCoords.Count; j++)
                {
                    outlinePnts[j].X = (float)((polygons[i].xSmoothCoords[j] - outExtent.MinX) / pixelSize);
                    outlinePnts[j].Y = (float)((outExtent.MaxY - polygons[i].ySmoothCoords[j]) / pixelSize);
                }
                path.AddPolygon(outlinePnts);

                for (int j = 0; j < polygons[i].xCoordsOfHoles.Count; j++)
                {
                    PointF[] holePnts = new PointF[polygons[i].xCoordsOfHoles[j].Count];
                    for (int k = 0; k < polygons[i].xCoordsOfHoles[j].Count; k++)
                    {
                        holePnts[k].X = (float)((polygons[i].xCoordsOfHoles[j][k] - outExtent.MinX) / pixelSize);
                        holePnts[k].Y = (float)((outExtent.MaxY - polygons[i].yCoordsOfHoles[j][k]) / pixelSize);
                    }
                    path.AddPolygon(holePnts);
                }
                gra.FillPath(currentBrush, path);
            }
            #endregion

            #region 画等值线
            Pen contourLinePen = new Pen(new SolidBrush(Color.DarkGray), 0.3f);
            for (int i = 0; i < lines.Count; i++)
            {
                int tmpLevel = lines[i].level;
                //Color currentColor = ColorMap.ColorMaps[colorMap].CalColor(tmpLevel, bandLevelBPs.Count - 1);
                //Pen tmpPen = new Pen(new SolidBrush(currentColor),0.5f);

                PointF[] pntsToDraw = new PointF[lines[i].pntCount];

                for (int j = 0; j < lines[i].pntCount; j++)
                {
                    pntsToDraw[j].X = (float)((lines[i].xCoords[j] - outExtent.MinX) / pixelSize);
                    pntsToDraw[j].Y = (float)((outExtent.MaxY - lines[i].yCoords[j]) / pixelSize);
                }
                //if (lines[i].isClosedRing)
                //    gra.DrawClosedCurve(tmpPen, pntsToDraw);
                //else gra.DrawCurve(tmpPen, pntsToDraw);
                gra.DrawLines(contourLinePen, pntsToDraw);
            } 
            #endregion

            #region Bitmap的后处理并返回
            Bitmap bmpTemp = new Bitmap(TempBitmap);//此处报内存不足（风险评估）0527
            Graphics g = Graphics.FromImage(bmpTemp);
            g.Clear(Color.Transparent);
            float[][] ptsArray ={ 
                            new float[] {1, 0, 0, 0, 0},
                            new float[] {0, 1, 0, 0, 0},
                            new float[] {0, 0, 1, 0, 0},
                            new float[] {0, 0, 0, tranparent, 0}, //f，图像的透明度
                            new float[] {0, 0, 0, 0, 1}};
            ColorMatrix clrMatrix = new ColorMatrix(ptsArray);
            ImageAttributes imgAttributes = new ImageAttributes();
            imgAttributes.SetColorMatrix(clrMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
            g.DrawImage(TempBitmap, new Rectangle(0, 0, TempBitmap.Width, TempBitmap.Height), 0, 0, TempBitmap.Width, TempBitmap.Height,
                GraphicsUnit.Pixel, imgAttributes);

            TempBitmap.Dispose();
            gra.Dispose();
            g.Dispose();
            GC.Collect();
            return bmpTemp;
            #endregion

            //gra.Dispose();
            //GC.Collect();
            //return TempBitmap;
        }
        #endregion

        /// <summary>
        /// 为保证和等值线生成功能的调用方式一致，这里将上面的第2-6步进行了一下整合，并将函数命名及参数表改为与等值面一致
        /// </summary>
        /// <param name="blks">插值生成的网格</param>
        /// <param name="bandLevelBPs">浓度断点</param>
        /// <param name="outExtent">绘制的区域边界</param>
        /// <param name="pixelSize">图片中1个像素代表的实际距离</param>
        /// <param name="colorMap">颜色表</param>
        /// <returns>生成的图片</returns>
        public static Bitmap GenBitmap(List<ContourGridBlock> blks,List<double> bandLevelBPs,ContourExtent outExtent,double pixelSize,float tranparent, string colorMap)
        {
            if (!ColorMap.ColorMaps.ContainsKey(colorMap)) throw new Exception("找不到对应的ColorMap:" + colorMap);

            Dictionary<int, List<ContourBandByPntsLib.ContourLineSegment>> segDic = ContourBandByPntsLib.ContourLineByPntsClass.GenSegments(blks, bandLevelBPs, outExtent);
            List<ContourBandByPntsLib.ContourLine> lines = ContourBandByPntsLib.ContourLineByPntsClass.GetLinesFromSegs(segDic, 1e-7);///将生成的线段连接成等值线，取网格单元的1/20长度作为阈值 

            lines = ContourBandByPntsLib.ContourLineByPntsClass.SmoothLines(lines);

            //获取一下边界范围四个角点的浓度在断点列表中所在的Level
            int tlLevel = -1, trLevel = -1, blLevel = -1, brLevel = -1;
            for (int i = 0; i < blks.Count; i++)
            {
                if (blks[i].minX == outExtent.MinX && blks[i].maxY == outExtent.MaxY)
                    tlLevel = CalLevel(bandLevelBPs, blks[i].tlValue);
                else if (blks[i].minX == outExtent.MinX && blks[i].minY == outExtent.MinY)
                    blLevel = CalLevel(bandLevelBPs, blks[i].blValue);
                else if (blks[i].maxX == outExtent.MaxX && blks[i].minY == outExtent.MinY)
                    brLevel = CalLevel(bandLevelBPs, blks[i].brValue);
                else if (blks[i].maxX == outExtent.MaxX && blks[i].maxY == outExtent.MaxY)
                    trLevel = CalLevel(bandLevelBPs, blks[i].trValue);
            }

            List<ContourBandByPntsLib.ContourPolygonBetweenLines> polygons = ContourBandByPntsLib.ContourLineByPntsClass.GenPolygons(lines, bandLevelBPs, outExtent, tlLevel, trLevel, blLevel, brLevel);


            return GetBitmap(lines, polygons, bandLevelBPs, outExtent, pixelSize, tranparent, colorMap);
        }

        /// <summary>
        /// 依据传入的网格，按照指定的色系及浓度断点等参数，渲染生成图像文件
        /// </summary>
        /// <param name="path">生成图像文件的路径</param>
        /// <param name="blocks">需要渲染的网格</param>
        /// <param name="bandLevelBPs">浓度断点（区分等值面的不同颜色）</param>
        /// <param name="outExtent">网格范围</param>
        /// <param name="pixelSize">生成图像时，图像的每个像素代表的尺寸（单位与网格坐标的单位相同）</param>
        /// <param name="tranparent">生成图像的透明度（0-1）</param>
        /// <param name="colorMap">生成图像的色系</param>
        public static void SavePic(string path, List<ContourGridBlock> blks, List<double> bandLevelBPs, ContourExtent outExtent, double pixelSize,float tranparent, string colorMap)
        {
            try
            {
                Bitmap bmpTemp = GenBitmap(blks, bandLevelBPs, outExtent, pixelSize, tranparent, colorMap);
                bmpTemp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                bmpTemp.Dispose();

            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                GC.Collect();
            }
        }
    }

    /// <summary>
    /// 生成的等值线线段（零散的，未拼合成一圈）
    /// </summary>
    public class ContourLineSegment
    {
        /// <summary> 当前等值线线段在断点列表中所处的级别 </summary>
        public int level;

        /// <summary> 当前等值线线段起点的X坐标 </summary>
        public double x1;

        /// <summary> 当前等值线线段起点的Y坐标 </summary>
        public double y1;

        /// <summary> 当前等值线线段终点的X坐标 </summary>
        public double x2;

        /// <summary> 当前等值线线段终点的Y坐标 </summary>
        public double y2;
    }

    /// <summary>
    /// 拼合后的等值线（由ContourLineSegment拼接而来）
    /// </summary>
    public class ContourLine
    {
        /// <summary> 当前等值线线段在断点列表中所处的级别 </summary>
        public int level;

        /// <summary> 是否为闭合的环 </summary>
        public bool isClosedRing
        {
            get
            {
                if (xCoords[0] == xCoords[xCoords.Count - 1] && yCoords[0] == yCoords[yCoords.Count - 1]) return true;
                else return false;
            }
        }

        /// <summary> 等值线上的点数（如果为闭合环，那么首末两点坐标相同，算两个点） </summary>
        public int pntCount
        { get { return xCoords.Count; } }

        /// <summary> 等值线中拐点的X坐标列表（平滑前，用于做拓扑关系计算） </summary>
        public List<double> xCoords = new List<double>();

        /// <summary> 等值线中拐点的Y坐标列表（平滑前，用于做拓扑关系计算） </summary>
        public List<double> yCoords = new List<double>();

        /// <summary> 平滑后，等值线中拐点的X坐标列表（平滑后，用于显示和渲染） </summary>
        public List<double> xSmoothCoords = new List<double>();

        /// <summary> 平滑后，等值线中拐点的Y坐标列表（平滑后，用于显示和渲染） </summary>
        public List<double> ySmoothCoords = new List<double>();
    }

    /// <summary>
    /// 两段等值线围成的多边形
    /// </summary>
    public class ContourPolygonBetweenLines
    {
        /// <summary> 多边形由两段等值线围成，该值为较低的等值线 </summary>
        public int lowLevel; 

        /// <summary> 多边形由两段等值线围成，该值为较高的等值线 </summary>
        public int upLevel;

        /// <summary> 多边形边界上的点数</summary>
        public int pntCount
        { get { return xCoords.Count; } }

        /// <summary> 多边形边界上拐点的X坐标列表（平滑前，用于拓扑关系计算） </summary>
        public List<double> xCoords = new List<double>();

        /// <summary> 多边形边界上拐点的Y坐标列表（平滑前，用于拓扑关系计算） </summary>
        public List<double> yCoords = new List<double>();

        /// <summary> 多边形边界上拐点的X坐标列表（平滑后，用于显示和渲染） </summary>
        public List<double> xSmoothCoords = new List<double>();

        /// <summary> 多边形边界上拐点的Y坐标列表（平滑后，用于显示和渲染） </summary>
        public List<double> ySmoothCoords = new List<double>();


        /// <summary> 多边形内部岛屿中拐点的X坐标列表 </summary> 
        public List<List<double>> xCoordsOfHoles = new List<List<double>>();
 
        /// <summary> 多边形内部岛屿中拐点的Y坐标列表 </summary> 
        public List<List<double>> yCoordsOfHoles = new List<List<double>>();
    }

    /// <summary>
    /// 用于等值线生成多边形时，边界追踪的类。用于描述一个边界与等值线（该等值线不闭合，而是与边界有2个交点）的交点
    /// </summary>
    public class PntOnEdge
    {
        /// <summary> 点的X坐标</summary>
        public double x;

        /// <summary> 点的Y坐标</summary>
        public double y;

        /// <summary> 点所在等值线</summary>
        public ContourLine line;

        /// <summary> 点所在等值线与边界的另一交点</summary>
        public PntOnEdge otherPnt;

        /// <summary> 追踪过程中，点被经过的次数（一般为2次）</summary> 
        public int times = 2;
    }

    /// <summary>
    /// 用于PntOnEdge对象的对比
    /// </summary>
    public class PntOnEdgeComparer : IComparer<PntOnEdge>
    {
        /// <param name="sortByX">是否按X值排序，true--按X排序，false--按Y排序</param>
        public bool sortByX;
        /// <param name="sortDirection">排序方向，true--从小到大，false--从大到小</param>
        public bool sortDirection;

        public PntOnEdgeComparer()
        { }

        public PntOnEdgeComparer(bool sortbyx, bool sortdir)
        {
            sortByX = sortbyx;
            sortDirection = sortdir;
        }

    
        public int Compare(PntOnEdge x, PntOnEdge y)
        {
            if (sortByX)
            {
                if (sortDirection)
                { 
                    //按X值排序，从小到大
                    if (x.x > y.x) return 1;
                    else if (x.x == y.x) return 0;
                    else return -1;
                }
                else
                {
                    //按X值排序，从大到小
                    if (x.x > y.x) return -1;
                    else if (x.x == y.x) return 0;
                    else return 1;
                }
            }
            else
            {
                if (sortDirection)
                { 
                    //按Y值排序，从小到大
                    if (x.y > y.y) return 1;
                    else if (x.y == y.y) return 0;
                    else return -1;
                } 
                else
                {
                    //按Y值排序，从大到小
                    if (x.y > y.y) return -1;
                    else if (x.y == y.y) return 0;
                    else return 1;
                }
            }
        }
    }

    /// <summary>
    /// 根据闭合等值线生成的多边形类，用于构造闭合等值线的树状关系，树的根结点是最大的一个闭合等值线，每一个结点的subNodes列表中为该结点包含的多边形
    /// </summary>
    public class PolygonTreeNode
    {
        /// <summary> 围成当前多边形的外边界 </summary>
        public ContourLine outLine;

        /// <summary> 围成当前多边形的外边界(平滑后) </summary>
        //public ContourLine outSmoothLine; 

        /// <summary> 外边界围成的多边形 </summary>
        public Polygon outLinePolygon;

        /// <summary> 多边形直接包括的子结点 </summary>
        public List<PolygonTreeNode> subNodes = new List<PolygonTreeNode>();
    }
}
