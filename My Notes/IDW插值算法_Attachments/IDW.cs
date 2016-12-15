using System;
using System.Collections.Generic;
using System.Text;

namespace IDWTest
{
    public class IDW
    {
        public IDW()
        { 
        }

        /// <summary>距离平方反比加权插值法</summary>
        /// <param name="x">已知点列的X坐标</param>
        /// <param name="y">已知点列的Y坐标</param>
        /// <param name="z">已知点列的值</param>
        /// <param name="z">插值点列的X坐标</param>
        /// <param name="z">插值点列的Y坐标</param>
        /// <returns>插值结果</returns>
        public static List<double> Interpolate(List<double> x, List<double> y, List<double> z, List<double> X, List<double> Y)
        {
            if (x.Count<3 || x.Count!=y.Count || x.Count!= z.Count || X.Count!=Y.Count)
                return null;

            int m0=x.Count;
            int m1=X.Count;

            List<double> Z = new List<double>();            

            //距离列表
            List<double> r = new List<double>();
            for (int i = 0; i < m1; i++)
			{
                for (int j = 0; j < m0; j++)
                {
                    double tmpDis = Math.Sqrt(Math.Pow(X[i] - x[j], 2) + Math.Pow(Y[i] - y[j], 2));
                    r.Add(tmpDis);
                }
			}

            //插值函数
            for (int i = 0; i < m1; i++)
            {
                //查找重复
                bool ifFind = false;
                for (int j = m0 * i; j < m0 * i + m0; j++)
                {
                    if (Math.Abs(r[j]) < 0.0001)
                    {
                        Z.Add(z[j-m0 * i]);
                        ifFind = true;
                        break;
                    }
                    else
                    {
                        
                    }
                }
                if (ifFind) continue;

                double numerator = 0;
                double denominator = 0;
                for (int j = m0 * i; j < m0 * i + m0; j++)
                {
                    numerator += z[j - m0 * i] / (r[j]*r[j]);
                    denominator += 1 / (r[j] * r[j]);
                }
                Z.Add(numerator / denominator);
            }
            return Z;
        }
    }
}
