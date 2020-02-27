// Copyright 2005, 2006 - Morten Nielsen (www.iter.dk)
//
// This file is part of SharpMap.
// SharpMap is free software; you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
// 
// SharpMap is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.

// You should have received a copy of the GNU Lesser General Public License
// along with SharpMap; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA 

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using GeoAPI.Geometries;
using SharpMap.Rendering.Symbolizer;
using SharpMap.Styles;
using SharpMap.Utilities;
using Point=GeoAPI.Geometries.Coordinate;
using System.Runtime.CompilerServices;

namespace SharpMap.Rendering
{
    /// <summary>
    /// This class renders individual geometry features to a graphics object using the settings of a map object.
    /// </summary>
    public static class VectorRenderer
    {
        internal const float ExtremeValueLimit = 1E+8f;
        internal const float NearZero = 1E-30f; // 1/Infinity

        static VectorRenderer()
        {
            SizeOfString = SizeOfStringCeiling;
        }

        private static readonly Bitmap Defaultsymbol =
            (Bitmap)
            Image.FromStream(
                Assembly.GetExecutingAssembly().GetManifestResourceStream("SharpMap.Styles.DefaultSymbol.png"));

        /// <summary>
        /// Renders a MultiLineString to the map.
        /// </summary>
        /// <param name="g">Graphics reference</param>
        /// <param name="lines">MultiLineString to be rendered</param>
        /// <param name="pen">Pen style used for rendering</param>
        /// <param name="map">Map reference</param>
        /// <param name="offset">Offset by which line will be moved to right</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void DrawMultiLineString(Graphics g, IMultiLineString lines, Pen pen, MapViewport map, float offset)
        {
            DrawMultiLineStringEx(g, lines, pen, map, offset);
        }

        /// <summary>
        /// Renders a MultiLineString to the map.
        /// </summary>
        /// <param name="g">Graphics reference</param>
        /// <param name="lines">MultiLineString to be rendered</param>
        /// <param name="pen">Pen style used for rendering</param>
        /// <param name="map">Map reference</param>
        /// <param name="offset">Offset by which line will be moved to right</param>
        /// <returns>The area of the map that was affected by the drawing operation</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static RectangleF DrawMultiLineStringEx(Graphics g, IMultiLineString lines, Pen pen, MapViewport map, float offset)

        {
            var affectedArea = new RectangleF();
            for(int i = 0; i < lines.NumGeometries; i++)
            {
                var line = (ILineString) lines[i];
                affectedArea = RectExpandToInclude(affectedArea, DrawLineStringEx(g, line, pen, map, offset));
            }

            return affectedArea;
        }

        /// <summary>
        /// Offset drawn linestring by given pixel width
        /// </summary>
        /// <param name="points"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        internal static PointF[] OffsetRight(PointF[] points, float offset)
        {
            int length = points.Length;
            var newPoints = new PointF[(length - 1) * 2];

            float space = (offset * offset / 4) + 1;

            //if there are two or more points
            if (length >= 2)
            {
                var counter = 0;
                float x = 0, y = 0;
                for (var i = 0; i < length - 1; i++)
                {
                    var b = -(points[i + 1].X - points[i].X);
                    if (b != 0)
                    {
                        var a = points[i + 1].Y - points[i].Y;
                        var c = a / b;
                        y = 2 * (float)Math.Sqrt(space / (c * c + 1));
                        y = b < 0 ? y : -y;
                        x = c * y;

                        if (offset < 0)
                        {
                            y = -y;
                            x = -x;
                        }

                        newPoints[counter] = new PointF(points[i].X + x, points[i].Y + y);
                        newPoints[counter + 1] = new PointF(points[i + 1].X + x, points[i + 1].Y + y);
                    }
                    else
                    {
                        newPoints[counter] = new PointF(points[i].X + x, points[i].Y + y);
                        newPoints[counter + 1] = new PointF(points[i + 1].X + x, points[i + 1].Y + y);
                    }
                    counter += 2;
                }

                return newPoints;
            }
            return points;
        }

        /// <summary>
        /// Renders a LineString to the map.
        /// </summary>
        /// <param name="g">Graphics reference</param>
        /// <param name="line">LineString to render</param>
        /// <param name="pen">Pen style used for rendering</param>
        /// <param name="map">Map reference</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        [Obsolete("Not called, will be removed")]
        public static void DrawLineString(Graphics g, ILineString line, Pen pen, MapViewport map)
        {
            DrawLineString(g, line, pen, map, 0f);
        }

        /// <summary>
        /// Renders a LineString to the map.
        /// </summary>
        /// <param name="g">Graphics reference</param>
        /// <param name="line">LineString to render</param>
        /// <param name="pen">Pen style used for rendering</param>
        /// <param name="map">Map reference</param>
        /// <param name="offset">Offset by which line will be moved to right</param>
        public static void DrawLineString(Graphics g, ILineString line, Pen pen, MapViewport map, float offset)
        {
            DrawLineStringEx(g, line, pen, map, offset);
        }

        /// <summary>
        /// Renders a LineString to the map.
        /// </summary>
        /// <param name="g">Graphics reference</param>
        /// <param name="line">LineString to render</param>
        /// <param name="pen">Pen style used for rendering</param>
        /// <param name="map">Map reference</param>
        /// <param name="offset">Offset by which line will be moved to right</param>
        /// <returns>The area of the map that was affected by the drawing of the geometry.</returns>
        public static RectangleF DrawLineStringEx(Graphics g, ILineString line, Pen pen, MapViewport map, float offset)
        {
            var points = line.TransformToImage(map);
            if (points.Length > 1)
            {
                using (var gp = new GraphicsPath())
                {
                    if (offset != 0d)
                        points = OffsetRight(points, offset);
                    gp.AddLines(LimitValues(points, ExtremeValueLimit));

                    g.DrawPath(pen, gp);
                    return gp.GetBounds();
                }
            }
            return new RectangleF();
        }

        /// <summary>
        /// Renders a multipolygon byt rendering each polygon in the collection by calling DrawPolygon.
        /// </summary>
        /// <param name="g">Graphics reference</param>
        /// <param name="multiPolygon">MultiPolygon to render</param>
        /// <param name="brush">Brush used for filling (null or transparent for no filling)</param>
        /// <param name="pen">Outline pen style (null if no outline)</param>
        /// <param name="clip">Specifies whether polygon clipping should be applied</param>
        /// <param name="map">Map reference</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void DrawMultiPolygon(Graphics g, IMultiPolygon multiPolygon, Brush brush, Pen pen, bool clip, MapViewport map)
        {
            DrawMultiPolygonEx(g, multiPolygon, brush, pen, clip, map);
        }

        /// <summary>
        /// Renders a multipolygon byt rendering each polygon in the collection by calling DrawPolygon.
        /// </summary>
        /// <param name="g">Graphics reference</param>
        /// <param name="multiPolygon">MultiPolygon to render</param>
        /// <param name="brush">Brush used for filling (null or transparent for no filling)</param>
        /// <param name="pen">Outline pen style (null if no outline)</param>
        /// <param name="clip">Specifies whether polygon clipping should be applied</param>
        /// <param name="map">Map reference</param>
        /// <returns>The area of the map that was affected by the drawing of the geometry.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static RectangleF DrawMultiPolygonEx(Graphics g, IMultiPolygon multiPolygon, Brush brush, Pen pen, bool clip, MapViewport map)
        {
            var affectedArea = new RectangleF();
            for (var i = 0; i < multiPolygon.NumGeometries;i++ )
            {
                var p = (IPolygon)multiPolygon[i];
                var rect = DrawPolygonEx(g, p, brush, pen, clip, map);
                affectedArea = RectangleF.Union(affectedArea, rect); //RectExpandToInclude(bounds, rect);
            }

            return affectedArea;
        }

        /// <summary>
        /// Renders a polygon to the map.
        /// </summary>
        /// <param name="g">Graphics reference</param>
        /// <param name="pol">Polygon to render</param>
        /// <param name="brush">Brush used for filling (null or transparent for no filling)</param>
        /// <param name="pen">Outline pen style (null if no outline)</param>
        /// <param name="clip">Specifies whether polygon clipping should be applied</param>
        /// <param name="map">Map reference</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void DrawPolygon(Graphics g, IPolygon pol, Brush brush, Pen pen, bool clip, MapViewport map)
        {
            DrawPolygonEx(g, pol, brush, pen, clip, map);
        }

        /// <summary>
        /// Renders a polygon to the map.
        /// </summary>
        /// <param name="g">Graphics reference</param>
        /// <param name="pol">Polygon to render</param>
        /// <param name="brush">Brush used for filling (null or transparent for no filling)</param>
        /// <param name="pen">Outline pen style (null if no outline)</param>
        /// <param name="clip">Specifies whether polygon clipping should be applied</param>
        /// <param name="map">Map reference</param>
        /// <returns>The area of the map that was affected by the drawing of the geometry.</returns>
        public static RectangleF DrawPolygonEx(Graphics g, IPolygon pol, Brush brush, Pen pen, bool clip, MapViewport map)
        {
            if (pol.ExteriorRing == null) 
                return RectangleF.Empty;

            var points = pol.ExteriorRing.TransformToImage(map);
            if (points.Length <= 2) return RectangleF.Empty;

            //Use a graphics path instead of DrawPolygon. DrawPolygon has a problem with several interior holes
            using (var gp = new GraphicsPath())
            {
                //Add the exterior polygon
                if (!clip)
                    gp.AddPolygon(LimitValues(points, ExtremeValueLimit));
                else
                    DrawPolygonClipped(gp, LimitValues(points, ExtremeValueLimit), map.Size.Width, map.Size.Height);

                //Add the interior polygons (holes)
                if (pol.NumInteriorRings > 0)
                {
                    foreach (ILinearRing ring in pol.InteriorRings)
                    {
                        points = ring.TransformToImage(map);
                        if (!clip)
                            gp.AddPolygon(LimitValues(points, ExtremeValueLimit));
                        else
                            DrawPolygonClipped(gp, LimitValues(points, ExtremeValueLimit), map.Size.Width,
                                map.Size.Height);
                    }
                }


                // Only render inside of polygon if the brush isn't null or isn't transparent
                if (brush != null && brush != Brushes.Transparent)
                    g.FillPath(brush, gp);

                // Create an outline if a pen style is available
                if (pen != null)
                    g.DrawPath(pen, gp);

                return gp.GetBounds();
            }
        }

        private static void DrawPolygonClipped(GraphicsPath gp, PointF[] polygon, int width, int height)
        {
            var clipState = DetermineClipState(polygon, width, height);
            if (clipState == ClipState.Within)
            {
                gp.AddPolygon(polygon);
            }
            else if (clipState == ClipState.Intersecting)
            {
                var clippedPolygon = ClipPolygon(polygon, width, height);
                if (clippedPolygon.Length > 2)
                    gp.AddPolygon(clippedPolygon);
            }
        }

        /// <summary>
        /// Purpose of this method is to prevent the 'overflow error' exception in the FillPath method. 
        /// This Exception is thrown when the coordinate values become too big (values over -2E+9f always 
        /// throw an exception, values under 1E+8f seem to be okay). This method limits the coordinates to 
        /// the values given by the second parameter (plus an minus). Theoretically the lines to and from 
        /// these limited points are not correct but GDI+ paints incorrect even before that limit is reached. 
        /// </summary>
        /// <param name="vertices">The vertices that need to be limited</param>
        /// <param name="limit">The limit at which coordinate values will be cutoff</param>
        /// <returns>The limited vertices</returns>
        public static PointF[] LimitValues(PointF[] vertices, float limit)
        {
            for (var i = 0; i < vertices.Length; i++)
            {
                vertices[i].X = Math.Max(-limit, Math.Min(limit, vertices[i].X));
                vertices[i].Y = Math.Max(-limit, Math.Min(limit, vertices[i].Y));
            }
            return vertices;
        }

        /// <summary>
        /// Signature for a function that evaluates the length of a string when rendered on a Graphics object with a given font
        /// </summary>
        /// <param name="g"><see cref="Graphics"/> object</param>
        /// <param name="text">the text to render</param>
        /// <param name="font">the font to use</param>
        /// <returns>the size</returns>
        public delegate SizeF SizeOfStringDelegate(Graphics g, string text, Font font);

        private static SizeOfStringDelegate _sizeOfString;

        /// <summary>
        /// Delegate used to determine the <see cref="SizeF"/> of a given string.
        /// </summary>
        public static SizeOfStringDelegate SizeOfString
        {
            get { return _sizeOfString ?? (_sizeOfString = SizeOfStringCeiling); }
            set 
            {  
                if (value != null )
                    _sizeOfString = value;
            }
        }

        /// <summary>
        /// Function to get the <see cref="SizeF"/> of a string when rendered with the given font.
        /// </summary>
        /// <param name="g"><see cref="Graphics"/> object</param>
        /// <param name="text">the text to render</param>
        /// <param name="font">the font to use</param>
        /// <returns>the size</returns>
        public static SizeF SizeOfStringBase(Graphics g, string text, Font font)
        {
            return g.MeasureString(text, font);
        }

        /// <summary>
        /// Function to get the <see cref="SizeF"/> of a string when rendered with the given font.
        /// </summary>
        /// <param name="g"><see cref="Graphics"/> object</param>
        /// <param name="text">the text to render</param>
        /// <param name="font">the font to use</param>
        /// <returns>the size</returns>
        [Obsolete]
        public static SizeF SizeOfString74(Graphics g, string text, Font font)
        {
            var s = g.MeasureString(text, font);
            return new SizeF(s.Width * 0.74f+1f, s.Height * 0.74f); 
        }
        /// <summary>
        /// Function to get the <see cref="SizeF"/> of a string when rendered with the given font.
        /// </summary>
        /// <param name="g"><see cref="Graphics"/> object</param>
        /// <param name="text">the text to render</param>
        /// <param name="font">the font to use</param>
        /// <returns>the size</returns>
        public static SizeF SizeOfStringCeiling(Graphics g, string text, Font font)
        {
            SizeF f = g.MeasureString(text, font);
            return new SizeF((float)Math.Ceiling(f.Width), (float)Math.Ceiling(f.Height));
        }

        /// <summary>
        /// Renders a label to the map.
        /// </summary>
        /// <param name="g">Graphics reference</param>
        /// <param name="labelPoint">Label placement</param>
        /// <param name="offset">Offset of label in screen coordinates</param>
        /// <param name="font">Font used for rendering</param>
        /// <param name="foreColor">Font foreground color</param>
        /// <param name="backColor">Background color</param>
        /// <param name="halo">Color of halo</param>
        /// <param name="rotation">Text rotation in degrees</param>
        /// <param name="text">Text to render</param>
        /// <param name="map">Map reference</param>
        /// <param name="alignment">Horizontal alignment for multi line labels. If not set <see cref="StringAlignment.Near"/> is used</param>
        /// <param name="rotationPoint">Point where the rotation should take place</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void DrawLabel(Graphics g, PointF labelPoint, PointF offset, Font font, Color foreColor,
            Brush backColor, Pen halo, float rotation, string text, MapViewport map,
            LabelStyle.HorizontalAlignmentEnum alignment = LabelStyle.HorizontalAlignmentEnum.Left,
            PointF? rotationPoint = null)
        {
            DrawLabelEx(g, labelPoint, offset, font, foreColor, backColor, halo, rotation, text, map, alignment, rotationPoint);
        }

        /// <summary>
        /// Renders a label to the map.
        /// </summary>
        /// <param name="g">Graphics reference</param>
        /// <param name="labelPoint">Label placement</param>
        /// <param name="offset">Offset of label in screen coordinates</param>
        /// <param name="font">Font used for rendering</param>
        /// <param name="foreColor">Font foreground color</param>
        /// <param name="backColor">Background color</param>
        /// <param name="halo">Color of halo</param>
        /// <param name="rotation">Text rotation in degrees</param>
        /// <param name="text">Text to render</param>
        /// <param name="map">Map reference</param>
        /// <param name="alignment">Horizontal alignment for multi line labels. If not set <see cref="StringAlignment.Near"/> is used</param>
        /// <param name="rotationPoint">Point where the rotation should take place</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static RectangleF DrawLabelEx(Graphics g, PointF labelPoint, PointF offset, Font font, Color foreColor,
            Brush backColor, Pen halo, float rotation, string text, MapViewport map,
            LabelStyle.HorizontalAlignmentEnum alignment = LabelStyle.HorizontalAlignmentEnum.Left,
            PointF? rotationPoint = null)
        {
                //Calculate the size of the text
                var labelSize = _sizeOfString(g, text, font);
            
            //Add label offset
            labelPoint.X += offset.X;
            labelPoint.Y += offset.Y;

            //Translate alignment to stringalignment
            StringAlignment salign;
            switch (alignment)
            {
                case LabelStyle.HorizontalAlignmentEnum.Left:
                    salign = StringAlignment.Near;
                    break;
                case LabelStyle.HorizontalAlignmentEnum.Center:
                    salign = StringAlignment.Center;
                    break;
                default:
                    salign = StringAlignment.Far;
                    break;
            }

            if (rotation != 0 && !float.IsNaN(rotation))
            {
                rotationPoint = rotationPoint ?? labelPoint;

                //g.FillEllipse(Brushes.LawnGreen, rotationPoint.Value.X - 1, rotationPoint.Value.Y - 1, 2, 2);
                RectangleF bounds;
                using (var t = g.Transform.Clone())
                {
                    g.TranslateTransform(rotationPoint.Value.X, rotationPoint.Value.Y);
                    g.RotateTransform(rotation);
                    //g.TranslateTransform(-labelSize.Width/2, -labelSize.Height/2);
                    
                    labelPoint = new PointF(labelPoint.X - rotationPoint.Value.X,
                        labelPoint.Y - rotationPoint.Value.Y);

                    //labelSize = new SizeF(labelSize.Width*0.74f + 1f, labelSize.Height*0.74f);
                    if (backColor != null && backColor != Brushes.Transparent)
                        g.FillRectangle(backColor, labelPoint.X, labelPoint.Y, labelSize.Width, labelSize.Height);

                    var path = new GraphicsPath();
                    path.AddString(text, font.FontFamily, (int) font.Style, font.Size,
                        new RectangleF(labelPoint, labelSize) /* labelPoint*/, 
                        new StringFormat { Alignment = salign } /*null*/);
                    if (halo != null)
                        g.DrawPath(halo, path);

                    g.FillPath(new SolidBrush(foreColor), path);

                    //g.DrawString(text, font, new System.Drawing.SolidBrush(forecolor), 0, 0);

                    using (var inv = new Matrix())
                    {
                        inv.Translate(rotationPoint.Value.X, rotationPoint.Value.Y);
                        inv.Rotate(rotation);
                        bounds = path.GetBounds(inv);
                    }
                    
                    g.Transform = t;
                }
                return bounds;
            }
            else
            {
                if (backColor != null && backColor != Brushes.Transparent)
                    g.FillRectangle(backColor, labelPoint.X, labelPoint.Y, labelSize.Width,
                                    labelSize.Height);

                var path = new GraphicsPath();
                path.AddString(text, font.FontFamily, (int) font.Style, font.Size, 
                               new RectangleF(labelPoint, labelSize) /* labelPoint*/,
                               new StringFormat { Alignment = salign } /*null*/);
                if (halo != null)
                    g.DrawPath(halo, path);
                g.FillPath(new SolidBrush(foreColor), path);
                //g.DrawString(text, font, new System.Drawing.SolidBrush(forecolor), LabelPoint.X, LabelPoint.Y);

                return path.GetBounds();
            }
        }

        private static ClipState DetermineClipState(PointF[] vertices, int width, int height)
        {
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;

            for (int i = 0; i < vertices.Length; i++)
            {
                minX = Math.Min(minX, vertices[i].X);
                minY = Math.Min(minY, vertices[i].Y);
                maxX = Math.Max(maxX, vertices[i].X);
                maxY = Math.Max(maxY, vertices[i].Y);
            }

            if (maxX < 0) return ClipState.Outside;
            if (maxY < 0) return ClipState.Outside;
            if (minX > width) return ClipState.Outside;
            if (minY > height) return ClipState.Outside;
            if (minX > 0 && maxX < width && minY > 0 && maxY < height) return ClipState.Within;
            return ClipState.Intersecting;
        }

        /// <summary>
        /// Clips a polygon to the view.
        /// Based on UMN Mapserver renderer 
        /// </summary>
        /// <param name="vertices">vertices in image coordinates</param>
        /// <param name="width">Width of map in image coordinates</param>
        /// <param name="height">Height of map in image coordinates</param>
        /// <returns>Clipped polygon</returns>
        internal static PointF[] ClipPolygon(PointF[] vertices, int width, int height)
        {
            var line = new List<PointF>();
            if (vertices.Length <= 1) /* nothing to clip */
                return vertices;

            for (int i = 0; i < vertices.Length - 1; i++)
            {
                var x1 = vertices[i].X;
                var y1 = vertices[i].Y;
                var x2 = vertices[i + 1].X;
                var y2 = vertices[i + 1].Y;

                var deltax = x2 - x1;
                if (deltax == 0f)
                {
                    // bump off of the vertical
                    deltax = (x1 > 0) ? -NearZero : NearZero;
                }
                var deltay = y2 - y1;
                if (deltay == 0f)
                {
                    // bump off of the horizontal
                    deltay = (y1 > 0) ? -NearZero : NearZero;
                }

                float xin;
                float xout;
                if (deltax > 0)
                {
                    //  points to right
                    xin = 0;
                    xout = width;
                }
                else
                {
                    xin = width;
                    xout = 0;
                }

                float yin;
                float yout;
                if (deltay > 0)
                {
                    //  points up
                    yin = 0;
                    yout = height;
                }
                else
                {
                    yin = height;
                    yout = 0;
                }

                var tinx = (xin - x1)/deltax;
                var tiny = (yin - y1)/deltay;

                float tin1;
                float tin2;
                if (tinx < tiny)
                {
                    // hits x first
                    tin1 = tinx;
                    tin2 = tiny;
                }
                else
                {
                    // hits y first
                    tin1 = tiny;
                    tin2 = tinx;
                }

                if (1 >= tin1)
                {
                    if (0 < tin1)
                        line.Add(new PointF(xin, yin));

                    if (1 >= tin2)
                    {
                        var toutx = (xout - x1)/deltax;
                        var touty = (yout - y1)/deltay;

                        var tout = (toutx < touty) ? toutx : touty;

                        if (0 < tin2 || 0 < tout)
                        {
                            if (tin2 <= tout)
                            {
                                if (0 < tin2)
                                {
                                    line.Add(tinx > tiny
                                                 ? new PointF(xin, y1 + tinx*deltay)
                                                 : new PointF(x1 + tiny*deltax, yin));
                                }

                                if (1 > tout)
                                {
                                    line.Add(toutx < touty
                                                 ? new PointF(xout, y1 + toutx*deltay)
                                                 : new PointF(x1 + touty*deltax, yout));
                                }
                                else
                                    line.Add(new PointF(x2, y2));
                            }
                            else
                            {
                                line.Add(tinx > tiny ? new PointF(xin, yout) : new PointF(xout, yin));
                            }
                        }
                    }
                }
            }
            if (line.Count > 0)
                line.Add(new PointF(line[0].X, line[0].Y));

            return line.ToArray();
        }

        /// <summary>
        /// Renders a point to the map.
        /// </summary>
        /// <param name="g">Graphics reference</param>
        /// <param name="point">Point to render</param>
        /// <param name="b">Brush reference</param>
        /// <param name="size">Size of drawn Point</param>
        /// <param name="offset">Symbol offset af scale=1</param>
        /// <param name="map">Map reference</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void DrawPoint(Graphics g, IPoint point, Brush b, float size, PointF offset, MapViewport map)
        {
            DrawPointEx(g, point, b, size, offset, map);
        }
        /// <summary>
        /// Renders a point to the map.
        /// </summary>
        /// <param name="g">Graphics reference</param>
        /// <param name="point">Point to render</param>
        /// <param name="b">Brush reference</param>
        /// <param name="size">Size of drawn Point</param>
        /// <param name="offset">Symbol offset af scale=1</param>
        /// <param name="map">Map reference</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static RectangleF DrawPointEx(Graphics g, IPoint point, Brush b, float size, PointF offset, MapViewport map)
        {
            if (point == null)
                return new RectangleF();

            var pp = map.WorldToImage(point.Coordinate);
            //var startingTransform = g.Transform;

            var width = size;
            var height = size;

            g.FillEllipse(b, (int)pp.X - width / 2 + offset.X ,
                        (int)pp.Y - height / 2 + offset.Y , width, height);
            
            return new RectangleF((int)pp.X - width / 2 + offset.X ,
                        (int)pp.Y - height / 2 + offset.Y , width, height);
        }

        /// <summary>
        /// Renders a point to the map.
        /// </summary>
        /// <param name="g">Graphics reference</param>
        /// <param name="point">Point to render</param>
        /// <param name="symbolizer">Symbolizer to decorate point</param>
        /// <param name="map">Map reference</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void DrawPoint(IPointSymbolizer symbolizer, Graphics g, IPoint point, MapViewport map) => DrawPointEx(symbolizer, g, point, map);

        /// <summary>
        /// Renders a point to the map.
        /// </summary>
        /// <param name="g">Graphics reference</param>
        /// <param name="point">Point to render</param>
        /// <param name="symbolizer">Symbolizer to decorate point</param>
        /// <param name="map">Map reference</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static RectangleF DrawPointEx(IPointSymbolizer symbolizer, Graphics g, IPoint point, MapViewport map)
        {
            if (point == null)
                return new RectangleF(); 

            symbolizer.Render(map, point, g);
            return ((IPointSymbolizerEx)symbolizer).Bounds; 
        }

        /// <summary>
        /// Renders a point to the map.
        /// </summary>
        /// <param name="g">Graphics reference</param>
        /// <param name="point">Point to render</param>
        /// <param name="symbol">Symbol to place over point</param>
        /// <param name="symbolscale">The amount that the symbol should be scaled. A scale of '1' equals to no scaling</param>
        /// <param name="offset">Symbol offset af scale=1</param>
        /// <param name="rotation">Symbol rotation in degrees</param>
        /// <param name="map">Map reference</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void DrawPoint(Graphics g, IPoint point, Image symbol, float symbolscale, PointF offset,
            float rotation, MapViewport map) => DrawPointEx(g, point, symbol, symbolscale, offset, rotation, map);

        /// <summary>
        /// Renders a point to the map.
        /// </summary>
        /// <param name="g">Graphics reference</param>
        /// <param name="point">Point to render</param>
        /// <param name="symbol">Symbol to place over point</param>
        /// <param name="symbolscale">The amount that the symbol should be scaled. A scale of '1' equals to no scaling</param>
        /// <param name="offset">Symbol offset af scale=1</param>
        /// <param name="rotation">Symbol rotation in degrees</param>
        /// <param name="map">Map reference</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static RectangleF DrawPointEx(Graphics g, IPoint point, Image symbol, float symbolscale, PointF offset,
            float rotation, MapViewport map)
        {
            if (point == null)
                return RectangleF.Empty;
            
            if (symbol == null) //We have no point style - Use a default symbol
                symbol = Defaultsymbol;

            var pp = map.WorldToImage(point.Coordinate);
            
            RectangleF bounds; 
            lock (symbol)
            {
                if (rotation != 0 && !Single.IsNaN(rotation))
                {
                    var old = g.Transform.Clone();

                    var mat = g.Transform;
                    var rotationCenter = pp;
                    mat.RotateAt(rotation, rotationCenter);
                    g.Transform = mat;

                    //if (symbolscale == 1f)
                    //{
                    //    g.DrawImage(symbol,  (pp.X - symbol.Width/2f + offset.X),
                    //                                (pp.Y - symbol.Height/2f + offset.Y));
                    //}
                    //else
                    //{
                    //    var width = symbol.Width*symbolscale;
                    //    var height = symbol.Height*symbolscale;
                    //    g.DrawImage(symbol, (int) pp.X - width/2 + offset.X*symbolscale,
                    //                        (int) pp.Y - height/2 + offset.Y*symbolscale, width, height);
                    //}
                    float width = symbol.Width * symbolscale;
                    float height = symbol.Height * symbolscale;
                    g.DrawImage(symbol, pp.X - width / 2 + offset.X * symbolscale,
                                        pp.Y - height / 2 + offset.Y * symbolscale, width, height);
                    g.Transform = old;
                    old.Dispose();
                    mat.Dispose();

                    using (mat = new Matrix())
                    {
                        float left = pp.X - width / 2 + offset.X * symbolscale;
                        float top = pp.Y - height / 2 + offset.Y * symbolscale; 
                        var pts = new PointF[]
                        {
                            new PointF(left, top),
                            new PointF(left + width, top),
                            new PointF(left + width, top + height),
                            new PointF(left, top + height)
                        };

                        mat.RotateAt(rotation, rotationCenter);
                        mat.TransformPoints(pts);

                        var minX = Math.Min(pts[0].X, Math.Min(pts[1].X, Math.Min(pts[2].X, pts[3].X)));
                        var maxX = Math.Max(pts[0].X, Math.Max(pts[1].X, Math.Max(pts[2].X, pts[3].X)));
                        var minY = Math.Min(pts[0].Y, Math.Min(pts[1].Y, Math.Min(pts[2].Y, pts[3].Y)));
                        var maxY = Math.Max(pts[0].Y, Math.Max(pts[1].Y, Math.Max(pts[2].Y, pts[3].Y)));
                        bounds = new RectangleF(minX, maxY, maxX - minX, maxY - minY);
                    }
                }
                else
                {
                    //if (symbolscale == 1f)
                    //{
                    //    g.DrawImageUnscaled(symbol, (int) (pp.X - symbol.Width/2f + offset.X),
                    //                                (int) (pp.Y - symbol.Height/2f + offset.Y));
                    //}
                    //else
                    //{
                    //    var width = symbol.Width*symbolscale;
                    //    var height = symbol.Height*symbolscale;
                    //    g.DrawImage(symbol, (int) pp.X - width/2 + offset.X*symbolscale,
                    //                        (int) pp.Y - height/2 + offset.Y*symbolscale, width, height);
                    //}
                    float width = symbol.Width * symbolscale;
                    float height = symbol.Height * symbolscale;
                    g.DrawImage(symbol, pp.X - width / 2 + offset.X * symbolscale,
                                        pp.Y - height / 2 + offset.Y * symbolscale, width, height);
                    
                    bounds = new RectangleF(
                                        pp.X - width / 2 + offset.X * symbolscale,
                                        pp.Y - height / 2 + offset.Y * symbolscale, width, height);
                }
            }
            
            return bounds;
        }

        /// <summary>
        /// Renders a <see cref="GeoAPI.Geometries.IMultiPoint"/> to the map.
        /// </summary>
        /// <param name="g">Graphics reference</param>
        /// <param name="points">MultiPoint to render</param>
        /// <param name="symbol">Symbol to place over point</param>
        /// <param name="symbolscale">The amount that the symbol should be scaled. A scale of '1' equals to no scaling</param>
        /// <param name="offset">Symbol offset af scale=1</param>
        /// <param name="rotation">Symbol rotation in degrees</param>
        /// <param name="map">Map reference</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void DrawMultiPoint(Graphics g, IMultiPoint points, Image symbol, float symbolscale,
            PointF offset, float rotation, MapViewport map) => DrawMultiPointEx(g, points, symbol, symbolscale, offset, rotation, map);

        /// <summary>
        /// Renders a <see cref="GeoAPI.Geometries.IMultiPoint"/> to the map.
        /// </summary>
        /// <param name="g">Graphics reference</param>
        /// <param name="points">MultiPoint to render</param>
        /// <param name="symbol">Symbol to place over point</param>
        /// <param name="symbolscale">The amount that the symbol should be scaled. A scale of '1' equals to no scaling</param>
        /// <param name="offset">Symbol offset af scale=1</param>
        /// <param name="rotation">Symbol rotation in degrees</param>
        /// <param name="map">Map reference</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static RectangleF DrawMultiPointEx(Graphics g, IMultiPoint points, Image symbol, float symbolscale, PointF offset,
            float rotation, MapViewport map)
        {
            var affectedArea = new RectangleF(); 
            for (var i = 0; i < points.NumGeometries; i++)
            {
                var point = (IPoint) points[i];
                var rect = DrawPointEx(g, point, symbol, symbolscale, offset, rotation, map);
                affectedArea = RectangleF.Union(affectedArea, rect); //RectExpandToInclude(affectedArea, rect);
            }

            return affectedArea;
        }

        /// <summary>
        /// Renders a <see cref="GeoAPI.Geometries.IMultiPoint"/> to the map.
        /// </summary>
        /// <param name="g">Graphics reference</param>
        /// <param name="points">MultiPoint to render</param>
        /// <param name="symbolizer">Symbolizer to decorate point</param>
        /// <param name="map">Map reference</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void DrawMultiPoint(IPointSymbolizer symbolizer, Graphics g, IMultiPoint points, MapViewport map)
            => DrawMultiPointEx(symbolizer, g, points, map);

        /// <summary>
        /// Renders a <see cref="GeoAPI.Geometries.IMultiPoint"/> to the map.
        /// </summary>
        /// <param name="g">Graphics reference</param>
        /// <param name="points">MultiPoint to render</param>
        /// <param name="symbolizer">Symbolizer to decorate point</param>
        /// <param name="map">Map reference</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static RectangleF DrawMultiPointEx(IPointSymbolizer symbolizer, Graphics g, IMultiPoint points, MapViewport map)
        {
            symbolizer.Render(map, points, g);
            return ((IPointSymbolizerEx)symbolizer).Bounds;
        }

        /// <summary>
        /// Renders a <see cref="GeoAPI.Geometries.IMultiPoint"/> to the map.
        /// </summary>
        /// <param name="g">Graphics reference</param>
        /// <param name="points">MultiPoint to render</param>
        /// <param name="brush">Brush reference</param>
        /// <param name="size">Size of drawn Point</param>
        /// <param name="offset">Symbol offset af scale=1</param>
        /// <param name="map">Map reference</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void DrawMultiPoint(Graphics g, IMultiPoint points, Brush brush, float size, PointF offset, MapViewport map)
            => DrawMultiPointEx(g, points, brush, size, offset, map);

        /// <summary>
        /// Renders a <see cref="GeoAPI.Geometries.IMultiPoint"/> to the map.
        /// </summary>
        /// <param name="g">Graphics reference</param>
        /// <param name="points">MultiPoint to render</param>
        /// <param name="brush">Brush reference</param>
        /// <param name="size">Size of drawn Point</param>
        /// <param name="offset">Symbol offset af scale=1</param>
        /// <param name="map">Map reference</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static RectangleF DrawMultiPointEx(Graphics g, IMultiPoint points, Brush brush, float size, PointF offset, MapViewport map)
        {
            var affectedArea = new RectangleF();
            for (var i = 0; i < points.NumGeometries; i++)
            {
                var point = (IPoint) points[i];
                var rect = DrawPointEx(g, point, brush, size, offset, map);
                affectedArea = RectangleF.Union(affectedArea, rect); //RectExpandToInclude(affectedArea, rect);
            }

            return affectedArea;
        }

        #region Nested type: ClipState

        private enum ClipState
        {
            Within,
            Outside,
            Intersecting
        } ;

        #endregion

        [Obsolete("Use RectangleF.Union")]
        public static RectangleF RectExpandToInclude(RectangleF first, RectangleF second)
        {
            return RectangleF.Union(first, second);
            /*
            if (second.IsEmpty) return first;
            if (first.IsEmpty) return second;

            float maxX = Math.Max(first.Right, second.Right);
            float maxY = Math.Max(first.Bottom,second.Bottom);

            if (first.X > second.X) first.X = second.X;
            if (first.Y > second.Y) first.Y = second.Y;

            if (first.Right < maxX) first.Width = maxX - first.X;
            if (first.Bottom < maxY) first.Height = maxY - first.Y;

            return first;
             */
        }
    }
}
