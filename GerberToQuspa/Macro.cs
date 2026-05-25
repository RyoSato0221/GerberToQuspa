using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace GerberToQuspa
{    
    //public interface IExpression
    //{
    //    double Evaluate(Dictionary<int, double> vars);
    //    IExpression Clone();
    //}

    //public class ConstantExpression : IExpression
    //{
    //    private readonly double _value;

    //    public ConstantExpression(long value)
    //    {
    //        _value = value;
    //    }

    //    public double Evaluate(Dictionary<int, double> vars)
    //    {
    //        return _value;
    //    }

    //    public IExpression Clone()
    //    {
    //        return this;
    //    }
    //}

    //public class VariableExpression : IExpression
    //{
    //    private readonly int _index;

    //    public VariableExpression(int index)
    //    {
    //        _index = index;
    //    }

    //    public double Evaluate(Dictionary<int, double> vars)
    //    {
    //        return vars[_index];
    //    }

    //    public IExpression Clone()
    //    {
    //        return this;
    //    }
    //}

    //public class VariableExpression2 : IExpression
    //{
    //    private readonly int _index;

    //    public VariableExpression2(int index)
    //    {
    //        _index = index;
    //    }

    //    public double Evaluate(Dictionary<int, double> vars)
    //    {
    //        return vars[_index] / 1000;
    //    }

    //    public IExpression Clone()
    //    {
    //        return this;
    //    }
    //}

    //public static class ExpressionFactory
    //{
    //    public static IExpression CreateReal(string token, CoordinateTransformer ct)
    //    {
    //        token = token.Trim();

    //        // 変数
    //        if (token.StartsWith("$"))
    //        {
    //            int idx = int.Parse(token.Substring(1));
    //            return new VariableExpression(idx);
    //        }

    //        // 実数（AMなど）
    //        decimal value = decimal.Parse(token, CultureInfo.InvariantCulture);
    //        long internalValue = ct.ParseRealToInternal(value);
    //        return new ConstantExpression(internalValue);            
    //    }

    //    public static IExpression CreateFixed(string token)
    //    {
    //        // 変数
    //        if (token.StartsWith("$"))
    //        {
    //            int idx = int.Parse(token.Substring(1));
    //            return new VariableExpression2(idx);
    //        }

    //        decimal value = decimal.Parse(token, CultureInfo.InvariantCulture);
    //        return new ConstantExpression((long)value);
    //    }
    //}

    public class Macro
    {
        public string Name { get; set; }
        public List<IPrimitive> Primitives { get; set; } = new List<IPrimitive>();

        public Macro Clone()
        {
            var copy = new Macro
            {
                Name = this.Name,
                Primitives = this.Primitives?
                .Select(p => p?.Clone())
                .ToList() ?? new List<IPrimitive>()
            };

            return copy;
        }

        /// <summary>
        /// 実数値への変換
        /// </summary>
        /// <param name="macroVariables"></param>
        public void ToInternalValue(Dictionary<int, double> macroVariables, CoordinateTransformer ct)
        {
            foreach (var primitive in Primitives)
            {
                primitive.ToInternalValue(macroVariables, ct);
            }
        }

        /// <summary>
        /// Geometry生成
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="isLPD"></param>
        /// <returns></returns>
        public Geometry CreateGeometry(Point pos, bool isLPD)
        {
            Geometry geo = null;

            foreach (var primitive in Primitives)
            {
                Geometry primitiveGeo = primitive.CreateGeometry(pos, isLPD);
                if (geo == null)
                {
                    geo = primitiveGeo;
                }
                else
                {
                    geo = Geometry.Combine(geo, primitiveGeo, GeometryCombineMode.Union, null);
                }
            }
            geo.Freeze();
            return geo;
        }

        public Rect GetBounds()
        {
            Rect rect = Rect.Empty;
            foreach (var primitive in Primitives)
            {
                rect.Union(primitive.GetBounds());    
            }
            return rect;
        }
    }

    public interface IPrimitive
    {
        void SetParameter(List<string> parameters, CoordinateTransformer ct, Dictionary<int, IExpression> vars);
        void ToInternalValue(Dictionary<int, double> macroVariables, CoordinateTransformer ct);
        Geometry CreateGeometry(Point pos, bool isLPD);
        Rect GetBounds();
        IPrimitive Clone();
    }

    /// <summary>
    /// 円プリミティブ
    /// </summary>
    public class CirclePrimitive : IPrimitive
    {
        private bool Exposure;
        private IExpression iDiameter;
        private IExpression iX;
        private IExpression iY;
        private double Diameter;
        private double X;
        private double Y;

        public void SetParameter(List<string> parameters, CoordinateTransformer ct, Dictionary<int, IExpression> vars)
        {
            Exposure = (parameters[1] == "1");
            iDiameter = ExpressionFactory.Create(parameters[2], ct, vars);
            iX = ExpressionFactory.Create(parameters[3], ct, vars);
            iY = ExpressionFactory.Create(parameters[4], ct, vars);
        }

        public void ToInternalValue(Dictionary<int, double> macroVariables, CoordinateTransformer ct)
        {            
            Diameter = iDiameter.Evaluate(macroVariables) / 2 * ct.UnitScale;
            X = iX.Evaluate(macroVariables) * ct.UnitScale;
            Y = iY.Evaluate(macroVariables) * ct.UnitScale;            
        }

        public Geometry CreateGeometry(Point pos, bool isLPD)
        {
            Point center = new Point(pos.X + X, pos.Y + Y);
            
            Geometry geo = new EllipseGeometry(center, Diameter, Diameter);
            geo.Freeze();
            return geo;
        }

        public Rect GetBounds()
        {
            return new Rect(X - Diameter, Y + Diameter, Diameter, Diameter);
        }

        public IPrimitive Clone()
        {
            return new CirclePrimitive
            {
                iDiameter = this.iDiameter,
                iX = this.iX,
                iY = this.iY
            };
        }
    }

    /// <summary>
    /// サーマルプリミティブ
    /// </summary>
    public class ThermalPrimitive : IPrimitive
    {
        public List<string> Params { get; set; } = new List<string>();

        public void SetParameter(List<string> parameters, CoordinateTransformer transformer, Dictionary<int, IExpression> vars)
        {
        }

        public void ToInternalValue(Dictionary<int, double> macroVariables, CoordinateTransformer ct)
        {
        }
        
        public Geometry CreateGeometry(Point pos, bool isLPD)
        {
            return null;
        }

        public Rect GetBounds()
        {
            return new Rect();
        }

        public IPrimitive Clone()
        {
            return new ThermalPrimitive();
        }
    }

    /// <summary>
    /// ベクターラインプリミティブ
    /// </summary>
    public class VectorLinePrimitive : IPrimitive
    {
        private bool Exposure;
        private IExpression iWidth;
        private IExpression iX1;
        private IExpression iY1;
        private IExpression iX2;
        private IExpression iY2;
        private IExpression iRotation;
        private double Width;
        private double X1;
        private double Y1;
        private double X2;
        private double Y2;
        private double Rotation;

        public void SetParameter(List<string> parameters, CoordinateTransformer ct, Dictionary<int, IExpression> vars)
        {
            Exposure = (parameters[1] == "1");
            iWidth = ExpressionFactory.Create(parameters[2], ct, vars);
            iX1 = ExpressionFactory.Create(parameters[3], ct, vars);
            iY1 = ExpressionFactory.Create(parameters[4], ct, vars);
            iX2 = ExpressionFactory.Create(parameters[5], ct, vars);
            iY2 = ExpressionFactory.Create(parameters[6], ct, vars);
            iRotation = ExpressionFactory.Create(parameters[7], ct, vars);
        }

        public void ToInternalValue(Dictionary<int, double> macroVariables, CoordinateTransformer ct)
        {
            Width = iWidth.Evaluate(macroVariables) * ct.UnitScale;
            X1 = iX1.Evaluate(macroVariables) * ct.UnitScale;
            Y1 = iY1.Evaluate(macroVariables) * ct.UnitScale;
            X2 = iX2.Evaluate(macroVariables) * ct.UnitScale;
            Y2 = iY2.Evaluate(macroVariables) * ct.UnitScale;
            Rotation = iRotation.Evaluate(macroVariables);
        }

        public Geometry CreateGeometry(Point pos, bool isLPD)
        {
            Point start = new Point(pos.X + X1, pos.Y + Y1);
            Point end = new Point(pos.X + X2, pos.Y + Y2);

            double dx = end.X - start.X;
            double dy = end.Y - start.Y;

            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len == 0)
                return Geometry.Empty;

            // 法線ベクトル
            double nx = -dy / len;
            double ny = dx / len;

            double offset = Width / 2;

            Point p1 = new Point(start.X + nx * offset, start.Y + ny * offset);
            Point p2 = new Point(start.X - nx * offset, start.Y - ny * offset);
            Point p3 = new Point(end.X - nx * offset, end.Y - ny * offset);
            Point p4 = new Point(end.X + nx * offset, end.Y + ny * offset);

            var geo = new StreamGeometry();

            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(p1, isLPD, true);
                ctx.LineTo(p2, true, false);
                ctx.LineTo(p3, true, false);
                ctx.LineTo(p4, true, false);
            }

            geo.Transform = new RotateTransform(Rotation, pos.X, pos.Y);
            geo.Freeze();

            return geo;
        }

        public Rect GetBounds()
        {
            return new Rect(X1, Y1 - Width / 2, X2 - X2, Width);
        }

        public IPrimitive Clone()
        {
            return new VectorLinePrimitive
            {
                iWidth = this.iWidth,
                iX1 = this.iX1,
                iY1 = this.iY1,
                iX2 = this.iX2,
                iY2 = this.iY2,
                iRotation = this.iRotation
            };
        }
    }

    /// <summary>
    /// センターラインプリミティブ
    /// </summary>
    public class CenterLinePrimitive : IPrimitive
    {
        private bool Exposure;
        private IExpression iWidth;
        private IExpression iHeight;
        private IExpression iX;
        private IExpression iY;
        private IExpression iRotation;
        private double Width;
        private double Height;
        private double X;
        private double Y;
        private double Rotation;

        public void SetParameter(List<string> parameters, CoordinateTransformer ct, Dictionary<int, IExpression> vars)
        {
            Exposure = (parameters[1] == "1");
            iWidth = ExpressionFactory.Create(parameters[2], ct, vars);
            iHeight = ExpressionFactory.Create(parameters[3], ct, vars);
            iX = ExpressionFactory.Create(parameters[4], ct, vars);
            iY = ExpressionFactory.Create(parameters[5], ct, vars);
            iRotation = ExpressionFactory.Create(parameters[6], ct, vars);
        }

        public void ToInternalValue(Dictionary<int, double> macroVariables, CoordinateTransformer ct)
        {
            Width = iWidth.Evaluate(macroVariables) * ct.UnitScale;
            Height = iHeight.Evaluate(macroVariables) * ct.UnitScale;
            X = iX.Evaluate(macroVariables) * ct.UnitScale;
            Y = iY.Evaluate(macroVariables) * ct.UnitScale;
            Rotation = iRotation.Evaluate(macroVariables);
        }

        public Geometry CreateGeometry(Point pos, bool isLPD)
        {            
            Point center = new Point(pos.X + X, pos.Y + X);
            double W = Width;
            double H = Height;

            Rect rect = new Rect(center.X - Width / 2, center.Y - Height / 2, Width, Height);

            var geo = new RectangleGeometry(rect);
            geo.Transform = new RotateTransform(Rotation, center.X, center.Y);
            geo.Freeze();

            return geo;
        }

        public Rect GetBounds()
        {
            return new Rect(X - Width / 2, Y - Height / 2, Width, Height);
        }

        public IPrimitive Clone()
        {
            return new CenterLinePrimitive
            {
                iWidth = this.iWidth,
                iHeight = this.iHeight,
                iX = this.iX,
                iY = this.iY,
                iRotation = this.iRotation
            };
        }
    }

    /// <summary>
    /// アウトラインプリミティブ
    /// </summary>
    public class OutLinePrimitive : IPrimitive
    {
        private bool Exposure;
        private List<IExpression> iPoints = new List<IExpression>();
        private IExpression iVertexCount;
        private IExpression iRotation;
        private List<double> Points = new List<double>();
        private int VertexCount;
        private double Rotation;

        public void SetParameter(List<string> parameters, CoordinateTransformer ct, Dictionary<int, IExpression> vars)
        {
            Exposure = (parameters[1] == "1");
            iVertexCount = ExpressionFactory.Create(parameters[2], ct, vars);
            for (int i = 3; i < parameters.Count() - 1; i++)
            {
                iPoints.Add(ExpressionFactory.Create(parameters[i], ct, vars));
            }
            iRotation = ExpressionFactory.Create(parameters[parameters.Count() - 1], ct, vars);
        }

        public void ToInternalValue(Dictionary<int, double> macroVariables, CoordinateTransformer ct)
        {
            foreach (IExpression item in iPoints)
            {
                Points.Add(item.Evaluate(macroVariables) * ct.UnitScale);
            }
            VertexCount = (int)(iVertexCount.Evaluate(macroVariables));
            Rotation = iRotation.Evaluate(macroVariables);
        }

        public Geometry CreateGeometry(Point pos, bool isLPD)
        {            
            List<Point> drawPoints = new List<Point>(VertexCount);

            int index = 0;

            for (int i = 0; i < VertexCount; i++)
            {
                double x = Points[index++];
                double y = Points[index++];

                drawPoints.Add(new Point(pos.X + x, pos.Y + y));
            }
            
            var geo = new StreamGeometry();
            geo.FillRule = FillRule.Nonzero;

            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(drawPoints[0], isLPD, true);

                for (int i = 1; i < drawPoints.Count; i++)
                {
                    ctx.LineTo(drawPoints[i], true, false);
                }
            }

            geo.Transform = new RotateTransform(Rotation, pos.X, pos.Y);

            geo.Freeze();

            return geo;
        }

        public Rect GetBounds()
        {
            return new Rect();
        }

        public IPrimitive Clone()
        {
            return new OutLinePrimitive
            {
                iRotation = this.iRotation,
                iPoints = this.iPoints?
                .Select(p => p?.Clone())
                .ToList() ?? new List<IExpression>(),
                iVertexCount = this.iVertexCount
            };
        }
    }
}