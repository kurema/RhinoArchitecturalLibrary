using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;


namespace kurema.RhinoTools
{
    public class ContinuousOptimization
    {
        public static IGaussNewtonTarget Optimize(IGaussNewtonTarget target)
        {
            var diff = GaussNewton(target);
            return LengthEstimation(target, diff);
        }

        public interface IGaussNewtonTarget
        {
            double JacobianDiff(int i, double original);
            Matrix Status { get; set; }
            Matrix Energy { get; }
            double[] LengthEstimation { get; }
            IGaussNewtonTarget Duplicate();
            object GetObject();
        }

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


        public static IGaussNewtonTarget LengthEstimation(IGaussNewtonTarget target, Matrix statusDiff)
        {
            return LengthEstimation(target, statusDiff, target.LengthEstimation);
        }

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

        public static Matrix GaussNewton(IGaussNewtonTarget target)
        {

            return GaussNewton(GetJacobian(target), target.Energy, target.Status);
        }

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
