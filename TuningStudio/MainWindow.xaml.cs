using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TuningStudio.FileFormats;

namespace TuningStudio
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ViewModel.Main _vm;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new ViewModel.Main();
            DataContext = _vm;
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDlg = new Microsoft.Win32.OpenFileDialog();
            Nullable<bool> result = openFileDlg.ShowDialog();
            if (result == true)
            {
                //SRecord sr = new SRecord(openFileDlg.FileName, true);
                //sr.Read();
                //string test = sr.ReadRangeFromFile("80660341", "80660462");
                //sr.LoadData = false;
                //sr.Modify("8066001D", "FFFFAAAAAAAAAAAAAAAABBBBBBBBBBBBCCCCCCCCDDDDDDDDEEEEEEEEFFFFFFFF000000001111111122222222");
                //sr.LoadData = true;
                //string test2 = sr.ReadRange("80660341", "80660462");
                //string test3 = sr.ReadRange("80660341", 2);
                //byte[] test4 = sr.ReadByteRange("80660341", "80660342");
                //byte[] test5 = sr.ReadByteRange("80660341", 2);
                //sr.ExportRangeToBin("C:\\Users\\ay50767\\OneDrive - Alliance\\Bureau\\test S19\\test.bin", "80660341", "80660462");
                //sr.ExportRangeToFile("C:\\Users\\ay50767\\OneDrive - Alliance\\Bureau\\test S19\\test.s19", "80660341", "80660462", specificHeader:"Hello everybody !", dataLength:200);
                //IntelHex intel = new IntelHex(openFileDlg.FileName, true);
                //intel.Read();
                //string test = intel.ReadRange("8081FF50", "80820050");
                //string test2 = intel.ReadRangeFromFile("8081FF50", "80820050");
                //intel.ExportRangeToBin("C:\\Users\\ay50767\\OneDrive - Alliance\\Bureau\\test S19\\test.bin", "8081FF51", "80820050");
                //intel.ExportRangeToFile("C:\\Users\\ay50767\\OneDrive - Alliance\\Bureau\\test S19\\test.hex", "8081FF51", "80820050");
                ////intel.Modify("8081FF51", "0000");
                //test = intel.ReadRange("8081FF50", "80820050");

                //intel.LoadData = false;
                //intel.Modify("8000005D", "FFFFAAAAAAAAAAAAAAAABBBBBBBBBBBBCCCCCCCCDDDDDDDDEEEEEEEEFFFFFFFF000000001111111122222222");
            }
        }

        private void TabItem_MouseEnter(object sender, MouseEventArgs e)
        {
            //MessageBox.Show("Test");

        }
    }
}
