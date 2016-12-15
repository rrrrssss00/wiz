using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Drawing2D;
using DotSpatial.Data;
using DotSpatial.Controls;
using DotSpatial.Symbology;
using DotSpatial.Topology;
using DotSpatial.Topology.Algorithm;
using Point = System.Drawing.Point;
using System.Windows.Forms;

namespace GISMODEL
{
    public class AddPolygon : MapFunction
    {
        //勾画河道边界工具
        private IMapLayer targetLayer, targetLayerPoint;
        private FeatureSet targetSetPoly, targetSetPoint;
        private Point position;
        private Rectangle source;
        private bool firstClick = true;
        public string modPath = "";

        public AddPolygon(IMap inMap)
            : base(inMap)
        {
            YieldStyle = YieldStyles.LeftButton;
        }

        protected override void OnActivate()
        {
            bool ifFind = false;
            for (int i = 0; i < Map.Layers.Count; i++)
            {
                if (Map.Layers[i].DataSet.Name == "河道轮廓" && Map.Layers[i] is IMapPolygonLayer)
                {
                    targetLayer = Map.Layers[i] as IMapPolygonLayer;
                    targetSetPoly = targetLayer.DataSet as FeatureSet;
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
            base.OnActivate();
        }

        private List<Coordinate> coordList;
        protected override void OnMouseDown(GeoMouseArgs e)
        {
            this.source = e.Map.MapFrame.View;
            this.position = e.Location;
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(GeoMouseArgs e)
        {
            if (this.position != e.Location && e.Button == MouseButtons.Left)
            {
                Point diff = new Point { X = this.position.X - e.X, Y = this.position.Y - e.Y };
                e.Map.MapFrame.View = new Rectangle(this.source.X + diff.X, this.source.Y + diff.Y, this.source.Width,
                                                    this.source.Height);
                Map.Invalidate();
            }
            else if (coordList != null && coordList.Count > 0)
            {
                List<Point> mpoints = coordList.Select(coord => Map.ProjToPixel(coord)).ToList();
                Rectangle oldRect = SymbologyGlobal.GetRectangle(_mousePosition, mpoints[mpoints.Count - 1]);
                Rectangle newRect = SymbologyGlobal.GetRectangle(e.Location, mpoints[mpoints.Count - 1]);
                Rectangle invalid = Rectangle.Union(newRect, oldRect);
                invalid = Rectangle.Union(invalid, SymbologyGlobal.GetRectangle(_mousePosition, mpoints[0]));
                invalid = Rectangle.Union(invalid, SymbologyGlobal.GetRectangle(e.Location, mpoints[0]));
                invalid.Inflate(20, 20);
                Map.Invalidate(invalid);
            }
            _mousePosition = e.Location;

            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(GeoMouseArgs e)
        {
            if (this.position == e.Location && e.Button == MouseButtons.Left)
            {
                #region
                if (targetSetPoly.Features.Count > 0)
                {
                    MessageBox.Show("\"河道边界\"已经存在,请不要重复输入");
                    return;
                }

                Coordinate coord = Map.PixelToProj(e.Location);
                if (firstClick)
                {
                    coordList = new List<Coordinate>();
                    coordList.Add(coord);
                    firstClick = false;
                }
                else
                {
                    bool ifFind = false;
                    for (int i = 0; i < coordList.Count; i++)
                    {
                        if (coordList[i].X == coord.X && coordList[i].Y == coord.Y)
                        {
                            ifFind = true;
                            break;
                        }
                    }
                    if (!ifFind)
                        coordList.Add(coord);
                }
                #endregion
            }
            else if (e.Button == MouseButtons.Right)
            {
                #region

                if (coordList == null) return;
                if (coordList.Count > 2)
                {
                    Polygon tmpPolygon = new Polygon(coordList);
                    IFeature poFeature = targetSetPoly.AddFeature(tmpPolygon);

                    targetSetPoly.InitializeVertices();
                    Map.ResetBuffer();
                    targetSetPoly.Save();

                    foreach (Coordinate coor in coordList)
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
                    coordList = null;
                    firstClick = true;
                    this.Deactivate();
                }
                #endregion
            }
            else
            {
                e.Map.MapFrame.ResetExtents();
            }
            base.OnMouseUp(e);
        }

        protected override void OnMouseDoubleClick(GeoMouseArgs e)
        {
            //if (e.Button == MouseButtons.Left && coordList.Count>2)
            //{
            //    Polygon tmpPolygon = new Polygon(coordList);
            //    IFeature poFeature = targetSet.AddFeature(tmpPolygon);

            //    targetSet.InitializeVertices();
            //    Map.ResetBuffer();
            //    targetSet.Save();

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
            //}

            base.OnMouseDoubleClick(e);
        }

        private Point _mousePosition = new Point();
        protected override void OnDraw(MapDrawArgs e)
        {
            Point mouseTest = Map.PointToClient(Control.MousePosition);

            bool hasMouse = Map.ClientRectangle.Contains(mouseTest);

            Pen redPen = new Pen(Color.Red, 3F);
            Brush redBrush = new SolidBrush(Color.Red);

            GraphicsPath previous = new GraphicsPath();
            previous.FillMode = FillMode.Winding;

            List<Point> points = new List<Point>();
            if (coordList != null)
            {
                foreach (Coordinate coord in coordList)
                {
                    points.Add(Map.ProjToPixel(coord));
                }
                if (points.Count > 1)
                {
                    e.Graphics.DrawLines(redPen, points.ToArray());
                }
            }
            if (points.Count > 0 && hasMouse)
            {
                e.Graphics.DrawLine(redPen, points[points.Count - 1], _mousePosition);
            }
            if (points.Count > 1 && hasMouse)
            {
                e.Graphics.DrawLine(redPen, points[0], _mousePosition);
            }

            redPen.Dispose();
            redBrush.Dispose();
            base.OnDraw(e);
        }
    }
}
