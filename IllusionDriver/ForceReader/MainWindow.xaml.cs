using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Threading;
using System.ComponentModel;
using System.IO.Ports;
using System.Windows.Threading;
using System.Windows.Media;

namespace ForceReaderStudy
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static string ipAddr = "192.168.1.1";
        static int port = 49152;
        Response resp;
        Force force;

        BackgroundWorker worker;

        bool workerOn = false;
        IPEndPoint ep;


        bool stretchBottomOut = false;
        double stretchBottomOutThresh = 200.0;

        bool twistBottomOut = false;
        double twistBottomOutThresh = 20.5;

        bool bendBottomOut = false;
        double bendBottomOutThresh = 20.5;


        public MainWindow()
        {
            InitializeComponent();

            resp = new Response();
            force = new Force();

            worker = new BackgroundWorker();            
            worker.DoWork += new DoWorkEventHandler(worker_doWork);
            worker.ProgressChanged += new ProgressChangedEventHandler
                    (worker_ProgressChanged);
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler
                    (worker_RunWorkerCompleted);
            worker.WorkerReportsProgress = true;
            worker.WorkerSupportsCancellation = true;            
            client = new UdpClient();
            ep = new IPEndPoint(IPAddress.Parse(ipAddr), port); // endpoint where server is listening
            client.Connect(ep);
        }


        int fps = 0;
        void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled) ;

            else if (e.Error != null)
            {
                Debug.WriteLine("Error while performing background operation.");
            }
            else
            {
                Debug.WriteLine("Everything complete");
            }
        }

        void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            force.SetForce(resp);
            
            fps++;
            ProcessForce();
        }
        
        void worker_doWork(object sender, DoWorkEventArgs e)
        {
            while(true)
            {
                Thread.Sleep(1); 
                GetFrame();                
                worker.ReportProgress(1);                
                if (worker.CancellationPending)
                {
                    e.Cancel = true;
                    worker.ReportProgress(0);
                    return;
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!workerOn)
            {
                workerOn = true;
                worker.RunWorkerAsync();
            }
        }

        UdpClient client;
        private void GetFrame()
        {            
            byte[] request = new byte[8];
            request[0] = 0x12;
            request[1] = 0x34;
            request[2] = 0x00;
            request[3] = 0x02;
            request[4] = 0x00;
            request[5] = 0x00;
            request[6] = 0x00;
            request[7] = 0x01;
                        
            client.Send(request, 8);
            var receivedData = client.Receive(ref ep);            
            
            for (int i = 0; i < 6; i ++)
            {
                var data = receivedData.Skip(12 + i * 4).Take(4).ToArray();
                resp.FTData[i] = BitConverter.ToInt32(data.Reverse().ToArray(),0);
            }
        }

        private void stopButton_Click(object sender, RoutedEventArgs e)
        {
            workerOn = false;
            worker.CancelAsync();
        }

        private void calButton_Click(object sender, RoutedEventArgs e)
        {
            resp.SetBG();
        }
        

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            
            DispatcherTimer UITimer = new DispatcherTimer();
            UITimer.Interval = TimeSpan.FromMilliseconds(16);
            UITimer.Tick += UITimer_Tick;
            UITimer.Start();

            DispatcherTimer FPSTimer = new DispatcherTimer();
            FPSTimer.Interval = TimeSpan.FromMilliseconds(1000);
            FPSTimer.Tick += FPSTimer_Tick;
            FPSTimer.Start();
        }

        private void UITimer_Tick(object sender, EventArgs e)
        {
            f1.Text = force.fx.ToString("0.00");
            f2.Text = force.fy.ToString("0.00");
            f3.Text = force.fz.ToString("0.00");
            t1.Text = force.tx.ToString("0.00");
            t2.Text = force.ty.ToString("0.00");
            t3.Text = force.tz.ToString("0.00");
        }

        private void FPSTimer_Tick(object sender, EventArgs e)
        {
            fpsBox.Content = fps.ToString();
            fps = 0;
        }

        double length = 0;        
        double twistValue = 0;
        Point bend;
        private void ProcessForce()
        {
            if (!freeze)
            {
                length = force.fz;
                twistValue = force.tz;
                bend = new Point(force.tx, force.ty);
            }                

            if (actuator.IsOpen)
            {
                if (mStretch)
                { 
                    if(Math.Abs(length) < stretchBottomOutThresh)
                    {
                        stretchBottomOut = false;
                        if (Math.Abs(length - lastStretch) > stretchThresh)
                        {
                            PlayGrain();
                            lastStretch = length;
                        }
                    }
                    else
                    {
                        if(!stretchBottomOut)
                        {
                            stretchBottomOut = true;
                            PlayBottomOut();
                        }
                    }
                }
                if(mTwist)
                {
                    if(Math.Abs(twistValue) < twistBottomOutThresh)
                    {
                        twistBottomOut = false;
                        if (Math.Abs(twistValue - lastTwist) > twistThresh)
                        {
                            PlayGrain();
                            lastTwist = twistValue;
                        }
                    }
                    else
                    {
                        if(!twistBottomOut)
                        {
                            twistBottomOut = true;
                            PlayBottomOut();
                        }
                    }
                    
                }
                if(mBend)
                {
                    double dx = bend.X - lastBend.X;
                    double dy = bend.Y - lastBend.Y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    double bendMag = Math.Sqrt(bend.X * bend.X + bend.Y * bend.Y);
                    
                    if(bendMag < bendBottomOutThresh)
                    {
                        bendBottomOut = false;
                        if (dist > bendThresh)
                        {
                            PlayGrain();
                            lastBend = new Point(bend.X, bend.Y);
                        }
                    }    
                    else
                    {
                        if(!bendBottomOut)
                        {
                            bendBottomOut = true;
                            PlayBottomOut();
                        }
                    }               
                }
            }
        }

        SerialPort actuator = new SerialPort();
        private void serialBtn_Click(object sender, RoutedEventArgs e)
        {
            actuator.BaudRate = 230400;
            actuator.PortName = portBox.Text;
            actuator.Open();
            if(actuator.IsOpen)
            {
                serialButton.Background = Brushes.Green;
            }
        }
        
        private void PlayBottomOut()
        {
            actuator.Write("b");
        }

        private void PlayGrain()
        {
            actuator.Write("a");
        }

        double lastStretch = 0;
        double lastTwist = 0;
        Point lastBend;
        
        double stretchThresh = 0.1;
        double bendThresh = 0.1;
        double twistThresh = 0.1;
        private void setButton_Click(object sender, RoutedEventArgs e)
        {
            stretchThresh = double.Parse(stretchThreshBox.Text);
            bendThresh = double.Parse(bendThreshBox.Text);
            twistThresh = double.Parse(twistThreshBox.Text);
            lastBend = new Point(0, 0);

            stretchBottomOutThresh = double.Parse(stretchLimitBox.Text);
            bendBottomOutThresh = double.Parse(bendLimitBox.Text);
            twistBottomOutThresh = double.Parse(twistLimitBox.Text);
        }

        bool freeze = false;
        private void sendCheck_Checked(object sender, RoutedEventArgs e)
        {
            freeze = true;
        }

        private void sendCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            freeze = false;
        }

        bool mStretch = true;
        bool mBend = false;
        bool mTwist = false;
        private void paramCheckChanged(object sender, RoutedEventArgs e)
        {
            if(sender == bendCheck)
            {
                mBend = bendCheck.IsChecked.Value;
            }
            else if (sender == stretchCheck)
            {
                mStretch = stretchCheck.IsChecked.Value;
            }
            else if (sender == twistCheck)
            {
                mTwist = twistCheck.IsChecked.Value;
            }
        }      
    }

    class Force
    {
        static double cntPerForce = 4448221.5;
        static double cntPerTorque = 112984.8281;

        public double fx, fy, fz;
        public double tx, ty, tz;
        public Force()
        {
            fx = 0; fy = 0; fz = 0;
            tx = 0; ty = 0; tz = 0;
        }
        public void SetForce(Response resp)
        {
            fx = (fx + Utils.lbToN(resp.GetData(0) / cntPerForce)) / 2;
            fy = (fy + Utils.lbToN(resp.GetData(1) / cntPerForce)) / 2;
            fz = (fz + Utils.lbToN(resp.GetData(2) / cntPerForce)) / 2;

            tx = Utils.lbInToNm(resp.GetData(3) / cntPerTorque);
            ty = Utils.lbInToNm(resp.GetData(4) / cntPerTorque);
            tz = Utils.lbInToNm(resp.GetData(5) / cntPerTorque);
        }        
    }

    class Response
    {
        public Int32[] FTData;
        public Int32[] FTbackground;

        public Response()
        {
            FTData = new Int32[6];
            FTbackground = new Int32[6];
        }
        public void SetBG()
        {
        for (int i = 0; i < 6; i++)
                FTbackground[i] = FTData[i];
        }
        public Int32 GetData(int i)
        {            
            return FTData[i] - FTbackground[i];
        }
    }
}
