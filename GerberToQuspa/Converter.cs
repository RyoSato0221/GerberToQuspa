using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace GerberToQuspa
{
    /// <summary>
    /// EnumをVisibilityに変換するコンバータ
    /// </summary>
    public class EnumToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var parameterString = parameter.ToString();
            if (null == parameterString)
                return Visibility.Collapsed;

            if (!Enum.IsDefined(value.GetType(), value))
                return Visibility.Collapsed;

            var parameterValue = Enum.Parse(value.GetType(), parameterString);
            return parameterValue.Equals(value) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// EnumをBooleanに変換するコンバータ
    /// </summary>
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var parameterString = parameter.ToString();
            if (null == parameterString)
                return DependencyProperty.UnsetValue;

            if (!Enum.IsDefined(value.GetType(), value))
                return DependencyProperty.UnsetValue;

            var parameterValue = Enum.Parse(value.GetType(), parameterString);
            return parameterValue.Equals(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var parameterString = parameter.ToString();
            if (null == parameterString)
                return DependencyProperty.UnsetValue;

            if (true.Equals(value))
                return Enum.Parse(targetType, parameterString);
            else
                return Binding.DoNothing;
        }
    }

    public class ClusterToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Color)
            {
                return new SolidColorBrush((Color)value);
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 表示時のみ値をスケール変換（例：×1000）し、小数点を表示しないコンバーター
    /// </summary>
    public class DoubleToScaledConverter : IValueConverter
    {
        public double Scale { get; set; }

        public DoubleToScaledConverter()
        {
            Scale = 1000.0; // デフォルト倍率
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double d;
            if (value == null || !double.TryParse(value.ToString(), out d))
                return "0";

            // 表示用：内部値 × Scale（整数表示）
            return Math.Round(d * Scale).ToString("0", culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double result;
            if (value == null || !double.TryParse(value.ToString(), NumberStyles.Float, culture, out result))
                return 0.0;

            // 入力値 → 内部値（÷Scale）
            return result / Scale;
        }
    }

    /// <summary>
    /// 入力途中の "-" や "." を許容する Double用コンバータ
    /// </summary>
    public class LooseDoubleConverter : IValueConverter
    {
        // ViewModel → TextBox
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            double d;
            if (double.TryParse(value.ToString(), out d))
                return d.ToString(culture);

            return value.ToString();
        }

        // TextBox → ViewModel
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = value as string;

            if (string.IsNullOrWhiteSpace(text))
                return 0.0; // 空欄時のデフォルト値（必要に応じて変更）

            // 入力途中（まだ数値として不完全）を許す
            if (text == "-" || text == "." || text == "-.")
                return Binding.DoNothing;

            double result;
            if (double.TryParse(text, NumberStyles.Any, culture, out result))
                return result;

            // 不正な文字列なら更新しない
            return Binding.DoNothing;
        }
    }

    public class OffsetMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2)
                return 0;

            double baseValue = 0;
            double offset = 0;

            if (values[0] != null)
                double.TryParse(values[0].ToString(), out baseValue);

            if (values[1] != null)
                double.TryParse(values[1].ToString(), out offset);

            return baseValue - offset;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            // 表示専用のため未実装
            return null;
        }
    }

    /// <summary>
    /// TrueとFalseを反転するコンバーター
    /// ex）Trueの時チェックなし、Falseの時チェック有のようなチェックボッスで使う
    /// </summary>
    public class BooleanInvertConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var boolValue = value as bool?;
            return boolValue.HasValue ? !boolValue.Value : false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var boolValue = value as bool?;
            return boolValue.HasValue ? !boolValue.Value : false;
        }
    }

    /// <summary>
    /// Enumの値が一致した際に非表示(Collapsed)にするコンバータ
    /// </summary>
    public class EnumEqualsToCollapsedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Visibility.Visible;

            string enumValueStr = value.ToString();
            string targetValueStr = parameter.ToString();

            if (enumValueStr.Equals(targetValueStr, StringComparison.OrdinalIgnoreCase))
                return Visibility.Collapsed;

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        private readonly BooleanToVisibilityConverter _converter
            = new BooleanToVisibilityConverter();

        public object Convert(object value, Type targetType,
                              object parameter, CultureInfo culture)
        {
            if (value is bool)
            {
                bool b = (bool)value;
                return _converter.Convert(!b, targetType, parameter, culture);
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType,
                                  object parameter, CultureInfo culture)
        {
            var result = _converter.ConvertBack(value, targetType, parameter, culture);

            if (result is bool)
            {
                bool b = (bool)value;
                return !b;
            }
            return false;
        }
    }
}
