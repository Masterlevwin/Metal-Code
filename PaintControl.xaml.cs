using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для PaintControl.xaml
    /// </summary>
    public partial class PaintControl : UserControl
    {
        //Text="{Binding Price, Mode=OneWay, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type local:PaintControl}}}"
        public static readonly DependencyProperty MyPropertyPrice =
            DependencyProperty.Register("Price", typeof(float), typeof(PaintControl));
        public float Price
        {
            get { return (float)GetValue(MyPropertyPrice); }
            set { SetValue(MyPropertyPrice, value); }
        }

        //Text="{Binding Ratio, Mode=OneWay, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type local:PaintControl}}}"
        public static readonly DependencyProperty MyPropertyRatio =
            DependencyProperty.Register("Ratio", typeof(string), typeof(PaintControl));
        public string Ratio
        {
            get { return (string)GetValue(MyPropertyRatio); }
            set { SetValue(MyPropertyRatio, value); }
        }

        //Text="{Binding Ral, Mode=OneWay, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type local:PaintControl}}}"
        public static readonly DependencyProperty MyPropertySquare =
            DependencyProperty.Register("Ral", typeof(string), typeof(PaintControl));
        public string Ral
        {
            get { return (string)GetValue(MyPropertySquare); }
            set { SetValue(MyPropertySquare, value); }
        }

        private readonly WorkControl work;
        public PaintControl(WorkControl _work)
        {
            InitializeComponent();
            work = _work;

            work.PropertiesChanged += SaveOrLoadProperties; // подписка на сохранение и загрузку файла
            work.type.Counted += PriceChanged;              // подписка на изменение количества типовых деталей  
            foreach (WorkControl w in work.type.WorkControls)
                if (w != work && w.workType is PropertyControl prop)
                    prop.MassChanged += MassChanged;        // подписка на изменение массы типовой детали

            // формирование списка видов расчета окраски
            foreach (string s in TypeDict.Keys) TypeDrop.Items.Add(s);
        }

        private void SetRal(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetRal(tBox.Text);
        }
        public void SetRal(string _ral)
        {
            Ral = _ral;
        }

        private Dictionary<string, float> TypeDict = new()
        {
            ["кг"] = 812,
            ["шт"] = 350
        };
        private void SetType(object sender, SelectionChangedEventArgs e)
        {
            SetType(TypeDrop.SelectedIndex);
        }
        public void SetType(int ndx)
        {
            TypeDrop.SelectedIndex = ndx;
            PriceChanged();
        }

        private void SetRatio(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetRatio(tBox.Text);
        }
        private void SetRatio(string _ratio)
        {
            Ratio = _ratio;
            PriceChanged();
        }

        float Mass { get; set; }
        private void MassChanged(PropertyControl prop)
        {
            Mass = prop.Mass/ MainWindow.Parser(prop.S_prop.Text)/ work.type.MetalDict[$"{work.type.MetalDrop.SelectedItem}"];
            PriceChanged();
        }
        private void PriceChanged()
        {
            float _ratio = 1;
            if (float.TryParse(Ratio, out float r)) _ratio = r;

            Price = work.Result = (float)Math.Round(TypeDrop.SelectedIndex == 0 ? Mass * TypeDict[$"{TypeDrop.SelectedItem}"] * _ratio * work.type.Count + work.Price
                : TypeDict[$"{TypeDrop.SelectedItem}"] * _ratio * work.type.Count + work.Price, 2);

            work.type.det.PriceResult();
        }

        public void SaveOrLoadProperties(WorkControl w, bool isSaved)
        {
            if (isSaved)
            {
                w.propsList.Clear();
                w.propsList.Add(Ral);
                w.propsList.Add($"{TypeDrop.SelectedIndex}");
                w.propsList.Add(Ratio);
            }
            else
            {
                SetRal(w.propsList[0]);
                SetType((int)MainWindow.Parser(w.propsList[1]));
                SetRatio(w.propsList[2]);
            }
        }
    }
}
