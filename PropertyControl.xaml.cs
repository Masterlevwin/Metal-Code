using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для PropertyControl.xaml
    /// </summary>
    public partial class PropertyControl : UserControl
    {
        public float Price { get; set; }

        private readonly WorkControl work;
        public PropertyControl(WorkControl _work)
        {
            InitializeComponent();
            work = _work;
            DataContext = work.DataContext;
            work.type.Priced += PriceView;
        }

        public void PriceView(TypeDetailControl t)
        {
            Price = t.Count * work.Price;
            TypeDetailPrice.Text = $"{Price}";
        }
    }
}
