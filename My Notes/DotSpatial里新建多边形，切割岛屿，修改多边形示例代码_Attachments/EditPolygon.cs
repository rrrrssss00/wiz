using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using DotSpatial.Data;
using DotSpatial.Controls;
using DotSpatial.Topology;
using Point = System.Drawing.Point;
using System.Windows.Forms;

namespace GISMODEL
{
    /// <summary>
    /// 编辑节点
    /// </summary>
    public class EditPolygon : MapFunction
    {
        private IMapLayer targetLayer, targetLayerPoint;
        private FeatureSet targetSet, targetSetPoint;
        private Point position;
        private Rectangle source;
        private ILinearRing[] daoyu;  //用来记录切割岛屿的多边形
        private ILinearRing lunkuo;   //用来记录河道轮廓的多边形
        /// <summary>
        /// 表示点的序号
        /// </summary>
        private int target = -1;        //
        /// <summary>
        /// 表示岛屿的序号
        /// </summary>
        private int target2 = -1;       //
        public string modPath = "";

        public EditPolygon(IMap inMap)
            : base(inMap)
        {
            YieldStyle = YieldStyles.LeftButton;
        }

        protected override void OnActivate()
        {
            bool ifFind = false;
            for (int i = 0; i < Map.Layers.Count; i++)
            {
                //
                if (Map.Layers[i].DataSet.Name == "河道轮廓" && Map.Layers[i] is IMapPolygonLayer)
                {
                    targetLayer = Map.Layers[i] as IMapPolygonLayer;
                    targetSet = targetLayer.DataSet as FeatureSet;
                    if (targetSet.Features.Count < 0)
                        ifFind = false;
                    else
                    {
                        //coordList = targetSet.Features[0].Coordinates;
                        daoyu = (targetSet.Features[0].BasicGeometry as Polygon).Holes;
                        lunkuo = (targetSet.Features[0].BasicGeometry as Polygon).Shell;
                    }

                    ifFind = true;
                }
                if (Map.Layers[i].DataSet.Name == "河道节点" && Map.Layers[i] is IMapPointLayer)
                {
                    targetLayerPoint = Map.Layers[i] as IMapPointLayer;
                    targetSetPoint = targetLayerPoint.DataSet as FeatureSet;
                }
            }
            if (!ifFind) this.Deactivate();

            Map.Cursor = Cursors.Default;
            Map.Invalidate();
            base.OnActivate();
        }

        //private IList<Coordinate> coordList;
        protected override void OnMouseDown(GeoMouseArgs e)
        {
            this.source = e.Map.MapFrame.View;
            this.position = e.Location;
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(GeoMouseArgs e)
        {
            if (this.target != -1)  //已经选中了一个点
            {
                if (this.target2 == -1)     //选中的外轮廓点
                {
                    this.lunkuo.Coordinates[target] = Map.PixelToProj(e.Location);
                    if (target == 0)
                        this.lunkuo.Coordinates[this.lunkuo.Coordinates.Count - 1] = Map.PixelToProj(e.Location);
                }
                else            //选中的是岛屿点
                {
                    this.daoyu[target2].Coordinates[target] = Map.PixelToProj(e.Location);
                    if (target == 0)
                        this.daoyu[target2].Coordinates[this.daoyu[target2].Coordinates.Count - 1] = Map.PixelToProj(e.Location);
                }
                //coordList[target] = Map.PixelToProj(e.Location);
                //if (target == 0)
                //    coordList[coordList.Count - 1] = Map.PixelToProj(e.Location);
                Map.Invalidate();
            }
            else if (this.position != e.Location && e.Button == MouseButtons.Left)  //移动
            {
                Point diff = new Point { X = this.position.X - e.X, Y = this.position.Y - e.Y };
                e.Map.MapFrame.View = new Rectangle(this.source.X + diff.X, this.source.Y + diff.Y, this.source.Width,
                                                    this.source.Height);
                Map.Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(GeoMouseArgs e)
        {
            if (this.position == e.Location && e.Button == MouseButtons.Left)
            {
                #region
                Coordinate coord = Map.PixelToProj(e.Location);
                if (this.target != -1)  //已选中点，重新放置点
                {
                    //选中的点是轮廓点
                    if (this.target2 == -1)
                        (targetSet.Features[0].BasicGeometry as Polygon).Shell = lunkuo;
                    else    //选中的点是岛屿点
                    {
                        if (this.lunkuo.Intersects(this.daoyu[target2]))
                        {
                            if (MessageBox.Show("该编辑的点属于岛屿点，不能在河道的外面！\n想要删除该岛屿吗？", "删除岛屿", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
                            {
                                this.daoyu[this.target2] = null;
                                (targetSet.Features[0].BasicGeometry as Polygon).Holes = daoyu;
                                this.daoyu = new ILinearRing[this.daoyu.Length - 1];
                                int j = 0;
                                for (int i = 0; i < (targetSet.Features[0].BasicGeometry as Polygon).Holes.Length; i++)
                                {
                                    if (i != this.target2)
                                    {
                                        this.daoyu[j] = (targetSet.Features[0].BasicGeometry as Polygon).Holes[i];
                                        j++;
                                    }
                                }
                            }
                            else
                            {
                                return;
                            }
                        }
                        (targetSet.Features[0].BasicGeometry as Polygon).Holes = daoyu;
                    }
                    targetSet.InitializeVertices();
                    Map.ResetBuffer();
                    targetSet.Save();

                    this.targetSetPoint.Features.Clear();
                    foreach (Coordinate coor in this.targetSet.Features[0].Coordinates)
                    {
                        DotSpatial.Topology.Point tmp = new DotSpatial.Topology.Point(coor);
                        IFeature feature = targetSetPoint.AddFeature(tmp);
                        feature.DataRow.BeginEdit();
                        feature.DataRow["X"] = coor.X;
                        feature.DataRow["Y"] = coor.Y;
                        feature.DataRow.EndEdit();
                    }
                    targetSetPoint.InitializeVertices();
                    targetSetPoint.Save();
                    targetSetPoint.InvalidateVertices();

                    this.target = -1;
                    this.target2 = -1;
                }
                else    //
                {
                    //for (int i = 0; i < coordList.Count; i++)
                    //{
                    //    Point p = Map.ProjToPixel(coordList[i]);
                    //    Rectangle r = new Rectangle(e.Location.X - 7, e.Location.Y - 7, 14, 14);
                    //    if (r.Contains(p))
                    //    {
                    //        this.target = i;
                    //        break;
                    //    }
                    //}
                    //轮廓
                    for (int i = 0; i < lunkuo.Coordinates.Count; i++)
                    {
                        Point p = Map.ProjToPixel(lunkuo.Coordinates[i]);
                        Rectangle r = new Rectangle(e.Location.X - 7, e.Location.Y - 7, 14, 14);
                        if (r.Contains(p))
                        {
                            this.target2 = -1;
                            this.target = i;
                            break;
                        }
                    }
                    //岛屿
                    for (int i = 0; i < daoyu.Length; i++)
                    {
                        for (int j = 0; j < daoyu[i].Coordinates.Count; j++)
                        {
                            Point p = Map.ProjToPixel(daoyu[i].Coordinates[j]);
                            Rectangle r = new Rectangle(e.Location.X - 7, e.Location.Y - 7, 14, 14);
                            if (r.Contains(p))
                            {
                                this.target2 = i;
                                this.target = j;
                                break;
                            }
                        }
                    }
                }
                #endregion
            }
            else if (e.Button == MouseButtons.Right)
            {
                #region
                //当前没有选中点的情况下，右键添加节点
                if (this.target == -1)
                {
                    DotSpatial.Topology.Point pnt = new DotSpatial.Topology.Point(Map.PixelToProj(e.Location));
                    if (this.lunkuo.Distance(pnt) < 7)          //鼠标位置到轮廓线的距离小于7，则在轮廓中添加点
                    {
                        for (int j = 0; j < lunkuo.NumPoints-1; j++)            //判断一下到轮廓线的哪一段距离小于7
                        {
                            LineString tmpLr = new LineString(new List<Coordinate>() { lunkuo.Coordinates[j], lunkuo.Coordinates[j + 1] });
                            if (tmpLr.Distance(pnt) < 7)
                            {
                                lunkuo.Coordinates.Insert(j + 1, pnt.Coordinate);

                                (targetSet.Features[0].BasicGeometry as Polygon).Shell = lunkuo;
                                targetSet.InitializeVertices();
                                Map.ResetBuffer();
                                targetSet.Save();

                                IFeature feature = targetSetPoint.AddFeature(pnt);
                                feature.DataRow.BeginEdit();
                                feature.DataRow["X"] = pnt.Coordinate.X;
                                feature.DataRow["Y"] = pnt.Coordinate.Y;
                                feature.DataRow.EndEdit();
                    
                                targetSetPoint.InitializeVertices();
                                targetSetPoint.Save();
                                targetSetPoint.InvalidateVertices();
                                break;
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < daoyu.Length; i++)
                        {
                            bool suc = false;
                            if (this.daoyu[i].Distance(pnt) < 7)    //鼠标位置到某个岛屿边界的距离小于7，则在该岛屿中添加点
                            {
                                for (int j = 0; j < daoyu[i].NumPoints - 1; j++)            //判断一下到轮廓线的哪一段距离小于7
                                {
                                    LineString tmpLr = new LineString(new List<Coordinate>() { daoyu[i].Coordinates[j], daoyu[i].Coordinates[j + 1] });
                                    if (tmpLr.Distance(pnt) < 7)
                                    {
                                        daoyu[i].Coordinates.Insert(j + 1, pnt.Coordinate);

                                        (targetSet.Features[0].BasicGeometry as Polygon).Holes = daoyu;
                                        targetSet.InitializeVertices();
                                        Map.ResetBuffer();
                                        targetSet.Save();

                                        IFeature feature = targetSetPoint.AddFeature(pnt);
                                        feature.DataRow.BeginEdit();
                                        feature.DataRow["X"] = pnt.Coordinate.X;
                                        feature.DataRow["Y"] = pnt.Coordinate.Y;
                                        feature.DataRow.EndEdit();

                                        targetSetPoint.InitializeVertices();
                                        targetSetPoint.Save();
                                        targetSetPoint.InvalidateVertices();

                                        suc = true;
                                        break;
                                    }
                                }
                            }
                            if (suc) break;
                        }
                    }
                }


                //if (coordList.Count > 2)
                //{
                //    Polygon tmpPolygon = new Polygon(coordList);
                //    IFeature poFeature = targetSetPoly.AddFeature(tmpPolygon);

                //    targetSetPoly.InitializeVertices();
                //    Map.ResetBuffer();
                //    targetSetPoly.Save();

                //    foreach (Coordinate coor in coordList)
                //    {
                //        DotSpatial.Topology.Point tmp = new DotSpatial.Topology.Point(coor);
                //        IFeature feature = targetSetPoint.AddFeature(tmp);
                //        feature.DataRow.BeginEdit();
                //        feature.DataRow["X"] = coor.X;
                //        feature.DataRow["Y"] = coor.Y;
                //        feature.DataRow.EndEdit();
                //    }
                //    targetSetPoint.InitializeVertices();
                //    targetSetPoint.Save();
                //    targetSetPoint.InvalidateVertices();
                //    coordList = null;
                //    firstClick = true;
                //    this.Deactivate();
                //}
                #endregion
            }
            else
            {
                e.Map.MapFrame.ResetExtents();
            }
            base.OnMouseUp(e);
        }

        protected override void OnDraw(MapDrawArgs e)
        {
            Pen redPen = new Pen(Color.Red, 3F);
            Brush redBrush = new SolidBrush(Color.Red);
            List<Point> points = new List<Point>();
            //if (coordList != null)
            //{
            //    foreach (Coordinate coord in coordList)
            //    {
            //        points.Add(Map.ProjToPixel(coord));
            //    }
            //    if (points.Count > 1)
            //    {
            //        e.Graphics.DrawLines(redPen, points.ToArray());
            //    }
            //}
            //轮廓
            foreach (Coordinate coord in lunkuo.Coordinates)
            {
                points.Add(Map.ProjToPixel(coord));
            }
            if (points.Count > 1)
            {
                e.Graphics.DrawPolygon(redPen, points.ToArray());
                for (int i = 0; i < points.Count; i++)
                {
                    e.Graphics.FillRectangle(redBrush, new Rectangle(points[i].X - 3, points[i].Y - 3, 7, 7));
                }
                
                //e.Graphics.DrawLines(redPen, points.ToArray());
            }

            //岛屿
            for (int i = 0; i < daoyu.Length; i++)
            {
                points.Clear();
                foreach (Coordinate coord in daoyu[i].Coordinates)
                {
                    points.Add(Map.ProjToPixel(coord));
                }
                if (points.Count > 1)
                {
                    e.Graphics.DrawPolygon(redPen, points.ToArray());
                     for (int j = 0; j < points.Count; j++)
                    {
                        e.Graphics.FillRectangle(redBrush, new Rectangle(points[j].X - 3, points[j].Y - 3, 7, 7));
                    }
                    //e.Graphics.DrawLines(redPen, points.ToArray());
                }
            }

            redPen.Dispose();
            base.OnDraw(e);
        }
    }
}
