using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
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
using System.Windows.Threading;
using System.Xml;
using ZedGraph;

namespace Arduino
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            port = new SerialPort();
            string[] gpn = SerialPort.GetPortNames();
            int[] bps = { 9600, 19200, 38400, 57600 };
            selSedal.ItemsSource = gpn;
            SelSpeed.ItemsSource = bps;
            port.DataReceived += PortReceived;
            lpc = new PointCollection();
            tpc = new PointCollection();
            ltb = new TextBoxChanged();
            ttb = new TextBoxChanged();
            sendlog = new List<string>();
            readlog = new List<string>();
            msg = new byte[3];
            tickStart = 0;
            textL.DataContext = ltb;
            textT.DataContext = ttb;
            InitGraph(graphT, "温度", System.Drawing.Color.Red);
            InitGraph(graphL, "光强", System.Drawing.Color.Blue);
            UpdateColor();
            log = false;
        }
        private void PortReceived(object sender, SerialDataReceivedEventArgs e)
        {
            for (int i = port.BytesToRead; i > 0; i--)
            {
                msg[len] = (byte)port.ReadByte();
                len++;
                if (len == 3)
                {
                    if (DisposeMsg())
                    {
                        string[] s = new string[3];
                        for (int j = 0; j < 3; j++)
                        {
                            s[j] = msg[j].ToString("X2");
                        }
                        string read = string.Join(" ", s);
                        Dispatcher.BeginInvoke(new Action(delegate ()
                        {
                            msgRead.Items.Add(read);
                        }));
                        if (log)
                        {
                            readlog.Add(read);
                        }
                    }
                    len = 0;
                }
            }
        }
        private bool DisposeMsg()
        {
            switch (msg[0] >> 4)
            {
                case 0xE:
                    int data = msg[1] | (msg[2] << 7);
                    switch (msg[0] & 0xF)
                    {
                        case 0x0:
                            ttb.Text = data.ToString();
                            UpdateGraph(graphT, data);
                            return true;
                        case 0x1:
                            ltb.Text = data.ToString();
                            UpdateGraph(graphL, data);
                            return true;
                    }
                    break;
            }
            return false;
        }
        private void PortSelected(object sender, SelectionChangedEventArgs e)
        {
            port.PortName = (string)selSedal.SelectedItem;
        }
        private void BaudSelected(object sender, SelectionChangedEventArgs e)
        {
            port.BaudRate = (int)SelSpeed.SelectedItem;
        }
        private void BtnConnected(object sender, RoutedEventArgs e)
        {
            try
            {
                port.Open();
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void BtnDisconted(object sender, RoutedEventArgs e)
        {
            try
            {
                port.Close();
            }
            catch (IOException ex)
            {
                MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void SendRGBValue(object sender, RoutedEventArgs e)
        {
            foreach(UIElement ctrl in gridRGB.Children)
            {
                if(ctrl is Slider)
                {
                    SendSliderMsg((Slider)ctrl);
                }
            }
        }
        private void SliderSlided(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            SendSliderMsg((Slider)sender);
            UpdateColor();
        }
        private void SendSliderMsg(Slider s)
        {
            if (!port.IsOpen)
            {
                MessageBox.Show("串口未打开！");
                return;
            }
            byte[] send = new byte[3];
            switch (s.Uid)
            {
                case "R":
                    send[0] = 0xD3;
                    break;
                case "G":
                    send[0] = 0xD5;
                    break;
                case "Y":
                    send[0] = 0xD6;
                    break;
                case "B":
                    send[0] = 0xD9;
                    break;
                case "W":
                    send[0] = 0xDA;
                    break;
            }
            byte v = SliderValue(s);
            send[1] = (byte)(v & 0x7F);
            send[2] = (byte)(v >> 0x7);
            port.Write(send, 0, 3);
            ShowSendMsg(send);
        }
        private byte SliderValue(Slider s)
        {
            return (byte)(s.Value * 25.5);
        }
        private void InitGraph(ZedGraphControl g, string t, System.Drawing.Color c)
        {
            GraphPane myPane = g.GraphPane;
            myPane.Title.Text = "曲线图";
            myPane.XAxis.Title.Text = "时间";
            myPane.YAxis.Title.Text = t;
            RollingPointPairList list = new RollingPointPairList(600);
            LineItem curve = myPane.AddCurve(t, list, c, SymbolType.Diamond);
            tickStart = Environment.TickCount;
            graphT.AxisChange();
        }
        private void UpdateGraph(ZedGraphControl g ,double x)
        {
            try
            {
                if (g.GraphPane.CurveList.Count <= 0) return;
                for (int idxList = 0; idxList < g.GraphPane.CurveList.Count; idxList++)
                {
                    LineItem curve = g.GraphPane.CurveList[idxList] as LineItem;
                    if (curve == null) return;
                    IPointListEdit list = curve.Points as IPointListEdit;
                    if (list == null) return;
                    double time = (Environment.TickCount - tickStart) / 1000.0;
                    list.Add(time, x);
                    Scale xScale = g.GraphPane.XAxis.Scale;
                    if (time > xScale.Max - xScale.MajorStep)
                    {
                        xScale.Max = time + xScale.MajorStep;
                        xScale.Min = xScale.Max - 30.0;
                    }
                }
                g.AxisChange();
                g.Invalidate();
            }
            catch (Exception)
            {

            }
        }
        private void UpdateColor()
        {
            colorBlock.Fill = new SolidColorBrush(Color.FromRgb(SliderValue(sliderR), SliderValue(sliderG), SliderValue(sliderB)));
        }
        private void ShowSendMsg(byte[] m)
        {
            string[] s = new string[3];
            for (int j = 0; j < 3; j++)
            {
                s[j] = m[j].ToString("X2");
            }
            string send = string.Join(" ", s);
            Dispatcher.BeginInvoke(new Action(delegate ()
            {
                msgSend.Items.Add(send);
            }));
            if (log)
            {
                sendlog.Add(send);
            }
        }
        private void ButtonBgn_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.DefaultExt = ".xml";
            dlg.Filter = "xml文件(.xml)|*.xml";
            dlg.RestoreDirectory = true;
            if (dlg.ShowDialog().Value)
            {
                xmlPath = dlg.FileName;
                XmlTextWriter writer = new XmlTextWriter(xmlPath, Encoding.UTF8);
                writer.Formatting = Formatting.Indented;
                writer.WriteStartDocument();
                writer.WriteStartElement("Log");
                writer.WriteEndElement();
                writer.Close();
            }
            else
            {
                xmlPath = string.Empty;
            }
        }
        private void BtnOkClick(object sender, RoutedEventArgs e)
        {
            log = true;
        }
        private void BtnClClick(object sender, RoutedEventArgs e)
        {
            log = false;
            sendlog.Clear();
            readlog.Clear();
        }
        private void LogEndClick(object sender, RoutedEventArgs e)
        {
            if (log && xmlPath != string.Empty)
            {
                XmlDocument xml = new XmlDocument();
                xml.Load(xmlPath);
                XmlNode node = xml.SelectSingleNode("Log");
                XmlElement send, read;
                foreach (string s in sendlog)
                {
                    send = xml.CreateElement("Send");
                    send.InnerText = s;
                    node.AppendChild(send);
                }
                foreach (string s in readlog)
                {
                    read = xml.CreateElement("Read");
                    read.InnerText = s;
                    node.AppendChild(read);
                }
                xml.Save(xmlPath);
                readlog.Clear();
                readlog.Clear();
            }
        }
        private PointCollection lpc;
        private PointCollection tpc;
        private TextBoxChanged ttb;
        private TextBoxChanged ltb;
        private SerialPort port;
        private byte[] msg;
        private int tickStart;
        private int len;
        private string xmlPath;
        private XmlDocument xml;
        private List<string> sendlog;
        private List<string> readlog;
        private bool log;

    }
    class TextBoxChanged : INotifyPropertyChanged
    {
        public string Text
        {
            set
            {
                text = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Text"));
            }
            get
            {
                return text;
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private string text;
    }
}
