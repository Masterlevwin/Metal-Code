using System;
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
            work.type.Counted += PriceChanged;
            work.type.Priced += PriceChanged;
            work.PropertiesChanged += SaveOrLoadProperties;
            TypeDrop.Items.Add("кг");
            TypeDrop.Items.Add("шт");
        }

        private void SetRal(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetRal(tBox.Text);
        }
        public void SetRal(string _ral)
        {
            Ral = _ral;
        }

        private void SetType(object sender, SelectionChangedEventArgs e)
        {
            SetType(TypeDrop.SelectedIndex);
        }
        public void SetType(int ndx = 0)
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

        private void PriceChanged()
        {
            float _ratio = 1;
            if (float.TryParse(Ratio, out float r)) _ratio = r;
            
            for (int i = 0; i < work.type.WorkControls.Count; i++)
            {
                WorkControl _work = work.type.WorkControls[i];
                if (_work.workType is PropertyControl prop)
                {
                    if (TypeDrop.SelectedIndex == 0)
                    {
                        Price = work.Result = (float)Math.Round(_ratio * work.type.Count * prop.Mass * 812
                            / MainWindow.Parser(prop.S_prop.Text) / work.type.MetalDict[$"{work.type.MetalDrop.SelectedItem}"], 2) + work.Price;
                    }
                    else if (TypeDrop.SelectedIndex == 1)
                    {
                        Price = work.Result = _ratio * work.type.Count * 350 + work.Price;
                    }
                    break;
                }
            }

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
