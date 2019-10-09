#define DevelopStart

using System;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.IO;

using System.Threading;
using System.Windows.Threading;

using log4net;
using AOIHardware;
using Emgu.CV.WPF;
using LibAuthentication;
using Honeywell;
using HidLibrary;
using Basler.Pylon;
using csLTDMC;
using LibCamera;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace AOIWeldOnline
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        #region 定义
        public static readonly ILog m_Logger = LogManager.GetLogger(typeof(App));
        private static Mutex m_SingleInstanceMutex;
        public static ShowViewModel m_ShowState = new ShowViewModel();

        private PixelDataConverter m_CameraPixelConverter = new PixelDataConverter();
        private Stopwatch m_CameraStopWatch = new Stopwatch();

        //public static List<string> m_ListTestResult = new List<string>();//测试结果缓冲区

        public static List<XElement> m_ListTestResult = new List<XElement>();//测试结果缓冲区

        /// <summary>
        /// 硬件
        /// </summary>
        public static LightSourceJL m_LightSourceJL = new LightSourceJL();
        public static Datalogic m_DataLogic = new Datalogic();
        public static KSJDevice m_KSJDevice = new KSJDevice();
        public static CameraBasler m_CameraBasler = new CameraBasler();
        public static MotionDMC5000 m_Motion = new MotionDMC5000();
        public static LineBodyControl m_LineBodyControl = new LineBodyControl();


        private Scanner m_OnlineScanner = null;
        private Scanner m_OfflineScanner = null;

        //private int m_iNumErrorCell = 0;
        public static List<string> m_ListBarcode = new List<string>();//扫码枪的扫码结果缓冲区

        #endregion 定义

        #region 属性

        private DelegateNoneParameter m_ThumbnailShowHandler = null;
        /// <summary>
        /// 缩略图生成后显示
        /// </summary>
        public DelegateNoneParameter ThumbnailShowHandler
        {
            get { return m_ThumbnailShowHandler; }
            set { m_ThumbnailShowHandler = value; }
        }

        private DelegateXElementParameter m_TestResultShowHandler = null;
        /// <summary>
        /// 测试结果在缩略图上显示
        /// </summary>
        public DelegateXElementParameter TestResultShowHandler
        {
            get { return m_TestResultShowHandler; }
            set { m_TestResultShowHandler = value; }
        }

        private DelegateXElementParameter m_TestResultLightShowHandler = null;
        /// <summary>
        /// 测试结果在缩略图上显示
        /// </summary>
        public DelegateXElementParameter TestResultLightShowHandler
        {
            get { return m_TestResultLightShowHandler; }
            set { m_TestResultLightShowHandler = value; }
        }

        private DelegateStringParameter m_OnlineScannerHandler = null;
        /// <summary>
        /// 在线扫描枪处理
        /// </summary>
        public DelegateStringParameter OnlineScannerHandler
        {
            get { return m_OnlineScannerHandler; }
            set { m_OnlineScannerHandler = value; }
        }

        private DelegateStringParameter m_OfflineScannerHandler = null;
        /// <summary>
        /// 离线扫描枪处理
        /// </summary>
        public DelegateStringParameter OfflineScannerHandler
        {
            get { return m_OfflineScannerHandler; }
            set { m_OfflineScannerHandler = value; }
        }

        private DelegateMetaShow m_NextMetaHandler = null;
        /// <summary>
        /// 显示下一个异常元件
        /// </summary>
        public DelegateMetaShow NextMetaHandler
        {
            get { return m_NextMetaHandler; }
            set { m_NextMetaHandler = value; }
        }

        private DelegateMetaShow m_LastMetaHandler = null;
        /// <summary>
        /// 显示上一个异常元件
        /// </summary>
        public DelegateMetaShow LastMetaHandler
        {
            get { return m_LastMetaHandler; }
            set { m_LastMetaHandler = value; }
        }

        private DelegateBoardShow m_NextBoardHandler = null;
        /// <summary>
        /// 显示下一个板的测试结果
        /// </summary>
        public DelegateBoardShow NextBoardHandler
        {
            get { return m_NextBoardHandler; }
            set { m_NextBoardHandler = value; }
        }

        #endregion 属性



        #region 对象函数

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            //关于如何使用用户登录窗口的方式有多种，这里参照其中一种：http://stackoverflow.com/questions/968051/login-wpf-is-it-right
            //Without the next line your app would've ended upon closing Login window:
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            #region 使用互斥量，判断程序是否已经被打开

            string sProductName = null;
            if (Application.ResourceAssembly != null)
                sProductName = Application.ResourceAssembly.GetName().Name;
            else
            {
                Shutdown();
                return;
            }

            bool createNew;
            m_SingleInstanceMutex = new Mutex(true, sProductName, out createNew);
            if (createNew == false)
            {
                MessageBox.Show("应用程序已经在运行中...",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);

                //  终止此进程并为基础操作系统提供指定的退出代码。
                Shutdown();
                return;
            }

            #endregion 使用互斥量，判断程序是否已经被打开

            #region 软件认证

            if (Doc.m_Authentication.AuthenResult == Authentication_Result.None)
            {
                Shutdown();
                return;
            }
            #endregion 软件认证

            #region 运动卡检查开关状态
#if DevelopStart

            if (m_Motion.MotionLinkState != MotionLinkState.Linked)
            {
                Shutdown();
                return;
            }

            if (m_Motion.IOServStop != 0 || m_Motion.IOEMGStop != 0)
            {
                EmgDetectDialog emgDetectDlg = new EmgDetectDialog();
                if (emgDetectDlg.ShowDialog() == false)
                {
                    Shutdown();
                    return;
                }
            }
#endif
            #endregion 运动卡检查开关状态

            #region 用户登录

            LoginDlg userLoginDlg = new LoginDlg();
            if (userLoginDlg.ShowDialog() == false)
            {
                Shutdown();
                return;
            }
            m_Motion.GoHome();
            //if you have some cache to load, then show some progress dialog,
            //or welcome screen, or whatever...
            //after this, the MainWindow executes, so restore the ShutdownMode,
            //so the app ends with closing of main window (otherwise, you have to call
            //Applicaiton.Current.Shutdown(); explicitly in Closed event of MainWindow)
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            #endregion 用户登录

            m_LineBodyControl.CallBackFunction = Thread_LinebodyControl;
            ParaChanged_LineBodyControl();
        }

        /// <summary>
        /// 程序启动，在OnStartup之前调用
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            //在这里进行资源或参数设置的载入
            Doc.LoadSystemSetupFile();
            Doc.LoadProductListFile();
            //Doc.LoadLibFile();
            Doc.DeleteHistoryFiles();

            #region 软件认证

            Doc.m_Authentication.Set_DElicence(AppDomain.CurrentDomain.BaseDirectory + "DElicence.dat");
            switch (Doc.m_Authentication.Do_Authentication())
            {
                case Authentication_Result.None:
                    MessageBox.Show("软件没有授权！",
                                    Doc.MESSAGE_SOFTNAME,
                                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                case Authentication_Result.Play:
                    MessageBox.Show("演示版本，无法进行AOI识别！",
                                    Doc.MESSAGE_SOFTNAME,
                                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    //KSJDevice.SoftDogRegist(Doc.m_Authentication);
                    break;
                case Authentication_Result.Try:
                    if (Doc.m_Authentication.LastDay < 0)
                    {
                        MessageBox.Show("软件试用期限已过，无法进行AOI识别！",
                                        Doc.MESSAGE_SOFTNAME,
                                        MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    }
                    else
                    {
                        //GearMatch.SoftDogRegist(Doc.m_Authentication);
                        //OpenCVUtility.SoftDogRegist(Doc.m_Authentication);
                        //SampleOffset.SoftDogRegist(Doc.m_Authentication);
                    }
                    //KSJDevice.SoftDogRegist(Doc.m_Authentication);
                    break;
                case Authentication_Result.OK:
                    //GearMatch.SoftDogRegist(Doc.m_Authentication);
                    //OpenCVUtility.SoftDogRegist(Doc.m_Authentication);
                    //SampleOffset.SoftDogRegist(Doc.m_Authentication);
                    //KSJDevice.SoftDogRegist(Doc.m_Authentication);
                    break;
            }

            #endregion 软件认证



#if DevelopStart
            //RestoreBaslerTestThread();



            if (Doc.m_SystemParam.CameraType == CameraType.KSJ)
            {
                m_KSJDevice.DeviceInit();
                m_KSJDevice.InitDeviceList();
                CameraParaChanged_KSJ();
            }
            else CameraParaChanged_Basler();


            m_LightSourceJL.SwitchCH1 = Doc.m_SystemParam.SwitchCH1;
            m_LightSourceJL.SwitchCH2 = Doc.m_SystemParam.SwitchCH2;
            m_LightSourceJL.SwitchCH3 = Doc.m_SystemParam.SwitchCH3;
            m_LightSourceJL.SwitchCH4 = Doc.m_SystemParam.SwitchCH4;


            m_Motion.StateShowMapHandler += SystemStatusShow;
            m_Motion.Board_Init();
            //MotionCard_IOState();

            //RebootScanner();
#endif
        }

        /// <summary>
        /// 程序最终退出
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            //threadBoardInPlace = null;
            m_LineBodyControl.StopThread();

#if DevelopStart
            //m_Timer.Stop();
            if (Doc.m_SystemParam.CameraType == CameraType.KSJ)
                m_KSJDevice.DeviceExit();
            else m_CameraBasler.BaslerClose();


            m_Motion.Board_Close();

            //if (m_Scanner != null)
            //{
            //    m_Scanner.StopListen();
            //    m_Scanner.Dispose();
            //}
#endif



            Doc.SaveSystemSetupFile();

            //Doc.SaveLibFile(m_ShowState.LibMode);
            //Doc.SaveProducListFile();
            //if (m_ShowState.CurrentProductType.XElement != null)
            //    m_ShowState.CurrentProductType.XElement.Save(string.Format(Doc.m_SystemParam.SetupPath + "\\" + Doc.m_SystemParam.ProductFilename));
        }

        #endregion 对象函数

        #region 扫描枪
        public void RebootOnlineScanner()
        {
            //if (m_OnlineScanner == null)
            //{//直接创建
            //    OpenOnlineScanner();
            //}
            //else
            //{//
            //    if (m_OnlineScanner.ToString() != Doc.m_SystemParam.OnlineScanner)
            //    {//先关闭后打开
            //        //m_Scanner.Dispose();
            //        m_OnlineScanner = null;
            //        OpenOnlineScanner();
            //    }
            //}
            OpenOnlineScanner();
        }

        private void OpenOnlineScanner()
        {
            //if (String.Compare(Doc.m_SystemParam.OnlineScanner, "None") == 0)
            //    return;

            //HidDevice[] deviceList = HidDevices.Enumerate(Scanner.HoneywellVendorId).ToArray();
            //foreach (HidDevice dev in deviceList)
            //{
            //    string sDev = dev.ToString();
            //    if (sDev == Doc.m_SystemParam.OnlineScanner)
            //    {
            //        m_OnlineScanner = new Scanner(dev);
            //        m_OnlineScanner.DataRecieved += OnlineScannerDataRecieved;
            //        m_OnlineScanner.StartListen();
            //        break;
            //    }
            //}
            try
            {
                m_DataLogic.Open(Doc.m_SystemParam.ScanComName);
                m_DataLogic.ReceiveChanged += OnlineScannerDataRecieved;
            }
            catch
            {
                return;
            }
        }

        private void OnlineScannerDataRecieved()
        {
            if (m_ShowState.CurrentProductType.XElement == null)
                return;

            if (m_OnlineScannerHandler != null)
                m_OnlineScannerHandler(Encoding.ASCII.GetString(m_DataLogic.ReceiveBuf));

            m_ListBarcode.Add(Encoding.ASCII.GetString(m_DataLogic.ReceiveBuf).Replace("\r", ""));

            //扫码成功， 扫码枪触发输出置为高电平
            LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_TestResult, MotionDMC5000.LEVEL_HIGH);
        }

        public void RebootOfflineScanner()
        {
            if (m_OfflineScanner == null)
            {//直接创建
                OpenOfflineScanner();
            }
            else
            {//
                if (m_OfflineScanner.ToString() != Doc.m_SystemParam.OfflineScanner)
                {//先关闭后打开
                    //m_Scanner.Dispose();
                    m_OfflineScanner = null;
                    OpenOfflineScanner();
                }
            }
        }

        private void OpenOfflineScanner()
        {
            if (String.Compare(Doc.m_SystemParam.OfflineScanner, "None") == 0)
                return;

            HidDevice[] deviceList = HidDevices.Enumerate(Scanner.HoneywellVendorId).ToArray();
            foreach (HidDevice dev in deviceList)
            {
                string sDev = dev.ToString();
                if (sDev == Doc.m_SystemParam.OfflineScanner)
                {
                    m_OfflineScanner = new Scanner(dev);
                    m_OfflineScanner.DataRecieved += OfflineScannerDataRecieved;
                    m_OfflineScanner.StartListen();
                    break;
                }
            }
        }

        private void OfflineScannerDataRecieved(byte[] data)
        {
            if (m_ShowState.CurrentProductType.XElement == null)
                return;

            if (m_OfflineScannerHandler != null)
                m_OfflineScannerHandler(Encoding.ASCII.GetString(data));
        }

        #endregion 扫描枪

        #region 相机
        private int m_iCurProcessor = 0;//当前识别线程所在的处理器
        #region Basler相机

        /// <summary>
        /// 相机测试线程恢复
        /// </summary>
        //public void RestoreBaslerTestThread()
        //{
        //    m_CameraBasler.ImageGrabbed = OnCameraImageGrabbed;
        //    m_CameraBasler.BaslerRestart();
        //}
        public void CameraParaChanged_Basler()
        {
            m_CameraBasler.BaslerClose();

            if (Doc.m_SystemParam.CameraType == CameraType.KSJ)
                return;



            List<ICameraInfo> allCameras = CameraFinder.Enumerate();
            int iIndex = 0;
            m_CameraBasler.bTrigger = true;
            m_CameraBasler.CameraInfo = allCameras[iIndex];
            m_CameraBasler.ImageGrabbed = OnBaslerCameraImageGrabbed;
            m_CameraBasler.BaslerRestart();



            //lst_Camera.Items.Add(cameraInfo[CameraInfoKey.FullName]);
            //lst_Camera.Items.Add(cameraInfo[CameraInfoKey.FriendlyName]);

            //foreach (KeyValuePair<string, string> kvp in cameraInfo)
            //{
            //    lst_Camera.Items.Add(kvp.Key + ": " + kvp.Value);
            //}
            //iInfo.Count();
            //iInfo.GetType();
            //iInfo.GetEnumerator();
            //camera = new Camera(selectedCamera);

            //camera.CameraOpened += Configuration.AcquireContinuous;
            //// Register for the events of the image provider needed for proper operation.
            //camera.ConnectionLost += OnConnectionLost;
            //camera.CameraOpened += OnCameraOpened;
            //camera.CameraClosed += OnCameraClosed;
            //camera.StreamGrabber.GrabStarted += OnGrabStarted;
            //camera.StreamGrabber.ImageGrabbed += OnImageGrabbed;
            //camera.StreamGrabber.GrabStopped += OnGrabStopped;
        }

        /// <summary>
        /// 相机拍照结束事件：读相机图像保存为文件，
        /// </summary>
        private void OnBaslerCameraImageGrabbed(Object sender, ImageGrabbedEventArgs e)
        {
            //读照片并保存
            // The grab result is automatically disposed when the event call back returns.
            // The grab result can be cloned using IGrabResult.Clone if you want to keep a copy of it (not shown in this sample).
            IGrabResult grabResult = e.GrabResult;
            // Image grabbed successfully?
            if (grabResult.GrabSucceeded)
            {
                // Access the image data.
                //byte[] buffer = grabResult.PixelData as byte[];
                //Console.WriteLine("Gray value of first pixel: {0}", buffer[0]);
                //Console.WriteLine("");
                //SystemStatusShow(HWUnify.STATUSBAR, String.Format("SizeX: {0}, SizeY: {1}", grabResult.Width, grabResult.Height));

                // Reduce the number of displayed images to a reasonable amount if the camera is acquiring images very fast.
                if (!m_CameraStopWatch.IsRunning || m_CameraStopWatch.ElapsedMilliseconds > 40)
                {
                    int iCurPosX = LTDMC.dmc_get_encoder(m_Motion.CardID, MotionDMC5000.AXIS_X);
                    int iCurPosY = LTDMC.dmc_get_encoder(m_Motion.CardID, MotionDMC5000.AXIS_Y);

                    // 获取当前拍照位置信息，必须放在Doc.m_Motion.TakePicState = true;前面
                    XElement xTakePicturePoint = null;
                    foreach (XElement xTmpPoint in m_Motion.XPath.Elements("Point"))
                    {
                        if (xTmpPoint.Element("IsCurrent") == null || Boolean.Parse((string)xTmpPoint.Element("IsCurrent")) == false)
                            continue;

                        xTakePicturePoint = xTmpPoint;
                        break;

                    }

                    m_CameraStopWatch.Restart();
                    m_Motion.TakePicState = true;

                    try
                    {
                        Bitmap bitmap = new Bitmap(grabResult.Width, grabResult.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                        // Lock the bits of the bitmap.
                        System.Drawing.Imaging.BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, bitmap.PixelFormat);
                        // Place the pointer to the buffer of the bitmap.
                        m_CameraPixelConverter.OutputPixelFormat = PixelType.BGRA8packed;
                        IntPtr ptrBmp = bmpData.Scan0;
                        m_CameraPixelConverter.Convert(ptrBmp, bmpData.Stride * bitmap.Height, grabResult); //Exception handling TODO
                        bitmap.UnlockBits(bmpData);

                        // 显示在界面上
                        MemoryStream ms = new MemoryStream();
                        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                        BitmapImage pBImage = new BitmapImage();
                        pBImage.BeginInit();
                        pBImage.StreamSource = ms;
                        pBImage.EndInit();
                        //m_CameraPixelConverter.OutputPixelFormat = PixelType.BGR8packed;
                        //Byte[] pData = new Byte[m_CameraPixelConverter.GetBufferSizeForConversion(PixelType.BGR8packed, grabResult.Width, grabResult.Height)];
                        //m_CameraPixelConverter.Convert<Byte>(pData, grabResult);
                        pBImage.Freeze();//非常重要！！！
                        SystemStatusShow(HWUnify.CAMERA_IMAGE, pBImage);

                        switch (m_Motion.MotionType)
                        {
                            case MotionType.FlowMousePic://随动拍照
                            case MotionType.TestRouteEdit://测试路径显示、编辑
                                break;
                            case MotionType.HelicalPitchMap://导程映射
                                //保存当前图像
                                String sDstFilename = String.Format("{0}\\HelicalPitchMapDst.bmp", Doc.m_SystemParam.SetupPath);
                                bitmap.Save(sDstFilename);
                                //ImagePersistence.Save(ImageFileFormat.Bmp, sDstFilename, grabResult);
                                //保存在显示的图像
                                String sSrcFilename = String.Format("{0}\\HelicalPitchMapSrc.bmp", Doc.m_SystemParam.SetupPath);
                                SaveCameraImage(sSrcFilename);

                                m_Motion.MotionType = MotionType.None;
                                break;
                            case MotionType.ThumbnailImage://缩略图拍照
                                if (xTakePicturePoint != null)
                                {
                                    int iNumHorizontal = int.Parse((string)m_Motion.XPath.Element("NumHorizontal"));
                                    int iNumVertical = int.Parse((string)m_Motion.XPath.Element("NumVertical"));
                                    int iIndex = int.Parse((string)m_Motion.XPath.Element("Index"));

                                    int left = ((iIndex / iNumHorizontal % 2) == 0) ? ((iNumHorizontal - iIndex % iNumHorizontal - 1) * Doc.m_ThumbnailSet.SubThumbWide) : ((iIndex % iNumHorizontal) * Doc.m_ThumbnailSet.SubThumbWide);
                                    int top = (iNumVertical - (iIndex / iNumHorizontal) - 1) * Doc.m_ThumbnailSet.SubThumbHeight;
                                    //int top = ((iIndex / iNumVertical % 2) == 0) ? ((iNumVertical - iIndex % iNumVertical - 1) * Doc.m_ThumbnailSet.SubThumbHeight) : ((iIndex % iNumVertical) * Doc.m_ThumbnailSet.SubThumbHeight);
                                    //int left = (iNumHorizontal - (iIndex / iNumVertical) - 1) * Doc.m_ThumbnailSet.SubThumbWide;
                                    Doc.m_ThumbnailSet.StretchBlt(bitmap, left, top);

                                    if (xTakePicturePoint.Element("X") != null || xTakePicturePoint.Element("Y") != null)
                                    {
                                        xTakePicturePoint.SetElementValue("PosRead", new System.Windows.Point(iCurPosX, iCurPosY));
                                        xTakePicturePoint.SetElementValue("PosSub", new System.Windows.Point(int.Parse((string)xTakePicturePoint.Element("X")) - iCurPosX, int.Parse((string)xTakePicturePoint.Element("Y")) - iCurPosY));
                                    }
                                    iIndex++;
                                    m_Motion.XPath.SetElementValue("Index", iIndex);

                                    if (iIndex == iNumHorizontal * iNumVertical)
                                    {
                                        Doc.m_ThumbnailSet.Save();
                                        Doc.m_ThumbnailSet = null;// 释放资源
                                        m_Motion.MotionType = MotionType.None;
                                        if (m_ThumbnailShowHandler != null)
                                            m_ThumbnailShowHandler();
                                    }
                                }
                                break;

                            case MotionType.Testing:// 测试：建立识别线程，均匀地放入处理器中
                            case MotionType.PCBStartAdjust://PCB起点校正
                                if (m_ShowState.CurrentProductType.XElement == null || xTakePicturePoint == null || xTakePicturePoint.Element("X") == null || xTakePicturePoint.Element("Y") == null)
                                    break;
                                int iX = int.Parse((string)xTakePicturePoint.Element("X"));
                                int iY = int.Parse((string)xTakePicturePoint.Element("Y"));
                                //准备识别线程的参数
                                IdentifyParams identifyParams = new IdentifyParams();

                                if (m_Motion.MotionType == MotionType.Testing)
                                {
                                    bool bPrepare = false;

                                    if (xTakePicturePoint.Element("Mark") != null)
                                    {// "Mark"点识别：获取本次测试的XY方向的偏移
                                        XElement xMark = new XElement("Mark");

                                        //判断
                                        int iPosX = 0;
                                        int iPosY = 0;
                                        bool bMarkOne = false;
                                        if (m_ShowState.CurrentProductType.MarkOneImage != null)
                                        {
                                            iPosX = (int)((m_ShowState.CurrentProductType.MarkOnePos.X + m_ShowState.CurrentProductType.PcbStart.X) * (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch));
                                            iPosY = (int)((m_ShowState.CurrentProductType.MarkOnePos.Y + m_ShowState.CurrentProductType.PcbStart.Y) * (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch));
                                            if (iPosX >= iX - 5 && iPosX <= iX + 5 && iPosY >= iY - 5 && iPosY <= iY + 5)
                                                bMarkOne = true;
                                            else bMarkOne = false;
                                        }
                                        if (bMarkOne)
                                        {
                                            xMark.SetElementValue("Image", m_ShowState.CurrentProductType.MarkOneImage);
                                        }
                                        else
                                        {
                                            iPosX = int.Parse((string)xTakePicturePoint.Element("X"));
                                            iPosY = int.Parse((string)xTakePicturePoint.Element("Y"));
                                            xMark.SetElementValue("Image", m_ShowState.CurrentProductType.MarkTwoImage);
                                        }

                                        identifyParams.IdentifyMode = EnumIdentifyMode.Mark;
                                        identifyParams.MarkXElement = xMark;
                                        identifyParams.CameraBitmap = bitmap;
                                        bPrepare = true;
                                    }
                                    else
                                    {// 测试CELL识别
                                        TestRoute tr = new TestRoute();
                                        tr.XElement = m_ShowState.CurrentProductType.XTestRoute;
                                        if (tr.MultiBoard == false)
                                        {// 单板
                                            if (tr.TestCellList != null && tr.TestCellList.Count() > 0)
                                            {
                                                int iMaxCount = tr.TestCellList.Count();
                                                foreach (XElement xTmp in tr.TestCellList)
                                                {
                                                    if (xTmp.Element("Position") == null)
                                                        continue;

                                                    //找与当前图像位置对应的CELL
                                                    TestCell tCell = new TestCell(new XElement(xTmp));//复制传递
                                                    int iPosX = (int)((tCell.Position.X + m_ShowState.CurrentProductType.PcbStart.X) * (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch));
                                                    int iPosY = (int)((tCell.Position.Y + m_ShowState.CurrentProductType.PcbStart.Y) * (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch));

                                                    //Debug.WriteLine(String.Format("{0},{1} ---- {2},{3}", iX, iY, iPosX, iPosY), DateTime.Now.ToString());
                                                    //if (iPosX == int.Parse((string)xTakePicturePoint.Element("X")) && iPosY == int.Parse((string)xTakePicturePoint.Element("Y")))
                                                    if (iPosX >= iX - 5 && iPosX <= iX + 5 && iPosY >= iY - 5 && iPosY <= iY + 5)
                                                    {//准备识别线程的参数
                                                        identifyParams.Cell = tCell;
                                                        identifyParams.CameraBitmap = bitmap;
                                                        identifyParams.IdentifyMode = EnumIdentifyMode.CELL;
                                                        identifyParams.MaxPicture = iMaxCount;
                                                        bPrepare = true;
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {// 拼板
                                            IEnumerable<XElement> subBoardList = tr.XElement.Elements("SubRectangle");
                                            //获得数目
                                            int iMaxCount = 0;
                                            foreach (XElement xSubBoard in subBoardList)
                                                iMaxCount += xSubBoard.Elements("TestCell").Count();
                                            //判断
                                            foreach (XElement xSubBoard in subBoardList)
                                            {
                                                int iSubMaxCount = xSubBoard.Elements("TestCell").Count();
                                                if (iSubMaxCount <= 0)
                                                    continue;

                                                foreach (XElement xTmp in xSubBoard.Elements("TestCell"))
                                                {
                                                    if (xTmp.Element("Position") == null)
                                                        continue;

                                                    //找与当前图像位置对应的CELL
                                                    TestCell tCell = new TestCell(new XElement(xTmp));//复制传递
                                                    int iPosX = (int)((tCell.Position.X + m_ShowState.CurrentProductType.PcbStart.X) * (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch));
                                                    int iPosY = (int)((tCell.Position.Y + m_ShowState.CurrentProductType.PcbStart.Y) * (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch));
                                                    //Debug.WriteLine(String.Format("{0},{1} ---- {2},{3}", iX, iY, iPosX, iPosY), DateTime.Now.ToString());
                                                    //if (iPosX == int.Parse((string)xTakePicturePoint.Element("X")) && iPosY == int.Parse((string)xTakePicturePoint.Element("Y")))
                                                    if (iPosX >= iX - 5 && iPosX <= iX + 5 && iPosY >= iY - 5 && iPosY <= iY + 5)
                                                    {//准备识别线程的参数
                                                        identifyParams.Cell = tCell;
                                                        identifyParams.CameraBitmap = bitmap;
                                                        identifyParams.IdentifyMode = EnumIdentifyMode.CELL;
                                                        identifyParams.MaxPicture = iMaxCount;
                                                        bPrepare = true;
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    if (bPrepare)
                                    {
                                        m_iCurProcessor++;
                                        m_iCurProcessor = m_iCurProcessor % Environment.ProcessorCount;
                                        // 识别线程不放在第一个CPU核上
                                        if (m_iCurProcessor == 0)
                                            m_iCurProcessor++;

                                        //建立识别线程
                                        ParameterizedThreadStart start = new ParameterizedThreadStart(IdentifyThread);
                                        ThreadEvenly threadEvenly = new ThreadEvenly(start);
                                        threadEvenly.ProcessorAffinity = (int)Math.Pow(2, m_iCurProcessor);
                                        threadEvenly.ManagedThread.Name = "ThreadOnCPU" + m_iCurProcessor + 1;
                                        //threadEvenly.Start();
                                        threadEvenly.Start(identifyParams);
                                    }
                                    else
                                    {
                                        m_Motion.NumErrorCell++;
                                        m_Logger.InfoFormat(String.Format("RX: {0}, ", m_ShowState.CurrentProductType.Name), 3333);
                                    }
                                }
                                else
                                {
                                    XElement xMark = new XElement("Mark");


                                    xMark.SetElementValue("Image", m_ShowState.CurrentProductType.MarkOneImage);
                                    xMark.SetElementValue("PCBNewStartX", (string)xTakePicturePoint.Element("PCBNewStartX"));
                                    xMark.SetElementValue("PCBNewStartY", (string)xTakePicturePoint.Element("PCBNewStartY"));
                                    identifyParams.MarkXElement = xMark;
                                    identifyParams.CameraBitmap = bitmap;

                                    Identify_PCBStartAdjust(identifyParams);
                                    m_Motion.MotionType = MotionType.None;
                                }
                                break;
                        }
                    }
                    catch (Exception ep)
                    {
                        SystemStatusShow(HWUnify.STATUSBAR, ep.Message);
                    }
                }
            }
            else
            {
                Console.WriteLine("Error: {0} {1}", grabResult.ErrorCode, grabResult.ErrorDescription);
            }
        }
        #endregion Basler相机


        #region KSJ相机

        public void CameraParaChanged_KSJ()
        {
            m_KSJDevice.PreviewRestart(false);
            m_KSJDevice.ClearCallBack();

            if (Doc.m_SystemParam.CameraType == CameraType.Basler)
                return;


            //设置CALLBACK
            //if (Doc.m_SystemParam.XKSJCameras.Count() <= 0)
            //    return;
            if (m_KSJDevice.DeviceInfo.Count() <= 0)
                return;

            //m_KSJDevice.bTrigger = Doc.m_SystemParam.TakePicMode == TakePicMode.Stream ? false : true;
            m_KSJDevice.bTrigger = true;

            m_KSJDevice.SetCallBack(m_KSJDevice.DeviceInfo[0].nIndex, CameraCallback_KSJ);
            m_KSJDevice.PreviewRestart(true);
        }


        private void CameraCallback_KSJ(IntPtr pImageData, int width, int height, int nBitCount, IntPtr lpContext)
        {
            CameraImage_Show(pImageData, width, height, nBitCount, HWUnify.CAMERA_IMAGE);
        }

        private void CameraImage_Show(IntPtr pImageData, int width, int height, int nBitCount, int iWindowIndex)
        {
            try
            {

                if (!m_CameraStopWatch.IsRunning || m_CameraStopWatch.ElapsedMilliseconds > 40)
                {
                    int iCurPosX = LTDMC.dmc_get_encoder(m_Motion.CardID, MotionDMC5000.AXIS_X);
                    int iCurPosY = LTDMC.dmc_get_encoder(m_Motion.CardID, MotionDMC5000.AXIS_Y);

                    // 获取当前拍照位置信息，必须放在Doc.m_Motion.TakePicState = true;前面
                    XElement xTakePicturePoint = null;
                    foreach (XElement xTmpPoint in m_Motion.XPath.Elements("Point"))
                    {
                        if (xTmpPoint.Element("IsCurrent") == null || Boolean.Parse((string)xTmpPoint.Element("IsCurrent")) == false)
                            continue;

                        xTakePicturePoint = xTmpPoint;
                        break;

                    }

                    m_CameraStopWatch.Restart();
                    m_Motion.TakePicState = true;


                    //Debug.WriteLine(String.Format("({0},{1})", width, height), "测试通道1");
                    //Bitmap bitmap = BytesToBitmap(pImageData, width, height, nBitCount);

                    System.Drawing.Imaging.PixelFormat bitmaptype = System.Drawing.Imaging.PixelFormat.Format8bppIndexed;
                    int nPixelBytes = nBitCount >> 3;

                    if (3 == nPixelBytes)
                        bitmaptype = System.Drawing.Imaging.PixelFormat.Format24bppRgb;
                    else if (4 == nPixelBytes)
                        bitmaptype = System.Drawing.Imaging.PixelFormat.Format32bppPArgb;
                    else return;

                    byte[] pByteImageData = new byte[height * width * nPixelBytes];
                    Marshal.Copy(pImageData, pByteImageData, 0, height * width * nPixelBytes);
                    Bitmap bitmap = BytesToBitmap(pByteImageData, width, height, nBitCount);

                    //Bitmap bitmap = new Bitmap(width, height, bitmaptype);
                    //// Lock the bits of the bitmap.
                    //System.Drawing.Imaging.BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, bitmap.PixelFormat);
                    //byte[] pByteImageData = new byte[height * width * nPixelBytes];
                    //Marshal.Copy(pImageData, pByteImageData, 0, height * width * nPixelBytes);
                    //Marshal.Copy(pByteImageData, 0, bmpData.Scan0, height * width * nPixelBytes);

                    ////IntPtr ipDestLine;
                    ////int iSrcLine;
                    ////for (int i = 0; i < height; i++)
                    ////{
                    ////    //iSrcLine = (height - i - 1) * width * nPixelBytes;
                    ////    iSrcLine = i * width * nPixelBytes;
                    ////    ipDestLine = bmpData.Scan0 + i * bmpData.Stride;
                    ////    for (int j = 0; j < width; j++)
                    ////        Marshal.Copy(pByteImageData, iSrcLine + (width - j - 1) * nPixelBytes, ipDestLine + j* nPixelBytes, nPixelBytes);
                    ////}

                    //bitmap.UnlockBits(bmpData);

                    // 显示在界面上：bitmap转换成image.source
                    MemoryStream ms = new MemoryStream();
                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                    BitmapImage pBImage = new BitmapImage();
                    pBImage.BeginInit();
                    pBImage.StreamSource = ms;
                    pBImage.EndInit();
                    pBImage.Freeze();//非常重要！！！
                    SystemStatusShow(iWindowIndex, pBImage);

                    switch (m_Motion.MotionType)
                    {
                        case MotionType.FlowMousePic://随动拍照
                        case MotionType.TestRouteEdit://测试路径显示、编辑
                            break;
                        case MotionType.HelicalPitchMap://导程映射
                                                        //保存当前图像
                            String sDstFilename = String.Format("{0}\\HelicalPitchMapDst.bmp", Doc.m_SystemParam.SetupPath);
                            bitmap.Save(sDstFilename);
                            //ImagePersistence.Save(ImageFileFormat.Bmp, sDstFilename, grabResult);
                            //保存在显示的图像
                            String sSrcFilename = String.Format("{0}\\HelicalPitchMapSrc.bmp", Doc.m_SystemParam.SetupPath);
                            SaveCameraImage(sSrcFilename);

                            m_Motion.MotionType = MotionType.None;
                            break;
                        case MotionType.ThumbnailImage://缩略图拍照
                            if (xTakePicturePoint != null)
                            {
                                int iNumHorizontal = int.Parse((string)m_Motion.XPath.Element("NumHorizontal"));
                                int iNumVertical = int.Parse((string)m_Motion.XPath.Element("NumVertical"));
                                int iIndex = int.Parse((string)m_Motion.XPath.Element("Index"));

                                int left = ((iIndex / iNumHorizontal % 2) == 0) ? ((iNumHorizontal - iIndex % iNumHorizontal - 1) * Doc.m_ThumbnailSet.SubThumbWide) : ((iIndex % iNumHorizontal) * Doc.m_ThumbnailSet.SubThumbWide);
                                int top = (iNumVertical - (iIndex / iNumHorizontal) - 1) * Doc.m_ThumbnailSet.SubThumbHeight;
                                //int top = ((iIndex / iNumVertical % 2) == 0) ? ((iNumVertical - iIndex % iNumVertical - 1) * Doc.m_ThumbnailSet.SubThumbHeight) : ((iIndex % iNumVertical) * Doc.m_ThumbnailSet.SubThumbHeight);
                                //int left = (iNumHorizontal - (iIndex / iNumVertical) - 1) * Doc.m_ThumbnailSet.SubThumbWide;
                                Doc.m_ThumbnailSet.StretchBlt(bitmap, left, top);

                                if (xTakePicturePoint.Element("X") != null || xTakePicturePoint.Element("Y") != null)
                                {
                                    xTakePicturePoint.SetElementValue("PosRead", new System.Windows.Point(iCurPosX, iCurPosY));
                                    xTakePicturePoint.SetElementValue("PosSub", new System.Windows.Point(int.Parse((string)xTakePicturePoint.Element("X")) - iCurPosX, int.Parse((string)xTakePicturePoint.Element("Y")) - iCurPosY));
                                }
                                iIndex++;
                                m_Motion.XPath.SetElementValue("Index", iIndex);

                                if (iIndex == iNumHorizontal * iNumVertical)
                                {
                                    //Doc.m_Motion.XPath.Save(Doc.m_SystemParam.SetupPath + "\\MotionData.xml");

                                    Doc.m_ThumbnailSet.Save();
                                    Doc.m_ThumbnailSet = null;// 释放资源
                                    m_Motion.MotionType = MotionType.None;
                                    if (m_ThumbnailShowHandler != null)
                                        m_ThumbnailShowHandler();
                                }
                            }
                            break;

                        case MotionType.Testing:// 测试：建立识别线程，均匀地放入处理器中
                        case MotionType.PCBStartAdjust://PCB起点校正
                            if (m_ShowState.CurrentProductType.XElement == null || xTakePicturePoint == null || xTakePicturePoint.Element("X") == null || xTakePicturePoint.Element("Y") == null)
                                break;

                            //准备识别线程的参数
                            IdentifyParams identifyParams = new IdentifyParams();

                            if (m_Motion.MotionType == MotionType.Testing)
                            {
                                bool bPrepare = false;

                                if (xTakePicturePoint.Element("Mark") != null)
                                {// "Mark"点识别：获取本次测试的XY方向的偏移
                                    XElement xMark = new XElement("Mark");

                                    //判断
                                    int iPosX = 0;
                                    int iPosY = 0;
                                    bool bMarkOne = false;
                                    if (m_ShowState.CurrentProductType.MarkOneImage != null)
                                    {
                                        iPosX = (int)((m_ShowState.CurrentProductType.MarkOnePos.X + m_ShowState.CurrentProductType.PcbStart.X) * (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch));
                                        iPosY = (int)((m_ShowState.CurrentProductType.MarkOnePos.Y + m_ShowState.CurrentProductType.PcbStart.Y) * (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch));
                                        if (iPosX == int.Parse((string)xTakePicturePoint.Element("X")) && iPosY == int.Parse((string)xTakePicturePoint.Element("Y")))
                                            bMarkOne = true;
                                        else bMarkOne = false;
                                    }
                                    if (bMarkOne)
                                    {
                                        xMark.SetElementValue("Image", m_ShowState.CurrentProductType.MarkOneImage);
                                    }
                                    else
                                    {
                                        iPosX = int.Parse((string)xTakePicturePoint.Element("X"));
                                        iPosY = int.Parse((string)xTakePicturePoint.Element("Y"));
                                        xMark.SetElementValue("Image", m_ShowState.CurrentProductType.MarkTwoImage);
                                    }

                                    identifyParams.IdentifyMode = EnumIdentifyMode.Mark;
                                    identifyParams.MarkXElement = xMark;
                                    identifyParams.CameraBitmap = bitmap;
                                    bPrepare = true;
                                }
                                else
                                {// 测试CELL识别
                                    TestRoute tr = new TestRoute();
                                    tr.XElement = m_ShowState.CurrentProductType.XTestRoute;
                                    if (tr.MultiBoard == false)
                                    {// 单板
                                        if (tr.TestCellList != null && tr.TestCellList.Count() > 0)
                                        {
                                            int iMaxCount = tr.TestCellList.Count();
                                            foreach (XElement xTmp in tr.TestCellList)
                                            {
                                                if (xTmp.Element("Position") == null)
                                                    continue;

                                                //找与当前图像位置对应的CELL
                                                TestCell tCell = new TestCell(new XElement(xTmp));//复制传递
                                                int iPosX = (int)((tCell.Position.X + m_ShowState.CurrentProductType.PcbStart.X) * (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch));
                                                int iPosY = (int)((tCell.Position.Y + m_ShowState.CurrentProductType.PcbStart.Y) * (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch));
                                                int iX = int.Parse((string)xTakePicturePoint.Element("X"));
                                                int iY = int.Parse((string)xTakePicturePoint.Element("Y"));
                                                //Debug.WriteLine(String.Format("{0},{1} ---- {2},{3}", iX, iY, iPosX, iPosY), DateTime.Now.ToString());
                                                //if (iPosX == int.Parse((string)xTakePicturePoint.Element("X")) && iPosY == int.Parse((string)xTakePicturePoint.Element("Y")))
                                                if (iPosX >= iX - 1 && iPosX <= iX + 1 && iPosY >= iY - 1 && iPosY <= iY + 1)
                                                {//准备识别线程的参数
                                                    identifyParams.Cell = tCell;
                                                    identifyParams.CameraBitmap = bitmap;
                                                    identifyParams.IdentifyMode = EnumIdentifyMode.CELL;
                                                    identifyParams.MaxPicture = iMaxCount;
                                                    bPrepare = true;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {// 拼板
                                        IEnumerable<XElement> subBoardList = tr.XElement.Elements("SubRectangle");
                                        //获得数目
                                        int iMaxCount = 0;
                                        foreach (XElement xSubBoard in subBoardList)
                                            iMaxCount += xSubBoard.Elements("TestCell").Count();
                                        //判断
                                        foreach (XElement xSubBoard in subBoardList)
                                        {
                                            int iSubMaxCount = xSubBoard.Elements("TestCell").Count();
                                            if (iSubMaxCount <= 0)
                                                continue;

                                            foreach (XElement xTmp in xSubBoard.Elements("TestCell"))
                                            {
                                                if (xTmp.Element("Position") == null)
                                                    continue;

                                                //找与当前图像位置对应的CELL
                                                TestCell tCell = new TestCell(new XElement(xTmp));//复制传递
                                                int iPosX = (int)((tCell.Position.X + m_ShowState.CurrentProductType.PcbStart.X) * (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch));
                                                int iPosY = (int)((tCell.Position.Y + m_ShowState.CurrentProductType.PcbStart.Y) * (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch));
                                                int iX = int.Parse((string)xTakePicturePoint.Element("X"));
                                                int iY = int.Parse((string)xTakePicturePoint.Element("Y"));
                                                //Debug.WriteLine(String.Format("{0},{1} ---- {2},{3}", iX, iY, iPosX, iPosY), DateTime.Now.ToString());
                                                //if (iPosX == int.Parse((string)xTakePicturePoint.Element("X")) && iPosY == int.Parse((string)xTakePicturePoint.Element("Y")))
                                                if (iPosX >= iX - 1 && iPosX <= iX + 1 && iPosY >= iY - 1 && iPosY <= iY + 1)
                                                {//准备识别线程的参数
                                                    identifyParams.Cell = tCell;
                                                    identifyParams.CameraBitmap = bitmap;
                                                    identifyParams.IdentifyMode = EnumIdentifyMode.CELL;
                                                    identifyParams.MaxPicture = iMaxCount;
                                                    bPrepare = true;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                                if (bPrepare)
                                {
                                    m_iCurProcessor++;
                                    m_iCurProcessor = m_iCurProcessor % Environment.ProcessorCount;
                                    // 识别线程不放在第一个CPU核上
                                    if (m_iCurProcessor == 0)
                                        m_iCurProcessor++;

                                    //建立识别线程
                                    ParameterizedThreadStart start = new ParameterizedThreadStart(IdentifyThread);
                                    ThreadEvenly threadEvenly = new ThreadEvenly(start);
                                    threadEvenly.ProcessorAffinity = (int)Math.Pow(2, m_iCurProcessor);
                                    threadEvenly.ManagedThread.Name = "ThreadOnCPU" + m_iCurProcessor + 1;
                                    //threadEvenly.Start();
                                    threadEvenly.Start(identifyParams);
                                }
                                else
                                {
                                    m_Motion.NumErrorCell++;
                                    m_Logger.InfoFormat(String.Format("RX: {0}, ", m_ShowState.CurrentProductType.Name), 3333);
                                }
                            }
                            else
                            {
                                XElement xMark = new XElement("Mark");
                                xMark.SetElementValue("Image", m_ShowState.CurrentProductType.MarkOneImage);
                                xMark.SetElementValue("PCBNewStartX", (string)xTakePicturePoint.Element("PCBNewStartX"));
                                xMark.SetElementValue("PCBNewStartY", (string)xTakePicturePoint.Element("PCBNewStartY"));
                                identifyParams.MarkXElement = xMark;
                                identifyParams.CameraBitmap = bitmap;

                                Identify_PCBStartAdjust(identifyParams);
                                m_Motion.MotionType = MotionType.None;
                            }
                            break;
                    }


                }
            }
            catch
            { }
        }

        public static unsafe System.Drawing.Bitmap BytesToBitmap(byte[] bytes, int width, int height, int nBitCount)
        {
            try
            {
                System.Drawing.Imaging.PixelFormat bitmaptype = System.Drawing.Imaging.PixelFormat.Format8bppIndexed;
                int nPixelBytes = nBitCount >> 3;

                if (3 == nPixelBytes)
                {
                    bitmaptype = System.Drawing.Imaging.PixelFormat.Format24bppRgb;
                }
                else if (4 == nPixelBytes)
                {
                    bitmaptype = System.Drawing.Imaging.PixelFormat.Format32bppPArgb;
                }

                Bitmap bitmap = new Bitmap(width, height, bitmaptype);
                //获取图像的BitmapData对像 
                BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, bitmaptype);

                byte* ptr = (byte*)(bmpData.Scan0);
                int nWidthBytes = width * nPixelBytes;
                int nSrcOffset = 0;
                int nDesOffset = 0;
                for (int i = 0; i < height; i++)
                {
                    nSrcOffset = (height - i - 1) * nWidthBytes;
                    nDesOffset = 0;
                    for (int j = 0; j < width; j++)
                    {
                        for (int k = 0; k < nPixelBytes; k++)
                        {
                            *(ptr + nDesOffset + k) = bytes[nSrcOffset + k];
                        }

                        nDesOffset += nPixelBytes;
                        nSrcOffset += nPixelBytes;
                    }

                    ptr = ptr + nWidthBytes;
                }

                bitmap.UnlockBits(bmpData);  // 解锁内存区域

                if (1 == nPixelBytes)
                {
                    // 修改生成位图的索引表，从伪彩修改为灰度
                    ColorPalette palette;
                    // 获取一个Format8bppIndexed格式图像的Palette对象
                    using (Bitmap bmp = new Bitmap(1, 1, System.Drawing.Imaging.PixelFormat.Format8bppIndexed))
                    {
                        palette = bmp.Palette;
                    }
                    for (int i = 0; i < 256; i++)
                    {
                        palette.Entries[i] = System.Drawing.Color.FromArgb(i, i, i);
                    }
                    // 修改生成位图的索引表
                    bitmap.Palette = palette;
                }

                return bitmap;
            }
            catch (ArgumentNullException ex)
            {
                throw ex;
            }
            catch (ArgumentException ex)
            {
                throw ex;
            }
        }


        #endregion KSJ相机

        #endregion 相机

        #region 运动卡

        private void Thread_EmgShow()
        {
            if (!Application.Current.Dispatcher.CheckAccess()) // CheckAccess returns true if you're on the dispatcher thread
            {
                Application.Current.Dispatcher.BeginInvoke(new DelegateNoneParameter(Thread_EmgShow));
                return;
            }

            EmgDetectDialog emgDetectDlg = new EmgDetectDialog();
            emgDetectDlg.ShowDialog();
        }

        #endregion 运动卡

        #region 主测试线程

        /// <summary>
        /// 识别线程：按键或屏幕触发测试线程，测试线程根据拍照路径设置控制运动系统运动到设置位置，触发相机拍照，拍照结束读取照片送到不同的处理器去识别分析，同时运动到下一路径位置去拍照
        /// </summary>
        public void StartTest()
        {
            if (!Application.Current.Dispatcher.CheckAccess()) // CheckAccess returns true if you're on the dispatcher thread
            {
                Application.Current.Dispatcher.BeginInvoke(new DelegateNoneParameter(StartTest));
                return;
            }

            if (m_Motion.MotionEnable() == false)
                return;

            if (m_Motion.MotionType != MotionType.None)
            {
                MessageBox.Show("测试没结束，不允许测试！",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            if (m_ShowState.CurrentProductType == null || m_ShowState.CurrentProductType.XTestRoute == null)
            {
                MessageBox.Show("产品型号或测试路径设置为空，不允许测试！",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            m_Motion.NumErrorCell = 0;
            TestRoute tr = new TestRoute();
            tr.XElement = m_ShowState.CurrentProductType.XTestRoute;
            if (tr.MultiBoard == false)
            {// 单板
                if (tr.TestCellList == null || tr.TestCellList.Count() <= 0)
                {
                    MessageBox.Show("测试路径个数设置为空，不允许测试！",
                                    Doc.MESSAGE_SOFTNAME,
                                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }
            }
            else
            {// 拼板
                IEnumerable<XElement> subBoardList = tr.XElement.Elements("SubRectangle");
                bool bFlag = false;
                if (subBoardList == null || subBoardList.Count() <= 0)
                    bFlag = true;
                if (bFlag == false)
                {
                    foreach (XElement route in subBoardList)
                    {
                        if (route.Elements("TestCell").Count() <= 0)
                            bFlag = true;
                        break;
                    }
                }
                if (bFlag)
                {
                    MessageBox.Show("测试路径个数设置为空，不允许测试！",
                                    Doc.MESSAGE_SOFTNAME,
                                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }
            }

            if (m_ShowState.CurrentProductType.BarcodeMode == EnumBarcodeMode.Scanner)
            {
                if (String.IsNullOrEmpty(App.m_ShowState.BarcodeTest))
                {
                    MessageBox.Show("二维码为空，不允许测试！",
                                    Doc.MESSAGE_SOFTNAME,
                                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }
            }

            XElement xRoute = new XElement("Path");
            XElement xPoint = null;
            int iPosX = 0;
            int iPosY = 0;

            //增加MARK点
            if (m_ShowState.CurrentProductType.MarkOneImage != null)
            {
                iPosX = (int)((m_ShowState.CurrentProductType.MarkOnePos.X + m_ShowState.CurrentProductType.PcbStart.X) * Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch);
                iPosY = (int)((m_ShowState.CurrentProductType.MarkOnePos.Y + m_ShowState.CurrentProductType.PcbStart.Y) * Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch);
                xPoint = new XElement("Point",
                                new XElement("Mark", true),        //Mark点位置
                                new XElement("Take", true),        //到位是否触发拍照
                                new XElement("X", iPosX),          //X方向行程
                                new XElement("Y", iPosY)           //Y方向行程
                                );
                xRoute.Add(xPoint);
            }
            if (m_ShowState.CurrentProductType.MarkTwoImage != null)
            {
                iPosX = (int)((m_ShowState.CurrentProductType.MarkTwoPos.X + m_ShowState.CurrentProductType.PcbStart.X) * Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch);
                iPosY = (int)((m_ShowState.CurrentProductType.MarkTwoPos.Y + m_ShowState.CurrentProductType.PcbStart.Y) * Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch);
                xPoint = new XElement("Point",
                                new XElement("Mark", true),          //Mark点位置
                                new XElement("Take", true),         //到位是否触发拍照
                                new XElement("X", iPosX),          //X方向行程
                                new XElement("Y", iPosY)          //Y方向行程
                                );
                xRoute.Add(xPoint);
            }
            //增加测试路径
            if (tr.MultiBoard == false)
            {// 单板
                foreach (XElement xTmp in tr.TestCellList)
                {
                    if (xTmp.Element("Position") == null)
                        continue;
                    TestCell tCell = new TestCell(xTmp);

                    iPosX = (int)((tCell.Position.X + m_ShowState.CurrentProductType.PcbStart.X) * Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch);
                    iPosY = (int)((tCell.Position.Y + m_ShowState.CurrentProductType.PcbStart.Y) * Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch);
                    xPoint = new XElement("Point",
                                    new XElement("Take", true),          //到位是否触发拍照
                                    new XElement("X", iPosX),          //X方向行程
                                    new XElement("Y", iPosY)          //Y方向行程
                                    );
                    xRoute.Add(xPoint);
                }
            }
            else
            {// 拼板
                IEnumerable<XElement> subBoardList = tr.XElement.Elements("SubRectangle");
                foreach (XElement route in subBoardList)
                {
                    foreach (XElement xTestCell in route.Elements("TestCell"))
                    {
                        if (xTestCell.Element("Position") == null)
                            continue;
                        TestCell tCell = new TestCell(xTestCell);

                        iPosX = (int)((tCell.Position.X + m_ShowState.CurrentProductType.PcbStart.X) * Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch);
                        iPosY = (int)((tCell.Position.Y + m_ShowState.CurrentProductType.PcbStart.Y) * Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch);
                        xPoint = new XElement("Point",
                                        new XElement("Take", true),          //到位是否触发拍照
                                        new XElement("X", iPosX),          //X方向行程
                                        new XElement("Y", iPosY)          //Y方向行程
                                        );
                        xRoute.Add(xPoint);
                    }
                }
            }
            // 回到放板位置
            xPoint = new XElement("Point",
                            new XElement("Take", false),          //到位是否触发拍照
                            new XElement("X", 0),          //X方向行程
                            new XElement("Y", (int)((Doc.m_SystemParam.AxisMaxRun_Y - 80) * Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch))          //Y方向行程
                            );
            xRoute.Add(xPoint);

            if (m_Motion.MotionEnable() == false)
                return;

            //黄灯点亮，红灯、绿灯关闭，蜂鸣器关闭
            LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_YELLOWLIGHT, MotionDMC5000.LEVEL_LOW);
            LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_REDLIGHT, MotionDMC5000.LEVEL_HIGH);
            LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_GREENLIGHT, MotionDMC5000.LEVEL_HIGH);
            LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_BUZZER, MotionDMC5000.LEVEL_HIGH);
            //if (m_TestResultShowHandler != null)
            //    m_TestResultShowHandler(null);

            m_Motion.XPath = xRoute;
            m_Motion.MotionType = MotionType.Testing;
            m_Motion.MotionThread_Start();
        }

        /// <summary>
        /// 识别线程：该线程均匀分布运行在CPU的不同处理器中，识别分析相机照片中的元件
        /// </summary>
        private void IdentifyThread(object inObject)
        {
            if (inObject == null || (inObject is IdentifyParams) == false)
                return;

            IdentifyParams idParams = inObject as IdentifyParams;
            if (idParams.CameraBitmap == null)
                return;

            try
            {
                if (idParams.IdentifyMode == EnumIdentifyMode.Mark)
                {// Mark点识别
                    if (idParams.MarkXElement == null)
                        return;

                    // 判断识别结束时保存
                    if (AOI_MarkIdentify(idParams.CameraBitmap, idParams.MarkXElement))
                        m_Motion.TestRecord.Add(idParams.MarkXElement);
                }
                else if (idParams.IdentifyMode == EnumIdentifyMode.CELL)
                {// Cell识别
                    if (idParams.Cell == null || idParams.Cell.XElement == null)
                        return;

                    //Debug.WriteLine("启动识别线程-----------------------！", DateTime.Now.ToString());
                    if (AOI_CellIdentify(idParams.CameraBitmap, idParams.Cell.XElement))
                    {// 当前测试结果加入到列表中
                        m_Motion.TestRecord.Add(idParams.CellXElement);
                        if (m_Motion.TestRecord.Elements("TestCell").Count() >= idParams.MaxPicture - m_Motion.NumErrorCell)
                        {// 最后一次CELL时，保存这次测试的结果

                            string sIdentifyPath = String.Format("{0}\\{1}_", Doc.m_SystemParam.DataPath, m_ShowState.CurrentProductType.Name);
                            Doc.m_DBTestResult = new DBTestResult();
                            Doc.m_DBTestResult.TestTime = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                            if (m_ShowState.CurrentProductType.BarcodeMode == EnumBarcodeMode.None)
                            {
                                Doc.m_DBTestResult.Barcode = Doc.m_DBTestResult.TestTime;//无二维码，测试结果中的二维码用测试时间填充
                                Doc.m_DBTestResult.TestFilename = String.Format("{0}.xml", Doc.m_DBTestResult.TestTime);
                            }
                            else if (m_ShowState.CurrentProductType.BarcodeMode == EnumBarcodeMode.Scanner)
                            {
                                Doc.m_DBTestResult.Barcode = m_ShowState.BarcodeTest;
                                Doc.m_DBTestResult.TestFilename = String.Format("{0}.xml", Doc.m_DBTestResult.Barcode);

                                //删除缓冲区中的第一个二维码
                                if (m_ListBarcode.Count > 0)
                                    m_ListBarcode.RemoveAt(0);
                            }

                            string sIdentifyFilename = sIdentifyPath + Doc.m_DBTestResult.TestFilename;

                            m_Motion.TestRecord.SetElementValue("TestTime", Doc.m_DBTestResult.TestTime);
                            m_Motion.TestRecord.SetElementValue("Review", false);

                            m_Motion.TestRecord.Save(sIdentifyFilename);
                            m_ListTestResult.Add(m_Motion.TestRecord);
                            if (m_ListTestResult.Count == 1)
                            {

                                if (m_TestResultShowHandler != null)
                                {
                                    m_TestResultShowHandler(null);//清除上一个显示结果

                                    m_TestResultShowHandler(m_Motion.TestRecord);
                                }
                            }
                            if (m_ListTestResult.Count > 2)
                            {
                                m_ListTestResult.RemoveAt(0);
                                //XElement result = null;
                                //if (File.Exists(m_ListTestResult[0]))
                                //{
                                //    result = XElement.Load(m_ListTestResult[0]);
                                //}

                                if (m_TestResultShowHandler != null)
                                {
                                    m_TestResultShowHandler(null);//清除上一个显示结果
                                    m_TestResultShowHandler(m_ListTestResult[0]);
                                }
                            }

                            //if (m_TestResultShowHandler != null)
                            //{
                            //    m_TestResultShowHandler(null);//清除上一个显示结果
                            //    m_TestResultShowHandler(m_Motion.TestRecord);
                            //}
                            if (m_TestResultLightShowHandler != null)
                                m_TestResultLightShowHandler(m_Motion.TestRecord);
                            m_Motion.MotionType = MotionType.None;
                            //string sIdentifyPath = String.Format("{0}\\{1}_", Doc.m_SystemParam.DataPath, m_ShowState.CurrentProductType.Name);
                            //string sIdentifyFilename;
                            //if (m_ShowState.CurrentProductType.BarcodeMode == EnumBarcodeMode.None)
                            //    sIdentifyFilename = sIdentifyPath + String.Format("{0}.xml", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                            //else// if (WeldContent.m_TestProductType.BarcodeMode == EnumBarcodeMode.Scanner)
                            //    sIdentifyFilename = sIdentifyPath + String.Format("{0}.xml", m_ShowState.BarcodeTest);

                            //m_Motion.TestRecord.SetElementValue("TestTime", DateTime.Now.ToString("yyyyMMdd HHmmss"));
                            //m_Motion.TestRecord.Save(sIdentifyFilename);
                            //if (m_TestResultShowHandler != null)
                            //    m_TestResultShowHandler(m_Motion.TestRecord);
                            //if (m_TestResultLightShowHandler != null)
                            //    m_TestResultLightShowHandler(m_Motion.TestRecord);
                            //m_Motion.MotionType = MotionType.None;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message, "识别线程异常退出！");
            }
            finally
            {

            }
        }


        //private XElement xMarkOffSet = new XElement("OffSet");
        /// <summary>
        /////// Mark点识别
        /// </summary>
        /// <param name="bmpImg">Mark图像</param>
        /// <param name="xMark">识别参数</param>
        /// <returns>识别正常退出返回true</returns>
        private bool AOI_MarkIdentify(System.Drawing.Bitmap bmpImg, XElement xMark)
        {
            if (bmpImg == null || xMark == null || xMark.Element("Image") == null)
                return false;

            try
            {
                MarkMatch cvMarkMatch = new MarkMatch();
                cvMarkMatch.MatMarkCell = bmpImg;
                cvMarkMatch.Threshold = Doc.m_SystemParam.MarkThreshold;
                cvMarkMatch.MatchingType = Emgu.CV.CvEnum.TemplateMatchingType.SqdiffNormed;
                cvMarkMatch.TestOffset = (int)Doc.m_SystemParam.MarkOffset;
                cvMarkMatch.MatTemplate = Doc.Convert_String2Bitmap((string)xMark.Element("Image"));
                cvMarkMatch.Mark_Matching();
                if (cvMarkMatch.MatchResult)
                {
                    xMark.SetElementValue("OffsetX", cvMarkMatch.MarkOffsetX);
                    xMark.SetElementValue("OffsetY", cvMarkMatch.MarkOffsetY);
                    //m_Logger.InfoFormat(String.Format("RX: {0}, ", cvMarkMatch.MarkOffsetX), 3333);
                    //m_Logger.InfoFormat(String.Format("RX: {0}, ", cvMarkMatch.MarkOffsetY), 3333);
                    return true;
                }
                else return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// PATH的CELL中各元件进行识别
        /// </summary>
        /// <param name="bmpImg">CELL图像</param>
        /// <param name="xCell">识别参数</param>
        /// <returns>识别正常退出返回true</returns>
        private bool AOI_CellIdentify(System.Drawing.Bitmap bmpImg, XElement xCell)
        {
            if (bmpImg == null || xCell == null)
                return false;

            try
            {
                MetaMatch cvMetaMatch = new MetaMatch();
                cvMetaMatch.MatCell = bmpImg;
                cvMetaMatch.TestOffset = (int)Doc.m_SystemParam.ModuleOffset;

                //Debug.WriteLine("启动识别线程---------adfasf--------------！", DateTime.Now.ToString());

                try
                {
                    //获得MARK点偏移
                    if (m_Motion.TestRecord != null && m_Motion.TestRecord.Elements("Mark").Count() > 0)
                    {
                        bool bFlag = false;
                        foreach (XElement xMark in m_Motion.TestRecord.Elements("Mark"))
                        {
                            if (bFlag == false)
                            {
                                cvMetaMatch.MarkOffsetX = int.Parse((string)xMark.Element("OffsetX"));
                                cvMetaMatch.MarkOffsetY = int.Parse((string)xMark.Element("OffsetY"));
                                bFlag = true;
                            }
                            else
                            {
                                cvMetaMatch.MarkOffsetX = (cvMetaMatch.MarkOffsetX + int.Parse((string)xMark.Element("OffsetX"))) / 2;
                                cvMetaMatch.MarkOffsetY = (cvMetaMatch.MarkOffsetY + int.Parse((string)xMark.Element("OffsetY"))) / 2;
                            }
                        }
                    }
                    else
                    {
                        cvMetaMatch.MarkOffsetX = 0;
                        cvMetaMatch.MarkOffsetY = 0;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
                foreach (XElement xMetaTmp in xCell.Elements("TestMeta"))
                {//遍历CELL上的测试元件
                    TestMeta tmpMeta = new TestMeta(xMetaTmp);
                    //tmpMeta.MetaXElement = xMetaTmp;

                    //该元件的元件图像、焊盘图像都不识别检测
                    if (tmpMeta.Test == false)
                        continue;

                    cvMetaMatch.MetaRegionWidth = tmpMeta.MetaWidth;
                    cvMetaMatch.MetaRegionHeight = tmpMeta.MetaHeight;
                    cvMetaMatch.Rotation = tmpMeta.Rotation;
                    cvMetaMatch.Threshold = tmpMeta.Threshold;

                    //偏移阈值由mm换算为像素数
                    cvMetaMatch.OffsetThreshold = (int)(Doc.m_SystemParam.MetaOffset * Doc.m_SystemParam.HelicalPitchMap.X / Doc.m_SystemParam.HelicalPitch);

                    //模板（此时模板是不含焊盘的元件图像）中心此时相对元件中心的偏移为0
                    cvMetaMatch.MetaCenterX = (int)m_ShowState.CameraImageWidth - (int)tmpMeta.Position.X;
                    cvMetaMatch.MetaCenterY = (int)m_ShowState.CameraImageHeight - (int)tmpMeta.Position.Y;
                    cvMetaMatch.TmplCenterX = 0;
                    cvMetaMatch.TmplCenterY = 0;


                    #region 手动画框焊点检测

                    if (xMetaTmp.Element("IsManual") != null && (bool)xMetaTmp.Element("IsManual") == true)
                    {//
                        string strBmp = (string)xMetaTmp.Element("MetaImage").Element("Bitmap");
                        if (String.IsNullOrEmpty(strBmp) == false)
                        {
                            cvMetaMatch.MatTemplate = Doc.Convert_String2Bitmap(strBmp);
                        }
                        cvMetaMatch.Threshold = 0;//阈值为0，先找出最相似的图像


                        if (cvMetaMatch.Meta_Matching())
                        {//模板匹配
                            if (cvMetaMatch.TemplateResult != null)
                                xMetaTmp.SetElementValue("BitmapResult", Doc.Convert_Bitmap2String(cvMetaMatch.TemplateResult));
                            bool bBlack = false;
                            cvMetaMatch.MatTemplate = cvMetaMatch.TemplateResult;
                            cvMetaMatch.Threshold = Doc.m_SystemParam.BlackThreshold;

                            cvMetaMatch.ColorCheck(0, 0, 0);//检测黑色，大于阈值视为有锡洞
                            bBlack = cvMetaMatch.MatchResult;



                            if (tmpMeta.Polarity == true)
                            {//有极性，视为多焊点
                                #region 多个焊点的检测框

                                if (cvMetaMatch.MatchValue > 100 - tmpMeta.Threshold || bBlack)
                                    xMetaTmp.SetElementValue("MetaResult", false);
                                else
                                    xMetaTmp.SetElementValue("MetaResult", true);

                                #endregion 多个焊点的检测框
                            }
                            else
                            {
                                #region 单个焊点的检测框
                                //bool bBlue = false;
                                //bool bBlack = false;
                                ////cvMetaMatch.MatTemplate = cvMetaMatch.TemplateResult;

                                //cvMetaMatch.Threshold = Doc.m_SystemParam.BlueThreshold;
                                ////检测颜色：是否少锡和锡洞

                                //if (cvMetaMatch.ColorCheck(255, 80, 30))//检测蓝色，少于阈值视为少锡
                                //    bBlue = cvMetaMatch.MatchResult;

                                //cvMetaMatch.Threshold = Doc.m_SystemParam.BlackThreshold;

                                //cvMetaMatch.ColorCheck(0, 0, 0);//检测黑色，大于阈值视为有锡洞
                                //bBlack = cvMetaMatch.MatchResult;

                                //if (bBlue == true && bBlack == false)
                                //{
                                //    xMetaTmp.SetElementValue("MetaResult", true);
                                //}
                                //else
                                //{
                                //xMetaTmp.SetElementValue("MetaResult", false);
                                //}
                                xMetaTmp.SetElementValue("MetaResult", !bBlack);
                                #endregion 单个个焊点的检测框
                            }
                        }
                        continue;
                    }




                    #endregion 手动画框焊点检测
                    double dResult = 10000;
                    Bitmap bmpResult = null;
                    bool bResult = false;
                    XElement SelectMetaImage = null;
                    foreach (XElement xMetaImage in tmpMeta.MetaImageList)
                    {

                        if (SelectMetaImage == null)
                        {
                            SelectMetaImage = xMetaImage;
                        }
                        if (xMetaImage.Element("Bitmap") == null)
                            continue;

                        string strBmp = (string)xMetaImage.Element("Bitmap");
                        if (String.IsNullOrEmpty(strBmp) == false)
                        {
                            cvMetaMatch.MatTemplate = Doc.Convert_String2Bitmap(strBmp);
                        }
                        if (cvMetaMatch.Meta_Matching())
                        {
                            double dresult = cvMetaMatch.MatchValue;
                            Bitmap bmp = cvMetaMatch.TemplateResult;
                            bool bresult = cvMetaMatch.MatchResult;
                            if (tmpMeta.Polarity == false)
                            {//无极性不匹配：可能是元件字符的影响，旋转180°后重新测试匹配情况
                                cvMetaMatch.Rotation = tmpMeta.Rotation + 180;
                                cvMetaMatch.Meta_Matching();

                                if (cvMetaMatch.MatchValue < dresult)
                                {
                                    dresult = cvMetaMatch.MatchValue;
                                    bmp = cvMetaMatch.TemplateResult;
                                    bresult = cvMetaMatch.MatchResult;
                                }
                            }
                            if (dresult < dResult)
                            {
                                dResult = dresult;
                                bmpResult = bmp;
                                bResult = bresult;
                                SelectMetaImage = xMetaImage;
                                xMetaTmp.SetElementValue("MetaResult", bResult);
                                if (cvMetaMatch.TemplateResult != null)
                                    xMetaTmp.SetElementValue("BitmapResult", Doc.Convert_Bitmap2String(bmpResult));
                            }
                        }
                    }
                    foreach (XElement xMetaImage in tmpMeta.MetaImageList)
                    {
                        xMetaImage.Remove();
                    }
                    tmpMeta.MetaXElement.Add(SelectMetaImage);
                    if (SelectMetaImage.Element("CheckItemList") != null && bResult == true)
                    {// 识别焊盘                         
                        cvMetaMatch.MatPadRegion = bmpResult;//识别到的元件图像作为焊盘识别的源图
                        foreach (XElement xPadTmp in SelectMetaImage.Element("CheckItemList").Elements("CheckItem"))
                        {
                            if (xPadTmp.Element("Position") == null || xPadTmp.Element("Bitmap") == null)
                                continue;

                            System.Windows.Point ptPadPosition = System.Windows.Point.Parse((string)xPadTmp.Element("Position")); //焊盘图像的位置，相对本元件图像中心的位置，单位是“像素”；旋转后位置会改变！！！！
                            cvMetaMatch.TmplCenterX = (int)ptPadPosition.X;
                            cvMetaMatch.TmplCenterY = (int)ptPadPosition.Y;
                            cvMetaMatch.MatTemplate = Doc.Convert_String2Bitmap((string)xPadTmp.Element("Bitmap"));


                            if (xPadTmp.Element("Name") != null)
                            {
                                if ((string)xPadTmp.Element("Name") == "CheckItem0")
                                {//元件本体
                                    bool bPadMatchResult = false;
                                    bool bPadOCRResult = false;
                                    cvMetaMatch.Threshold = tmpMeta.PadThreshold;
                                    cvMetaMatch.PadRotation = 0;
                                    if (cvMetaMatch.Pad_Matching())
                                    {
                                        double dPadResult = cvMetaMatch.PadMatchValue;
                                        bPadMatchResult = cvMetaMatch.MatchResult;
                                        if (tmpMeta.Polarity == false)
                                        {//无极性，旋转180°
                                            cvMetaMatch.PadRotation = 180;
                                            cvMetaMatch.Pad_Matching();

                                            if (cvMetaMatch.MatchValue < dPadResult)
                                            {
                                                bPadMatchResult = cvMetaMatch.MatchResult;
                                            }
                                        }
                                        //xPadTmp.SetElementValue("CheckResult", bPadResult);
                                        if (xPadTmp.Element("OCR") != null)
                                        {
                                            string ocr = (string)xPadTmp.Element("OCR");
                                            if (cvMetaMatch.OCRMatch(ocr))
                                            {
                                                bPadOCRResult = cvMetaMatch.OCRResult;
                                            }

                                        }

                                    }
                                    if (bPadOCRResult)
                                    {
                                        xPadTmp.SetElementValue("CheckResult", bPadOCRResult);
                                    }
                                    else
                                    {
                                        xPadTmp.SetElementValue("CheckResult", bPadMatchResult);
                                    }



                                }
                                else
                                {//元件焊盘
                                    if (xMetaTmp.Element("TypeName") != null)
                                    {
                                        if ((string)xMetaTmp.Element("TypeName") == EnumMouseMode.Diode.ToString())
                                        {//电阻类元件焊盘用颜色阈值算法
                                            cvMetaMatch.Threshold = Doc.m_SystemParam.RedThreshold;
                                            if (cvMetaMatch.PadCheck())
                                                xPadTmp.SetElementValue("CheckResult", cvMetaMatch.MatchResult);
                                        }
                                        else
                                        {//芯片类元件焊盘用模板匹配算法
                                            cvMetaMatch.PadRotation = 0;
                                            cvMetaMatch.Threshold = tmpMeta.PadThreshold;
                                            if (cvMetaMatch.Pad_Matching())
                                                xPadTmp.SetElementValue("CheckResult", cvMetaMatch.MatchResult);
                                        }
                                    }
                                }
                            }

                        }

                    }
                    try
                    {
                        //在此删除正常的元件图像
                        bool bError = false;
                        if (xMetaTmp.Element("MetaResult") == null)
                            continue;
                        if (bool.Parse((string)xMetaTmp.Element("MetaResult")) == false)
                            bError = true;

                        if (xMetaTmp.Element("MetaImage") != null && xMetaTmp.Element("MetaImage").Element("CheckItemList") != null)
                        {
                            foreach (XElement xPad in xMetaTmp.Element("MetaImage").Element("CheckItemList").Elements("CheckItem"))
                            {
                                if (xPad.Element("CheckResult") == null)
                                    continue;
                                if (bool.Parse((string)xPad.Element("CheckResult")) == false)
                                {
                                    bError = true;
                                    break;
                                }
                            }
                        }
                        if (bError == false)
                        {
                            if (xMetaTmp.Element("MetaImage") != null)
                                xMetaTmp.Element("MetaImage").Remove();
                            if (xMetaTmp.Element("BitmapResult") != null)
                                xMetaTmp.Element("BitmapResult").Remove();
                        }
                        else
                        {
                            xMetaTmp.SetElementValue("MetaResult", false);
                            if (xMetaTmp.Element("MetaImage") != null && xMetaTmp.Element("MetaImage").Element("CheckItemList") != null)
                                xMetaTmp.Element("MetaImage").Element("CheckItemList").Remove();
                        }

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                        return false;
                    }



                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
            finally
            {//删除Cell的图像
                if (xCell.Element("Picture") != null)
                    xCell.Element("Picture").Remove();
            }

            return true;
        }

        private void Identify_PCBStartAdjust(object inObject)
        {
            if (inObject == null || (inObject is IdentifyParams) == false)
                return;

            IdentifyParams idParams = inObject as IdentifyParams;
            if (idParams.CameraBitmap == null)
                return;

            try
            {
                if (idParams.MarkXElement == null)
                    return;

                // 判断识别结束时保存
                if (AOI_MarkIdentify(idParams.CameraBitmap, idParams.MarkXElement))
                {//公式计算：PCB新的起点位置 = 新选择的PCB起点位置 + MARK点偏移；MARK点偏移的是像素，转换为MM
                    double dPosX = int.Parse((string)idParams.MarkXElement.Element("OffsetX")) * Doc.m_SystemParam.HelicalPitch / Doc.m_SystemParam.HelicalPitchMap.X;
                    double dPosY = int.Parse((string)idParams.MarkXElement.Element("OffsetY")) * Doc.m_SystemParam.HelicalPitch / Doc.m_SystemParam.HelicalPitchMap.Y;
                    dPosX += double.Parse((string)idParams.MarkXElement.Element("PCBNewStartX"));
                    dPosY += double.Parse((string)idParams.MarkXElement.Element("PCBNewStartY"));
                    System.Windows.Point positionPCBNewStart = new System.Windows.Point(dPosX, dPosY);
                    m_ShowState.CurrentProductType.PcbStart = positionPCBNewStart;
                }
                else
                {
                    MessageBox.Show("MARK识别失败，没有成功进行PCB起点校准！",
                                    Doc.MESSAGE_SOFTNAME,
                                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message, "识别线程异常退出！");
            }
        }

        public void SaveCameraImage(String sFilename)
        {
            if (!Dispatcher.CurrentDispatcher.CheckAccess()) // CheckAccess returns true if you're on the dispatcher thread
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(new DelegateStringParameter(SaveCameraImage), sFilename);
                return;
            }

            if (m_ShowState.CameraImage == null || String.IsNullOrEmpty(sFilename))
                return;

            BmpBitmapEncoder encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(m_ShowState.CameraImage));
            FileStream fileStream = new FileStream(sFilename, FileMode.Create, FileAccess.ReadWrite);
            encoder.Save(fileStream);
            fileStream.Close();
        }

        /// <summary>
        /// 系统状态显示
        /// </summary>
        /// <param name="textSystemState">状态显示窗口</param>
        public void SystemStatusShow(Object oRes, Object sState)
        {
            // 线程的Dispatcher是否与应用程序的显示Dispatcher一致
            //if (Dispatcher.CurrentDispatcher != Application.Current.Dispatcher)
            //{
            //    Application.Current.Dispatcher.Invoke(new EventHandler<Object>(SystemStatusShow), oRes, sState);
            //    //m_ShowState.Dispatcher.BeginInvoke(new EventHandler<Object>(SystemStatusShow), oRes, sState);
            //    return;
            //}
            if (!Dispatcher.CurrentDispatcher.CheckAccess()) // CheckAccess returns true if you're on the dispatcher thread
            {
                //Dispatcher.CurrentDispatcher.Invoke(new EventHandler<Object>(SystemStatusShow), oRes, sState);
                Dispatcher.CurrentDispatcher.BeginInvoke(new EventHandler<Object>(SystemStatusShow), oRes, sState);
                return;
            }

            switch ((int)oRes)
            {
                case HWUnify.STATUSBAR:
                    m_ShowState.StatusBarTextScreen1 = (String)sState;
                    break;
                case HWUnify.CAMERA_IMAGE:
                    try
                    {
                        m_ShowState.CameraImage = (BitmapImage)sState;
                    }
                    catch (Exception e)
                    {
                        m_ShowState.StatusBarTextScreen1 = e.Message;
                    }
                    break;
                case HWUnify.THUMBNAIL_IMAGE:
                    try
                    {
                        m_ShowState.ThumbnailImage = (BitmapImage)sState;
                    }
                    catch (Exception e)
                    {
                        m_ShowState.StatusBarTextScreen1 = e.Message;
                    }
                    break;
                case HWUnify.MotionType_Recover:
                    switch ((MotionType)sState)
                    {
                        case MotionType.ThumbnailImage:// 保存缩略图图像
                            break;
                    }
                    break;
            }
        }

        #endregion 主测试线程

        #region 线体线程

        /// <summary>
        /// 打开线体通讯口（光源）和线体测试线程
        /// </summary>
        public void ParaChanged_LineBodyControl()
        {
            m_LineBodyControl.ComName = Doc.m_SystemParam.LightComName;
            m_LineBodyControl.Baudrate = Doc.m_SystemParam.LightBaudrate;
            m_LineBodyControl.DataBit = Doc.m_SystemParam.LightDataBit;
            m_LineBodyControl.Parity = Doc.m_SystemParam.LightParity;
            m_LineBodyControl.StopBits = Doc.m_SystemParam.LightStopBits;
            m_LineBodyControl.ParameterChanged();
        }

        uint uiNextMeta = 1;
        uint uiLastMeta = 1;
        uint uiNextBoard = 1;
        uint uiOidNextMeta = 1;
        uint uiOldLastMeta = 1;
        uint uiOldNextBoard = 1;
        private void Thread_LinebodyControl(Object oIn)
        {
            if ((oIn is LineBodyControl) == false)
                return;

            if (m_Motion.MotionLinkState != MotionLinkState.Linked)
                return;

            LineBodyControl lbControl = oIn as LineBodyControl;

            try
            {
                lbControl.Running = true;
                Debug.WriteLine("启动测试线程！", DateTime.Now.ToString());
                uint uiIOInputRet1 = 0;
                uint uiIOInputRet2 = 0;
                //uint uiIOOutputRet = 0;
                //线体自动运行线程


                Thread threadFstLineBoard = new Thread(new ParameterizedThreadStart(FstLineBoard_AutoRun));
                threadFstLineBoard.IsBackground = true;
                Thread threadScdLineBoard = new Thread(new ParameterizedThreadStart(ScdLineBoard_AutoRun));
                threadScdLineBoard.IsBackground = true;
                Thread threadThdLineBoard = new Thread(new ParameterizedThreadStart(ThdLineBoard_AutoRun));
                threadThdLineBoard.IsBackground = true;

                threadFstLineBoard.Start(oIn);
                threadScdLineBoard.Start(oIn);
                threadThdLineBoard.Start(oIn);
                bool bWait = false;
                while (lbControl.Running)
                {

                    #region 查状态
                    uiIOInputRet1 = LTDMC.dmc_read_inport(m_Motion.CardID, 0);
                    uiIOInputRet2 = LTDMC.dmc_read_inport(m_Motion.CardID, 1);

                    uint uiResult = (uiIOInputRet1 & (1 << MotionDMC5000.INPUT_EMG));
                    m_ShowState.EMG = uiResult == 0 ? true : false;
                    if (uiResult != 0)
                    {// 高电平
                        m_Motion.IOEMGStop = 0;
                    }
                    else
                    {// 低电平
                        m_Motion.IOEMGStop++;//连续高电平状态
                        if (m_Motion.IOEMGStop > 100)
                            m_Motion.IOEMGStop = 2;
                    }

                    uiResult = (uiIOInputRet1 & (1 << MotionDMC5000.INPUT_SERVON));
                    m_ShowState.EMGHard = uiResult == 0 ? true : false;
                    if (uiResult != 0)
                    {// 高电平
                        m_Motion.IOServStop = 0;
                    }
                    else
                    {// 低电平
                        m_Motion.IOServStop++;//连续高电平状态
                        if (m_Motion.IOServStop > 100)
                            m_Motion.IOServStop = 2;
                    }
                    uiResult = (uiIOInputRet1 & (1 << MotionDMC5000.INPUT_DOOROPEN));
                    m_ShowState.EMGDOOR = uiResult == 0 ? true : false;
                    if (uiResult != 0)
                    {// 高电平
                        m_Motion.IOEMGDOOR = 0;
                    }
                    else
                    {// 低电平
                        m_Motion.IOEMGDOOR++;//连续高电平状态
                        if (m_Motion.IOEMGDOOR > 100)
                            m_Motion.IOEMGDOOR = 2;
                    }

                    if (m_Motion.IOEMGStop > 1 || m_Motion.IOServStop > 1 || m_Motion.IOEMGDOOR > 1)
                    {

                        Thread showEmg_Thread = new Thread(Thread_EmgShow);
                        showEmg_Thread.Start();
                    }

                    #region 按键开关轨道调宽
                    uint uiWide = (uiIOInputRet1 & (1 << MotionDMC5000.INPUT_ChangeWide));
                    uint uiNarrow = (uiIOInputRet1 & (1 << MotionDMC5000.INPUT_ChangeNarrow));
                    if (uiWide == 0)
                    {
                        if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_LineBody) == MotionDMC5000.AXIS_STOP)
                            m_Motion.LineBodyChangeWidthBySpeed(true);
                    }
                    if (uiNarrow == 0)
                    {
                        if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_LineBody) == MotionDMC5000.AXIS_STOP)
                            m_Motion.LineBodyChangeWidthBySpeed(false);
                    }
                    if (uiWide != 0 && uiNarrow != 0)
                    {
                        m_Motion.LineBodyChangeWidthStop();
                    }
                    #endregion 按键开关轨道调宽

                    #region 按键开关切换测试结果显示

                    uiNextMeta = (uiIOInputRet1 & (1 << MotionDMC5000.INPUT_NextMeta));
                    uiLastMeta = (uiIOInputRet1 & (1 << MotionDMC5000.INPUT_LastMeta));
                    if (uiNextMeta == 0 && uiOidNextMeta != 0)
                    {
                        if (m_NextMetaHandler != null)
                            m_NextMetaHandler(null, null);
                    }
                    if (uiLastMeta == 0 && uiOldLastMeta != 0)
                    {
                        if (m_LastMetaHandler != null)
                            m_LastMetaHandler(null, null);
                    }
                    uiNextBoard = (uiIOInputRet1 & (1 << MotionDMC5000.INPUT_NextBoard));
                    if (uiNextBoard == 0 && uiOldNextBoard != 0)
                    {
                        if (m_NextBoardHandler != null)
                            m_NextBoardHandler(null, null);
                    }

                    uiOidNextMeta = uiNextMeta;
                    uiOldLastMeta = uiLastMeta;
                    uiOldNextBoard = uiNextBoard;
                    #endregion 按键开关切换测试结果显示

                    uint dwRet = LTDMC.dmc_axis_io_status(m_Motion.CardID, MotionDMC5000.AXIS_X);
                    m_ShowState.ALM_X = (dwRet & 0x0001) == 0 ? false : true;
                    m_ShowState.ELAdd_X = (dwRet & 0x0002) == 0 ? false : true;
                    m_ShowState.ELMinus_X = (dwRet & 0x0004) == 0 ? false : true;
                    m_ShowState.EMG_X = (dwRet & 0x0008) == 0 ? false : true;
                    m_ShowState.ORG_X = (dwRet & 0x0010) == 0 ? false : true;
                    m_ShowState.INP_X = (dwRet & 0x0100) == 0 ? false : true;
                    m_ShowState.EZ_X = (dwRet & 0x0200) == 0 ? false : true;
                    dwRet = LTDMC.dmc_axis_io_status(m_Motion.CardID, MotionDMC5000.AXIS_Y);
                    m_ShowState.ALM_Y = (dwRet & 0x0001) == 0 ? false : true;
                    m_ShowState.ELAdd_Y = (dwRet & 0x0002) == 0 ? false : true;
                    m_ShowState.ELMinus_Y = (dwRet & 0x0004) == 0 ? false : true;
                    m_ShowState.EMG_Y = (dwRet & 0x0008) == 0 ? false : true;
                    m_ShowState.ORG_Y = (dwRet & 0x0010) == 0 ? false : true;
                    m_ShowState.INP_Y = (dwRet & 0x0100) == 0 ? false : true;
                    m_ShowState.EZ_Y = (dwRet & 0x0200) == 0 ? false : true;

                    uiResult = (uiIOInputRet2 & (1 << MotionDMC5000.INPUT_RDY0));
                    m_ShowState.RDY_X = uiResult == 0 ? false : true;
                    uiResult = (uiIOInputRet2 & (1 << (MotionDMC5000.INPUT_RDY0 + 1)));
                    m_ShowState.RDY_Y = uiResult == 0 ? false : true;

                    short shRet = LTDMC.dmc_read_sevon_pin(m_Motion.CardID, MotionDMC5000.AXIS_X);
                    m_ShowState.SevOn_X = shRet == 0 ? true : false;
                    shRet = LTDMC.dmc_read_sevon_pin(m_Motion.CardID, MotionDMC5000.AXIS_Y);
                    m_ShowState.SevOn_Y = shRet == 0 ? true : false;

                    #endregion 查状态
                    bWait = lbControl.SignalEvent.WaitOne(LineBodyControl.SleepTime);
                    if (lbControl.Running == false)
                    {
                        Debug.WriteLine("测试线程退出！", DateTime.Now.ToString());
                        continue;
                    }

                    if (bWait)
                    {
                        #region 信号触发：处理命令

                        Debug.WriteLine("信号触发：处理命令！", DateTime.Now.ToString());
                        switch (lbControl.StateLineBodyDebug)
                        {
                            case StateLineBodyDebug.BoardLock://夹紧气缸升起，夹紧板子
                                LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_ACLock, MotionDMC5000.LEVEL_LOW);
                                break;
                            case StateLineBodyDebug.BoardUnLock://夹紧气缸落下，松开板子
                                LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_ACLock, MotionDMC5000.LEVEL_HIGH);
                                break;
                            case StateLineBodyDebug.StopUp://阻挡气缸升起
                                LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_ACStop, MotionDMC5000.LEVEL_LOW);
                                break;
                            case StateLineBodyDebug.StopDown://阻挡气缸下落
                                LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_ACStop, MotionDMC5000.LEVEL_HIGH);
                                break;
                            case StateLineBodyDebug.LineMoveStart://线体转动进出板
                                m_Motion.LineMoveStart((ushort)m_Motion.AXIS_FstLineBody, Doc.m_SystemParam.LineBodySpeed);
                                m_Motion.LineMoveStart(MotionDMC5000.AXIS_ScdLineBody, Doc.m_SystemParam.LineBodySpeed);
                                m_Motion.LineMoveStart((ushort)m_Motion.AXIS_ThdLineBody, Doc.m_SystemParam.LineBodySpeed);
                                LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_InBoardOutput, MotionDMC5000.LEVEL_LOW);
                                break;
                            case StateLineBodyDebug.LineMoveStop:
                                m_Motion.LineMoveStop((ushort)m_Motion.AXIS_FstLineBody);
                                m_Motion.LineMoveStop(MotionDMC5000.AXIS_ScdLineBody);
                                m_Motion.LineMoveStop((ushort)m_Motion.AXIS_ThdLineBody);
                                LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_InBoardOutput, MotionDMC5000.LEVEL_HIGH);
                                break;
                            case StateLineBodyDebug.LineExpand://线体调宽
                                m_Motion.LineBodyChangeWidthBySpeed(true);
                                break;
                            case StateLineBodyDebug.LineReduce://线体调窄
                                m_Motion.LineBodyChangeWidthBySpeed(false);
                                break;
                            case StateLineBodyDebug.LineWidthChangeStop://线体停止调整宽度
                                m_Motion.LineBodyChangeWidthStop();
                                break;
                            case StateLineBodyDebug.XYGoHome://运动轴回原点运动
                                m_Motion.GoHome();
                                break;
                            case StateLineBodyDebug.TestStart://软启动测试
                                StartTest();
                                break;
                            case StateLineBodyDebug.LineStateInit://自动进板状态初始化到初始化的检测进板状态
                                lbControl.StateFstLineBody = StateFstLineBody.TestInBoard;
                                lbControl.StateScdLineBody = StateScdLineBody.StateCheck;
                                lbControl.StateThdLineBody = StateThdLineBody.InBoard;
                                break;
                        }
                        lbControl.StateLineBodyDebug = StateLineBodyDebug.None;

                        #endregion 信号触发：处理命令
                    }
                    else
                    {//延时超过：查询状态
                    }
                }
            }

            catch (Exception e)
            {
                Debug.WriteLine(e.Message, "线体线程异常退出！");
            }
            finally
            {
            }
        }

        /// <summary>
        /// 第一段轨道线程
        /// </summary>
        /// <param name="oIn"></param>
        private void FstLineBoard_AutoRun(Object oIn)
        {
            if (oIn == null || (oIn is LineBodyControl) == false)
                return;
            LineBodyControl lbControl = oIn as LineBodyControl;
            bool bWaitScd2Fst = false;
            while (lbControl.Running)
            {
                short sOutputInBoard = LTDMC.dmc_read_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_InBoardOutput);
                if (Doc.m_SystemParam.AutoPass)
                {//直通
                   if (Doc.m_SystemParam.ValidInBoardOutput && sOutputInBoard == 0)
                    {// 要板输出高电平有效，但IO没输出高电平：输出高电平
                        LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_InBoardOutput, MotionDMC5000.LEVEL_HIGH);
                    }
                    else if (Doc.m_SystemParam.ValidInBoardOutput == false && sOutputInBoard != 0)
                    {// 要板输出低电平有效，但IO没输出低电平：输出低电平
                        LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_InBoardOutput, MotionDMC5000.LEVEL_LOW);
                    }

                    if (LTDMC.dmc_check_done(m_Motion.CardID, (ushort)m_Motion.AXIS_FstLineBody) == MotionDMC5000.AXIS_STOP)
                    {//线体马达是否停止状态，停止：运行线体马达
                        m_Motion.LineMoveStart((ushort)m_Motion.AXIS_FstLineBody, Doc.m_SystemParam.LineBodySpeed);
                    }
                }
                else
                {

                    bWaitScd2Fst = lbControl.SignalScd2Fst.WaitOne(LineBodyControl.SleepTime);
                    #region 查状态
                    short sInputInBoard = LTDMC.dmc_read_inbit(m_Motion.CardID, MotionDMC5000.INPUT_InputInBoard);

                    short sOutputScanner = LTDMC.dmc_read_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_ScannerTrigger);
                    short sInputInboardSwitch = LTDMC.dmc_read_inbit(m_Motion.CardID, (ushort)m_Motion.INPUT_FstInBoardSwitch);
                    short sInputBoardInPlaceSwitch = LTDMC.dmc_read_inbit(m_Motion.CardID, (ushort)m_Motion.INPUT_FstBoardInPlaceSwitch);

                    #endregion 查状态

                    //if (Doc.m_SystemParam.LineOnState)
                    //{//自动过板检测
                    //自动上板：线体状态判断，执行线体流程
                    switch (lbControl.StateFstLineBody)
                    {

                        case StateFstLineBody.TestInBoard://检测进板信号：信号无效：马达停止，等待信号有效；

                            if (Doc.m_SystemParam.ValidInBoardOutput && sOutputInBoard == 0)
                            {// 要板输出高电平有效，但IO没输出高电平：输出高电平
                                LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_InBoardOutput, MotionDMC5000.LEVEL_HIGH);
                            }
                            else if (Doc.m_SystemParam.ValidInBoardOutput == false && sOutputInBoard != 0)
                            {// 要板输出低电平有效，但IO没输出低电平：输出低电平
                                LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_InBoardOutput, MotionDMC5000.LEVEL_LOW);
                            }
                            if ((Doc.m_SystemParam.ValidInBoardInput && sInputInBoard != 0)
                                || (Doc.m_SystemParam.ValidInBoardInput == false && sInputInBoard == 0))
                            {//检测进板信号：有效
                                if (LTDMC.dmc_check_done(m_Motion.CardID, (ushort)m_Motion.AXIS_FstLineBody) == MotionDMC5000.AXIS_STOP)
                                {//线体马达是否停止状态，停止：运行线体马达
                                    m_Motion.LineMoveStart((ushort)m_Motion.AXIS_FstLineBody, Doc.m_SystemParam.LineBodySpeed);
                                }
                                lbControl.StateFstLineBody = StateFstLineBody.BoardInSwitch;
                            }
                            else
                            {//进板信号无效，无板进入
                                if (sInputBoardInPlaceSwitch != 0)
                                {//到位传感器无效，板已出
                                    m_Motion.LineMoveStop((ushort)m_Motion.AXIS_FstLineBody);
                                }
                            }
                            break;
                        case StateFstLineBody.BoardInSwitch://进板：检测进板开关
                            if (sInputInboardSwitch == 0)
                            {//检测进板传感器开关：低电平有效
                                lbControl.StateFstLineBody = StateFstLineBody.BoardOutSwitch;
                            }
                            break;
                        case StateFstLineBody.BoardOutSwitch://进板：检测进板开关
                                                             //进入此步，马达有概率停止，加入马达状态判断
                            if (LTDMC.dmc_check_done(m_Motion.CardID, (ushort)m_Motion.AXIS_FstLineBody) == MotionDMC5000.AXIS_STOP)
                            {//线体马达是否停止状态，停止：运行线体马达
                                m_Motion.LineMoveStart((ushort)m_Motion.AXIS_FstLineBody, Doc.m_SystemParam.LineBodySpeed); ;
                            }

                            if (sInputInboardSwitch != 0)
                            {//进板传感器高电平，PCB板已离开进板传感器
                             //复位要板信号
                                if (Doc.m_SystemParam.ValidInBoardOutput && sOutputInBoard != 0)
                                {// 要板输出高电平有效，但IO没输出高电平：输出高电平
                                    LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_InBoardOutput, MotionDMC5000.LEVEL_LOW);
                                }
                                else if (Doc.m_SystemParam.ValidInBoardOutput == false && sOutputInBoard == 0)
                                {// 要板输出低电平有效，但IO没输出低电平：输出低电平
                                    LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_InBoardOutput, MotionDMC5000.LEVEL_HIGH);
                                }

                                if (m_ShowState.CurrentProductType.BarcodeMode == EnumBarcodeMode.Scanner)
                                {//开始下一块板的扫码
                                    if (Doc.m_SystemParam.ValidResultOutput && sOutputScanner == 0)
                                    {// 扫码枪输出高电平有效，但IO没输出高电平：输出高电平
                                        LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_ScannerTrigger, MotionDMC5000.LEVEL_HIGH);
                                    }
                                    else if (Doc.m_SystemParam.ValidResultOutput == false && sOutputScanner != 0)
                                    {// 扫码枪输出低电平有效，但IO没输出低电平：输出低电平
                                        LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_ScannerTrigger, MotionDMC5000.LEVEL_LOW);
                                    }
                                }
                                lbControl.StateFstLineBody = StateFstLineBody.BoardInPlace;
                            }
                            break;
                        case StateFstLineBody.BoardInPlace:
                            if (sInputBoardInPlaceSwitch == 0)
                            {//到位传感器低有效
                                m_Motion.LineMoveStop((ushort)m_Motion.AXIS_FstLineBody);
                                if (LTDMC.dmc_check_done(m_Motion.CardID, (ushort)m_Motion.AXIS_FstLineBody) == MotionDMC5000.AXIS_STOP)
                                    lbControl.StateFstLineBody = StateFstLineBody.OutBoard;
                            }
                            break;
                        case StateFstLineBody.OutBoard:
                            lbControl.SignalFst.Set();//向第二段轨道发送出板信号
                            if (bWaitScd2Fst)
                            {//等待第二段轨道要板
                                if (LTDMC.dmc_check_done(m_Motion.CardID, (ushort)m_Motion.AXIS_FstLineBody) == MotionDMC5000.AXIS_STOP)
                                {//线体马达是否停止状态，停止：运行线体马达
                                    m_Motion.LineMoveStart((ushort)m_Motion.AXIS_FstLineBody, Doc.m_SystemParam.LineBodySpeed);
                                }
                                if (LTDMC.dmc_check_done(m_Motion.CardID, (ushort)m_Motion.AXIS_FstLineBody) == MotionDMC5000.AXIS_RUN)
                                {
                                    lbControl.StateFstLineBody = StateFstLineBody.TestInBoard;
                                }

                            }
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// 第二段轨道线程
        /// </summary>
        /// <param name="oIn"></param>
        private void ScdLineBoard_AutoRun(Object oIn)
        {
            if (oIn == null || (oIn is LineBodyControl) == false)
                return;
            LineBodyControl lbControl = oIn as LineBodyControl;
            bool bWaitFst = false;
            bool bWaitThd = false;
            while (lbControl.Running)
            {
                bWaitFst = lbControl.SignalFst.WaitOne(LineBodyControl.SleepTime);
                bWaitThd = lbControl.SignalThd.WaitOne(LineBodyControl.SleepTime);
                #region 查状态
                short sInputInBoardSwitch = LTDMC.dmc_read_inbit(m_Motion.CardID, (ushort)m_Motion.INPUT_ScdInBoardSwitch);
                short sInputBoardInPlaceSwitch = LTDMC.dmc_read_inbit(m_Motion.CardID, (ushort)m_Motion.INPUT_ScdBoardInPlaceSwitch);
                short sOutputStop = LTDMC.dmc_read_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_ACStop);
                short sOutputLock = LTDMC.dmc_read_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_ACLock);
                short sOutputYellowLight = LTDMC.dmc_read_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_YELLOWLIGHT);
                #endregion 查状态

                if (Doc.m_SystemParam.ManualInBoard)
                {//手动上板调试编程

                    #region 手动放板编程
                    switch (lbControl.StateScdLineBody)
                    {
                        case StateScdLineBody.StateCheck:
                            if (sOutputLock == 0)
                            {//压紧取消
                                LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_ACLock, MotionDMC5000.LEVEL_HIGH);
                            }
                            if (sOutputStop == 0)
                            {//阻挡取消
                                LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_ACStop, MotionDMC5000.LEVEL_HIGH);
                            }
                            if (sOutputLock != 0 && sOutputStop != 0)
                            {
                                lbControl.StateScdLineBody = StateScdLineBody.InBoard;
                            }
                            break;
                        case StateScdLineBody.InBoard:
                            lbControl.SignalScd2Fst.Set();//向上一段轨道发送信号，可以进板了
                            if (bWaitFst)
                            {//等待上段轨道进板，转动马达
                                if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_ScdLineBody) == MotionDMC5000.AXIS_STOP)
                                {//线体马达是否停止状态，停止：运行线体马达
                                    m_Motion.LineMoveStart(MotionDMC5000.AXIS_ScdLineBody, Doc.m_SystemParam.LineBodySpeed);
                                }
                                if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_ScdLineBody) == MotionDMC5000.AXIS_RUN)
                                {
                                    lbControl.StateScdLineBody = StateScdLineBody.InBoardSwitch;
                                }
                            }
                            else
                            {
                                if (sInputBoardInPlaceSwitch != 0)
                                {//到位传感器无效，轨道无板
                                    if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_ScdLineBody) == MotionDMC5000.AXIS_RUN)
                                    {
                                        m_Motion.LineMoveStop(MotionDMC5000.AXIS_ScdLineBody);
                                    }
                                }
                            }
                            break;
                        case StateScdLineBody.InBoardSwitch:
                            if (sInputInBoardSwitch == 0)
                            {//进板传感器有效
                                if (sOutputStop != 0)
                                {//阻挡气缸升起
                                    LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_ACStop, MotionDMC5000.LEVEL_LOW);
                                }
                                if (sOutputStop == 0)
                                {
                                    lbControl.StateScdLineBody = StateScdLineBody.BoardInPlaceSwith;
                                }
                            }
                            break;
                        case StateScdLineBody.BoardInPlaceSwith:
                            if (sInputBoardInPlaceSwitch == 0)
                            {//到位
                                m_Motion.LineMoveStop(MotionDMC5000.AXIS_ScdLineBody);
                                if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_ScdLineBody) == MotionDMC5000.AXIS_RUN)
                                {//马达停止
                                    m_Motion.LineMoveStop(MotionDMC5000.AXIS_ScdLineBody);
                                }
                                //if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_ScdLineBody) == MotionDMC5000.AXIS_STOP)
                                //{//压紧气缸动作
                                //    LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_ACLock, MotionDMC5000.LEVEL_LOW);
                                //}
                            }
                            break;
                    }
                    #endregion 手动放板编程
                }
                if (Doc.m_SystemParam.BoardPass)
                {//过板
                    #region 过板

                    switch (lbControl.StateScdLineBody)
                    {
                        case StateScdLineBody.StateCheck:

                            if (sOutputLock == 0)
                            {//压紧取消
                                LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_ACLock, MotionDMC5000.LEVEL_HIGH);
                            }
                            if (sOutputStop == 0)
                            {//阻挡取消
                                LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_ACStop, MotionDMC5000.LEVEL_HIGH);
                            }
                            if (sOutputLock != 0 && sOutputStop != 0)
                            {
                                lbControl.StateScdLineBody = StateScdLineBody.InBoard;
                            }

                            break;

                        case StateScdLineBody.InBoard:
                            lbControl.SignalScd2Fst.Set();//向上一段轨道发送信号，可以进板了
                            if (bWaitFst)
                            {//等待上段轨道进板，转动马达

                                if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_ScdLineBody) == MotionDMC5000.AXIS_STOP)
                                {//线体马达是否停止状态，停止：运行线体马达
                                    m_Motion.LineMoveStart(MotionDMC5000.AXIS_ScdLineBody, Doc.m_SystemParam.LineBodySpeed);
                                }
                                if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_ScdLineBody) == MotionDMC5000.AXIS_RUN)
                                {
                                    lbControl.StateScdLineBody = StateScdLineBody.BoardInPlaceSwith;
                                }
                            }
                            else
                            {
                                if (sInputBoardInPlaceSwitch != 0)
                                {//到位传感器无效，轨道无板
                                    if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_ScdLineBody) == MotionDMC5000.AXIS_RUN)
                                    {
                                        m_Motion.LineMoveStop(MotionDMC5000.AXIS_ScdLineBody);
                                    }
                                }
                            }
                            break;
                        case StateScdLineBody.BoardInPlaceSwith:
                            if (sInputBoardInPlaceSwitch == 0)
                            {//到位
                                lbControl.StateScdLineBody = StateScdLineBody.OutBoard;
                            }
                            break;
                        case StateScdLineBody.OutBoard:
                            lbControl.SignalScd2Thd.Set();
                            if (bWaitThd)
                            {//等待第三段轨道要板信号
                                if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_ScdLineBody) == MotionDMC5000.AXIS_STOP)
                                {
                                    m_Motion.LineMoveStart(MotionDMC5000.AXIS_ScdLineBody, Doc.m_SystemParam.LineBodySpeed);
                                }
                                if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_ScdLineBody) == MotionDMC5000.AXIS_RUN)
                                {
                                    lbControl.StateScdLineBody = StateScdLineBody.StateCheck;
                                }
                            }
                            break;
                    }
                    #endregion 过板
                }

                if (Doc.m_SystemParam.LineOnState)
                {//自动过板检测

                    #region 自动过板检测
                    switch (lbControl.StateScdLineBody)
                    {
                        case StateScdLineBody.StateCheck:

                            if (sOutputLock == 0)
                            {//压紧取消
                                LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_ACLock, MotionDMC5000.LEVEL_HIGH);
                            }
                            if (sOutputStop == 0)
                            {//阻挡取消
                                LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_ACStop, MotionDMC5000.LEVEL_HIGH);
                            }
                            if (sOutputLock != 0 && sOutputStop != 0)
                            {
                                lbControl.StateScdLineBody = StateScdLineBody.InBoard;
                            }

                            break;


                        case StateScdLineBody.InBoard:
                            lbControl.SignalScd2Fst.Set();//向上一段轨道发送信号，可以进板了
                            if (bWaitFst)
                            {//等待上段轨道进板，转动马达

                                if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_ScdLineBody) == MotionDMC5000.AXIS_STOP)
                                {//线体马达是否停止状态，停止：运行线体马达
                                    m_Motion.LineMoveStart(MotionDMC5000.AXIS_ScdLineBody, Doc.m_SystemParam.LineBodySpeed);
                                }
                                if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_ScdLineBody) == MotionDMC5000.AXIS_RUN)
                                {
                                    lbControl.StateScdLineBody = StateScdLineBody.InBoardSwitch;
                                }
                            }
                            else
                            {
                                if (sInputBoardInPlaceSwitch != 0)
                                {//到位传感器无效，轨道无板
                                    if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_ScdLineBody) == MotionDMC5000.AXIS_RUN)
                                    {
                                        m_Motion.LineMoveStop(MotionDMC5000.AXIS_ScdLineBody);
                                    }
                                }
                            }
                            break;
                        case StateScdLineBody.InBoardSwitch:
                            if (sInputInBoardSwitch == 0)
                            {//进板传感器有效
                                if (Doc.m_SystemParam.Direction)
                                {
                                    lbControl.StateScdLineBody = StateScdLineBody.BoardInPlaceSwith;
                                }
                                else
                                {
                                    if (sOutputStop != 0)
                                    {//阻挡气缸升起
                                        LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_ACStop, MotionDMC5000.LEVEL_LOW);
                                    }
                                    if (sOutputStop == 0)
                                    {
                                        lbControl.StateScdLineBody = StateScdLineBody.BoardInPlaceSwith;
                                    }
                                }
                            }
                            break;
                        case StateScdLineBody.BoardInPlaceSwith:
                            if (sInputBoardInPlaceSwitch == 0)
                            {//到位
                                m_Motion.LineMoveStop(MotionDMC5000.AXIS_ScdLineBody);
                                if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_ScdLineBody) == MotionDMC5000.AXIS_RUN)
                                {//马达停止
                                    m_Motion.LineMoveStop(MotionDMC5000.AXIS_ScdLineBody);
                                }
                                if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_ScdLineBody) == MotionDMC5000.AXIS_STOP)
                                //{//压紧气缸动作
                                //LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_ACLock, MotionDMC5000.LEVEL_LOW);
                                //}
                                //if (sOutputLock == 0)
                                {
                                    App.m_Motion.IOStart++;//连续高电平状态
                                    if (App.m_Motion.IOStart > 2)
                                    {//等待1秒
                                        Thread startTest_Thread = new Thread(StartTest);
                                        startTest_Thread.Start();
                                        LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_YELLOWLIGHT, MotionDMC5000.LEVEL_LOW);
                                        lbControl.StateScdLineBody = StateScdLineBody.Testing;
                                        App.m_Motion.IOStart = 0;
                                    }

                                }
                            }
                            break;
                        case StateScdLineBody.Testing:
                            if (sOutputYellowLight != 0)
                            {
                                lbControl.StateScdLineBody = StateScdLineBody.TestEnd;
                            }
                            break;
                        case StateScdLineBody.TestEnd:
                            if (sOutputLock == 0)
                            {//压紧取消
                                LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_ACLock, MotionDMC5000.LEVEL_HIGH);
                            }
                            if (sOutputStop == 0)
                            {//阻挡取消
                                LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_ACStop, MotionDMC5000.LEVEL_HIGH);
                            }
                            if (sOutputLock != 0 && sOutputStop != 0)
                            {
                                lbControl.StateScdLineBody = StateScdLineBody.OutBoard;
                            }
                            break;
                        case StateScdLineBody.OutBoard:
                            lbControl.SignalScd2Thd.Set();
                            if (bWaitThd)
                            {//等待第三段轨道要板信号
                                if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_ScdLineBody) == MotionDMC5000.AXIS_STOP)
                                {
                                    m_Motion.LineMoveStart(MotionDMC5000.AXIS_ScdLineBody, Doc.m_SystemParam.LineBodySpeed);
                                }
                                if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_ScdLineBody) == MotionDMC5000.AXIS_RUN)
                                {
                                    lbControl.StateScdLineBody = StateScdLineBody.OutPlaceSwith;
                                }
                            }
                            break;
                        case StateScdLineBody.OutPlaceSwith:
                            //板已离开到位传感器
                            if (sInputBoardInPlaceSwitch != 0)
                            {
                                lbControl.StateScdLineBody = StateScdLineBody.StateCheck;
                            }
                            break;
                    }
                    #endregion 自动过板检测
                }
                if (Doc.m_SystemParam.AutoPass)
                {//直通

                    if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_ScdLineBody) == MotionDMC5000.AXIS_STOP)
                    {//线体马达是否停止状态，停止：运行线体马达
                        m_Motion.LineMoveStart(MotionDMC5000.AXIS_ScdLineBody, Doc.m_SystemParam.LineBodySpeed);
                    }

                }
            }
        }

        /// <summary>
        /// 第三段轨道线程
        /// </summary>
        private void ThdLineBoard_AutoRun(Object oIn)
        {
            if (oIn == null || (oIn is LineBodyControl) == false)
                return;
            LineBodyControl lbControl = oIn as LineBodyControl;
            bool bWaitScd2Thd = false;
            while (lbControl.Running)
            {

                if (Doc.m_SystemParam.AutoPass)
                {//直通

                    if (LTDMC.dmc_check_done(m_Motion.CardID, (ushort)m_Motion.AXIS_ThdLineBody) == MotionDMC5000.AXIS_STOP)
                    {//线体马达是否停止状态，停止：运行线体马达
                        m_Motion.LineMoveStart((ushort)m_Motion.AXIS_ThdLineBody, Doc.m_SystemParam.LineBodySpeed);
                    }

                }
                else
                {


                    bWaitScd2Thd = lbControl.SignalScd2Thd.WaitOne(LineBodyControl.SleepTime);
                    #region 查状态
                    short sInputBoardInPlaceSwitch = LTDMC.dmc_read_inbit(m_Motion.CardID, (ushort)m_Motion.INPUT_ThdBoardInPlaceSwitch);
                    short sInputOutBoard = LTDMC.dmc_read_inbit(m_Motion.CardID, MotionDMC5000.INPUT_InputOutBoard);
                    short sOutputOutBoard = LTDMC.dmc_read_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_OutBoardOutput);
                    #endregion 查状态
                    //if (Doc.m_SystemParam.LineOnState)
                    //{//自动过板检测
                    switch (lbControl.StateThdLineBody)
                    {
                        case StateThdLineBody.InBoard:
                            lbControl.SignalThd.Set();
                            if (bWaitScd2Thd)
                            {//等待上段轨道进板，转动马达
                                if (LTDMC.dmc_check_done(m_Motion.CardID, (ushort)m_Motion.AXIS_ThdLineBody) == MotionDMC5000.AXIS_STOP)
                                {//线体马达是否停止状态，停止：运行线体马达
                                    m_Motion.LineMoveStart((ushort)m_Motion.AXIS_ThdLineBody, Doc.m_SystemParam.LineBodySpeed);
                                }
                                if (LTDMC.dmc_check_done(m_Motion.CardID, (ushort)m_Motion.AXIS_ThdLineBody) == MotionDMC5000.AXIS_RUN)
                                {
                                    lbControl.StateThdLineBody = StateThdLineBody.BoardInPlace;
                                }
                            }
                            else
                            {
                                if (sInputBoardInPlaceSwitch != 0)
                                {//
                                    if (LTDMC.dmc_check_done(m_Motion.CardID, (ushort)m_Motion.AXIS_ThdLineBody) == MotionDMC5000.AXIS_RUN)
                                    {
                                        m_Motion.LineMoveStop((ushort)m_Motion.AXIS_ThdLineBody);
                                    }
                                }
                            }
                            break;
                        case StateThdLineBody.BoardInPlace:
                            if (sInputBoardInPlaceSwitch == 0)
                            {//到位传感器触发

                                if (LTDMC.dmc_check_done(m_Motion.CardID, (ushort)m_Motion.AXIS_ThdLineBody) == MotionDMC5000.AXIS_RUN)
                                {//马达停止
                                    m_Motion.LineMoveStop((ushort)m_Motion.AXIS_ThdLineBody);
                                }
                                if (sOutputOutBoard != 0)
                                {//出板输出
                                    LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_OutBoardOutput, MotionDMC5000.LEVEL_LOW);
                                }
                                if (sOutputOutBoard == 0 && LTDMC.dmc_check_done(m_Motion.CardID, (ushort)m_Motion.AXIS_ThdLineBody) == MotionDMC5000.AXIS_STOP)
                                {
                                    lbControl.StateThdLineBody = StateThdLineBody.OutBoard;
                                }
                            }
                            break;
                        case StateThdLineBody.OutBoard:
                            if (sInputOutBoard == 0)
                            {//要板输入
                                if (LTDMC.dmc_check_done(m_Motion.CardID, (ushort)m_Motion.AXIS_ThdLineBody) == MotionDMC5000.AXIS_STOP)
                                {//线体马达是否停止状态，停止：运行线体马达
                                    m_Motion.LineMoveStart((ushort)m_Motion.AXIS_ThdLineBody, Doc.m_SystemParam.LineBodySpeed);
                                }
                                if (LTDMC.dmc_check_done(m_Motion.CardID, (ushort)m_Motion.AXIS_ThdLineBody) == MotionDMC5000.AXIS_RUN)
                                {
                                    lbControl.StateThdLineBody = StateThdLineBody.OutBoardOutSwitch;
                                }
                            }
                            break;
                        case StateThdLineBody.OutBoardOutSwitch:
                            if (sInputBoardInPlaceSwitch != 0)
                            {//板离开到位传感器
                                lbControl.StateThdLineBody = StateThdLineBody.InBoard;
                            }
                            break;


                    }
                }
            }
        }

        #endregion 线体线程  

        //private void Thread_LinebodyControl(Object oIn)
        //{
        //    if ((oIn is LineBodyControl) == false)
        //        return;

        //    if (m_Motion.MotionLinkState != MotionLinkState.Linked)
        //        return;

        //    LineBodyControl lbControl = oIn as LineBodyControl;

        //    try
        //    {
        //        lbControl.Running = true;
        //        Debug.WriteLine("启动测试线程！", DateTime.Now.ToString());

        //        //线体自动运行线程
        //        Thread threadBoardInPlace = new Thread(new ParameterizedThreadStart(LineBoard_AutoRun));
        //        threadBoardInPlace.IsBackground = true;
        //        threadBoardInPlace.Start(oIn);

        //        bool bWait = false;
        //        while (lbControl.Running)
        //        {
        //            bWait = lbControl.SignalEvent.WaitOne(LineBodyControl.SleepTime);
        //            if (lbControl.Running == false)
        //            {
        //                Debug.WriteLine("测试线程退出！", DateTime.Now.ToString());
        //                continue;
        //            }

        //            if (bWait)
        //            {
        //                #region 信号触发：处理命令

        //                Debug.WriteLine("信号触发：处理命令！", DateTime.Now.ToString());
        //                switch (lbControl.StateLineBodyDebug)
        //                {
        //                    case StateLineBodyDebug.BoardLock://夹紧气缸升起，夹紧板子
        //                        LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_ACLock, MotionDMC5000.LEVEL_LOW);
        //                        break;
        //                    case StateLineBodyDebug.BoardUnLock://夹紧气缸落下，松开板子
        //                        LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_ACLock, MotionDMC5000.LEVEL_HIGH);
        //                        break;
        //                    case StateLineBodyDebug.StopUp://阻挡气缸升起
        //                        LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_ACStop, MotionDMC5000.LEVEL_LOW);
        //                        break;
        //                    case StateLineBodyDebug.StopDown://阻挡气缸下落
        //                        LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_ACStop, MotionDMC5000.LEVEL_HIGH);
        //                        break;
        //                    case StateLineBodyDebug.BoardIn://进板
        //                        break;
        //                    case StateLineBodyDebug.BoardOut://出板
        //                        break;
        //                    case StateLineBodyDebug.LineMoveStart://线体转动进出板
        //                        m_Motion.LineMoveStart(Doc.m_SystemParam.LineBodySpeed);
        //                        LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_InBoardOutput, MotionDMC5000.LEVEL_LOW);
        //                        break;
        //                    case StateLineBodyDebug.LineInboardEdit://线体转动进出板
        //                        m_Motion.LineMoveStart(Doc.m_SystemParam.LineBodySpeed);
        //                        LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_InBoardOutput, MotionDMC5000.LEVEL_LOW);
        //                        break;
        //                    case StateLineBodyDebug.LineMoveStop://线体转动进出板
        //                        m_Motion.LineMoveStop();
        //                        LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_InBoardOutput, MotionDMC5000.LEVEL_HIGH);
        //                        break;
        //                    case StateLineBodyDebug.LineExpand://线体调宽
        //                        m_Motion.LineBodyChangeWidthBySpeed(true);
        //                        break;
        //                    case StateLineBodyDebug.LineReduce://线体调窄
        //                        m_Motion.LineBodyChangeWidthBySpeed(false);
        //                        break;
        //                    case StateLineBodyDebug.LineWidthChangeStop://线体停止调整宽度
        //                        m_Motion.LineBodyChangeWidthStop();
        //                        break;
        //                    case StateLineBodyDebug.XYGoHome://运动轴回原点运动
        //                        m_Motion.GoHome();
        //                        break;
        //                    case StateLineBodyDebug.TestStart://软启动测试
        //                        StartTest();
        //                        break;
        //                    case StateLineBodyDebug.LineStateInit://自动进板状态初始化到初始化的检测进板状态
        //                        lbControl.StateLineBodyOnline = StateLineBodyOnline.TestInBoard;
        //                        break;
        //                }
        //                lbControl.StateLineBodyDebug = StateLineBodyDebug.None;

        //                #endregion 信号触发：处理命令
        //            }
        //            else
        //            {//延时超过：查询状态
        //            }
        //        }

        //    }
        //    catch (Exception e)
        //    {
        //        Debug.WriteLine(e.Message, "线体线程异常退出！");
        //    }
        //    finally
        //    {
        //    }
        //}

        ///// <summary>
        ///// 线体自动运行线程
        ///// </summary>
        ///// <param name="oIn"></param>
        //private void LineBoard_AutoRun(Object oIn)
        //{
        //    if (oIn == null || (oIn is LineBodyControl) == false)
        //        return;
        //    LineBodyControl lbControl = oIn as LineBodyControl;

        //    uint uiIOInputRet1 = 0;
        //    uint uiIOInputRet2 = 0;
        //    uint uiIOOutputRet = 0;
        //    uint iNumBoardIn = 0;     //当前轨道进入的PCB数量
        //    uint iNumBoardOut = 0;    //当前轨道走出的PCB数量
        //    uint iNumTestEndIn = 0;  //当前板测试完成时的进板计数
        //    uint iNumTestEndOut = 0; //当前板测试完成时的出板计数

        //    ushort InMode = 2;       //0:禁止；1：上升沿计数；2：下降沿计数
        //    ushort OutMode = 1;       //0:禁止；1：上升沿计数；2：下降沿计数
        //    double filter = 0.001; //滤波时间为0.001s
        //    //设置进板传感器和出板传感器对应输入口用来计数
        //    LTDMC.dmc_set_io_count_mode(m_Motion.CardID, MotionDMC5000.INPUT_InBoard, InMode, filter);
        //    LTDMC.dmc_set_io_count_mode(m_Motion.CardID, MotionDMC5000.INPUT_OutBoard, OutMode, filter);
        //    //计数值清零
        //    LTDMC.dmc_set_io_count_value(m_Motion.CardID, MotionDMC5000.INPUT_InBoard, 0);
        //    LTDMC.dmc_set_io_count_value(m_Motion.CardID, MotionDMC5000.INPUT_OutBoard, 0);
        //    try
        //    {
        //        while (lbControl.Running)
        //        {
        //            #region 查状态

        //            uiIOInputRet1 = LTDMC.dmc_read_inport(m_Motion.CardID, 0);
        //            uiIOInputRet2 = LTDMC.dmc_read_inport(m_Motion.CardID, 1);
        //            uiIOOutputRet = LTDMC.dmc_read_outport(m_Motion.CardID, 0);

        //            uint uiResult = (uiIOInputRet1 & (1 << MotionDMC5000.INPUT_EMG));
        //            m_ShowState.EMG = uiResult == 0 ? true : false;
        //            if (uiResult != 0)
        //            {// 高电平
        //                m_Motion.IOEMGStop = 0;
        //            }
        //            else
        //            {// 低电平
        //                m_Motion.IOEMGStop++;//连续高电平状态
        //                if (m_Motion.IOEMGStop > 100)
        //                    m_Motion.IOEMGStop = 2;
        //            }

        //            uiResult = (uiIOInputRet1 & (1 << MotionDMC5000.INPUT_SERVON));
        //            m_ShowState.EMGHard = uiResult == 0 ? true : false;
        //            if (uiResult != 0)
        //            {// 高电平
        //                m_Motion.IOServStop = 0;
        //            }
        //            else
        //            {// 低电平
        //                m_Motion.IOServStop++;//连续高电平状态
        //                if (m_Motion.IOServStop > 100)
        //                    m_Motion.IOServStop = 2;
        //            }
        //            if (m_Motion.IOEMGStop > 1 || m_Motion.IOServStop > 1)
        //            {
        //                Thread showEmg_Thread = new Thread(Thread_EmgShow);
        //                showEmg_Thread.Start();
        //            }

        //            uint dwRet = LTDMC.dmc_axis_io_status(m_Motion.CardID, MotionDMC5000.AXIS_X);
        //            m_ShowState.ALM_X = (dwRet & 0x0001) == 0 ? false : true;
        //            m_ShowState.ELAdd_X = (dwRet & 0x0002) == 0 ? false : true;
        //            m_ShowState.ELMinus_X = (dwRet & 0x0004) == 0 ? false : true;
        //            m_ShowState.EMG_X = (dwRet & 0x0008) == 0 ? false : true;
        //            m_ShowState.ORG_X = (dwRet & 0x0010) == 0 ? false : true;
        //            m_ShowState.INP_X = (dwRet & 0x0100) == 0 ? false : true;
        //            m_ShowState.EZ_X = (dwRet & 0x0200) == 0 ? false : true;
        //            dwRet = LTDMC.dmc_axis_io_status(m_Motion.CardID, MotionDMC5000.AXIS_Y);
        //            m_ShowState.ALM_Y = (dwRet & 0x0001) == 0 ? false : true;
        //            m_ShowState.ELAdd_Y = (dwRet & 0x0002) == 0 ? false : true;
        //            m_ShowState.ELMinus_Y = (dwRet & 0x0004) == 0 ? false : true;
        //            m_ShowState.EMG_Y = (dwRet & 0x0008) == 0 ? false : true;
        //            m_ShowState.ORG_Y = (dwRet & 0x0010) == 0 ? false : true;
        //            m_ShowState.INP_Y = (dwRet & 0x0100) == 0 ? false : true;
        //            m_ShowState.EZ_Y = (dwRet & 0x0200) == 0 ? false : true;

        //            uiResult = (uiIOInputRet2 & (1 << MotionDMC5000.INPUT_RDY0));
        //            m_ShowState.RDY_X = uiResult == 0 ? false : true;
        //            uiResult = (uiIOInputRet2 & (1 << (MotionDMC5000.INPUT_RDY0 + 1)));
        //            m_ShowState.RDY_Y = uiResult == 0 ? false : true;

        //            short shRet = LTDMC.dmc_read_sevon_pin(m_Motion.CardID, MotionDMC5000.AXIS_X);
        //            m_ShowState.SevOn_X = shRet == 0 ? true : false;
        //            shRet = LTDMC.dmc_read_sevon_pin(m_Motion.CardID, MotionDMC5000.AXIS_Y);
        //            m_ShowState.SevOn_Y = shRet == 0 ? true : false;

        //            #endregion 查状态

        //            //读出进出板开关的计数值
        //            LTDMC.dmc_get_io_count_value(m_Motion.CardID, MotionDMC5000.INPUT_InBoard, ref iNumBoardIn);
        //            LTDMC.dmc_get_io_count_value(m_Motion.CardID, MotionDMC5000.INPUT_OutBoard, ref iNumBoardOut);
        //            m_ShowState.NumInBoard = iNumBoardIn;
        //            m_ShowState.NumOutBoard = iNumBoardOut;



        //            if (Doc.m_SystemParam.ManualInBoard)
        //            {//手动上板
        //                if ((uiIOInputRet1 & (1 << MotionDMC5000.INPUT_BoardInPlace)) == 0)
        //                {//检测到位传感器开关：低电平有效
        //                    if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_LineMove) == MotionDMC5000.AXIS_RUN)
        //                    {//线体马达是否正在运行，正在运行：停止线体马达
        //                        LTDMC.dmc_stop(m_Motion.CardID, MotionDMC5000.AXIS_LineMove, 0);//减速停止
        //                    }
        //                }
        //                else
        //                {
        //                    if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_LineMove) == MotionDMC5000.AXIS_STOP)
        //                    {//线体马达是否停止状态，停止：运行线体马达
        //                        m_Motion.LineMoveStart(Doc.m_SystemParam.LineBodySpeed);
        //                    }
        //                }
        //            }
        //            if (Doc.m_SystemParam.BoardPass)
        //            {//过板
        //                if (Doc.m_SystemParam.ValidInBoardOutput && (uiIOOutputRet & (1 << MotionDMC5000.OUTPUT_InBoardOutput)) == 0)
        //                {// 要板输出高电平有效，但IO没输出高电平：输出高电平
        //                    LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_InBoardOutput, MotionDMC5000.LEVEL_HIGH);
        //                }
        //                else if (Doc.m_SystemParam.ValidInBoardOutput == false && (uiIOOutputRet & (1 << MotionDMC5000.OUTPUT_InBoardOutput)) != 0)
        //                {// 要板输出低电平有效，但IO没输出低电平：输出低电平
        //                    LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_InBoardOutput, MotionDMC5000.LEVEL_LOW);
        //                }
        //                if ((Doc.m_SystemParam.ValidInBoardInput && (uiIOInputRet1 & (1 << MotionDMC5000.INPUT_InputInBoard)) != 0)
        //                             || (Doc.m_SystemParam.ValidInBoardInput == false && (uiIOInputRet1 & (1 << MotionDMC5000.INPUT_InputInBoard)) == 0))
        //                {//检测进板信号：有效
        //                    if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_LineMove) == MotionDMC5000.AXIS_STOP)
        //                    {//线体马达是否停止状态，停止：运行线体马达
        //                        m_Motion.LineMoveStart(Doc.m_SystemParam.LineBodySpeed);
        //                    }
        //                }
        //                else
        //                {
        //                    if (iNumBoardIn - iNumBoardOut > 0)
        //                    {
        //                        if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_LineMove) == MotionDMC5000.AXIS_STOP)
        //                        {//线体马达是否停止状态，停止：运行线体马达
        //                            m_Motion.LineMoveStart(Doc.m_SystemParam.LineBodySpeed);
        //                        }
        //                    }
        //                    else
        //                    {
        //                        if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_LineMove) == MotionDMC5000.AXIS_RUN)
        //                        {//线体马达是否正在运行，正在运行：停止线体马达
        //                            LTDMC.dmc_stop(m_Motion.CardID, MotionDMC5000.AXIS_LineMove, 0);//减速停止
        //                        }
        //                    }
        //                }
        //                //if (iNumBoardIn - iNumBoardOut >0)
        //                //{
        //                //    if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_LineMove) == MotionDMC5000.AXIS_STOP)
        //                //    {//线体马达是否停止状态，停止：运行线体马达
        //                //        m_Motion.LineMoveStart(Doc.m_SystemParam.LineBodySpeed);
        //                //    }
        //                //}

        //                //if (iNumBoardIn - iNumBoardOut ==0)
        //                //{//轨道无板
        //                //    if ((Doc.m_SystemParam.ValidInBoardInput && (uiIOInputRet1 & (1 << MotionDMC5000.INPUT_InputInBoard)) == 0) || (Doc.m_SystemParam.ValidInBoardInput == false && (uiIOInputRet1 & (1 << MotionDMC5000.INPUT_InputInBoard)) != 0))
        //                //    {//检测进板信号：无效
        //                //        if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_LineMove) == MotionDMC5000.AXIS_RUN)
        //                //        {//线体马达是否正在运行，正在运行：停止线体马达
        //                //            LTDMC.dmc_stop(m_Motion.CardID, MotionDMC5000.AXIS_LineMove, 0);//减速停止
        //                //        }
        //                //    }
        //                //}
        //            }
        //            if (Doc.m_SystemParam.LineOnState)
        //            {//自动过板检测
        //                //自动上板：线体状态判断，执行线体流程
        //                switch (lbControl.StateLineBodyOnline)
        //                {
        //                    case StateLineBodyOnline.TestInBoard://检测进板信号：信号无效：马达停止，等待信号有效；

        //                        if (Doc.m_SystemParam.ValidInBoardOutput && (uiIOOutputRet & (1 << MotionDMC5000.OUTPUT_InBoardOutput)) == 0)
        //                        {// 要板输出高电平有效，但IO没输出高电平：输出高电平
        //                            LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_InBoardOutput, MotionDMC5000.LEVEL_HIGH);
        //                        }
        //                        else if (Doc.m_SystemParam.ValidInBoardOutput == false && (uiIOOutputRet & (1 << MotionDMC5000.OUTPUT_InBoardOutput)) != 0)
        //                        {// 要板输出低电平有效，但IO没输出低电平：输出低电平
        //                            LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_InBoardOutput, MotionDMC5000.LEVEL_LOW);
        //                        }
        //                        if ((Doc.m_SystemParam.ValidInBoardInput && (uiIOInputRet1 & (1 << MotionDMC5000.INPUT_InputInBoard)) != 0)
        //                            || (Doc.m_SystemParam.ValidInBoardInput == false && (uiIOInputRet1 & (1 << MotionDMC5000.INPUT_InputInBoard)) == 0))
        //                        {//检测进板信号：有效
        //                            lbControl.StateLineBodyOnline = StateLineBodyOnline.InBoardAction;
        //                        }
        //                        else
        //                        {//检测进板信号：无效
        //                            if (iNumTestEndOut - iNumBoardOut > 0 && iNumTestEndIn - iNumBoardIn < 1)
        //                            {//上一块出完且下一块板未进入
        //                                if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_LineMove) == MotionDMC5000.AXIS_RUN)
        //                                {//线体马达是否正在运行，正在运行：停止线体马达
        //                                    m_Motion.LineMoveStop();
        //                                }
        //                            }
        //                        }
        //                        break;
        //                    case StateLineBodyOnline.InBoardAction://进板动作：线体运动
        //                        if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_LineMove) == MotionDMC5000.AXIS_STOP)
        //                        {//线体马达是否停止状态，停止：运行线体马达
        //                            m_Motion.LineMoveStart(Doc.m_SystemParam.LineBodySpeed);
        //                        }
        //                        if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_LineMove) == MotionDMC5000.AXIS_RUN)
        //                        {//线体马达是否正在运行，正在运行：进入下一步骤
        //                            lbControl.StateLineBodyOnline = StateLineBodyOnline.InBoard;
        //                        }
        //                        break;
        //                    case StateLineBodyOnline.InBoard://进板：检测进板开关
        //                        if ((uiIOInputRet1 & (1 << MotionDMC5000.INPUT_InBoard)) == 0)
        //                        {//检测进板传感器开关：低电平有效
        //                         // 要板信号取消
        //                            if (Doc.m_SystemParam.ValidInBoardOutput && (uiIOOutputRet & (1 << MotionDMC5000.OUTPUT_InBoardOutput)) != 0)
        //                            {// 要板输出高电平有效：输出低电平
        //                                LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_InBoardOutput, MotionDMC5000.LEVEL_LOW);
        //                            }
        //                            else if (Doc.m_SystemParam.ValidInBoardOutput == false && (uiIOOutputRet & (1 << MotionDMC5000.OUTPUT_InBoardOutput)) == 0)
        //                            {// 要板输出低电平有效：输出高电平
        //                                LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_InBoardOutput, MotionDMC5000.LEVEL_HIGH);
        //                            }
        //                            if (((Doc.m_SystemParam.ValidInBoardOutput && (uiIOOutputRet & (1 << MotionDMC5000.OUTPUT_InBoardOutput)) == 0) || (Doc.m_SystemParam.ValidInBoardOutput == false && (uiIOOutputRet & (1 << MotionDMC5000.OUTPUT_InBoardOutput)) != 0)))
        //                            {// 要板信号取消，进入下一步
        //                                lbControl.StateLineBodyOnline = StateLineBodyOnline.InOutBoard;
        //                            }
        //                        }
        //                        break;
        //                    case StateLineBodyOnline.InOutBoard://进板：检测进板开关
        //                        //进入此步，马达有概率停止，加入马达状态判断
        //                        if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_LineMove) == MotionDMC5000.AXIS_STOP)
        //                        {//线体马达是否停止状态，停止：运行线体马达
        //                            m_Motion.LineMoveStart(Doc.m_SystemParam.LineBodySpeed);
        //                        }
        //                        if ((uiIOInputRet1 & (1 << MotionDMC5000.INPUT_InBoard)) != 0)
        //                        {//进板传感器高电平，PCB板已离开进板传感器
        //                            lbControl.StateLineBodyOnline = StateLineBodyOnline.BoardInPlace;
        //                        }
        //                        break;
        //                    case StateLineBodyOnline.BoardInPlace://到位：检测到位开关
        //                        if ((uiIOInputRet1 & (1 << MotionDMC5000.INPUT_BoardInPlace)) == 0)
        //                        {//检测到位传感器开关：低电平有效
        //                            if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_LineMove) == MotionDMC5000.AXIS_RUN)
        //                            {//线体马达是否正在运行，正在运行：停止线体马达
        //                                LTDMC.dmc_stop(m_Motion.CardID, MotionDMC5000.AXIS_LineMove, 0);//减速停止
        //                            }
        //                            else
        //                            {//检测到位传感器开关：低电平有效  
        //                                App.m_Motion.IOStart++;//连续高电平状态
        //                                if (App.m_Motion.IOStart > 2)
        //                                {//等待1秒
        //                                    Thread startTest_Thread = new Thread(StartTest);
        //                                    startTest_Thread.Start();
        //                                    LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_YELLOWLIGHT, MotionDMC5000.LEVEL_LOW);
        //                                    lbControl.StateLineBodyOnline = StateLineBodyOnline.Testing;
        //                                    App.m_Motion.IOStart = 0;
        //                                }
        //                            }
        //                        }
        //                        break;
        //                    case StateLineBodyOnline.Testing://测试等待
        //                        if ((uiIOOutputRet & (1 << MotionDMC5000.OUTPUT_YELLOWLIGHT)) != 0)
        //                        {
        //                            lbControl.StateLineBodyOnline = StateLineBodyOnline.TestEnd;
        //                        }
        //                        break;
        //                    case StateLineBodyOnline.TestEnd://测试结束：现场恢复
        //                        if (Doc.m_SystemParam.ValidOutBoardOutput && (uiIOOutputRet & (1 << MotionDMC5000.OUTPUT_OutBoardOutput)) == 0)
        //                        {// 出板输出高电平有效，但IO没输出高电平：输出高电平
        //                            LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_OutBoardOutput, MotionDMC5000.LEVEL_HIGH);
        //                        }
        //                        else if (Doc.m_SystemParam.ValidOutBoardOutput == false && (uiIOOutputRet & (1 << MotionDMC5000.OUTPUT_OutBoardOutput)) != 0)
        //                        {// 出板输出低电平有效，但IO没输出低电平：输出低电平
        //                            LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_OutBoardOutput, MotionDMC5000.LEVEL_LOW);
        //                        }
        //                        //if (Doc.m_SystemParam.ValidResultOutput && (uiIOOutputRet & (1 << MotionDMC5000.OUTPUT_TestResult)) == 0)
        //                        //{// 测试结果输出高电平有效，但IO没输出高电平：输出高电平
        //                        //    LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_TestResult, MotionDMC5000.LEVEL_HIGH);
        //                        //}
        //                        //else if (Doc.m_SystemParam.ValidResultOutput == false && (uiIOOutputRet & (1 << MotionDMC5000.OUTPUT_TestResult)) != 0)
        //                        //{// 测试结果输出低电平有效，但IO没输出低电平：输出低电平
        //                        //    LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_TestResult, MotionDMC5000.LEVEL_LOW);
        //                        //}
        //                        if ((Doc.m_SystemParam.ValidOutBoardOutput && (uiIOOutputRet & (1 << MotionDMC5000.OUTPUT_OutBoardOutput)) != 0) || (Doc.m_SystemParam.ValidOutBoardOutput == false && (uiIOOutputRet & (1 << MotionDMC5000.OUTPUT_OutBoardOutput)) == 0))
        //                        {
        //                            iNumTestEndIn = iNumBoardIn;
        //                            iNumTestEndOut = iNumBoardOut;
        //                            lbControl.StateLineBodyOnline = StateLineBodyOnline.TestOutBoard;
        //                        }
        //                        break;
        //                    case StateLineBodyOnline.TestOutBoard://检测出板信号
        //                        //if ((Doc.m_SystemParam.ValidInBoardInput && (uiIOInputRet1 & (1 << MotionDMC5000.INPUT_InputOutBoard)) != 0)
        //                        //     || (Doc.m_SystemParam.ValidInBoardInput == false && (uiIOInputRet1 & (1 << MotionDMC5000.INPUT_InputOutBoard)) == 0))
        //                        //{//检测出板输入信号：有效，马达转动
        //                        if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_LineMove) == MotionDMC5000.AXIS_STOP)
        //                        {//线体马达是否停止状态，停止：运行线体马达
        //                            m_Motion.LineMoveStart(Doc.m_SystemParam.LineBodySpeed);
        //                        }
        //                        if (LTDMC.dmc_check_done(m_Motion.CardID, MotionDMC5000.AXIS_LineMove) == MotionDMC5000.AXIS_RUN)
        //                        {
        //                            lbControl.StateLineBodyOnline = StateLineBodyOnline.TestInBoard;
        //                        }
        //                        //if ((uiIOInputRet1 & (1 << MotionDMC5000.INPUT_BoardInPlace)) != 0)
        //                        //{//检测到位传感器开关：高电平PCB板已离开到位开关
        //                        //    //m_Motion.LineMoveStart(Doc.m_SystemParam.LineBodySpeed);
        //                        //    lbControl.StateLineBodyOnline = StateLineBodyOnline.TestInBoard;
        //                        //}
        //                        break;
        //                        //case StateLineBodyOnline.OutBoardSwitchFront://出板：检测出板开关,PCB板进入开关
        //                        //    if ((uiIOInputRet1 & (1 << MotionDMC5000.INPUT_OutBoard)) == 0)
        //                        //    {//检测出板传感器开关：低电平有效
        //                        //        if (Doc.m_SystemParam.ValidOutBoardOutput && (uiIOOutputRet & (1 << MotionDMC5000.OUTPUT_OutBoardOutput)) != 0)
        //                        //        {// 出板输出高电平有效，但IO没输出低电平：输出低电平
        //                        //            LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_OutBoardOutput, MotionDMC5000.LEVEL_LOW);
        //                        //        }
        //                        //        else if (Doc.m_SystemParam.ValidOutBoardOutput == false && (uiIOOutputRet & (1 << MotionDMC5000.OUTPUT_OutBoardOutput)) == 0)
        //                        //        {// 出板输出低电平有效，但IO没输出高电平：输出高电平
        //                        //            LTDMC.dmc_write_outbit(m_Motion.CardID, MotionDMC5000.OUTPUT_OutBoardOutput, MotionDMC5000.LEVEL_HIGH);
        //                        //        }

        //                        //        if ((Doc.m_SystemParam.ValidOutBoardOutput && (uiIOOutputRet & (1 << MotionDMC5000.OUTPUT_OutBoardOutput)) != 0) || (Doc.m_SystemParam.ValidOutBoardOutput == false && (uiIOOutputRet & (1 << MotionDMC5000.OUTPUT_OutBoardOutput)) == 0))
        //                        //            lbControl.StateLineBodyOnline = StateLineBodyOnline.OutBoardSwitchBack;
        //                        //    }
        //                        //    break;
        //                        //case StateLineBodyOnline.OutBoardSwitchBack://出板：检测出板开关，PCB板走出开关
        //                        //    if ((uiIOInputRet1 & (1 << MotionDMC5000.INPUT_OutBoard)) != 0)
        //                        //    {//检测出板传感器开关：输出高电平
        //                        //        lbControl.StateLineBodyOnline = StateLineBodyOnline.TestInBoard;
        //                        //    }
        //                        //    break;
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        Debug.WriteLine(e.Message, "线体线程异常退出！");
        //    }
        //    finally
        //    {

        //    }
        //}

        //#endregion 线体线程

    }
}
