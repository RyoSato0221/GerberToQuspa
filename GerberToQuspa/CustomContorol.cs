using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace GerberToQuspa
{
    public class CustomToggleButton : ToggleButton
    {
        /// <summary>
        /// ON時のテキスト
        /// </summary>
        public static readonly DependencyProperty OnTextProperty =
            DependencyProperty.Register(nameof(OnText), typeof(object), typeof(CustomToggleButton), new UIPropertyMetadata(null));

        public object OnText
        {
            get { return (object)GetValue(OnTextProperty); }
            set { SetValue(OnTextProperty, value); }
        }

        /// <summary>
        /// OFF時のテキスト
        /// </summary>
        public static readonly DependencyProperty OffTextProperty =
            DependencyProperty.Register(nameof(OffText), typeof(object), typeof(CustomToggleButton), new UIPropertyMetadata(null));

        public object OffText
        {
            get { return (object)GetValue(OffTextProperty); }
            set { SetValue(OffTextProperty, value); }
        }

        /// <summary>
        /// InnerFillProperty
        /// </summary>
        public static readonly DependencyProperty InnerFillProperty
            = DependencyProperty.Register(nameof(InnerFill), typeof(object), typeof(CustomToggleButton), new UIPropertyMetadata(null));

        public object InnerFill
        {
            get { return GetValue(InnerFillProperty); }
            set { SetValue(InnerFillProperty, value); }
        }

        /// <summary>
        /// ThumbのMargin
        /// </summary>
        public static readonly DependencyProperty ThumbMarginProperty
            = DependencyProperty.Register(nameof(ThumbMargin), typeof(Thickness), typeof(CustomToggleButton), new PropertyMetadata(new Thickness(0)));

        public Thickness ThumbMargin
        {
            get { return (Thickness)GetValue(ThumbMarginProperty); }
            set { SetValue(ThumbMarginProperty, value); }
        }

        /// <summary>
        /// ThumbのWidth
        /// </summary>
        public static readonly DependencyProperty ThumbWidthProperty =
            DependencyProperty.Register(nameof(ThumbWidth), typeof(double), typeof(CustomToggleButton), new PropertyMetadata(0.0));

        public double ThumbWidth
        {
            get { return (double)GetValue(ThumbWidthProperty); }
            set { SetValue(ThumbWidthProperty, value); }
        }

        /// <summary>
        /// ThumbのHeight
        /// </summary>
        public static readonly DependencyProperty ThumbHeightProperty =
            DependencyProperty.Register(nameof(ThumbHeight), typeof(double), typeof(CustomToggleButton), new PropertyMetadata(0.0));

        public double ThumbHeight
        {
            get { return (double)GetValue(ThumbHeightProperty); }
            set { SetValue(ThumbHeightProperty, value); }
        }
    }

    /// -------------------------------------------------------------------------------
    /// <summary>
    /// RadioButtonカスタマイズ
    /// </summary>
    /// -------------------------------------------------------------------------------
    public class CustomRadioButton : RadioButton
    {
        /// <summary>
        /// OuterBorderProperty
        /// 外枠角の半径を指定
        /// </summary>
        public static readonly DependencyProperty OuterBorderProperty
            = DependencyProperty.Register(nameof(OuterBorder), typeof(object), typeof(CustomRadioButton), new UIPropertyMetadata(null));

        public object OuterBorder
        {
            get { return GetValue(OuterBorderProperty); }
            set { SetValue(OuterBorderProperty, value); }
        }

        /// <summary>
        /// OuterFillProperty
        /// </summary>
        public static readonly DependencyProperty OuterFillProperty
            = DependencyProperty.Register(nameof(OuterFill), typeof(object), typeof(CustomRadioButton), new UIPropertyMetadata(null));

        public object OuterFill
        {
            get { return GetValue(OuterFillProperty); }
            set { SetValue(OuterFillProperty, value); }
        }

        /// <summary>
        /// InnerBorderProperty
        /// </summary>
        public static readonly DependencyProperty InnerBorderProperty
            = DependencyProperty.Register(nameof(InnerBorder), typeof(object), typeof(CustomRadioButton), new UIPropertyMetadata(null));

        public object InnerBorder
        {
            get { return GetValue(InnerBorderProperty); }
            set { SetValue(InnerBorderProperty, value); }
        }

        /// <summary>
        /// InnerFillProperty
        /// </summary>
        public static readonly DependencyProperty InnerFillProperty
            = DependencyProperty.Register(nameof(InnerFill), typeof(object), typeof(CustomRadioButton), new UIPropertyMetadata(null));

        public object InnerFill
        {
            get { return GetValue(InnerFillProperty); }
            set { SetValue(InnerFillProperty, value); }
        }

        /// <summary>
        /// InnerDiameterProperty
        /// </summary>
        public static readonly DependencyProperty InnerDiameterProperty =
            DependencyProperty.Register(nameof(InnerDiameter), typeof(double), typeof(CustomRadioButton), new PropertyMetadata(8.0));

        public double InnerDiameter
        {
            get { return (double)GetValue(InnerDiameterProperty); }
            set { SetValue(InnerDiameterProperty, value); }
        }
    }

    /// -------------------------------------------------------------------------------
    /// <summary>
    /// CheckBoxカスタマイズ
    /// </summary>
    /// -------------------------------------------------------------------------------
    public class CustomCheckBox : CheckBox
    {
        /// <summary>
        /// CheckForegroundProperty
        /// チェックマークの色を指定する
        /// </summary>
        public static readonly DependencyProperty CheckForegroundProperty =
            DependencyProperty.Register(nameof(CheckForeground), typeof(object), typeof(CustomCheckBox), new UIPropertyMetadata(null));

        public object CheckForeground
        {
            get { return (object)GetValue(CheckForegroundProperty); }
            set { SetValue(CheckForegroundProperty, value); }
        }
    }
}
