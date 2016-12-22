using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;


namespace kurema.RhinoTools
{
    /// <summary>
    /// 連続的最適化(主にガウス・ニュートン法)に関する機能を含みます。
    /// </summary>
    public class ContinuousOptimization
    {
        /// <summary>
        /// 対象にガウス・ニュートン法を実行します。
        /// </summary>
        /// <param name="target">最適化対象</param>
        /// <returns>最適化結果</returns>
        public static IGaussNewtonTarget Optimize(IGaussNewtonTarget target)
        {
            var diff = GaussNewton(target);
            return LengthEstimation(target, diff);
        }

        /// <summary>
        /// ガウス・ニュートン法を実行可能な対象です。
        /// </summary>
        public interface IGaussNewtonTarget
        {
            /// <summary>
            /// ヤコビアンの微分結果
            /// </summary>
            /// <param name="i">対象の番号</param>
            /// <param name="original">対象の値</param>
            /// <returns>微分結果</returns>
            double JacobianDiff(int i, double original);
            /// <summary>
            /// 現在の推測値を行列として取得します。
            /// </summary>
            Matrix Status { get; set; }
            /// <summary>
            /// 現在の評価値を行列として取得します。
            /// </summary>
            Matrix Energy { get; }
            /// <summary>
            /// 長さ推定を行う際の値です。
            /// </summary>
            double[] LengthEstimation { get; }
            /// <summary>
            /// 複製します。
            /// </summary>
            /// <returns>複製結果</returns>
            IGaussNewtonTarget Duplicate();
            /// <summary>
            /// 現在の状態に対応する出力を得ます。
            /// </summary>
            /// <returns>出力</returns>
            object GetObject();
        }

        /// <summary>
        /// 現在の評価値の自乗和を得ます。
        /// </summary>
        /// <param name="target">計測対象</param>
        /// <returns></returns>
        public static double GetEnergySquareSum(IGaussNewtonTarget target)
        {
            var matrix = target.Energy;
            double result = 0;
            try
            {
                for (int i = 0; i < matrix.RowCount; i++)
                {
                    checked
                    {
                        result += matrix[i, 0];
                    }
                }
            }
            catch (OverflowException)
            {
                return double.MaxValue;
            }
            return result;
        }

        /// <summary>
        /// 最適化方向への移動量を推定します。
        /// </summary>
        /// <param name="target">最適化対象</param>
        /// <param name="statusDiff">差分</param>
        /// <returns>最適化結果</returns>
        public static IGaussNewtonTarget LengthEstimation(IGaussNewtonTarget target, Matrix statusDiff)
        {
            return LengthEstimation(target, statusDiff, target.LengthEstimation);
        }

        /// <summary>
        /// 最適化方向への移動量を推定します。
        /// </summary>
        /// <param name="target">最適化対象</param>
        /// <param name="statusDiff">差分</param>
        /// <param name="Lengths">調査する長さ</param>
        /// <returns>最適化結果</returns>
        public static IGaussNewtonTarget LengthEstimation(IGaussNewtonTarget target, Matrix statusDiff, double[] Lengths)
        {
            var temp = target.Duplicate();
            double bestEnergy = double.MaxValue;
            IGaussNewtonTarget bestResult = target;

            foreach (var len in target.LengthEstimation)
            {
                var statusDiffNew = statusDiff.Duplicate();
                statusDiffNew.Scale(len);
                temp.Status = target.Status + statusDiffNew;
                var currentResult = GetEnergySquareSum(temp);
                if (currentResult < bestEnergy)
                {
                    bestEnergy = currentResult;
                    bestResult = temp.Duplicate();
                }
            }
            return bestResult;
        }

        /// <summary>
        /// ヤコビアンを取得します。
        /// </summary>
        /// <param name="target">計測対象</param>
        /// <returns>ヤコビアン</returns>
        public static Matrix GetJacobian(IGaussNewtonTarget target)
        {
            var status = target.Status;
            var originalEnergy = target.Energy;

            var result = new Matrix(originalEnergy.RowCount, status.RowCount);

            for (int i = 0; i < status.RowCount; i++)
            {
                var diff = target.JacobianDiff(i, status[i, 0]);
                status[i, 0] += diff;
                target.Status = status;
                var newEnergy = target.Energy;
                status[i, 0] -= diff;
                target.Status = status;

                for (int j = 0; j < originalEnergy.ColumnCount; j++)
                {
                    result[j, i] = (newEnergy[i, 0] - originalEnergy[i, 0]) / diff;
                }
            }
            return result;
        }

        /// <summary>
        /// ガウス・ニュートン法を実行します。
        /// </summary>
        /// <param name="target">最適化対象</param>
        /// <returns>最適化結果</returns>
        public static Matrix GaussNewton(IGaussNewtonTarget target)
        {

            return GaussNewton(GetJacobian(target), target.Energy, target.Status);
        }

        /// <summary>
        /// ガウス・ニュートン法を実行します。
        /// </summary>
        /// <param name="Jacobian">ヤコビアン</param>
        /// <param name="EnergyVector">現在の評価値</param>
        /// <param name="StatusVector">現在の推測値</param>
        /// <returns></returns>
        public static Matrix GaussNewton(Matrix Jacobian, Matrix EnergyVector, Matrix StatusVector)
        {
            //Jacobian.Transpose();
            Matrix M1 = Jacobian.Duplicate();
            M1.Transpose();
            Matrix M2 = M1 * Jacobian;
            M2.Invert(1e-10);//(ZeroTolerance)
            M1 = M1 * EnergyVector;
            M1 = M2 * M1;
            M1.Scale(-1);
            return M1;
        }

        /// <summary>
        /// 配列を行列に変換します。
        /// </summary>
        /// <param name="arg">変換元</param>
        /// <param name="IsVertical">ベクトルの向きを指定します(trueで縦、falseで横)</param>
        /// <returns></returns>
        public static Matrix ArrayToMatrix(double[] arg, bool IsVertical = true)
        {
            int rowCount = IsVertical ? arg.Count() : 1;
            int columnCount = IsVertical ? 1 : arg.Count();
            var result = new Matrix(rowCount, columnCount);
            for (int i = 0; i < arg.Count(); i++)
            {
                if (IsVertical)
                {
                    result[i, 0] = arg[i];
                }
                else
                {
                    result[0, i] = arg[i];
                }
            }
            return result;
        }

        /// <summary>
        /// 行列を配列にしんす。
        /// </summary>
        /// <param name="arg">変換元</param>
        /// <returns>変換結果</returns>
        public static double[] MatrixToArray(Matrix arg)
        {
            var result = new List<double>();
            for (int i = 0; i < arg.RowCount; i++)
            {
                for (int j = 0; j < arg.ColumnCount; j++)
                {
                    result.Add(arg[i, j]);
                }
            }
            return result.ToArray();
        }
    }
}
