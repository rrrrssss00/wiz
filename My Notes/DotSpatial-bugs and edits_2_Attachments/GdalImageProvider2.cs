﻿// ********************************************************************************************************
// Product Name: DotSpatial.Gdal
// Description:  This is a data extension for the System.Spatial framework.
// ********************************************************************************************************
// The contents of this file are subject to the Gnu Lesser General Public License (LGPL)
// you may not use this file except in compliance with the License. You may obtain a copy of the License at
// http://dotspatial.codeplex.com/license
//
// Software distributed under the License is distributed on an "AS IS" basis, WITHOUT WARRANTY OF
// ANY KIND, either expressed or implied. See the License for the specific language governing rights and
// limitations under the License.
//
// The Original Code is from a plugin for MapWindow version 6.0
//
// The Initial Developer of this Original Code is Ted Dunsford. Created 12/10/2008 11:32:21 AM
//
// Contributor(s): (Open source contributors should list themselves and their modifications here).
// |     Name          |    Date     |              Comments
// |-------------------|-------------|-------------------------------------------------------------------
// ********************************************************************************************************

using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using OSGeo.GDAL;

namespace DotSpatial.Data.Rasters.GdalExtension
{
    /// <summary>
    /// GDalImageProvider acts as the factory to create IImageData files that use the GDAL libraries
    /// </summary>
    public class GdalImageProvider2 : IImageDataProvider
    {
        private IProgressHandler _prog;
        public GdalImageProvider2()
        {
            string[] extensions = { ".tif", ".tiff", ".adf" };
            foreach (string extension in extensions)
            {
                if (!DotSpatial.Data.DataManager.DefaultDataManager.PreferredProviders.ContainsKey(extension))
                {
                    DotSpatial.Data.DataManager.DefaultDataManager.PreferredProviders.Add(extension, this);
                }
            }
        }


        #region IImageDataProvider Members

        /// <summary>
        /// Creates a new image given the specified file format
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="inRam">if set to <c>true</c> should load entire file in ram.</param>
        /// <param name="progHandler">The prog handler.</param>
        /// <param name="bandType">Type of the band.</param>
        /// <returns></returns>
        public IImageData Create(string fileName, int width, int height, bool inRam, IProgressHandler progHandler, ImageBandType bandType)
        {
            Gdal.AllRegister();
            Driver d = GetDriverByExtension(fileName);
            if (d == null) return null;
            Dataset ds;
            if (bandType == ImageBandType.ARGB)
            {
                ds = d.Create(fileName, width, height, 4, DataType.GDT_Byte, new string[] { });
            }
            else if (bandType == ImageBandType.RGB)
            {
                ds = d.Create(fileName, width, height, 3, DataType.GDT_Byte, new string[] { });
            }
            else if (bandType == ImageBandType.PalletCoded)
            {
                ds = d.Create(fileName, width, height, 1, DataType.GDT_Byte, new string[] { });
            }
            else
            {
                ds = d.Create(fileName, width, height, 1, DataType.GDT_Byte, new string[] { });
            }

            return new GdalImage2(fileName, ds, bandType);
        }

        /// <summary>
        /// Opens an existing file using the specified parameters
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public IImageData Open(string fileName)
        {
            //int fileNameBytes = System.Text.Encoding.UTF8.GetByteCount(fileName);
            //if (fileNameBytes > 64) throw new Exception("路径过长");
            return OpenFile(fileName);
        }

        IDataSet IDataProvider.Open(string fileName)
        {
            //int fileNameBytes = System.Text.Encoding.UTF8.GetByteCount(fileName);
            //if (fileNameBytes > 100) throw new Exception("路径过长");
            return OpenFile(fileName);
        }

        /// <summary>
        /// Gets or sets the description
        /// </summary>
        public string Description
        {
            get { return "Images supported by GDAL"; }
        }

        /// <summary>
        /// Gets or sets the dialog read filter
        /// </summary>
        public string DialogReadFilter
        {
            get { return "Images|*.bmp;*.jpg;*.gif;*.gen;*.thf;*.blx;*.xlb;*.kap;*.bag;*.bt;*.doq;*.dt0;*.dt2;*.ers;*.n1;*.fits;*.hdr;*.grb;*.img;*.mpr;*.mpl;*.j2k;*.tif;*.sid;*.ecw;*.jp2;*.png;*.ppm;*.pgm;*.rik;*.rsw;*.mtw;*.ddf;*.ter;*.dem;*.toc"; }
        }

        /// <summary>
        /// Gets or sets the dialog write filter
        /// </summary>
        public string DialogWriteFilter
        {
            get { return "Images|*.bmp;*.jpg;*.gif;*.gen;*.thf;*.blx;*.xlb;*.bag;*.kap;*.bt;*.doq;*.dt0;*.dt2;*.ers;*.n1;*.fits;*.hdr;*.grb;*.img;*.mpr;*.mpl;*.j2k;*.tif;*.sid;*.ecw;*.jp2;*.png;*.ppm;*.pgm;*.rik;*.rsw;*.mtw;*.ddf;*.ter;*.dem;*.toc"; }
        }

        /// <summary>
        /// Gets or sets the string name
        /// </summary>
        public string Name
        {
            get { return "GDAL"; }
        }

        /// <summary>
        /// Gets or sets the progress handler
        /// </summary>
        public IProgressHandler ProgressHandler
        {
            get { return _prog; }
            set { _prog = value; }
        }

        #endregion

        /// <summary>
        /// http://www.gdal.org/formats_list.html
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        private static Driver GetDriverByExtension(string filename)
        {
            string ext = Path.GetExtension(filename);
            if (ext != null)
            {
                ext = ext.Replace(".", String.Empty).ToLower();
                switch (ext)
                {
                    case @"asc": return Gdal.GetDriverByName("AAIGrid");
                    case @"gen":
                    case @"thf": return Gdal.GetDriverByName("ADRG");
                    case @"adf": return Gdal.GetDriverByName("AIG");
                    case @"blx":
                    case @"bxl": return Gdal.GetDriverByName("BLX");
                    case @"bag":
                        return Gdal.GetDriverByName("BAG");
                    case @"bmp":
                        return Gdal.GetDriverByName("BMP");
                    case @"kap":
                        return Gdal.GetDriverByName("BSB");
                    case @"bt":
                        return Gdal.GetDriverByName("BT");
                    case @"dim":
                        return Gdal.GetDriverByName("DIM");
                    case @"doq":
                        return Gdal.GetDriverByName("DOQ2");
                    case @"dt0":
                    case @"dt1":
                    case @"dt2":
                        return Gdal.GetDriverByName("DTED");
                    case @"ecw":
                        return Gdal.GetDriverByName("ECW");
                    case @"htr":
                        return Gdal.GetDriverByName("EHdr");
                    case @"ers":
                        return Gdal.GetDriverByName("ERS");
                    case @"nl":
                        return Gdal.GetDriverByName("ESAT");
                    case @"gif":
                        return Gdal.GetDriverByName("GIF");
                    case @"tif":
                        return Gdal.GetDriverByName("GTiff");
                    case @"jpg":
                        return Gdal.GetDriverByName("JPEG");
                    case @"jp2":
                    case @"j2k":
                        return Gdal.GetDriverByName("JPEG2000");
                    case @"ppm":
                    case @"pgm":
                        return Gdal.GetDriverByName("PNM");
                    case @"png":
                        return Gdal.GetDriverByName("PNG");
                    case @"rik":
                        return Gdal.GetDriverByName("RIK");
                    case @"rsw":
                    case @"mtw":
                        return Gdal.GetDriverByName("RMF");
                    case @"ter":
                        return Gdal.GetDriverByName("TERRAGEN");
                    case @"dem":
                        return Gdal.GetDriverByName("USGSDEM");
                    case @".vrt":
                        return Gdal.GetDriverByName("VRT");
                    case @"xpm":
                        return Gdal.GetDriverByName("XPM");
                }
            }
            else
            {
                return Gdal.GetDriverByName("AAIGrid");
            }
            return null;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private IImageData OpenFile(string fileName)
        {
            Dataset dataset;
            try
            {
                dataset = Gdal.Open(fileName, Access.GA_Update);
            }
            catch
            {
                try
                {
                    dataset = Gdal.Open(fileName, Access.GA_ReadOnly);
                }
                catch (Exception ex)
                {
                    throw new GdalException(ex.ToString());
                }
            }

            Band red = dataset.GetRasterBand(1);
            ColorInterp bandType = red.GetRasterColorInterpretation();
            if (bandType != ColorInterp.GCI_PaletteIndex &&
                bandType != ColorInterp.GCI_GrayIndex &&
                bandType != ColorInterp.GCI_RedBand &&
                bandType != ColorInterp.GCI_AlphaBand)
            {
                // This is an image, not a raster, so return null.
                dataset.Dispose();
                dataset = null;
                return null;
            }

            GdalImage2 result = new GdalImage2(fileName);
            if (result.Width > 8000 || result.Height > 8000)
            {
                // Firstly, if there are pyramids inside of the GDAL file itself, we can just work with this directly,
                // without creating our own pyramid image.

                // For now, we can't get fast, low-res versions without some kind of pyramiding happening.
                // since that can take a while for huge images, I'd rather do this once, and create a kind of
                // standardized file-based pyramid system.  Maybe in future pyramid tiffs could be used instead?
                string pyrFile = Path.ChangeExtension(fileName, ".mwi");
                if (File.Exists(pyrFile))
                {
                    if (File.Exists(Path.ChangeExtension(pyrFile, ".mwh")))
                    {
                        return new PyramidImage(fileName);
                    }
                    File.Delete(pyrFile);
                }

                GdalImageSource2 gs = new GdalImageSource2(fileName);
                PyramidImage py = new PyramidImage(pyrFile, gs.Bounds);
                int width = gs.Bounds.NumColumns;
                int blockHeight = 64000000 / width;
                if (blockHeight > gs.Bounds.NumRows) blockHeight = gs.Bounds.NumRows;
                int numBlocks = (int)Math.Ceiling(gs.Bounds.NumRows / (double)blockHeight);
                ProgressMeter pm = new ProgressMeter(ProgressHandler, "Copying Data To Pyramids", numBlocks * 2);
                //ProgressHandler.Progress("pyramid", 0, "Copying Data To Pyramids: 0% Complete");
                Application.DoEvents();
                for (int j = 0; j < numBlocks; j++)
                {
                    int h = blockHeight;
                    if (j == numBlocks - 1)
                    {
                        h = gs.Bounds.NumRows - j * blockHeight;
                    }
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    byte[] vals = gs.ReadWindow(j * blockHeight, 0, h, width, 0);
                    Debug.WriteLine("Reading Value time: " + sw.ElapsedMilliseconds);
                    pm.CurrentValue = j * 2 + 1;
                    sw.Reset();
                    sw.Start();
                    py.WriteWindow(vals, j * blockHeight, 0, h, width, 0);
                    sw.Stop();
                    Debug.WriteLine("Writing Pyramid time: " + sw.ElapsedMilliseconds);
                    pm.CurrentValue = (j + 1) * 2;
                }
                gs.Dispose();
                pm.Reset();
                py.ProgressHandler = ProgressHandler;
                py.CreatePyramids();
                py.WriteHeader(pyrFile);
                return py;
            }
            result.Open();
            return result;
        }
    }
}