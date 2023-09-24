using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для TypeDetailSettings.xaml
    /// </summary>
    public partial class TypeDetailSettings : Window
    {
        public TypeDetail TypeDetail { get; set; }
        public TypeDetailSettings(TypeDetail typeDetail)
        {
            InitializeComponent();
            TypeDetail = typeDetail;
            DataContext = TypeDetail;
        }

        void Accept_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
