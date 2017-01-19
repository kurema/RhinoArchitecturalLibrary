using Rhino;
using Rhino.Geometry;
using Rhino.DocObjects;
using Rhino.Collections;

using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.Data;
using System.Drawing;
using System.Reflection;
using System.Collections;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace kurema.RhinoTools
{
    /// <summary>
    /// 影を示すオブジェクトなどを含みます。
    /// </summary>
    public class ShadowObject
    {
        /// <summary>
        /// 単純な影を提供可能です。
        /// </summary>
        public interface ShadowSingleProvider
        {
            ShadowSingle GetShadow(Vector3d direction,double height);
        }

        /// <summary>
        /// 複合した影を提供可能です。
        /// </summary>
        public interface ShadowCombinedProvider
        {
            ShadowCombined GetShadow(Vector3d direction, double height);
        }

        /// <summary>
        /// 影から除外される部分を提供可能です。例えば窓からの光などです。
        /// </summary>
        public interface ShadowExclusionProvider
        {
            Curve[] GetShadowExclusion(Vector3d direction, double height);
        }

        /// <summary>
        /// 単純な影を示します。
        /// </summary>
        public class ShadowSingle
        {
            /// <summary>
            /// 輪郭です。
            /// </summary>
            public Curve Contour { get; set; }
            /// <summary>
            /// 除外部分です。
            /// </summary>
            public Curve[] ExcludedArea { get; set; }

            /// <summary>
            /// コンストラクタ。
            /// </summary>
            /// <param name="Contour">輪郭</param>
            public ShadowSingle(Curve Contour)
            {
                this.Contour = Contour;
                this.ExcludedArea = new Curve[0];
            }

            /// <summary>
            /// コンストラクタ。
            /// </summary>
            /// <param name="Contour">輪郭</param>
            /// <param name="ExcludedArea">除外部分</param>
            public ShadowSingle(Curve Contour,Curve[] ExcludedArea)
            {
                this.Contour = Contour;
                this.ExcludedArea = ExcludedArea;
            }
        }

        /// <summary>
        /// 複合した影を示します。
        /// </summary>
        public class ShadowCombined
        {
            /// <summary>
            /// それぞれ離れた輪郭線を示します。
            /// </summary>
            public Curve[] Contours { get { return _Contours.ToArray(); } }
            private Curve[] _Contours = new Curve[0];
            /// <summary>
            /// 除外部分を示します。
            /// </summary>
            public Curve[] ExcludedArea { get; private set; }

            /// <summary>
            /// 影を追加します。
            /// </summary>
            /// <param name="arg">影</param>
            public void Add(ShadowSingle arg)
            {
                var temp = _Contours.ToList();
                temp.Add(arg.Contour);
                _Contours = Curve.CreateBooleanUnion(temp);

                var result = new List<Curve>();
                for (int i = 0; i < ExcludedArea.Count(); i++)
                {
                    result.AddRange(Curve.CreateBooleanDifference(ExcludedArea[i], arg.Contour));
                }

                foreach (var item in arg.ExcludedArea)
                {
                    List<Curve> exc = new List<Curve>();
                    exc.Add(item.DuplicateCurve());
                    for (int i = 0; i < Contours.Count(); i++)
                    {
                        for (int j = 0; j < exc.Count; j++)
                        {

                            var tempcurve = Curve.CreateBooleanDifference(exc[j], Contours[i]);
                            exc[j] = tempcurve[0];
                            for (int k = 1; k < tempcurve.Count(); k++)
                            {
                                exc.Add(tempcurve[k]);
                            }
                        }
                    }
                    result.AddRange(exc);
                }
                ExcludedArea = result.ToArray();
            }
        }

        /// <summary>
        /// 影に関する補助機能を提供します。
        /// </summary>
        public static class Helper
        {
            /// <summary>
            /// 光の方向から平面図による影を適切に配置します。
            /// </summary>
            /// <param name="Plan">平面図</param>
            /// <param name="direction">入射方向</param>
            /// <param name="height">影の高さ</param>
            /// <returns>影</returns>
            public static ShadowSingle GetShadowFromPlan(Curve Plan, Vector3d direction, double height)
            {
                var horizontalDirection = GetTranslationDirection(direction, height);
                if (horizontalDirection == null) { return null; }
                var pl = Plan.DuplicateCurve();
                pl.Transform(Transform.Translation(horizontalDirection.Value));
                return new ShadowSingle(pl);
            }

            /// <summary>
            /// 光の方向と高さからずれ方向を返します。
            /// </summary>
            /// <param name="direction">入射方向</param>
            /// <param name="height">高さ</param>
            /// <returns>方向</returns>
            public static Vector3d? GetTranslationDirection(Vector3d direction, double height)
            {
                if (Math.Sign(height) != Math.Sign(direction.Z)) { return null; }
                double scale = height / direction.Z;
                return new Vector3d(direction.X * scale, direction.Y * scale, 0);
            }

            public static Polyline GetShadowOfBoard(Point2d Start,Vector2d boardDirection,double boardHeight,Vector3d direction)
            {
                var pl = new Polyline(5);
                var dr = GetTranslationDirection(direction, boardHeight).Value;
                pl.Add(Start.X, Start.Y, 0);
                pl.Add(Start.X + boardDirection.X, Start.Y + boardDirection.Y, 0);
                pl.Add(Start.X + boardDirection.X + dr.X, Start.Y + boardDirection.Y + dr.Y, 0);
                pl.Add(Start.X + dr.X, Start.Y + dr.Y, 0);
                pl.Add(Start.X, Start.Y, 0);
                return pl;
            }

            public static Polyline GetShadowOfBoard(Point2d Start, Vector2d boardDirection, double boardHeight, Vector3d direction,double totalHeight)
            {
                if(0<=totalHeight&& totalHeight <= boardHeight)
                {
                    if (Math.Sign(totalHeight) == Math.Sign(direction.Z))
                    {
                        return GetShadowOfBoard(Start, boardDirection, boardHeight- totalHeight, direction);
                    }
                    else
                    {
                        return GetShadowOfBoard(Start, boardDirection, -totalHeight, direction);
                    }
                }
                var pl = GetShadowOfBoard(Start, boardDirection, boardHeight, direction);
                var dr = GetTranslationDirection(direction, boardHeight);
                if (dr == null) { return null; }
                pl.Transform(Transform.Translation(dr.Value));
                return pl;
            }
        }

        /// <summary>
        /// 太陽関係の計算を含みます。簡易計算につき誤差を含みます。
        /// </summary>
        public static class SolarHelper
        {
            /// <summary>
            /// 太陽の向きをベクトルで求める。
            /// </summary>
            /// <param name="Latitude">北緯(degree)</param>
            /// <param name="Longitude">経度(degree)</param>
            /// <param name="dt">日時</param>
            /// <returns>結果(東をx,北をy,上をzとするベクトル)</returns>
            public static Vector3d SunVector(double Latitude, double Longitude, DateTime dt)
            {
                double sa = SolarAzimuth(Latitude, Longitude, dt);
                double sh = SunHeight(Latitude, Longitude, dt);
                return new Vector3d(-Math.Sin(sa) * Math.Cos(sh), -Math.Cos(sa) * Math.Cos(sh), Math.Sin(sh));
            }

            /// <summary>
            /// 太陽赤緯を計算します。
            /// </summary>
            /// <param name="dt">計算対象日</param>
            /// <returns>計算結果(radian)</returns>
            public static double SunDeclination(DateTime dt)
            {
                //δ=0.33281-22.984*cos(ω*J)-0.34990*cos(2*ω*J)-0.13980*cos(3*ω*J)+3.7872*sin(ω*J)+0.0325*sin(2*ω*J)+0.07187*sin(3*ω*J)
                //ω=2*π/365、うるう年はω=2*π/366、J=元旦からの通算日数+0.5
                double J = dt.DayOfYear - 0.5;
                double omega = 2 * Math.PI / (DateTime.IsLeapYear(dt.Year) ? 366 : 365);
                return (0.33281 - 22.984 * Math.Cos(omega * J) - 0.34990 * Math.Cos(2 * omega * J) - 0.13980 * Math.Cos(3 * omega * J) - 0.1229 * Math.Sin(omega * J) - 0.1565 * Math.Sin(2 * omega * J) - 0.0041 * Math.Sin(3 * omega * J)) / 180 * Math.PI;
            }

            /// <summary>
            /// 太陽方位角を計算します。
            /// </summary>
            /// <param name="Latitude">北緯(degree)</param>
            /// <param name="Longitude">経度(degree)</param>
            /// <param name="dt">日時</param>
            /// <returns>結果(radian)</returns>
            public static double SolarAzimuth(double Latitude, double Longitude, DateTime dt)
            {
                double omega = 2 * Math.PI / (DateTime.IsLeapYear(dt.Year) ? 366 : 365);
                //return Math.Acos(
                //    (Math.Sin(SunHeight(Latitude, Longitude, dt)) * Math.Sin(omega) - Math.Sin(SunDeclination(dt)))
                //    / (Math.Cos(SunHeight(Latitude, Longitude, dt)) * Math.Cos(omega)));
                return Math.Asin(
                    Math.Cos(SunDeclination(dt)) * Math.Sin(HourAngle(Longitude, dt)) / Math.Cos(SunHeight(Latitude, Longitude, dt))
                    );
            }

            /// <summary>
            /// 太陽高度を計算します。
            /// </summary>
            /// <param name="Latitude">北緯(degree)</param>
            /// <param name="Longitude">経度(degree)</param>
            /// <param name="dt">日時</param>
            /// <returns>結果(radian)</returns>
            public static double SunHeight(double Latitude, double Longitude, DateTime dt)
            {
                double dots = SunDeclination(dt);
                double t = HourAngle(Longitude, dt);
                return Math.Asin(Math.Sin(Latitude * Math.PI / 180) * Math.Sin(dots) + Math.Cos(Latitude * Math.PI / 180) * Math.Cos(dots) * Math.Cos(t));
            }

            /// <summary>
            /// 時角。
            /// </summary>
            /// <param name="Longitude">経度(degree)</param>
            /// <param name="dt"></param>
            /// <returns>結果(radian)</returns>
            public static double HourAngle(double Longitude, DateTime dt)
            {
                return Math.PI / 12 * TrueSolarTime(Longitude, dt) - Math.PI;

            }

            /// <summary>
            /// 真太陽時。
            /// </summary>
            /// <param name="Longitude">経度(degree)</param>
            /// <param name="dt">日時</param>
            /// <returns>結果</returns>
            public static double TrueSolarTime(double Longitude, DateTime dt)
            {
                return (dt.Hour + dt.Minute / 60 + dt.Second / 3600) + (Longitude - 135) / 15 + EquationOfTime(dt);
            }

            /// <summary>
            /// 均時差。
            /// </summary>
            /// <param name="dt">日時</param>
            /// <returns>結果(hour)</returns>
            public static double EquationOfTime(DateTime dt)
            {
                double J = dt.DayOfYear - 0.5;
                double omega = 2 * Math.PI / (DateTime.IsLeapYear(dt.Year) ? 366 : 365);
                return 0.0072 * Math.Cos(omega * J) - 0.0528 * Math.Cos(2 * omega * J) - 0.0012 * Math.Cos(3 * omega * J)
                     - 0.1229 * Math.Sin(omega * J) - 0.1565 * Math.Sin(2 * omega * J) - 0.0041 * Math.Sin(3 * omega * J);
            }
        }
    }
}
