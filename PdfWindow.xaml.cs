using Spire.Pdf;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для PdfWindow.xaml
    /// </summary>
    public partial class PdfWindow : Window
    {
        public static PdfWindow P = new();

        public PdfWindow()
        {
            InitializeComponent();
            P = this;
            DataContext = this;
        }

        private void HideWindow(object sender, CancelEventArgs e)
        {
            Hide();
            e.Cancel = true;
        }

        public void OpenFile(string path)
        {
            PdfDocument doc = new();
            doc.LoadFromFile(path);

            Stream fs = doc.SaveAsImage(0);
            BinaryReader br = new(fs);
            int numBytes = (int)fs.Length;
            byte[] buff = br.ReadBytes(numBytes);

            PdfImage.Source = MainWindow.CreateBitmap(buff);
        }
    }
}