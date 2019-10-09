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

using System.Xml.Linq;
using System.Windows.Threading;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Collections;

using Emgu.CV.WPF;
using csLTDMC;
using AOIHardware;
using GerbDll;

namespace AOIWeldOnline
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : MainWindowBase
    {
        public static TestProductType m_TestProductType = null;
        private const int PADNUMBER = 10;

        private System.Windows.Shapes.Rectangle m_rectCameraSelectArea;
        private System.Windows.Point m_CameraSelectAreaStartPoint;
        private App m_App = Application.Current as App;
        DispatcherTimer m_Timer;
        private double m_iScale = 3;//相机图像组合成缩略图的缩放倍数

        #region 窗口函数


        public MainWindow()
        {
            this.InitializeComponent();
            this.DataContext = App.m_ShowState;

            this.Loaded += (sender, e) =>
            {
                UserManagerBtn.Click += HeadBtn_Click;
                ReLoginBtn.Click += HeadBtn_Click;
                SysSetupBtn.Click += HeadBtn_Click;
                DebugBtn.Click += HeadBtn_Click;

                AboutButton.Click += HeadBtn_Click;
                HelpButton.Click += HeadBtn_Click;
                CloseButton.Click += HeadBtn_Click;
            };

            this.WindowStartupLocation = WindowStartupLocation.Manual;
            this.WindowState = WindowState.Normal;
            this.WindowStyle = WindowStyle.None;
            this.ResizeMode = ResizeMode.NoResize;

            this.Top = SystemParameters.VirtualScreenTop;
            this.Left = SystemParameters.VirtualScreenLeft;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;

            //this.Width = SystemParameters.PrimaryScreenWidth;
            //this.Height = SystemParameters.PrimaryScreenHeight;           
            this.CommandBindings.Add(new CommandBinding(CustomCommand.CommandAddMeta, AddTestMeta_Click));//命令
            this.CommandBindings.Add(new CommandBinding(CustomCommand.CommandNextTestResult, ShowNextTestResult));

            m_Timer = new DispatcherTimer();
            m_Timer.Interval = new TimeSpan(0, 0, 1);   //间隔1
            m_Timer.Tick += new EventHandler(Timer_Tick);
            m_Timer.Start();
        }

        private void AOIWeldOnline_Loaded(object sender, RoutedEventArgs e)
        {
            if (Doc.m_SystemParam.ShowMode == ShowMode.System)
            {//系统测试
                RightTitleColumnDefinition.Width = new GridLength(0, GridUnitType.Auto);
                Text_HeadTitle1.Visibility = Visibility.Collapsed;

                leftColumnDefinition.Width = new GridLength(1, GridUnitType.Star);
                LeftStation.Visibility = Visibility.Visible;
                rightColumnDefinition.Width = new GridLength(0, GridUnitType.Auto);
                RightStation.Visibility = Visibility.Collapsed;
            }
            else if (Doc.m_SystemParam.ShowMode == ShowMode.Repair)
            {//维修站
                RightTitleColumnDefinition.Width = new GridLength(0, GridUnitType.Auto);
                Text_HeadTitle1.Visibility = Visibility.Collapsed;

                leftColumnDefinition.Width = new GridLength(0, GridUnitType.Auto);
                LeftStation.Visibility = Visibility.Collapsed;
                rightColumnDefinition.Width = new GridLength(1, GridUnitType.Star);
                RightStation.Visibility = Visibility.Visible;
            }
            else
            {//双屏
                RightTitleColumnDefinition.Width = new GridLength(1, GridUnitType.Star);
                Text_HeadTitle1.Visibility = Visibility.Visible;

                leftColumnDefinition.Width = new GridLength(1, GridUnitType.Star);
                LeftStation.Visibility = Visibility.Visible;
                rightColumnDefinition.Width = new GridLength(1, GridUnitType.Star);
                RightStation.Visibility = Visibility.Visible;
            }

            if (Doc.m_SystemUser.UserLevel <= UserLevel.Manager)
                SysSetupBtn.Visibility = Visibility.Visible;
            else SysSetupBtn.Visibility = Visibility.Collapsed;
            if (Doc.m_SystemUser.UserLevel <= UserLevel.SuperUser)
            {
                rbSysProgram.IsChecked = true;
                bdProgramTest.Visibility = Visibility.Visible;
            }
            else
            {
                rbSysTest.IsChecked = true;
                bdProgramTest.Visibility = Visibility.Collapsed;
            }

            //foreach (TestProductType type in ProductTypeEditComboBox.Items)
            //{
            //    if (type.Name == Doc.m_SystemParam.CurrentProductType)
            //    {
            //        ProductTypeEditComboBox.SelectedItem = type;
            //        break;
            //    }
            //}
            foreach (TestProduct type in ProductTypeEditComboBox.Items)
            {
                if (type.TypeName == Doc.m_SystemParam.CurrentProductType)
                {
                    ProductTypeEditComboBox.SelectedItem = type;
                    String testProductTypeFileName = Doc.m_SystemParam.SetupPath + "\\" + type.FileName;
                    if (File.Exists(testProductTypeFileName) == true)
                    {
                        m_TestProductType.XElement = XElement.Load(testProductTypeFileName);
                    }
                    else
                    {//编程文件不存在
                        m_TestProductType.XElement = new XElement("ProductType");
                    }

                    App.m_ShowState.CurrentProductType = m_TestProductType;
                    break;
                }
            }

            Doc.m_Authentication.m_AuthenticationException += this.AuthenticationExceptionHandle;
            m_App.ThumbnailShowHandler += CanvasProgramThumbnailShow;
            m_App.TestResultShowHandler += Show_TestResult;
            m_App.TestResultLightShowHandler += LightShow_TestResult;
            m_App.OnlineScannerHandler += OnlineScanner_Handler;
            m_App.OfflineScannerHandler += OfflineScanner_Handler;
            m_App.NextMetaHandler += ClickHandlerKeyDown;
            m_App.LastMetaHandler += ClickHandlerKeyUp;
            m_App.NextBoardHandler += ShowNextTestResult;

            //var layer1 = AdornerLayer.GetAdornerLayer(canvasCameraImage);
            //if(layer1==null)
            //{
            //    MessageBox.Show("1null");
            //}

            //var layer2= AdornerLayer.GetAdornerLayer(canvasProgramThumbnail);
            //if (layer2 == null)
            //{
            //    MessageBox.Show("2null");
            //}

            if (Doc.m_SystemParam.Direction)
            {
                App.m_Motion.AXIS_FstLineBody = 5;
                App.m_Motion.AXIS_ThdLineBody = 3;

                App.m_Motion.INPUT_FstInBoardSwitch = 7;
                App.m_Motion.INPUT_FstBoardInPlaceSwitch = 6;
                App.m_Motion.INPUT_ScdInBoardSwitch = 5;
                App.m_Motion.INPUT_ScdBoardInPlaceSwitch = 4;
                App.m_Motion.INPUT_ThdBoardInPlaceSwitch = 3;


            }
            else
            {
                App.m_Motion.AXIS_FstLineBody = 3;
                App.m_Motion.AXIS_ThdLineBody = 5;

                App.m_Motion.INPUT_FstInBoardSwitch = 3;
                App.m_Motion.INPUT_FstBoardInPlaceSwitch = 4;
                App.m_Motion.INPUT_ScdInBoardSwitch = 5;
                App.m_Motion.INPUT_ScdBoardInPlaceSwitch = 6;
                App.m_Motion.INPUT_ThdBoardInPlaceSwitch = 7;
            }

        }

        /// <summary>
        /// 检测到无软件狗：退出
        /// </summary>
        private void AuthenticationExceptionHandle()
        {
            if (this.CheckAccess() == false)
                this.Dispatcher.Invoke(new DelegateNoneParameter(AuthenticationExceptionHandle), System.Windows.Threading.DispatcherPriority.Normal);
            else this.Close();
        }

        void Timer_Tick(object sender, EventArgs e)
        {
            Text_Timer.Text = DateTime.Now.ToString("yyyy年MM月dd日 HH时mm分ss秒");//yyyy年MM月dd日 HH:hh:ss 也可以
        }

        private void HeadBtn_Click(object sender, RoutedEventArgs e)
        {
            //系统
            if (sender == UserManagerBtn)
            {//用户管理
                UserManager umDlg = new UserManager();

                umDlg.Owner = this;
                umDlg.ShowDialog();
            }
            if (sender == ReLoginBtn)
            {//重新登录
                LoginDlg loginDlg = new LoginDlg();

                loginDlg.Owner = this;
                if (loginDlg.ShowDialog() == true)
                {
                    if (Doc.m_SystemUser.UserLevel <= UserLevel.Manager)
                        SysSetupBtn.Visibility = Visibility.Visible;
                    else SysSetupBtn.Visibility = Visibility.Collapsed;
                    if (Doc.m_SystemUser.UserLevel <= UserLevel.SuperUser)
                    {
                        rbSysProgram.IsChecked = true;
                        bdProgramTest.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        rbSysTest.IsChecked = true;
                        bdProgramTest.Visibility = Visibility.Collapsed;
                    }
                }
            }
            else if (sender == SysSetupBtn)
            {//系统设置
                if (Permit_SetupParam() == false)
                    return;

                SysSetupDlg sysDlg = new SysSetupDlg();

                sysDlg.Owner = this;
                if (sysDlg.ShowDialog() == true)
                {
                    if (sysDlg.PLCParaChanged == true)
                    {//PLC串口参数修改
                        //PLCParaChanged();
                    }

                    if (Doc.m_SystemParam.ShowMode == ShowMode.System)
                    {//系统测试
                        RightTitleColumnDefinition.Width = new GridLength(0, GridUnitType.Auto);
                        Text_HeadTitle1.Visibility = Visibility.Collapsed;

                        leftColumnDefinition.Width = new GridLength(1, GridUnitType.Star);
                        LeftStation.Visibility = Visibility.Visible;
                        rightColumnDefinition.Width = new GridLength(0, GridUnitType.Auto);
                        RightStation.Visibility = Visibility.Collapsed;
                        m_bMTResultShow = false;
                    }
                    else if (Doc.m_SystemParam.ShowMode == ShowMode.Repair)
                    {//维修站
                        RightTitleColumnDefinition.Width = new GridLength(0, GridUnitType.Auto);
                        Text_HeadTitle1.Visibility = Visibility.Collapsed;

                        leftColumnDefinition.Width = new GridLength(0, GridUnitType.Auto);
                        LeftStation.Visibility = Visibility.Collapsed;
                        rightColumnDefinition.Width = new GridLength(1, GridUnitType.Star);
                        RightStation.Visibility = Visibility.Visible;
                    }
                    else
                    {//双屏
                        RightTitleColumnDefinition.Width = new GridLength(1, GridUnitType.Star);
                        Text_HeadTitle1.Visibility = Visibility.Visible;

                        leftColumnDefinition.Width = new GridLength(1, GridUnitType.Star);
                        LeftStation.Visibility = Visibility.Visible;
                        rightColumnDefinition.Width = new GridLength(1, GridUnitType.Star);
                        RightStation.Visibility = Visibility.Visible;
                    }

                    m_App.RebootOnlineScanner();
                    m_App.RebootOfflineScanner();
                }
            }
            else if (sender == DebugBtn)
            {//调试窗口
                DebugDlg debugDlg = new DebugDlg();

                debugDlg.Owner = this;
                debugDlg.ShowDialog();
            }
            else if (sender == AboutButton)
            {//关于
                AboutDialog aboutDlg = new AboutDialog();

                aboutDlg.Owner = this;
                aboutDlg.ShowDialog();
            }
            else if (sender == HelpButton)
            {//帮助
                string filePathName = AppDomain.CurrentDomain.BaseDirectory + "AOIWeldOnlineHelp.chm";

                if (File.Exists(filePathName))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(filePathName);
                    }
                    catch
                    {
                        MessageBox.Show("没有应用程序与帮助文件关联！",
                            Doc.MESSAGE_SOFTNAME,
                            MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    }
                }
                else
                {
                    MessageBox.Show("帮助文件不存在！",
                        Doc.MESSAGE_SOFTNAME,
                        MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }
            else if (sender == CloseButton)
            {//关闭
                if (ThreadTesting())
                {
                    MessageBox.Show("测试没有结束，请结束后再退出！",
                        Doc.MESSAGE_SOFTNAME,
                        MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
                else
                    this.Close();
            }
        }

        ThreadTestState m_ThreadTestState = ThreadTestState.TestEnd;

        private bool ThreadTesting()
        {
            if (m_ThreadTestState == ThreadTestState.Testing)
                return true;

            return false;
        }

        private bool Permit_SetupParam()
        {
            if (Doc.m_SystemUser.UserLevel > UserLevel.SuperUser)
            {
                MessageBox.Show("权限不够，不允许设置参数！",
                    Doc.MESSAGE_SOFTNAME,
                    MessageBoxButton.OK, MessageBoxImage.Exclamation);

                return false;
            }

            return true;
        }

        private void SystemRadiobutton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender == rbSysProgram)
            {//编程
                stpProgram.Visibility = Visibility.Visible;
                stpTest.Visibility = Visibility.Collapsed;
            }
            else
            {//测试
                stpProgram.Visibility = Visibility.Collapsed;
                stpTest.Visibility = Visibility.Visible;
            }
        }

        #endregion 窗口函数

        #region 鼠标操作

        #region 相机图像窗口

        private System.Windows.Shapes.Rectangle rectVertical = null;
        private System.Windows.Shapes.Rectangle rectHorizontal = null;
        private System.Windows.Shapes.Rectangle rectSelectVertical = null;
        private System.Windows.Shapes.Rectangle rectSelectHorizontal = null;
        private ThumbResizeAdorner m_ThumbResizeAdorner = null;
        /// <summary>
        /// 鼠标进入相机图像窗口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void canvasCameraImage_MouseEnter(object sender, MouseEventArgs e)
        {
            if (rbSysProgram.IsChecked == false)
                return;

            switch (App.m_ShowState.EnumProgram)
            {
                case EnumProgram.MakeThumbnail:// 获得缩略图的位置
                case EnumProgram.PCBStartAdjust:// PCB起点校正
                    double xSelect = 0;
                    double ySelect = 0;
                    if (rectVertical == null)
                    {
                        rectVertical = new System.Windows.Shapes.Rectangle() { Width = 2, Height = canvasCameraImage.Height, Fill = new SolidColorBrush(Colors.Red) };
                        canvasCameraImage.Children.Add(rectVertical);
                    }
                    else
                    {
                        canvasCameraImage.Children.Add(rectVertical);
                    }
                    if (rectHorizontal == null)
                    {
                        rectHorizontal = new System.Windows.Shapes.Rectangle() { Width = canvasCameraImage.Width, Height = 2, Fill = new SolidColorBrush(Colors.Red) };
                        canvasCameraImage.Children.Add(rectHorizontal);
                    }
                    else
                    {
                        canvasCameraImage.Children.Add(rectHorizontal);
                    }
                    if (radioPosStart.IsChecked == true)
                    {
                        xSelect = (App.m_Motion.MotionPositon[0] / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) - App.m_ShowState.CurrentProductType.PcbStart.X) * Doc.m_SystemParam.HelicalPitchMap.X / Doc.m_SystemParam.HelicalPitch + canvasCameraImage.Width / 2;
                        ySelect = (App.m_Motion.MotionPositon[1] / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) - App.m_ShowState.CurrentProductType.PcbStart.Y) * Doc.m_SystemParam.HelicalPitchMap.Y / Doc.m_SystemParam.HelicalPitch + canvasCameraImage.Height / 2;
                    }
                    else
                    {
                        xSelect = (App.m_Motion.MotionPositon[0] / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) - App.m_ShowState.CurrentProductType.PcbEnd.X) * Doc.m_SystemParam.HelicalPitchMap.X / Doc.m_SystemParam.HelicalPitch + canvasCameraImage.Width / 2;
                        ySelect = (App.m_Motion.MotionPositon[1] / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) - App.m_ShowState.CurrentProductType.PcbEnd.Y) * Doc.m_SystemParam.HelicalPitchMap.Y / Doc.m_SystemParam.HelicalPitch + canvasCameraImage.Height / 2;
                    }
                    if (rectSelectVertical == null)
                    {
                        rectSelectVertical = new System.Windows.Shapes.Rectangle() { Width = 2, Height = canvasCameraImage.Height, Fill = new SolidColorBrush(Colors.Yellow) };
                        rectSelectVertical.SetValue(Canvas.LeftProperty, xSelect - 1);
                        rectSelectVertical.SetValue(Canvas.TopProperty, 0.0);
                        canvasCameraImage.Children.Add(rectSelectVertical);
                    }
                    else
                    {
                        rectSelectVertical.SetValue(Canvas.LeftProperty, xSelect - 1);
                        rectSelectVertical.SetValue(Canvas.TopProperty, 0.0);
                        canvasCameraImage.Children.Add(rectSelectVertical);
                    }
                    if (rectSelectHorizontal == null)
                    {
                        rectSelectHorizontal = new System.Windows.Shapes.Rectangle() { Width = canvasCameraImage.Width, Height = 2, Fill = new SolidColorBrush(Colors.Yellow) };
                        rectSelectHorizontal.SetValue(Canvas.LeftProperty, 0.0);
                        rectSelectHorizontal.SetValue(Canvas.TopProperty, ySelect - 1);
                        canvasCameraImage.Children.Add(rectSelectHorizontal);
                    }
                    else
                    {
                        rectSelectHorizontal.SetValue(Canvas.LeftProperty, 0.0);
                        rectSelectHorizontal.SetValue(Canvas.TopProperty, ySelect - 1);
                        canvasCameraImage.Children.Add(rectSelectHorizontal);
                    }
                    canvasCameraImage.Cursor = Cursors.None;
                    break;
            }
        }

        /// <summary>
        /// 鼠标离开相机图像窗口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void canvasCameraImage_MouseLeave(object sender, MouseEventArgs e)
        {
            if (rbSysProgram.IsChecked == false)
                return;

            switch (App.m_ShowState.EnumProgram)
            {
                case EnumProgram.MakeThumbnail:// 获得缩略图的位置
                case EnumProgram.PCBStartAdjust:// PCB起点校正
                    if (canvasCameraImage.Children.Contains(rectVertical))
                    {
                        canvasCameraImage.Children.Remove(rectVertical);
                        rectVertical = null;
                    }
                    if (canvasCameraImage.Children.Contains(rectHorizontal))
                    {
                        canvasCameraImage.Children.Remove(rectHorizontal);
                        rectHorizontal = null;
                    }
                    if (canvasCameraImage.Children.Contains(rectSelectVertical))
                    {
                        canvasCameraImage.Children.Remove(rectSelectVertical);
                        rectSelectVertical = null;
                    }
                    if (canvasCameraImage.Children.Contains(rectSelectHorizontal))
                    {
                        canvasCameraImage.Children.Remove(rectSelectHorizontal);
                        rectSelectHorizontal = null;
                    }
                    canvasCameraImage.Cursor = Cursors.Arrow;
                    break;
            }
        }

        /// <summary>
        /// 鼠标在相机图像窗口移动
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void canvasCameraImage_MouseMove(object sender, MouseEventArgs e)
        {
            var posaa = e.GetPosition(canvasCameraImage);
            App.m_ShowState.StatusBarTextScreen1 = String.Format("鼠标位置：（{0:N2}，{1:N2}）", posaa.X, posaa.Y);

            if (rbSysProgram.IsChecked == false)
                return;

            switch (App.m_ShowState.EnumProgram)
            {
                case EnumProgram.MakeThumbnail:
                case EnumProgram.PCBStartAdjust:// PCB起点校正
                    Canvas.SetTop(rectHorizontal, e.GetPosition(canvasCameraImage).Y - 1);
                    Canvas.SetLeft(rectHorizontal, 0);
                    Canvas.SetTop(rectVertical, 0);
                    Canvas.SetLeft(rectVertical, e.GetPosition(canvasCameraImage).X - 1);
                    break;
                case EnumProgram.LibMode:
                case EnumProgram.MarkSetup:
                case EnumProgram.MetaLibEdit:
                    if (cameraImage.ImageSource != null)
                    {
                        if (e.LeftButton == MouseButtonState.Released || m_rectCameraSelectArea == null)
                            return;

                        var pos = e.GetPosition(canvasCameraImage);

                        //    Set the position of rectangle
                        var x = Math.Min(pos.X, m_CameraSelectAreaStartPoint.X);
                        var y = Math.Min(pos.Y, m_CameraSelectAreaStartPoint.Y);

                        //    Set the dimenssion of the rectangle
                        var w = Math.Max(pos.X, m_CameraSelectAreaStartPoint.X) - x;
                        var h = Math.Max(pos.Y, m_CameraSelectAreaStartPoint.Y) - y;

                        m_rectCameraSelectArea.Width = w;
                        m_rectCameraSelectArea.Height = h;

                        Canvas.SetLeft(m_rectCameraSelectArea, x);
                        Canvas.SetTop(m_rectCameraSelectArea, y);
                    }
                    break;
                case EnumProgram.PathSetup:

                    if (rbSelect.IsChecked == true && e.LeftButton == MouseButtonState.Pressed)
                    {
                        if (m_rectCameraSelectArea == null)
                            return;
                        var pos = e.GetPosition(canvasCameraImage);

                        //    Set the position of rectangle
                        var x = Math.Min(pos.X, m_CameraSelectAreaStartPoint.X);
                        var y = Math.Min(pos.Y, m_CameraSelectAreaStartPoint.Y);

                        var w = Math.Max(pos.X, m_CameraSelectAreaStartPoint.X) - x;
                        var h = Math.Max(pos.Y, m_CameraSelectAreaStartPoint.Y) - y;

                        m_rectCameraSelectArea.Width = w;
                        m_rectCameraSelectArea.Height = h;

                        Canvas.SetLeft(m_rectCameraSelectArea, x);
                        Canvas.SetTop(m_rectCameraSelectArea, y);
                    }

                    //if (e.LeftButton == MouseButtonState.Pressed && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    //{
                    //    System.Windows.Point p = e.GetPosition(canvasCameraImage);
                    //    foreach (Shape shape in canvasCameraImage.Children)
                    //    {
                    //        if (shape is System.Windows.Shapes.Path)
                    //        {
                    //            System.Windows.Shapes.Path s = shape as System.Windows.Shapes.Path;
                    //            Rect r = s.Data.Bounds;
                    //            double x = r.X + r.Width / 2;
                    //            double y = r.Y + r.Height / 2;
                    //            if (x > Math.Min(p.X, m_CameraSelectAreaStartPoint.X) && x < Math.Max(p.X, m_CameraSelectAreaStartPoint.X) && y > Math.Min(p.Y, m_CameraSelectAreaStartPoint.Y) && y < Math.Max(p.Y, m_CameraSelectAreaStartPoint.Y))
                    //            {
                    //                if (!m_ListPadShapes.Contains(s))
                    //                {
                    //                    //s.Stroke = new SolidColorBrush(Colors.Red);
                    //                    s.Fill = new SolidColorBrush() { Color = Colors.Red, Opacity = 0.3 };
                    //                    m_ListPadShapes.Add(s);
                    //                }
                    //            }
                    //        }
                    //    }
                    //if (p != m_CameraSelectAreaStartPoint && m_rectCameraSelectArea == null)
                    //    {
                    //        m_rectCameraSelectArea = new System.Windows.Shapes.Rectangle() { Stroke = new SolidColorBrush(Colors.Blue), StrokeThickness = 1 };
                    //        canvasCameraImage.Children.Add(m_rectCameraSelectArea);
                    //    }
                    //    if (m_rectCameraSelectArea == null)
                    //        return;
                    //    m_rectCameraSelectArea.Width = Math.Abs(p.X - m_CameraSelectAreaStartPoint.X);
                    //    m_rectCameraSelectArea.Height = Math.Abs(p.Y - m_CameraSelectAreaStartPoint.Y);
                    //    Canvas.SetLeft(m_rectCameraSelectArea, Math.Min(p.X, m_CameraSelectAreaStartPoint.X));
                    //    Canvas.SetTop(m_rectCameraSelectArea, Math.Min(p.Y, m_CameraSelectAreaStartPoint.Y));
                    //}
                    break;
            }
        }

        /// <summary>
        /// 相机图像窗口中鼠标左键按下
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void canvasCameraImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (rbSysProgram.IsChecked == false)
                return;

            switch (App.m_ShowState.EnumProgram)
            {
                case EnumProgram.MakeThumbnail:
                    System.Windows.Point pt = e.GetPosition(canvasCameraImage);
                    if (radioPosStart.IsChecked == true)
                    {//缩略图起点位置
                     // 建立缩略图时，缩略图窗口坐标系是与运动系统的坐标系对应的
                        System.Windows.Point thumbCameraCenter = new System.Windows.Point(0, 0); //缩略图第一张照片的中心位置，单位：毫米
                        thumbCameraCenter.X = App.m_Motion.MotionPositon[0] / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) + (canvasCameraImage.Width - pt.X) * Doc.m_SystemParam.HelicalPitch / Doc.m_SystemParam.HelicalPitchMap.X;
                        thumbCameraCenter.Y = App.m_Motion.MotionPositon[1] / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) + (canvasCameraImage.Height - pt.Y) * Doc.m_SystemParam.HelicalPitch / Doc.m_SystemParam.HelicalPitchMap.Y;
                        App.m_ShowState.CurrentProductType.PositionStart = thumbCameraCenter;

                        System.Windows.Point pPcbStart = new System.Windows.Point(0, 0);       //PCB左下角相对运动系统原点位置，单位mm
                        pPcbStart.X = thumbCameraCenter.X - (canvasCameraImage.Width / 2) * Doc.m_SystemParam.HelicalPitch / Doc.m_SystemParam.HelicalPitchMap.X;
                        pPcbStart.Y = thumbCameraCenter.Y - (canvasCameraImage.Height / 2) * Doc.m_SystemParam.HelicalPitch / Doc.m_SystemParam.HelicalPitchMap.Y;
                        App.m_ShowState.PositionStart = pPcbStart.ToString();
                        App.m_ShowState.CurrentProductType.PcbStart = pPcbStart;
                    }
                    else
                    {//缩略图终点位置
                        System.Windows.Point pPcbEnd = new System.Windows.Point(0, 0); //PCB板在运动坐标系中的物理位置，单位：毫米
                        pPcbEnd.X = App.m_Motion.MotionPositon[0] / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) + (canvasCameraImage.Width / 2 - pt.X) * Doc.m_SystemParam.HelicalPitch / Doc.m_SystemParam.HelicalPitchMap.X;
                        pPcbEnd.Y = App.m_Motion.MotionPositon[1] / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) + (canvasCameraImage.Height / 2 - pt.Y) * Doc.m_SystemParam.HelicalPitch / Doc.m_SystemParam.HelicalPitchMap.Y;
                        App.m_ShowState.PositionEnd = pPcbEnd.ToString();
                        App.m_ShowState.CurrentProductType.PcbEnd = pPcbEnd;
                    }
                    rectSelectVertical.SetValue(Canvas.LeftProperty, pt.X - 1);
                    rectSelectVertical.SetValue(Canvas.TopProperty, 0.0);
                    rectSelectHorizontal.SetValue(Canvas.LeftProperty, 0.0);
                    rectSelectHorizontal.SetValue(Canvas.TopProperty, pt.Y - 1);
                    break;
                case EnumProgram.PCBStartAdjust:
                    System.Windows.Point psa = e.GetPosition(canvasCameraImage);
                    // 缩略图窗口坐标系是与运动系统的坐标系对应的
                    System.Windows.Point cameraCenter = new System.Windows.Point(0, 0); //照片的中心位置，单位：毫米
                    cameraCenter.X = App.m_Motion.MotionPositon[0] / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) + (canvasCameraImage.Width - psa.X) * Doc.m_SystemParam.HelicalPitch / Doc.m_SystemParam.HelicalPitchMap.X;
                    cameraCenter.Y = App.m_Motion.MotionPositon[1] / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) + (canvasCameraImage.Height - psa.Y) * Doc.m_SystemParam.HelicalPitch / Doc.m_SystemParam.HelicalPitchMap.Y;
                    System.Windows.Point pPcbStartNew = new System.Windows.Point(0, 0); //PCB左下角相对运动系统原点位置，单位mm
                    pPcbStartNew.X = cameraCenter.X - (canvasCameraImage.Width / 2) * Doc.m_SystemParam.HelicalPitch / Doc.m_SystemParam.HelicalPitchMap.X;
                    pPcbStartNew.Y = cameraCenter.Y - (canvasCameraImage.Height / 2) * Doc.m_SystemParam.HelicalPitch / Doc.m_SystemParam.HelicalPitchMap.Y;
                    App.m_ShowState.PositionPCBNewStart = pPcbStartNew.ToString();
                    rectSelectVertical.SetValue(Canvas.LeftProperty, psa.X - 1);
                    rectSelectVertical.SetValue(Canvas.TopProperty, 0.0);
                    rectSelectHorizontal.SetValue(Canvas.LeftProperty, 0.0);
                    rectSelectHorizontal.SetValue(Canvas.TopProperty, psa.Y - 1);
                    break;
                case EnumProgram.LibMode:
                case EnumProgram.MarkSetup:
                case EnumProgram.MetaLibEdit:
                    if (cameraImage.ImageSource != null && e.ButtonState == MouseButtonState.Pressed)
                    {
                        m_CameraSelectAreaStartPoint = e.GetPosition(canvasCameraImage);

                        if (m_rectCameraSelectArea != null && canvasCameraImage.Children.Contains(m_rectCameraSelectArea))
                            canvasCameraImage.Children.Remove(m_rectCameraSelectArea);

                        m_rectCameraSelectArea = new System.Windows.Shapes.Rectangle
                        {
                            Stroke = System.Windows.Media.Brushes.LightBlue,
                            StrokeThickness = 1
                        };

                        Canvas.SetLeft(m_rectCameraSelectArea, m_CameraSelectAreaStartPoint.X);
                        Canvas.SetTop(m_rectCameraSelectArea, m_CameraSelectAreaStartPoint.Y);
                        canvasCameraImage.Children.Add(m_rectCameraSelectArea);
                    }
                    break;
                case EnumProgram.PathSetup:
                    m_CameraSelectAreaStartPoint = e.GetPosition(canvasCameraImage);
                    System.Windows.Shapes.Rectangle targetElement = Mouse.DirectlyOver as System.Windows.Shapes.Rectangle;
                    m_rCurrentRect = targetElement;
                    switch (App.m_ShowState.EnumMouseMode)
                    {
                        case EnumMouseMode.None:
                            break;
                        case EnumMouseMode.Select://手动画检测框
                            XElement xMeta = new XElement("Meta");
                            xMeta.Add(new XElement("Name", string.Format("N{0}", DateTime.Now.ToString("MMddHHmmss"))));
                            xMeta.Add(new XElement("IsManual", true));
                            xMeta.Add(new XElement("Position", new Point(canvasCameraImage.Width - m_CameraSelectAreaStartPoint.X, canvasCameraImage.Height - m_CameraSelectAreaStartPoint.Y)));
                            xMeta.Add(new XElement("Rotation", 0));
                            System.Windows.Shapes.Rectangle rect = new System.Windows.Shapes.Rectangle()
                            {
                                Name = (string)xMeta.Element("Name"),                                
                                Fill = new SolidColorBrush() { Color = Colors.Red, Opacity = 0.0 },
                                Stroke = new SolidColorBrush(Colors.White),
                                StrokeThickness = 3,
                                Width = 50,
                                Height = 50
                            };
                            Canvas.SetLeft(rect, m_CameraSelectAreaStartPoint.X - rect.Width / 2);
                            Canvas.SetTop(rect, m_CameraSelectAreaStartPoint.Y - rect.Height / 2);
                            canvasCameraImage.Children.Add(rect);
                            rect.DataContext = xMeta;

                            System.Windows.Controls.ContextMenu contextMenu = new System.Windows.Controls.ContextMenu();
                            System.Windows.Controls.MenuItem menuItem = new System.Windows.Controls.MenuItem() { Header = "添加测试" };
                            menuItem.Command = CustomCommand.CommandAddMeta;
                            contextMenu.Items.Add(menuItem);
                            menuItem = new System.Windows.Controls.MenuItem() { Header = "删除元件" };
                            menuItem.Click += DelTestMeta_Click;
                            contextMenu.Items.Add(menuItem);
                            menuItem = new System.Windows.Controls.MenuItem() { Header = "取消测试" };
                            menuItem.Click += TestCancle_Click;
                            contextMenu.Items.Add(menuItem);
                            menuItem = new System.Windows.Controls.MenuItem() { Header = "极性检测", IsCheckable = true, IsChecked = false };
                            if (rect.DataContext != null && rect.DataContext is XElement == true)
                            {
                                XElement xmeta = rect.DataContext as XElement;
                                if (xmeta.Element("Polarity") != null)
                                {
                                    bool b = (bool)xmeta.Element("Polarity");
                                    if (b)
                                        menuItem.IsChecked = true;
                                }
                            }
                            menuItem.Click += IsPolarity_Click;
                            contextMenu.Items.Add(menuItem);
                            menuItem = new System.Windows.Controls.MenuItem() { Header = "元件旋转" };
                            menuItem.Click += RotateMeta_Click;
                            contextMenu.Items.Add(menuItem);
                            rect.ContextMenu = contextMenu;
                            break;
                        case EnumMouseMode.Resize:
                            AdornerLayer layer = AdornerLayer.GetAdornerLayer(canvasCameraImage);
                            if (m_ThumbResizeAdorner != null)
                                layer.Remove(m_ThumbResizeAdorner);
                            if (Mouse.DirectlyOver is System.Windows.Shapes.Rectangle)
                            {
                                //System.Windows.Shapes.Rectangle targetElement = Mouse.DirectlyOver as System.Windows.Shapes.Rectangle;
                                m_ThumbResizeAdorner = new ThumbResizeAdorner(targetElement);
                                layer.Add(m_ThumbResizeAdorner);
                            }
                            break;
                        #region 元件模型
                        case EnumMouseMode.Diode:
                            double rotation = 0;
                            if (targetElement == null)
                                return;
                            XElement metaData = targetElement.DataContext as XElement;
                            if (metaData == null)
                                return;

                            metaData.SetElementValue("TypeName", EnumMouseMode.Diode);
                            targetElement.DataContext = metaData;

                            if (metaData.Element("Rotation") != null)
                            {
                                rotation = double.Parse((string)metaData.Element("Rotation"));
                            }
                            double pCenterX = Canvas.GetLeft(targetElement) + targetElement.Width / 2;
                            double pCenterY = Canvas.GetTop(targetElement) + targetElement.Height / 2;
                            System.Windows.Point p = new System.Windows.Point(pCenterX, pCenterY);
                            DrawDiode(p, targetElement.Width, targetElement.Height, rotation);



                            break;
                        case EnumMouseMode.Dynatron:

                            rotation = 0;
                            if (targetElement == null)
                                return;

                            metaData = targetElement.DataContext as XElement;
                            if (metaData == null)
                                return;

                            metaData.SetElementValue("TypeName", EnumMouseMode.Diode);
                            targetElement.DataContext = metaData;
                            if (metaData.Element("Rotation") != null)
                            {
                                rotation = double.Parse((string)metaData.Element("Rotation"));
                            }
                            pCenterX = Canvas.GetLeft(targetElement) + targetElement.Width / 2;
                            pCenterY = Canvas.GetTop(targetElement) + targetElement.Height / 2;
                            p = new System.Windows.Point(pCenterX, pCenterY);

                            DrawDynatron(p, targetElement.Width, targetElement.Height, rotation);


                            break;

                        case EnumMouseMode.IC13:
                            rotation = 0;
                            if (targetElement == null)
                                return;
                            metaData = targetElement.DataContext as XElement;
                            if (metaData == null)
                                return;
                            metaData.SetElementValue("TypeName", EnumMouseMode.Diode);
                            targetElement.DataContext = metaData;
                            if (metaData.Element("Rotation") != null)
                            {
                                rotation = double.Parse((string)metaData.Element("Rotation"));
                            }
                            pCenterX = Canvas.GetLeft(targetElement) + targetElement.Width / 2;
                            pCenterY = Canvas.GetTop(targetElement) + targetElement.Height / 2;
                            p = new System.Windows.Point(pCenterX, pCenterY);
                            DrawIC13(p, targetElement.Width, targetElement.Height, rotation);
                            break;
                        case EnumMouseMode.IC22:
                            rotation = 0;
                            if (targetElement == null)
                                return;
                            metaData = targetElement.DataContext as XElement;
                            if (metaData == null)
                                return;
                            metaData.SetElementValue("TypeName", EnumMouseMode.Diode);
                            targetElement.DataContext = metaData;
                            if (metaData.Element("Rotation") != null)
                            {
                                rotation = double.Parse((string)metaData.Element("Rotation"));
                            }
                            pCenterX = Canvas.GetLeft(targetElement) + targetElement.Width / 2;
                            pCenterY = Canvas.GetTop(targetElement) + targetElement.Height / 2;
                            p = new System.Windows.Point(pCenterX, pCenterY);
                            DrawIC22(p, targetElement.Width, targetElement.Height, rotation);
                            break;
                        case EnumMouseMode.IC23:
                            rotation = 0;
                            if (targetElement == null)
                                return;
                            metaData = targetElement.DataContext as XElement;
                            if (metaData == null)
                                return;
                            metaData.SetElementValue("TypeName", EnumMouseMode.Diode);
                            targetElement.DataContext = metaData;
                            if (metaData.Element("Rotation") != null)
                            {
                                rotation = double.Parse((string)metaData.Element("Rotation"));
                            }
                            pCenterX = Canvas.GetLeft(targetElement) + targetElement.Width / 2;
                            pCenterY = Canvas.GetTop(targetElement) + targetElement.Height / 2;
                            p = new System.Windows.Point(pCenterX, pCenterY);
                            DrawIC23(p, targetElement.Width, targetElement.Height, rotation);
                            break;
                        case EnumMouseMode.IC33:
                            rotation = 0;
                            if (targetElement == null)
                                return;
                            metaData = targetElement.DataContext as XElement;
                            if (metaData == null)
                                return;

                            metaData.SetElementValue("TypeName", EnumMouseMode.Diode);
                            targetElement.DataContext = metaData;
                            if (metaData.Element("Rotation") != null)
                            {
                                rotation = double.Parse((string)metaData.Element("Rotation"));
                            }
                            pCenterX = Canvas.GetLeft(targetElement) + targetElement.Width / 2;
                            pCenterY = Canvas.GetTop(targetElement) + targetElement.Height / 2;
                            p = new System.Windows.Point(pCenterX, pCenterY);
                            DrawIC33(p, targetElement.Width, targetElement.Height, rotation);
                            break;

                        case EnumMouseMode.IC2:
                             rotation = 0;
                            if (targetElement == null)
                                return;
                             metaData = targetElement.DataContext as XElement;
                            if (metaData == null)
                                return;

                            metaData.SetElementValue("TypeName", EnumMouseMode.IC4);
                            targetElement.DataContext = metaData;

                            if (metaData.Element("Rotation") != null)
                            {
                                rotation = double.Parse((string)metaData.Element("Rotation"));
                            }
                             pCenterX = Canvas.GetLeft(targetElement) + targetElement.Width / 2;
                             pCenterY = Canvas.GetTop(targetElement) + targetElement.Height / 2;
                            p = new System.Windows.Point(pCenterX, pCenterY);
                            DrawDiode(p, targetElement.Width, targetElement.Height, rotation);
                            break;
                            case EnumMouseMode.IC4:
                            rotation = 0;
                            if (targetElement == null)
                                return;
                            metaData = targetElement.DataContext as XElement;
                            if (metaData == null)
                                return;
                            metaData.SetElementValue("TypeName", EnumMouseMode.IC4);
                            targetElement.DataContext = metaData;
                            if (metaData.Element("Rotation") != null)
                            {
                                rotation = double.Parse((string)metaData.Element("Rotation"));
                            }
                            pCenterX = Canvas.GetLeft(targetElement) + targetElement.Width / 2;
                            pCenterY = Canvas.GetTop(targetElement) + targetElement.Height / 2;
                            p = new System.Windows.Point(pCenterX, pCenterY);
                            DrawIC4(p, targetElement.Width, targetElement.Height, rotation);
                            break;
                            #endregion 元件模型

                    }
                    break;
                case EnumProgram.DataInput:
                default:
                    break;
            }
        }

        /// <summary>
        /// 相机图像窗口中鼠标抬起
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void canvasCameraImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            switch (App.m_ShowState.EnumProgram)
            {
                case EnumProgram.LibMode:
                case EnumProgram.MarkSetup:
                    if (cameraImage.ImageSource != null && m_rectCameraSelectArea != null && (int)m_rectCameraSelectArea.Width > 0 && (int)m_rectCameraSelectArea.Height > 0)
                    {// 导程映射显示选择区域
                        if (e.ClickCount == 1)
                        {// 单击
                            var pos = e.GetPosition(canvasCameraImage);

                            // Set the position of rectangle
                            var x = Math.Min(pos.X, m_CameraSelectAreaStartPoint.X);
                            var y = Math.Min(pos.Y, m_CameraSelectAreaStartPoint.Y);
                            // Set the dimenssion of the rectangle
                            var w = Math.Max(pos.X, m_CameraSelectAreaStartPoint.X) - x;
                            var h = Math.Max(pos.Y, m_CameraSelectAreaStartPoint.Y) - y;

                            m_rectCameraSelectArea.Width = w;
                            m_rectCameraSelectArea.Height = h;

                            Canvas.SetLeft(m_rectCameraSelectArea, x);
                            Canvas.SetTop(m_rectCameraSelectArea, y);
                        }
                        else if (e.ClickCount == 2)
                        {// 双击

                        }
                    }
                    break;
                case EnumProgram.PathSetup:
                    if (m_rectCameraSelectArea != null)
                    {
                        canvasCameraImage.Children.Remove(m_rectCameraSelectArea);
                        m_rectCameraSelectArea = null;
                    }
                    break;
            }
        }

        private void cameraImage_Changed(object sender, EventArgs e)
        {
            // 背景的消隐与显示根据cameraImage.ImageSource是否为空
            if (recCameraImageBkg == null)
                return;

            if (cameraImage.ImageSource != null)
            {
                if (recCameraImageBkg.Visibility == Visibility.Visible)
                    recCameraImageBkg.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (recCameraImageBkg.Visibility == Visibility.Collapsed)
                    recCameraImageBkg.Visibility = Visibility.Visible;
            }
            canvasCameraImage.Children.Clear();
            if (m_PadRectData.Elements("Pad") != null)
                m_PadRectData.Elements("Pad").Remove();

            switch (App.m_Motion.MotionType)
            {
                case MotionType.TestRouteEdit:
                    if (App.m_Motion.XPath != null)
                        Edit_TestRoute(App.m_Motion.XPath.Element("TestCell"));
                    App.m_Motion.MotionType = MotionType.None;
                    break;
            }
        }

        #endregion 相机图像窗口

        #region 缩略图图像窗口

        private System.Windows.Shapes.Rectangle m_rectThumbnailSelectArea;
        private System.Windows.Point m_ThumbnailSelectAreaStartPoint;
        private System.Windows.Shapes.Rectangle m_rectThumbnailAddArea;//新增的子板区域
        private bool m_bIsSelectedOK = false;//指示拼版单板区域是否选择完成（防止拼版区域选择后，鼠标误操作重新回重复选择）
        /// <summary>
        /// 程序缩略图中左键双击鼠标，移动相机到当前位置进行拍照
        /// </summary>
        private void canvasProgramThumbnail_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            m_ThumbnailSelectAreaStartPoint = e.GetPosition(canvasProgramThumbnail);
            //m_rectThumbnailSelectArea = null;
            if (e.ClickCount == 2)
            {// 判断鼠标双击
                if (App.m_Motion.MotionEnable() == false)
                    return;

                System.Windows.Point pos = e.GetPosition(canvasProgramThumbnail);
                if (ThumbnailImage.ImageSource == null)
                {//映射到行程区域：缩略图窗口坐标系是与运动系统的坐标系对应的
                    // 随动：鼠标在缩略图区双击控制运动系统运动
                    int iPosX = (int)(pos.X / canvasProgramThumbnail.Width * Doc.m_SystemParam.AxisMaxRun_X * (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch));
                    int iPosY = (int)(pos.Y / canvasProgramThumbnail.Height * Doc.m_SystemParam.AxisMaxRun_Y * (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch));

                    App.m_Motion.MotionPositon = new int[] { iPosX, iPosY };
                    App.m_Motion.PointMotion(iPosX, iPosY, true);
                }
                else
                {//映射到缩略图区域：缩略图窗口坐标系是缩略图图像的坐标系开始点，与运动系统中电路板的坐标系是对应的
                    if (App.m_ShowState.CurrentProductType.XElement != null)
                    {
                        System.Windows.Point p = new System.Windows.Point();
                        p.X = App.m_ShowState.CurrentProductType.PcbStart.X + (App.m_ShowState.CurrentProductType.ThumbnailWidth - pos.X) / App.m_ShowState.CurrentProductType.ThumbnailWidth * App.m_ShowState.CurrentProductType.MotionWidth;
                        p.Y = App.m_ShowState.CurrentProductType.PcbStart.Y + (App.m_ShowState.CurrentProductType.ThumbnailHeight - pos.Y) / App.m_ShowState.CurrentProductType.ThumbnailHeight * App.m_ShowState.CurrentProductType.MotionHeight;

                        //不允许超越范围
                        if (p.X < App.m_ShowState.CurrentProductType.PositionStart.X)
                            p.X = App.m_ShowState.CurrentProductType.PositionStart.X;

                        if (p.Y < App.m_ShowState.CurrentProductType.PositionStart.Y)
                            p.Y = App.m_ShowState.CurrentProductType.PositionStart.Y;

                        if (p.X > Doc.m_SystemParam.AxisMaxRun_X || p.Y > Doc.m_SystemParam.AxisMaxRun_Y)

                        {
                            MessageBox.Show("超出运动极限区间，不允许！",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        }

                        App.m_Motion.MotionPositon = new int[] { (int)(p.X * (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch)), (int)(p.Y * (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch)) };
                        App.m_Motion.PointMotion((int)(p.X * (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch)), (int)(p.Y * (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch)), true);
                    }
                }

            }
        }

        private void canvasProgramThumbnail_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && checkboxMulti.IsChecked == true)
            {
                System.Windows.Point p = e.GetPosition(canvasProgramThumbnail);

                if (p != m_ThumbnailSelectAreaStartPoint && m_rectThumbnailSelectArea == null)
                {
                    m_rectThumbnailSelectArea = new System.Windows.Shapes.Rectangle() { Stroke = new SolidColorBrush(Colors.Red), StrokeThickness = 5 };
                    canvasProgramThumbnail.Children.Add(m_rectThumbnailSelectArea);
                }
                if (m_rectThumbnailSelectArea == null || m_bIsSelectedOK == true)
                    return;
                m_rectThumbnailSelectArea.Width = Math.Abs(p.X - m_ThumbnailSelectAreaStartPoint.X);
                m_rectThumbnailSelectArea.Height = Math.Abs(p.Y - m_ThumbnailSelectAreaStartPoint.Y);
                Canvas.SetLeft(m_rectThumbnailSelectArea, Math.Min(p.X, m_ThumbnailSelectAreaStartPoint.X));
                Canvas.SetTop(m_rectThumbnailSelectArea, Math.Min(p.Y, m_ThumbnailSelectAreaStartPoint.Y));
            }

        }

        private void OnClick_SelectOK(object sender, RoutedEventArgs e)
        {
            if (m_bIsSelectedOK == false)
            {
                m_bIsSelectedOK = true;
                btn_SelectArea.Content = "开始选择";
            }
            else
            {
                m_bIsSelectedOK = false;
                btn_SelectArea.Content = "选择完成";
            }
        }

        /// <summary>
        /// 缩略图图像更改：缩略图图像改变时自动调用，缩略图图像为空时显示背景图像，缩略图图像不为空时显示缩略图图像
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ThumbnailImage_Changed(object sender, EventArgs e)
        {
            if (recAxisMaxRunBkg == null)
                return;

            if (ThumbnailImage.ImageSource != null)
            {
                canvasProgramThumbnail.Width = ThumbnailImage.ImageSource.Width;
                canvasProgramThumbnail.Height = ThumbnailImage.ImageSource.Height;
                if (recAxisMaxRunBkg.Visibility != Visibility.Collapsed)
                    recAxisMaxRunBkg.Visibility = Visibility.Collapsed;
            }
            else
            {
                canvasProgramThumbnail.Width = Doc.m_SystemParam.AxisMaxRun_X * MotionDMC5000.MAP_ThumbnailPixel;
                canvasProgramThumbnail.Height = Doc.m_SystemParam.AxisMaxRun_Y * MotionDMC5000.MAP_ThumbnailPixel;
                if (recAxisMaxRunBkg.Visibility != Visibility.Visible)
                    recAxisMaxRunBkg.Visibility = Visibility.Visible;
            }

            gridProgramTypeData.IsEnabled = ThumbnailImage.ImageSource != null ? true : false;
        }

        #endregion 缩略图图像窗口

        #endregion 鼠标操作

        #region 编程

        private void radioLib_Checked(object sender, RoutedEventArgs e)
        {
            Doc.LoadLibFile(App.m_ShowState.LibMode);
        }

        #region 产品型号编辑

        /// <summary>
        /// 增加当前修改的内容到列表
        /// </summary>
        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrEmpty(ProductTypeEditComboBox.Text))
            {
                MessageBox.Show("产品名称设置为空，不允许添加！",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            if (ProductType_IsExist(ProductTypeEditComboBox.Text))
                return;
            TestProduct testProduct = new TestProduct() { XElement = new XElement("Product"), TypeName = ProductTypeEditComboBox.Text, FileName = ProductTypeEditComboBox.Text + ".xml" };
            Doc.m_TestProductList.AddProductType(testProduct.XElement);
            m_TestProductType = new TestProductType();
            if (m_TestProductType.XElement == null)
                m_TestProductType.XElement = new XElement("ProductType");
            m_TestProductType.Name = ProductTypeEditComboBox.Text;
            double lineWidth = LTDMC.dmc_get_encoder(App.m_Motion.CardID, 2);
            //m_TestProductType.LineWidth= lineWidth / Doc.m_SystemParam.PulsePerCircle * Doc.m_SystemParam.LineBodyPitch +Doc.m_SystemParam.LineBodyInitialWidth;
            Doc.m_SystemParam.ProductFilename = testProduct.FileName;
            m_TestProductType.XElement.Save(string.Format(Doc.m_SystemParam.SetupPath + "\\" + Doc.m_SystemParam.ProductFilename));

            Doc.SaveProducListFile();
            App.m_ShowState.ProductTypes = Doc.m_TestProductList.ProductList;
            foreach (TestProduct type in ProductTypeEditComboBox.Items)
            {
                if (type.TypeName == ProductTypeEditComboBox.Text)
                {
                    ProductTypeEditComboBox.SelectedItem = type;
                    break;
                }
            }
        }

        /// <summary>
        /// 从列表中删除当前选择项
        /// </summary>
        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (ProductTypeEditComboBox.SelectedItem == null)
            {
                MessageBox.Show("没有选择的产品型号，不允许删除！",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            if (MessageBox.Show(String.Format("真的要删除产品型号：\"{0}\"及其包含的所以数据？", ProductTypeEditComboBox.SelectedItem.ToString()),
                            Doc.MESSAGE_SOFTNAME,
                            MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.Yes)
            {
                //Doc.m_TestProgram.DeleteProductType((TestProductType)ProductTypeEditComboBox.SelectedItem);
                //DataBase.DeleteProductType((TestProduct)ProductTypeEditComboBox.SelectedItem);
                Doc.m_TestProductList.DeleteProduct((TestProduct)ProductTypeEditComboBox.SelectedItem);
                File.Delete(string.Format(Doc.m_SystemParam.SetupPath + "\\" + Doc.m_SystemParam.ProductFilename));
                //App.m_ShowState.ProductTypes = DataBase.GetProducts();
                Doc.SaveProducListFile();
                App.m_ShowState.ProductTypes = Doc.m_TestProductList.ProductList;
                if (ProductTypeEditComboBox.Items.Count > 0)
                    ProductTypeEditComboBox.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// 修改列表中的当前选择项
        /// </summary>
        private void BtnModify_Click(object sender, RoutedEventArgs e)
        {
            if (m_TestProductType == null)
            {
                MessageBox.Show("没有选择的产品型号，不允许修改！",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            if (String.IsNullOrEmpty(ProductTypeEditComboBox.Text))
            {
                MessageBox.Show("产品型号名称设置为空，不允许修改！",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            if (ProductType_IsExist(ProductTypeEditComboBox.Text))
                return;
            File.Delete(string.Format(Doc.m_SystemParam.SetupPath + "\\" + Doc.m_SystemParam.ProductFilename));
            foreach (TestProduct product in Doc.m_TestProductList.ProductList)
            {
                if (product.TypeName == m_TestProductType.Name)
                {
                    product.TypeName = ProductTypeEditComboBox.Text;
                    product.FileName = string.Format("{0}.xml", ProductTypeEditComboBox.Text);
                    break;
                }
            }
            Doc.m_SystemParam.ProductFilename = string.Format("{0}.xml", ProductTypeEditComboBox.Text);
            m_TestProductType.Name = ProductTypeEditComboBox.Text;
            m_TestProductType.XElement.Save(string.Format(Doc.m_SystemParam.SetupPath + "\\" + Doc.m_SystemParam.ProductFilename));
            Doc.SaveProducListFile();
            App.m_ShowState.ProductTypes = Doc.m_TestProductList.ProductList;
            foreach (TestProduct type in ProductTypeEditComboBox.Items)
            {
                if (type.TypeName == m_TestProductType.Name)
                {
                    ProductTypeEditComboBox.SelectedItem = type;
                    break;
                }
            }
        }

        /// <summary>
        /// 列表中的当前选择项改变
        /// </summary>
        private void ProductTypeEditComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TestProduct testProduct = (TestProduct)ProductTypeEditComboBox.SelectedItem;
            if (testProduct != null)
            {
                Doc.m_SystemParam.ProductFilename = testProduct.FileName;
                m_TestProductType = new TestProductType() { XElement = Doc.LoadTestProgramFile() };
                //App.m_ShowState.PositionStart = m_TestProductType.PcbStart.ToString();
                App.m_ShowState.PositionPCBNewStart = m_TestProductType.PcbStart.ToString();
                App.m_ShowState.PositionEnd = m_TestProductType.PcbEnd.ToString();
                App.m_ShowState.BarcodeMode = m_TestProductType.BarcodeMode;
                App.m_ShowState.CurrentProductType = m_TestProductType;
                Doc.m_SystemParam.CurrentProductType = testProduct.TypeName;
                int lineBodyWidthPulse = (int)((m_TestProductType.LineWidth - Doc.m_SystemParam.LineBodyInitialWidth) * Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.LineBodyPitch);
                m_bIsSelectedOK = false;
                //App.m_Motion.LineBodyChangeWidthByPulse(lineBodyWidthPulse);
                CanvasProgramThumbnailShow();
            }
            gridProgramTypeContent.IsEnabled = m_TestProductType != null ? true : false;

            App.m_ShowState.EnumProgram = EnumProgram.None;


        }

        private bool ProductType_IsExist(string sName)
        {
            bool bRet = false;
            foreach (TestProduct type in ProductTypeEditComboBox.Items)
            {
                if (type.TypeName == sName)
                {
                    MessageBox.Show("同名的产品型号已存在！",
                                    Doc.MESSAGE_SOFTNAME,
                                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    bRet = true;
                    break;
                }
            }
            return bRet;
        }
        #endregion 产品型号编辑

        #region 线体
        private void radio_LineOnState_Checked(object sender, RoutedEventArgs e)
        {
            App.m_LineBodyControl.StateChange(StateLineBodyDebug.LineStateInit);//切换到进板模式时，进板状态初始到检测进板过程
            if (radio_LineOnState.IsChecked == true)
            {
                if (App.m_ShowState.CurrentProductType.BarcodeMode == EnumBarcodeMode.Scanner)
                    LTDMC.dmc_write_outbit(App.m_Motion.CardID, MotionDMC5000.OUTPUT_ScannerTrigger, MotionDMC5000.LEVEL_LOW);
            }
            else
            {
                if (App.m_ShowState.CurrentProductType.BarcodeMode == EnumBarcodeMode.Scanner)
                    LTDMC.dmc_write_outbit(App.m_Motion.CardID, MotionDMC5000.OUTPUT_ScannerTrigger, MotionDMC5000.LEVEL_HIGH);
            }
            DebugBtn_ShowState();
        }


        //private void radio_LineOnState_Checked(object sender, RoutedEventArgs e)
        //{
        //    App.m_LineBodyControl.StateChange(StateLineBodyDebug.LineStateInit);//切换到进板模式时，进板状态初始到检测进板过程

        //    if (App.m_ShowState.CurrentProductType.BarcodeMode == EnumBarcodeMode.Scanner)
        //        LTDMC.dmc_write_outbit(App.m_Motion.CardID, MotionDMC5000.OUTPUT_ScannerTrigger, MotionDMC5000.LEVEL_LOW);
        //    DebugBtn_ShowState();
        //}

        //private void radio_LineOnState_Unchecked(object sender, RoutedEventArgs e)
        //{
        //    if (App.m_ShowState.CurrentProductType.BarcodeMode == EnumBarcodeMode.Scanner)
        //        LTDMC.dmc_write_outbit(App.m_Motion.CardID, MotionDMC5000.OUTPUT_ScannerTrigger, MotionDMC5000.LEVEL_HIGH);
        //    DebugBtn_ShowState();
        //}

        private void DebugBtn_ShowState()
        {
            if (Doc.m_SystemUser.UserLevel >= UserLevel.SuperUser)
                return;

            if (Doc.m_SystemParam.LineOnState)
                DebugBtn.Visibility = Visibility.Collapsed;
            else DebugBtn.Visibility = Visibility.Visible;
        }

        #endregion 线体

        #region 二维码模式设置

        private void radioBarcode_Checked(object sender, RoutedEventArgs e)
        {
            if (App.m_ShowState.CurrentProductType.XElement != null)
            {
                App.m_ListBarcode.Clear();
                if (App.m_ShowState.BarcodeMode == EnumBarcodeMode.None)
                {
                    LTDMC.dmc_write_outbit(App.m_Motion.CardID, MotionDMC5000.OUTPUT_ScannerTrigger, MotionDMC5000.LEVEL_HIGH);
                }
                else
                {
                    LTDMC.dmc_write_outbit(App.m_Motion.CardID, MotionDMC5000.OUTPUT_ScannerTrigger, MotionDMC5000.LEVEL_LOW);
                }
            }
        }
        #endregion 二维码模式设置

        #region 缩略图制作

        /// <summary>
        /// 缩略图制作：IsChecked为true时去掉缩略图窗口图像的显示，false时如果有当前型号的缩略图图像则显示在缩略图窗口中；缩略图生成完成自动显示图像
        /// </summary>
        private void RadioButton_MakeThumbnail_Click(object sender, RoutedEventArgs e)
        {
            if (radioMakeThumbnail.IsChecked == true)
            {//删除缩略图窗口中缩略图的显示
                if (ThumbnailImage.ImageSource != null)
                    ThumbnailImage.ImageSource = null;
            }
            else
            {//在缩略图窗口中显示缩略图
                CanvasProgramThumbnailShow();
            }
        }

        /// <summary>
        /// 缩略图制作：IsChecked为true时去掉缩略图窗口图像的显示，false时如果有当前型号的缩略图图像则显示在缩略图窗口中；缩略图生成完成自动显示图像
        /// </summary>
        private void radioMakeThumbnail_Unchecked(object sender, RoutedEventArgs e)
        {//在缩略图窗口中显示缩略图
            CanvasProgramThumbnailShow();
        }

        /// <summary>
        /// 缩略图制作：IsChecked为true时去掉缩略图窗口图像的显示，false时如果有当前型号的缩略图图像则显示在缩略图窗口中；缩略图生成完成自动显示图像
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void radioMakeThumbnail_Checked(object sender, RoutedEventArgs e)
        {//删除缩略图窗口中缩略图的显示
            if (ThumbnailImage.ImageSource != null)
            {
                ThumbnailImage.ImageSource = null;
                canvasProgramThumbnail.Children.Clear();
            }
        }

        /// <summary>
        /// 缩略图显示
        /// </summary>
        private void CanvasProgramThumbnailShow()
        {
            if (!this.Dispatcher.CheckAccess()) // CheckAccess returns true if you're on the dispatcher thread
            {
                this.Dispatcher.BeginInvoke(new DelegateNoneParameter(CanvasProgramThumbnailShow));
                return;
            }

            ThumbnailImage.ImageSource = null;

            if (App.m_ShowState.CurrentProductType.XElement != null)
            {
                if (App.m_ShowState.CurrentProductType.Thumbnail != null)
                {
                    string sSample = Doc.m_SystemParam.SetupPath + "\\Thumbnail\\" + App.m_ShowState.CurrentProductType.Thumbnail;
                    if (File.Exists(sSample))
                    {
                        BitmapImage bmpImage = new BitmapImage(new Uri(sSample));
                        ThumbnailImage.ImageSource = bmpImage;
                    }
                }
            }

            radioMakeThumbnail.IsChecked = false;
        }

        /// <summary>
        /// 生成缩略图
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnThumbnailCreate_Click(object sender, RoutedEventArgs e)
        {
            if (App.m_ShowState.CurrentProductType.XElement == null)
                return;

            string filename = Doc.m_SystemParam.SetupPath + "\\Thumbnail\\" + String.Format("{0}.bmp", App.m_ShowState.CurrentProductType.Name); ;
            if (File.Exists(filename))
            {
                if (MessageBox.Show("类型已存在，替换？",
                                  Doc.MESSAGE_SOFTNAME,
                                  MessageBoxButton.OKCancel, MessageBoxImage.Exclamation) == MessageBoxResult.Cancel)
                {
                    return;
                }
            }
            if (App.m_ShowState.CurrentProductType.PcbEnd.X <= App.m_ShowState.CurrentProductType.PcbStart.X || App.m_ShowState.CurrentProductType.PcbEnd.Y <= App.m_ShowState.CurrentProductType.PcbStart.Y)
            {
                MessageBox.Show("缩略图起点或终点选择错误！",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            if (App.m_Motion.MotionEnable() == false)
                return;

            double dCameraWidth = canvasCameraImage.Width * Doc.m_SystemParam.HelicalPitch / Doc.m_SystemParam.HelicalPitchMap.X; //一副照片对应的物理宽度，单位：毫米
            double dCameraHeight = canvasCameraImage.Height * Doc.m_SystemParam.HelicalPitch / Doc.m_SystemParam.HelicalPitchMap.Y; //一副照片对应的物理高度，单位：毫米

            XElement xPath = new XElement("Path");
            int iNumHorizontal = (int)((App.m_ShowState.CurrentProductType.PcbEnd.X - App.m_ShowState.CurrentProductType.PcbStart.X) / dCameraWidth + 1); // 缩略图上水平拍照个数
            int iNumVertical = (int)((App.m_ShowState.CurrentProductType.PcbEnd.Y - App.m_ShowState.CurrentProductType.PcbStart.Y) / dCameraHeight + 1); // 缩略图上垂直拍照个数
            bool bDirect = false;
            //X轴运动方向：false：从小到大运动；true：从大到小运动
            for (int y = 0; y < iNumVertical; y++)
            {
                for (int x = 0; x < iNumHorizontal; x++)
                {
                    int iX = bDirect == false ? ((int)((Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) * (App.m_ShowState.CurrentProductType.PositionStart.X + x * dCameraWidth))) : ((int)((Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) * (App.m_ShowState.CurrentProductType.PositionStart.X + (iNumHorizontal - x - 1) * dCameraWidth)));
                    XElement xPoint = new XElement("Point",
                                            new XElement("Take", true),          //到位是否触发拍照
                                            new XElement("X", iX),   //X方向行程，单位：脉冲, 10mm对应10000脉冲
                                            new XElement("Y", (int)((Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) * (App.m_ShowState.CurrentProductType.PositionStart.Y + y * dCameraHeight)))    //Y方向行程，单位：脉冲
                                            );
                    xPath.Add(xPoint);
                }

                bDirect = !bDirect;
            }


            //for (int x = 0; x < iNumHorizontal; x++)
            //{
            //    for (int y = 0; y < iNumVertical; y++)
            //    {
            //        int iY = bDirect == false ? ((int)((Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) * (App.m_ShowState.CurrentProductType.PositionStart.Y + y * dCameraHeight))) : ((int)((Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) * (App.m_ShowState.CurrentProductType.PositionStart.Y + (iNumVertical - y - 1) * dCameraHeight)));
            //        XElement xPoint = new XElement("Point",
            //                                new XElement("Take", true),          //到位是否触发拍照
            //                                new XElement("Y", iY),   //X方向行程，单位：脉冲, 10mm对应10000脉冲
            //                                new XElement("X", (int)((Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) * (App.m_ShowState.CurrentProductType.PositionStart.X + x * dCameraWidth)))    //Y方向行程，单位：脉冲
            //                                );
            //        xPath.Add(xPoint);
            //    }

            //    bDirect = !bDirect;
            //}

            //int iSubThumbWide = (int)dCameraWidth * MotionDMC5000.MAP_ThumbnailPixel;
            //int iSubThumbHeight = (int)dCameraHeight * MotionDMC5000.MAP_ThumbnailPixel;

            int iSubThumbWide = (int)(canvasCameraImage.Width / m_iScale);
            int iSubThumbHeight = (int)(canvasCameraImage.Height / m_iScale);
            int iThumbWide = iSubThumbWide * iNumHorizontal; // 缩略图宽，单位：像素
            int iThumbHeight = iSubThumbHeight * iNumVertical; // 缩略图高，单位：像素

            App.m_ShowState.CurrentProductType.Thumbnail = String.Format("{0}.bmp", App.m_ShowState.CurrentProductType.Name);
            App.m_ShowState.CurrentProductType.MotionWidth = dCameraWidth * iNumHorizontal;
            App.m_ShowState.CurrentProductType.MotionHeight = dCameraHeight * iNumVertical;
            App.m_ShowState.CurrentProductType.ThumbnailWidth = iThumbWide;
            App.m_ShowState.CurrentProductType.ThumbnailHeight = iThumbHeight;
            App.m_ShowState.CurrentProductType.ThumbnailSubWidth = iSubThumbWide;
            App.m_ShowState.CurrentProductType.ThumbnailSubHeight = iSubThumbHeight;

            Doc.m_ThumbnailSet = new ThumbnailSet(iThumbWide, iThumbHeight, iSubThumbWide, iSubThumbHeight);
            Doc.m_ThumbnailSet.Filename = Doc.m_SystemParam.SetupPath + "\\Thumbnail\\" + App.m_ShowState.CurrentProductType.Thumbnail;

            xPath.SetElementValue("Index", 0);
            xPath.SetElementValue("NumHorizontal", iNumHorizontal);
            xPath.SetElementValue("NumVertical", iNumVertical);

            if (App.m_Motion.MotionEnable() == false)
                return;
            App.m_Motion.XPath = xPath;
            App.m_Motion.MotionType = MotionType.ThumbnailImage;
            App.m_Motion.MotionThread_Start();
        }

        #endregion 缩略图制作

        #region Mark点设置

        /// <summary>
        /// MARK点保存
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnClick_MarkSave(object sender, RoutedEventArgs e)
        {
            if (m_rectCameraSelectArea == null || canvasCameraImage.Children.Contains(m_rectCameraSelectArea) == false || cameraImage.ImageSource == null)
            {
                MessageBox.Show("请选择MARK区域！",
                           Doc.MESSAGE_SOFTNAME,
                           MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            double dWidth = m_rectCameraSelectArea.Width;
            double dHeight = m_rectCameraSelectArea.Height;
            double dXPos = Canvas.GetLeft(m_rectCameraSelectArea);
            double dYPos = Canvas.GetTop(m_rectCameraSelectArea);

            // Mark点相对PCB起点的位置
            double dMarkX = App.m_Motion.MotionPositon[0] / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) - canvasCameraImage.Width / 2 * Doc.m_SystemParam.HelicalPitch / Doc.m_SystemParam.HelicalPitchMap.X + (canvasCameraImage.Width - dXPos - dWidth / 2) * Doc.m_SystemParam.HelicalPitch / Doc.m_SystemParam.HelicalPitchMap.X - App.m_ShowState.CurrentProductType.PcbStart.X;
            double dMarkY = App.m_Motion.MotionPositon[1] / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) - canvasCameraImage.Height / 2 * Doc.m_SystemParam.HelicalPitch / Doc.m_SystemParam.HelicalPitchMap.Y + (canvasCameraImage.Height - dYPos - dHeight / 2) * Doc.m_SystemParam.HelicalPitch / Doc.m_SystemParam.HelicalPitchMap.Y - App.m_ShowState.CurrentProductType.PcbStart.Y;
            //从图像获得BYTES进行转换，保存为XML
            System.Drawing.Bitmap bmpImg = GetBitmapFromBitmapSource(((BitmapSource)cameraImage.ImageSource));
            if (bmpImg == null)
                return;
            //string sFilename = AppDomain.CurrentDomain.BaseDirectory + "Picture\\Mark.bmp";
            //bmpImg.Save(sFilename);
            //获得子图像
            System.Drawing.Bitmap bmpSubImg = OpenCVUtility.GetSubBitmap_Matching(bmpImg, new System.Drawing.Rectangle((int)dXPos, (int)dYPos, (int)dWidth, (int)dHeight));
            if (bmpSubImg == null)
                return;
            String sMarkImage = Convert_Bitmap2String(bmpSubImg);
            //bmpSubImg.Save(sFilename);
            //System.Drawing.Bitmap bm = Convert_String2Bitmap(sStr);
            if (radioMark1.IsChecked == true)
            {
                App.m_ShowState.CurrentProductType.MarkOnePos = new System.Windows.Point(dMarkX, dMarkY);
                App.m_ShowState.CurrentProductType.MarkOneImage = sMarkImage;
            }
            else //if (radioMark2.IsChecked == true)
            {
                App.m_ShowState.CurrentProductType.MarkTwoPos = new System.Windows.Point(dMarkX, dMarkY);
                App.m_ShowState.CurrentProductType.MarkTwoImage = sMarkImage;
            }

            if (m_rectCameraSelectArea != null && canvasCameraImage.Children.Contains(m_rectCameraSelectArea))
                canvasCameraImage.Children.Remove(m_rectCameraSelectArea);
            MessageBox.Show(String.Format("Mark{0}保存成功！", radioMark1.IsChecked == true ? 1 : 2),
                       Doc.MESSAGE_SOFTNAME,
                       MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }

        /// <summary>
        /// Mark点删除
        /// </summary>
        private void OnClick_MarkDelete(object sender, RoutedEventArgs e)
        {
            if (radioMark1.IsChecked == true)
            {
                App.m_ShowState.CurrentProductType.MarkOnePos = new System.Windows.Point(0, 0);
                App.m_ShowState.CurrentProductType.MarkOneImage = "";
            }
            else //if (radioMark2.IsChecked == true)
            {
                App.m_ShowState.CurrentProductType.MarkTwoPos = new System.Windows.Point(0, 0);
                App.m_ShowState.CurrentProductType.MarkTwoImage = "";
            }

            MessageBox.Show(String.Format("Mark{0}删除成功！", radioMark1.IsChecked == true ? 1 : 2),
                       Doc.MESSAGE_SOFTNAME,
                       MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }




        System.Windows.Point m_pMarkOnePos = new System.Windows.Point(0, 0);
        System.Windows.Point m_pMarkTwoPos = new System.Windows.Point(0, 0);


        /// <summary>
        /// 通过Mark点校正坐标文件中的坐标
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnClick_MarkCalibrate(object sender, RoutedEventArgs e)
        {
            double anglePos = Math.Atan2((m_pMarkTwoPos.Y - m_pMarkOnePos.Y), (m_pMarkTwoPos.X - m_pMarkOnePos.X));//坐标文件中两个Mark点所在直线与水平线的夹角
            double angleImage = Math.Atan2((App.m_ShowState.CurrentProductType.MarkTwoPos.Y - App.m_ShowState.CurrentProductType.MarkOnePos.Y), (App.m_ShowState.CurrentProductType.MarkTwoPos.X - App.m_ShowState.CurrentProductType.MarkOnePos.X));//相机拍照的两个Mark点所在直线与水平线的夹角
            double angel = angleImage - anglePos;
            System.Windows.Point pcbStart = App.m_ShowState.CurrentProductType.PcbStart;
            foreach (XElement meta in App.m_ShowState.CurrentProductType.XMetaData.Elements("Meta"))
            {
                double x = System.Windows.Point.Parse((string)meta.Element("Position")).X;
                double y = System.Windows.Point.Parse((string)meta.Element("Position")).Y;
                double newX = (x - pcbStart.X) * Math.Cos(angel) - (y - pcbStart.Y) * Math.Sin(angel) + pcbStart.X;
                double newY = (x - pcbStart.X) * Math.Sin(angel) + (y - pcbStart.Y) * Math.Cos(angel) + pcbStart.Y;
                meta.SetElementValue("Position", new System.Windows.Point(newX, newY));
            }
            MessageBox.Show("校正成功");
        }

        public static System.Drawing.Bitmap GetBitmapFromBitmapSource(BitmapSource source)
        {
            System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(source.PixelWidth, source.PixelHeight, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

            System.Drawing.Imaging.BitmapData data = bmp.LockBits(new System.Drawing.Rectangle(System.Drawing.Point.Empty, bmp.Size), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            source.CopyPixels(Int32Rect.Empty, data.Scan0, data.Height * data.Stride, data.Stride);
            bmp.UnlockBits(data);
            return bmp;
        }

        public static String Convert_Bitmap2String(System.Drawing.Bitmap bmpImg)
        {
            if (bmpImg == null)
                return null;

            System.IO.MemoryStream ms = new System.IO.MemoryStream();
            bmpImg.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
            return Convert.ToBase64String(ms.GetBuffer());
        }

        public static System.Drawing.Bitmap Convert_String2Bitmap(String imgStr)
        {
            if (String.IsNullOrEmpty(imgStr) == true)
                return null;

            byte[] reqData = Convert.FromBase64String(imgStr);
            System.IO.MemoryStream ms = new System.IO.MemoryStream();
            ms = new System.IO.MemoryStream(reqData);
            return (System.Drawing.Bitmap)System.Drawing.Image.FromStream(ms);
        }

        #endregion Mark点设置  

        #region PCB起点校正

        /// <summary>
        /// PCB起点校正：因COPY产品型号设置参数时，不同机器的PCB起点会有偏差，所以需要进行PCB起点重新校正
        /// 实现过程：
        ///     1、点击缩略图的起点位置，移动运动系统拍照，在照片上选择新的起点位置
        ///     2、根据MARK点和新的起点位置识别获得MARK点偏移
        ///     3、根据公式计算：PCB新的起点位置 = 新选择的PCB起点位置 + MARK点偏移
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnPCBStartAdjust_Click(object sender, RoutedEventArgs e)
        {
            if (App.m_ShowState.CurrentProductType.XElement == null)
            {
                MessageBox.Show("产品型号设置为空，不允许PCB起点校正！",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            if (App.m_Motion.MotionEnable() == false)
                return;
            //优化MARK点
            if (MarkOptimize() <= 0)
            {
                MessageBox.Show("MARK点设置为空，不允许PCB起点校正！",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            //增加MARK点
            System.Windows.Point positionPCBNewStart = System.Windows.Point.Parse(App.m_ShowState.PositionPCBNewStart);
            XElement xRoute = new XElement("Path");
            int iPosX = (int)((App.m_ShowState.CurrentProductType.MarkOnePos.X + positionPCBNewStart.X) * (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch));
            int iPosY = (int)((App.m_ShowState.CurrentProductType.MarkOnePos.Y + positionPCBNewStart.Y) * (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch));
            XElement xPoint = new XElement("Point",
                            new XElement("Mark", true),          //Mark点位置
                            new XElement("Take", true),          //到位是否触发拍照
                            new XElement("PCBNewStartX", positionPCBNewStart.X),
                            new XElement("PCBNewStartY", positionPCBNewStart.Y),
                            new XElement("X", iPosX),          //X方向行程
                            new XElement("Y", iPosY)          //Y方向行程
                            );
            xRoute.Add(xPoint);

            if (App.m_Motion.MotionEnable() == false)
                return;
            App.m_Motion.XPath = xRoute;
            App.m_Motion.MotionType = MotionType.PCBStartAdjust;
            App.m_Motion.MotionThread_Start();
        }

        #endregion PCB起点校正

        #region 测试路径


        private XElement m_PadRectData = new XElement("PadRectData");//记录焊盘信息
                                                                     //private List<System.Windows.Shapes.Path> m_ListPadShapes = new List<System.Windows.Shapes.Path>();//记录每个焊盘由哪些形状组成
        private Rectangle m_rCurrentRect = null;//相机图像窗口当前选择测元件框
        private XElement m_xCurrentCell = null; //相机图像窗口当前显示的测试点


        /// <summary>
        /// 显示编程路径的状态改变
        /// </summary>
        private void checkboxPathShow_Click(object sender, RoutedEventArgs e)
        {
            if (checkboxPathShow.IsChecked == true)
            {//显示编程路径
                PathShow();
            }
            else
            {//清除测试路径的显示
                canvasProgramThumbnail.Children.Clear();
            }
        }

        /// <summary>
        /// 路径显示
        /// </summary>
        private void PathShow()
        {
            if (App.m_ShowState.CurrentProductType.XElement == null || App.m_ShowState.CurrentProductType.XTestRoute == null)
                return;

            RouteOptimize();

            List<System.Windows.Point> list = new List<System.Windows.Point>();

            if (App.m_ShowState.CurrentProductType.MarkOneImage != null)
            {
                double X = App.m_ShowState.CurrentProductType.ThumbnailWidth - App.m_ShowState.CurrentProductType.MarkOnePos.X / App.m_ShowState.CurrentProductType.MotionWidth * App.m_ShowState.CurrentProductType.ThumbnailWidth;
                double Y = App.m_ShowState.CurrentProductType.ThumbnailHeight - App.m_ShowState.CurrentProductType.MarkOnePos.Y / App.m_ShowState.CurrentProductType.MotionHeight * App.m_ShowState.CurrentProductType.ThumbnailHeight;
                list.Add(new System.Windows.Point(X, Y));
            }
            if (App.m_ShowState.CurrentProductType.MarkTwoImage != null)
            {
                double X = App.m_ShowState.CurrentProductType.ThumbnailWidth - App.m_ShowState.CurrentProductType.MarkTwoPos.X / App.m_ShowState.CurrentProductType.MotionWidth * App.m_ShowState.CurrentProductType.ThumbnailWidth;
                double Y = App.m_ShowState.CurrentProductType.ThumbnailHeight - App.m_ShowState.CurrentProductType.MarkTwoPos.Y / App.m_ShowState.CurrentProductType.MotionHeight * App.m_ShowState.CurrentProductType.ThumbnailHeight;
                list.Add(new System.Windows.Point(X, Y));
            }

            if (App.m_ShowState.CurrentProductType.XTestRoute.Element("MultiBoard") != null && bool.Parse((string)App.m_ShowState.CurrentProductType.XTestRoute.Element("MultiBoard")) == true)
            {//判断是否为拼板
                foreach (XElement subRect in App.m_ShowState.CurrentProductType.XTestRoute.Elements("SubRectangle"))
                {
                    double dPosX = System.Windows.Point.Parse((string)subRect.Element("Position")).X;
                    double dPosY = System.Windows.Point.Parse((string)subRect.Element("Position")).Y;
                    double dWidth = double.Parse((string)subRect.Element("Width")) * App.m_ShowState.CurrentProductType.ThumbnailWidth / App.m_ShowState.CurrentProductType.MotionWidth;
                    double dHeight = double.Parse((string)subRect.Element("Height")) * App.m_ShowState.CurrentProductType.ThumbnailHeight / App.m_ShowState.CurrentProductType.MotionHeight;
                    double dAngle = double.Parse((string)subRect.Element("Rotation"));
                    double posX = App.m_ShowState.CurrentProductType.ThumbnailWidth - dPosX * App.m_ShowState.CurrentProductType.ThumbnailWidth / App.m_ShowState.CurrentProductType.MotionWidth;
                    double posY = App.m_ShowState.CurrentProductType.ThumbnailHeight - dPosY * App.m_ShowState.CurrentProductType.ThumbnailHeight / App.m_ShowState.CurrentProductType.MotionHeight;
                    double left = posX - dWidth / 2;
                    double top = posY - dHeight / 2;
                    System.Windows.Shapes.Rectangle rect = new System.Windows.Shapes.Rectangle()
                    {
                        Fill = new SolidColorBrush() { Color = Colors.Red, Opacity = 0.0 },
                        Stroke = new SolidColorBrush(Colors.Red),
                        StrokeThickness = 10,
                        Width = dWidth,
                        Height = dHeight
                    };
                    Canvas.SetTop(rect, top);
                    Canvas.SetLeft(rect, left);
                    if (m_rectThumbnailSelectArea == null)
                    {
                        m_rectThumbnailSelectArea = rect;
                        m_bIsSelectedOK = true;
                    }

                    rect.RenderTransform = new RotateTransform() { Angle = dAngle, CenterX = dWidth / 2, CenterY = dHeight / 2 };
                    System.Windows.Controls.ContextMenu contextMenu = new System.Windows.Controls.ContextMenu();
                    System.Windows.Controls.MenuItem menuItem = new System.Windows.Controls.MenuItem();
                    menuItem.Header = "删除";
                    menuItem.Click += DelTestSubRect_Click;
                    contextMenu.Items.Add(menuItem);
                    rect.ContextMenu = contextMenu;
                    rect.DataContext = subRect;
                    canvasProgramThumbnail.Children.Add(rect);
                    foreach (XElement cell in subRect.Elements("TestCell"))
                    {
                        TestCell testCell = new TestCell(cell);
                        double X = App.m_ShowState.CurrentProductType.ThumbnailWidth - testCell.Position.X / App.m_ShowState.CurrentProductType.MotionWidth * App.m_ShowState.CurrentProductType.ThumbnailWidth;
                        double Y = App.m_ShowState.CurrentProductType.ThumbnailHeight - testCell.Position.Y / App.m_ShowState.CurrentProductType.MotionHeight * App.m_ShowState.CurrentProductType.ThumbnailHeight;
                        list.Add(new System.Windows.Point(X, Y));
                        DrawPathRect(X, Y, cell);
                    }

                }
                //if (m_rectThumbnailSelectArea != null && (!canvasProgramThumbnail.Children.Contains(m_rectThumbnailSelectArea)))
                //{
                //    canvasProgramThumbnail.Children.Add(m_rectThumbnailSelectArea);
                //}
            }
            else
            {
                foreach (XElement cell in App.m_ShowState.CurrentProductType.XTestRoute.Elements("TestCell"))
                {
                    TestCell testCell = new TestCell(cell);
                    double X = App.m_ShowState.CurrentProductType.ThumbnailWidth - testCell.Position.X / App.m_ShowState.CurrentProductType.MotionWidth * App.m_ShowState.CurrentProductType.ThumbnailWidth;
                    double Y = App.m_ShowState.CurrentProductType.ThumbnailHeight - testCell.Position.Y / App.m_ShowState.CurrentProductType.MotionHeight * App.m_ShowState.CurrentProductType.ThumbnailHeight;
                    list.Add(new System.Windows.Point(X, Y));

                    DrawPathRect(X, Y, cell);
                }
            }

            for (int i = 0; i < list.Count - 1; i++)
            {
                var line = new LineGeometry(list[i], list[i + 1]);
                var path = new System.Windows.Shapes.Path()
                {
                    Stroke = System.Windows.Media.Brushes.Yellow,
                    StrokeThickness = 2,
                    Data = line
                };
                canvasProgramThumbnail.Children.Add(path);
            }
        }


        /// <summary>
        /// 在缩略图显示区画测试点区域框
        /// </summary>
        private void DrawPathRect(double X, double Y, XElement xCell)
        {
            System.Windows.Shapes.Rectangle rec = new System.Windows.Shapes.Rectangle
            {
                Fill = new SolidColorBrush() { Color = Colors.Red, Opacity = 0.0 },
                Stroke = new SolidColorBrush(Colors.Red),
                StrokeThickness = 8,
                Width = App.m_ShowState.CurrentProductType.ThumbnailSubWidth,
                Height = App.m_ShowState.CurrentProductType.ThumbnailSubHeight
            };
            rec.SetValue(Canvas.LeftProperty, X - App.m_ShowState.CurrentProductType.ThumbnailSubWidth / 2);
            rec.SetValue(Canvas.TopProperty, Y - App.m_ShowState.CurrentProductType.ThumbnailSubHeight / 2);
            rec.MouseLeftButtonDown += Rec_MouseLeftButtonDown_TestCell;
            canvasProgramThumbnail.Children.Add(rec);

            System.Windows.Controls.ContextMenu contextMenu = new System.Windows.Controls.ContextMenu();
            System.Windows.Controls.MenuItem menuItem = new System.Windows.Controls.MenuItem();
            menuItem.Header = "删除";
            menuItem.Click += DelTestCell_Click;
            contextMenu.Items.Add(menuItem);
            rec.ContextMenu = contextMenu;
            rec.DataContext = xCell;
        }

        /// <summary>
        /// 缩略图上测试点区域鼠标左键双击后，拍照图像显示区显示测试点的元件框和焊盘
        /// </summary>
        private void Rec_MouseLeftButtonDown_TestCell(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2)
                return;

            e.Handled = true;

            if ((sender is System.Windows.Shapes.Rectangle) == false)
                return;
            System.Windows.Shapes.Rectangle recCell = sender as System.Windows.Shapes.Rectangle;
            if (recCell.DataContext == null || (recCell.DataContext is XElement) == false)
                return;
            XElement xCell = recCell.DataContext as XElement;

            //显示CELL
            double posX = App.m_ShowState.CurrentProductType.PcbStart.X + System.Windows.Point.Parse((string)xCell.Element("Position")).X;
            double posY = App.m_ShowState.CurrentProductType.PcbStart.Y + System.Windows.Point.Parse((string)xCell.Element("Position")).Y;

            XElement xRoute = new XElement("Path");
            XElement xPoint = new XElement("Point",
                            new XElement("Take", true),          //到位是否触发拍照
                            new XElement("X", (int)(posX * (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch))),          //X方向行程
                            new XElement("Y", (int)(posY * (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch)))          //Y方向行程
                            );
            xRoute.Add(xPoint);
            xRoute.Add(xCell);


            if (App.m_Motion.MotionEnable() == false)
                return;

            App.m_Motion.XPath = xRoute;
            App.m_Motion.MotionType = MotionType.TestRouteEdit;
            App.m_Motion.MotionThread_Start();
            m_xCurrentCell = xCell;
        }


        //对在缩略图上双击选择TestCell中的元件进行编辑
        private void Edit_TestRoute(XElement xCell)
        {
            if (!this.Dispatcher.CheckAccess()) // CheckAccess returns true if you're on the dispatcher thread
            {
                this.Dispatcher.BeginInvoke(new DelegateXElementParameter(Edit_TestRoute), xCell);
                return;
            }
            //----------------------------------------------------------------------------------------------------------

            System.Windows.Point testPoint = new System.Windows.Point();
            testPoint.X = System.Windows.Point.Parse((string)xCell.Element("Position")).X;
            testPoint.Y = System.Windows.Point.Parse((string)xCell.Element("Position")).Y;

            //测试点拍照图片左下角位置，单位mm
            double x1 = testPoint.X - canvasCameraImage.Width / Doc.m_SystemParam.HelicalPitchMap.X / 2 * Doc.m_SystemParam.HelicalPitch;
            double x2 = testPoint.X + canvasCameraImage.Width / Doc.m_SystemParam.HelicalPitchMap.X / 2 * Doc.m_SystemParam.HelicalPitch;
            //测试点拍照图片右上角位置，单位mm
            double y1 = testPoint.Y - canvasCameraImage.Height / Doc.m_SystemParam.HelicalPitchMap.Y / 2 * Doc.m_SystemParam.HelicalPitch;
            double y2 = testPoint.Y + canvasCameraImage.Height / Doc.m_SystemParam.HelicalPitchMap.Y / 2 * Doc.m_SystemParam.HelicalPitch;

            //var layer = AdornerLayer.GetAdornerLayer(canvasCameraImage);

            foreach (XElement meta in xCell.Elements("TestMeta"))
            {//检索测试点中的元件
                double x = System.Windows.Point.Parse((string)meta.Element("Position")).X;
                double y = System.Windows.Point.Parse((string)meta.Element("Position")).Y;
                double rotation = double.Parse((string)meta.Element("Rotation"));
                System.Windows.Shapes.Rectangle rect = new System.Windows.Shapes.Rectangle()
                {

                    Fill = new SolidColorBrush() { Color = Colors.Red, Opacity = 0.0 },
                    Stroke = bool.Parse((string)meta.Element("Test")) ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.White),
                    StrokeThickness = 3,
                    Width = 50,
                    Height = 50
                };

                if (meta.Element("Name") != null)
                    rect.Name = (string)meta.Element("Name");

                if (bool.Parse((string)meta.Element("Test")))
                {
                    if (rotation % 360 == 90 || rotation % 360 == 270)
                    {
                        rect.Width = Double.Parse((string)meta.Element("MetaHeight"));
                        rect.Height = Double.Parse((string)meta.Element("MetaWidth"));
                    }
                    else //将不是90旋转的元件都视作为无旋转
                    {
                        rect.Width = Double.Parse((string)meta.Element("MetaWidth"));
                        rect.Height = Double.Parse((string)meta.Element("MetaHeight"));
                    }
                }

                rect.DataContext = meta;

                if (checkboxMetaMatch.IsChecked == true)
                {//所有元件均从元件库中自动匹配

                    List<MetaItem> metaItemList = new List<MetaItem>();
                    IEnumerable<XElement> CodeList = meta.Elements("Code");
                    foreach (XElement code in CodeList)
                    {
                        string name = code.Value;
                        foreach (MetaItem metaItem in Doc.m_MetaLib.Items)
                        {
                            if (metaItem.Name == name)
                            {
                                meta.SetElementValue("TypeName", metaItem.TypeName);
                                  meta.SetElementValue("Polarity", metaItem.Polarity);
                                metaItemList.Add(metaItem);
                            }
                        }
                    }
                    if (metaItemList.Count != 0)//库中无匹配的元件，继续下一个元件
                    {
                        rect.DataContext = metaItemList;
                        foreach (MetaItem metaItem in metaItemList)
                        {//将匹配到的元件更改元件框的大小
                            if (rotation % 360 == 90 || rotation % 360 == 270)
                            {
                                rect.Width = metaItem.MetaHeight;
                                rect.Height = metaItem.MetaWidth;
                            }
                            else
                            {
                                rect.Width = metaItem.MetaWidth;
                                rect.Height = metaItem.MetaHeight;
                            }
                            break;
                        }
                    }
                }
                rect.SetValue(Canvas.LeftProperty, canvasCameraImage.Width - x - rect.Width / 2);
                rect.SetValue(Canvas.TopProperty, canvasCameraImage.Height - y - rect.Height / 2);
                //rect.MouseRightButtonDown += Rect_MouseRightButtonDown;
                canvasCameraImage.Children.Add(rect);

                if (meta.Element("IsManual") != null && (bool.Parse((string)meta.Element("IsManual")) == true))
                {//手动画框元件
                    System.Windows.Controls.ContextMenu contextMenu = new System.Windows.Controls.ContextMenu();
                    System.Windows.Controls.MenuItem menuItem = new System.Windows.Controls.MenuItem() { Header = "添加测试" };
                    menuItem.Command = CustomCommand.CommandAddMeta;
                    //menuItem.CommandTarget = rect;
                    contextMenu.Items.Add(menuItem);
                    menuItem = new System.Windows.Controls.MenuItem() { Header = "删除元件" };
                    menuItem.Click += DelTestMeta_Click;
                    contextMenu.Items.Add(menuItem);
                    menuItem = new System.Windows.Controls.MenuItem() { Header = "取消测试" };
                    menuItem.Click += TestCancle_Click;
                    contextMenu.Items.Add(menuItem);
                    menuItem = new System.Windows.Controls.MenuItem() { Header = "极性检测", IsCheckable = true};

                    if (meta.Element("Polarity") != null)
                    {
                        if ((bool)meta.Element("Polarity"))
                            menuItem.IsChecked = true;
                    }
                    menuItem.Click += IsPolarity_Click;
                    contextMenu.Items.Add(menuItem);
                    menuItem = new System.Windows.Controls.MenuItem() { Header = "元件旋转" };
                    menuItem.Click += RotateMeta_Click;
                    contextMenu.Items.Add(menuItem);
                    rect.ContextMenu = contextMenu;
                }
                else
                {
                    System.Windows.Controls.ContextMenu contextMenu = new System.Windows.Controls.ContextMenu();
                    System.Windows.Controls.MenuItem menuItem = new System.Windows.Controls.MenuItem() { Header = "元件匹配" };
                    menuItem.Click += MetaMatch_Click;
                    contextMenu.Items.Add(menuItem);
                    menuItem = new System.Windows.Controls.MenuItem() { Header = "添加测试" };
                    menuItem.Command = CustomCommand.CommandAddMeta;
                    //menuItem.CommandTarget = rect;
                    contextMenu.Items.Add(menuItem);
                    menuItem = new System.Windows.Controls.MenuItem() { Header = "取消测试" };
                    menuItem.Click += TestCancle_Click;
                    contextMenu.Items.Add(menuItem);
                    menuItem = new System.Windows.Controls.MenuItem() { Header = "极性检测", IsCheckable = true, IsChecked = false };
                    if (meta.Element("Polarity") != null)
                    {
                        if ((bool)meta.Element("Polarity"))
                            menuItem.IsChecked = true;
                    }
                    menuItem.Click += IsPolarity_Click;
                    contextMenu.Items.Add(menuItem);
                    menuItem = new System.Windows.Controls.MenuItem() { Header = "添加到元件库" };
                    menuItem.Click += AddToLib_Click;
                    contextMenu.Items.Add(menuItem);
                    menuItem = new System.Windows.Controls.MenuItem() { Header = "旋转检测框" };
                    menuItem.Click += RotateCheckItem_Click;
                    contextMenu.Items.Add(menuItem);
                    menuItem = new System.Windows.Controls.MenuItem() { Header = "元件旋转" };
                    menuItem.Click += RotateMeta_Click;
                    contextMenu.Items.Add(menuItem);
                    menuItem = new System.Windows.Controls.MenuItem() { Header = "设置阈值" };
                    menuItem.Click += MetaSetThreshold_Click;
                    contextMenu.Items.Add(menuItem);
                    rect.ContextMenu = contextMenu;
                }
            }
            #region 焊盘显示

            //foreach (XElement pad in m_TestProductType.XPadData.Elements("Pad"))
            //{
            //    if (String.IsNullOrEmpty((string)pad.Element("Type")))
            //        continue;

            //    switch ((PadType)Enum.Parse(typeof(PadType), (string)pad.Element("Type")))
            //    {
            //        case PadType.Ellipse:
            //            double x = System.Windows.Point.Parse((string)pad.Element("Position")).X;
            //            double y = System.Windows.Point.Parse((string)pad.Element("Position")).Y;
            //            if (x > x1 && x < x2 && y > y1 && y < y2)
            //            {//焊盘在拍照图像范围内

            //                //元件在拍照图像上的位置，单位：像素
            //                double X = (x - x1) * Doc.m_SystemParam.HelicalPitchMap.X / Doc.m_SystemParam.HelicalPitch;
            //                double Y = (y - y1) * Doc.m_SystemParam.HelicalPitchMap.Y / Doc.m_SystemParam.HelicalPitch;
            //                DrawEllipsePad((double)pad.Element("Width") * Doc.m_SystemParam.HelicalPitchMap.X / Doc.m_SystemParam.HelicalPitch, (double)pad.Element("Height") * Doc.m_SystemParam.HelicalPitchMap.Y / Doc.m_SystemParam.HelicalPitch, X, Y);

            //            }
            //            break;
            //        case PadType.Line:
            //            double xs = System.Windows.Point.Parse((string)pad.Element("PosStart")).X;
            //            double ys = System.Windows.Point.Parse((string)pad.Element("PosStart")).Y;
            //            double xe = System.Windows.Point.Parse((string)pad.Element("PosEnd")).X;
            //            double ye = System.Windows.Point.Parse((string)pad.Element("PosEnd")).Y;
            //            if ((xs > x1 && xs < x2 && ys > y1 && ys < y2) || (xe > x1 && xe < x2 && ye > y1 && ye < y2))
            //            {
            //                double XS = (xs - x1) * Doc.m_SystemParam.HelicalPitchMap.X / Doc.m_SystemParam.HelicalPitch;
            //                double YS = (ys - y1) * Doc.m_SystemParam.HelicalPitchMap.Y / Doc.m_SystemParam.HelicalPitch;
            //                double XE = (xe - x1) * Doc.m_SystemParam.HelicalPitchMap.X / Doc.m_SystemParam.HelicalPitch;
            //                double YE = (ye - y1) * Doc.m_SystemParam.HelicalPitchMap.Y / Doc.m_SystemParam.HelicalPitch;
            //                DrawLinePad(XS, YS, XE, YE, (double)pad.Element("Width") * Doc.m_SystemParam.HelicalPitchMap.X / Doc.m_SystemParam.HelicalPitch);

            //            }
            //            break;
            //        case PadType.Rectangle:
            //            double xr = System.Windows.Point.Parse((string)pad.Element("Position")).X;
            //            double yr = System.Windows.Point.Parse((string)pad.Element("Position")).Y;
            //            if (xr > x1 && xr < x2 && yr > y1 && yr < y2)
            //            {//焊盘在拍照图像范围内

            //                //元件在拍照图像上的位置，单位：像素
            //                double X = (xr - x1) * Doc.m_SystemParam.HelicalPitchMap.X / Doc.m_SystemParam.HelicalPitch;
            //                double Y = (yr - y1) * Doc.m_SystemParam.HelicalPitchMap.Y / Doc.m_SystemParam.HelicalPitch;
            //                DrawRectanglePad((double)pad.Element("Width") * Doc.m_SystemParam.HelicalPitchMap.X / Doc.m_SystemParam.HelicalPitch, (double)pad.Element("Height") * Doc.m_SystemParam.HelicalPitchMap.Y / Doc.m_SystemParam.HelicalPitch, X, Y, (double)pad.Element("Rotation"));

            //            }
            //            break;

            //    }
            //}
            #endregion 焊盘显示
        }

        /// <summary>
        /// 缩略图鼠标右键弹出菜单，删除选中的子板区域
        /// </summary>      
        private void DelTestSubRect_Click(object sender, RoutedEventArgs e)
        {
            if ((sender is System.Windows.Controls.MenuItem) == false)
                return;
            System.Windows.Controls.MenuItem mi = sender as System.Windows.Controls.MenuItem;
            if (mi.Parent == null || (mi.Parent is System.Windows.Controls.ContextMenu) == false)
                return;
            System.Windows.Controls.ContextMenu cm = mi.Parent as System.Windows.Controls.ContextMenu;
            if (cm.PlacementTarget == null || (cm.PlacementTarget is System.Windows.Shapes.Rectangle) == false)
                return;
            System.Windows.Shapes.Rectangle rcTmp = cm.PlacementTarget as System.Windows.Shapes.Rectangle;

            XElement xSubRectRemove = null;

            if (App.m_ShowState.CurrentProductType.XTestRoute.Element("MultiBoard") != null && bool.Parse((string)App.m_ShowState.CurrentProductType.XTestRoute.Element("MultiBoard")) == true)
            {//判断是否为拼板
                foreach (XElement xSubRect in App.m_ShowState.CurrentProductType.XTestRoute.Elements("SubRectangle"))
                {

                    double x = App.m_ShowState.CurrentProductType.ThumbnailWidth - System.Windows.Point.Parse((string)xSubRect.Element("Position")).X * App.m_ShowState.CurrentProductType.ThumbnailWidth / App.m_ShowState.CurrentProductType.MotionWidth;
                    double y = App.m_ShowState.CurrentProductType.ThumbnailHeight - System.Windows.Point.Parse((string)xSubRect.Element("Position")).Y * App.m_ShowState.CurrentProductType.ThumbnailHeight / App.m_ShowState.CurrentProductType.MotionHeight;
                    double width = double.Parse((string)xSubRect.Element("Width")) * App.m_ShowState.CurrentProductType.ThumbnailWidth / App.m_ShowState.CurrentProductType.MotionWidth;
                    double height = double.Parse((string)xSubRect.Element("Height")) * App.m_ShowState.CurrentProductType.ThumbnailHeight / App.m_ShowState.CurrentProductType.MotionHeight;
                    if (Canvas.GetLeft(rcTmp) == x - width / 2 && Canvas.GetTop(rcTmp) == y - height / 2)
                    {
                        xSubRectRemove = xSubRect;
                        break;
                    }
                }
            }

            if (xSubRectRemove != null)
                xSubRectRemove.Remove();
            canvasProgramThumbnail.Children.Clear();
            PathShow();
        }

        /// <summary>
        /// 缩略图鼠标右键弹出菜单，删除选中的测试点CELL（一个相机的拍照识别位置）
        /// </summary>
        private void DelTestCell_Click(object sender, RoutedEventArgs e)
        {
            if ((sender is System.Windows.Controls.MenuItem) == false)
                return;
            System.Windows.Controls.MenuItem mi = sender as System.Windows.Controls.MenuItem;
            if (mi.Parent == null || (mi.Parent is System.Windows.Controls.ContextMenu) == false)
                return;
            System.Windows.Controls.ContextMenu cm = mi.Parent as System.Windows.Controls.ContextMenu;
            if (cm.PlacementTarget == null || (cm.PlacementTarget is System.Windows.Shapes.Rectangle) == false)
                return;
            System.Windows.Shapes.Rectangle rcTmp = cm.PlacementTarget as System.Windows.Shapes.Rectangle;

            XElement xCellRemove = null;

            if (App.m_ShowState.CurrentProductType.XTestRoute.Element("MultiBoard") != null && bool.Parse((string)App.m_ShowState.CurrentProductType.XTestRoute.Element("MultiBoard")) == true)
            {//判断是否为拼板
                foreach (XElement xCell in App.m_ShowState.CurrentProductType.XTestRoute.Elements("SubRectangle").Elements("TestCell"))
                {
                    TestCell testCell = new TestCell(xCell);
                    double x = App.m_ShowState.CurrentProductType.ThumbnailWidth - testCell.Position.X / App.m_ShowState.CurrentProductType.MotionWidth * App.m_ShowState.CurrentProductType.ThumbnailWidth;
                    double y = App.m_ShowState.CurrentProductType.ThumbnailHeight - testCell.Position.Y / App.m_ShowState.CurrentProductType.MotionHeight * App.m_ShowState.CurrentProductType.ThumbnailHeight;
                    if (Canvas.GetLeft(rcTmp) == x - App.m_ShowState.CurrentProductType.ThumbnailSubWidth / 2 && Canvas.GetTop(rcTmp) == y - App.m_ShowState.CurrentProductType.ThumbnailSubHeight / 2)
                    {
                        xCellRemove = xCell;
                        break;
                    }
                }
            }
            else
            {
                foreach (XElement xCell in App.m_ShowState.CurrentProductType.XTestRoute.Elements("TestCell"))
                {
                    TestCell testCell = new TestCell(xCell);
                    double x = App.m_ShowState.CurrentProductType.ThumbnailWidth - testCell.Position.X / App.m_ShowState.CurrentProductType.MotionWidth * App.m_ShowState.CurrentProductType.ThumbnailWidth;
                    double y = App.m_ShowState.CurrentProductType.ThumbnailHeight - testCell.Position.Y / App.m_ShowState.CurrentProductType.MotionHeight * App.m_ShowState.CurrentProductType.ThumbnailHeight;
                    if (Canvas.GetLeft(rcTmp) == x - App.m_ShowState.CurrentProductType.ThumbnailSubWidth / 2 && Canvas.GetTop(rcTmp) == y - App.m_ShowState.CurrentProductType.ThumbnailSubHeight / 2)
                    {
                        xCellRemove = xCell;
                        break;
                    }
                }
            }
            if (xCellRemove != null)
                xCellRemove.Remove();
            canvasProgramThumbnail.Children.Clear();
            PathShow();
        }
        /// <summary>
        /// 优化MARK点
        /// </summary>
        private int MarkOptimize()
        {
            if (App.m_ShowState.CurrentProductType.XElement == null)
                return 0;

            if (App.m_ShowState.CurrentProductType.MarkTwoImage == null)
            {
                if (App.m_ShowState.CurrentProductType.MarkOneImage != null)
                    return 1;
                else return 0;
            }
            else if (App.m_ShowState.CurrentProductType.MarkOneImage == null)
            {// 将第二个MARK交给第一个
                App.m_ShowState.CurrentProductType.MarkOneImage = App.m_ShowState.CurrentProductType.MarkTwoImage;
                App.m_ShowState.CurrentProductType.MarkTwoImage = null;
                App.m_ShowState.CurrentProductType.MarkOnePos = App.m_ShowState.CurrentProductType.MarkTwoPos;
                App.m_ShowState.CurrentProductType.MarkTwoPos = new System.Windows.Point(0, 0);
                return 1;
            }
            else
            {// 比较两个MARK，第一MARK点离原点最近
                System.Windows.Point mark1 = new System.Windows.Point(0, 0);
                mark1.X = App.m_ShowState.CurrentProductType.MarkOnePos.X + App.m_ShowState.CurrentProductType.PcbStart.X;
                mark1.Y = App.m_ShowState.CurrentProductType.MarkOnePos.Y + App.m_ShowState.CurrentProductType.PcbStart.Y;
                System.Windows.Point mark2 = new System.Windows.Point(0, 0);
                mark2.X = App.m_ShowState.CurrentProductType.MarkTwoPos.X + App.m_ShowState.CurrentProductType.PcbStart.X;
                mark2.Y = App.m_ShowState.CurrentProductType.MarkTwoPos.Y + App.m_ShowState.CurrentProductType.PcbStart.Y;
                if ((Math.Pow(mark1.X, 2) + Math.Pow(mark1.Y, 2)) > (Math.Pow(mark2.X, 2) + Math.Pow(mark2.Y, 2)))
                {// 交换
                    String sTmp = App.m_ShowState.CurrentProductType.MarkOneImage;
                    App.m_ShowState.CurrentProductType.MarkOneImage = App.m_ShowState.CurrentProductType.MarkTwoImage;
                    App.m_ShowState.CurrentProductType.MarkTwoImage = sTmp;
                    mark1 = App.m_ShowState.CurrentProductType.MarkOnePos;
                    App.m_ShowState.CurrentProductType.MarkOnePos = App.m_ShowState.CurrentProductType.MarkTwoPos;
                    App.m_ShowState.CurrentProductType.MarkTwoPos = mark1;
                }
                return 2;
            }
        }

        /// <summary>
        /// 路径优化：计算出最短路径
        /// </summary>
        private void RouteOptimize()
        {
            if (App.m_ShowState.CurrentProductType.XElement == null || App.m_ShowState.CurrentProductType.XTestRoute == null || App.m_ShowState.CurrentProductType.XTestRoute.Element("MultiBoard") == null)
                return;
            // 算法：
            // 1、优化MARK点，第一MARK点离原点最近
            // 2、MARK点不存在：找离原点最近的CELL点作为第一点；MAKR点存在：找离最后一个MARK点最近的CELL点作为第一点
            // 3、其他CELL点相对当前CELL点最近的作为下一个CELL点
            int iMarkNumber = MarkOptimize();
            System.Windows.Point curPoint = new System.Windows.Point(0, 0);
            if (iMarkNumber == 1)
            {
                curPoint.X = App.m_ShowState.CurrentProductType.MarkOnePos.X + App.m_ShowState.CurrentProductType.PcbStart.X;
                curPoint.Y = App.m_ShowState.CurrentProductType.MarkOnePos.Y + App.m_ShowState.CurrentProductType.PcbStart.Y;
            }
            else if (iMarkNumber == 2)
            {// 两个MARK点
                curPoint.X = App.m_ShowState.CurrentProductType.MarkTwoPos.X + App.m_ShowState.CurrentProductType.PcbStart.X;
                curPoint.Y = App.m_ShowState.CurrentProductType.MarkTwoPos.Y + App.m_ShowState.CurrentProductType.PcbStart.Y;
            }
            if ((bool)App.m_ShowState.CurrentProductType.XTestRoute.Element("MultiBoard") == true)
            {//拼板
                XElement xRstRoute = new XElement("TestRoute");
                xRstRoute.Add(App.m_ShowState.CurrentProductType.XTestRoute.Element("MultiBoard"));
                XElement xSrcRoute = new XElement(App.m_ShowState.CurrentProductType.XTestRoute);
                XElement xCurCell = null;
                while (true)
                {
                    IEnumerable<XElement> xRecList = xSrcRoute.Elements("SubRectangle");
                    if (xRecList.Count() <= 0)
                        break;

                    XElement xCurRect = null;
                    foreach (XElement subRect in xSrcRoute.Elements("SubRectangle"))
                    {
                        xCurRect = subRect;

                        XElement xRstSubRect = new XElement("SubRectangle");
                        XElement tmpSubRect = new XElement(subRect);
                        xRstSubRect.Add(subRect.Element("Position"));
                        xRstSubRect.Add(subRect.Element("Width"));
                        xRstSubRect.Add(subRect.Element("Height"));
                        xRstSubRect.Add(subRect.Element("Rotation"));

                        double dOld = 0, dNew = 0;
                        while (true)
                        {
                            IEnumerable<XElement> xCellList = tmpSubRect.Elements("TestCell");
                            if (xCellList.Count() <= 0)
                                break;

                            dOld = 0;
                            dNew = 0;
                            xCurCell = null;
                            TestCell testCell = new TestCell();
                            foreach (XElement xCell in xCellList)
                            {
                                testCell.XElement = xCell;
                                if (dOld == 0)
                                {
                                    dOld = Math.Pow((testCell.Position.X + App.m_ShowState.CurrentProductType.PcbStart.X - curPoint.X), 2) + Math.Pow((testCell.Position.Y + App.m_ShowState.CurrentProductType.PcbStart.Y - curPoint.Y), 2);
                                    xCurCell = xCell;
                                }
                                else dNew = Math.Pow((testCell.Position.X + App.m_ShowState.CurrentProductType.PcbStart.X - curPoint.X), 2) + Math.Pow((testCell.Position.Y + App.m_ShowState.CurrentProductType.PcbStart.Y - curPoint.Y), 2);
                                if (dNew != 0)
                                {// 比较，找距离最短的
                                    if (dOld > dNew)
                                    {
                                        dOld = dNew;
                                        xCurCell = xCell;
                                    }
                                }
                            }

                            if (xCurCell != null)
                            {
                                xCurCell.Remove();
                                xRstSubRect.Add(xCurCell);
                            }
                            testCell.XElement = xCurCell;
                            curPoint.X = testCell.Position.X + App.m_ShowState.CurrentProductType.PcbStart.X;
                            curPoint.Y = testCell.Position.Y + App.m_ShowState.CurrentProductType.PcbStart.Y;
                        }

                        //Rectangle排序后加入
                        XElement xCurRstSubRect = null;
                        System.Windows.Point ptRstSubRect = System.Windows.Point.Parse((string)xRstSubRect.Element("Position"));
                        foreach (XElement xTmpRstRoute in xRstRoute.Elements("SubRectangle"))
                        {
                            System.Windows.Point ptRstRoute = System.Windows.Point.Parse((string)xTmpRstRoute.Element("Position"));
                            if ((Math.Pow(ptRstRoute.X, 2) + Math.Pow(ptRstRoute.Y, 2)) > (Math.Pow(ptRstSubRect.X, 2) + Math.Pow(ptRstSubRect.Y, 2)))
                            {
                                xCurRstSubRect = xTmpRstRoute;
                                break;
                            }
                        }
                        if (xCurRstSubRect == null)
                            xRstRoute.Add(xRstSubRect);
                        else xCurRstSubRect.AddBeforeSelf(xRstSubRect);
                        break;
                    }
                    xCurRect.Remove();
                }
                App.m_ShowState.CurrentProductType.XTestRoute.Remove();
                App.m_ShowState.CurrentProductType.XElement.Add(xRstRoute);
            }
            else
            {// 单板
                XElement xRstRoute = new XElement("TestRoute");
                xRstRoute.Add(App.m_ShowState.CurrentProductType.XTestRoute.Element("MultiBoard"));
                XElement xSrcRoute = new XElement(App.m_ShowState.CurrentProductType.XTestRoute);
                XElement xCurCell = null;
                double dOld = 0, dNew = 0;
                while (true)
                {
                    IEnumerable<XElement> xCellList = xSrcRoute.Elements("TestCell");
                    if (xCellList.Count() <= 0)
                        break;
                    dOld = 0;
                    dNew = 0;
                    xCurCell = null;
                    TestCell testCell = new TestCell();
                    foreach (XElement xCell in xCellList)
                    {
                        testCell.XElement = xCell;
                        if (dOld == 0)
                        {
                            dOld = Math.Pow((testCell.Position.X + App.m_ShowState.CurrentProductType.PcbStart.X - curPoint.X), 2) + Math.Pow((testCell.Position.Y + App.m_ShowState.CurrentProductType.PcbStart.Y - curPoint.Y), 2);
                            xCurCell = xCell;
                        }
                        else dNew = Math.Pow((testCell.Position.X + App.m_ShowState.CurrentProductType.PcbStart.X - curPoint.X), 2) + Math.Pow((testCell.Position.Y + App.m_ShowState.CurrentProductType.PcbStart.Y - curPoint.Y), 2);
                        if (dNew != 0)
                        {// 比较，找距离最短的
                            if (dOld > dNew)
                            {
                                dOld = dNew;
                                xCurCell = xCell;
                            }
                        }
                    }

                    if (xCurCell != null)
                    {
                        xCurCell.Remove();
                        xRstRoute.Add(xCurCell);
                    }
                    testCell.XElement = xCurCell;
                    curPoint.X = testCell.Position.X + App.m_ShowState.CurrentProductType.PcbStart.X;
                    curPoint.Y = testCell.Position.Y + App.m_ShowState.CurrentProductType.PcbStart.Y;
                }

                App.m_ShowState.CurrentProductType.XTestRoute.Remove();
                App.m_ShowState.CurrentProductType.XElement.Add(xRstRoute);
            }
        }

        /// <summary>
        /// 添加当前运动位置为测试路径的CELL（一个相机的拍照识别位置）
        /// </summary>
        private void OnClick_PathAddPosition(object sender, RoutedEventArgs e)
        {
            if (App.m_Motion.MotionEnable() == false)
            {
                MessageBox.Show("测试点添加失败：有效测试点为运动结束位置！",
                           Doc.MESSAGE_SOFTNAME,
                           MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }


            //所添加测试点超出PCB区域，不允许添加。
            if (LTDMC.dmc_get_encoder(0, MotionDMC5000.AXIS_X) / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) > App.m_ShowState.CurrentProductType.PcbEnd.X || LTDMC.dmc_get_encoder(0, MotionDMC5000.AXIS_Y) / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) > App.m_ShowState.CurrentProductType.PcbEnd.Y)
            {
                MessageBox.Show("测试点添加失败：添加的测试点超出PCB板区域！",
                          Doc.MESSAGE_SOFTNAME,
                          MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            canvasCameraImage.Children.Clear();
            System.Windows.Point testPoint = new System.Windows.Point();
            //测试点在PCB板上的位置
            testPoint.X = LTDMC.dmc_get_encoder(0, MotionDMC5000.AXIS_X) / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) - App.m_ShowState.CurrentProductType.PcbStart.X;
            testPoint.Y = LTDMC.dmc_get_encoder(0, MotionDMC5000.AXIS_Y) / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) - App.m_ShowState.CurrentProductType.PcbStart.Y;

            //测试点映射到缩略图上的位置
            double thumbX = App.m_ShowState.CurrentProductType.ThumbnailWidth - testPoint.X / App.m_ShowState.CurrentProductType.MotionWidth * App.m_ShowState.CurrentProductType.ThumbnailWidth;
            double thumbY = App.m_ShowState.CurrentProductType.ThumbnailHeight - testPoint.Y / App.m_ShowState.CurrentProductType.MotionHeight * App.m_ShowState.CurrentProductType.ThumbnailHeight;
            TestRoute testRoute = new TestRoute();
            testRoute.XElement = App.m_ShowState.CurrentProductType.XTestRoute;
            testRoute.MultiBoard = false;

            #region 拼版
            if (checkboxMulti.IsChecked == true)
            {
                testRoute.MultiBoard = true;
                if (m_rectThumbnailSelectArea == null)
                {

                    MessageBox.Show("请在缩略图上选择单板区域！",
                         Doc.MESSAGE_SOFTNAME,
                         MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }


                if (thumbX < Canvas.GetLeft(m_rectThumbnailSelectArea) || thumbX > Canvas.GetLeft(m_rectThumbnailSelectArea) + m_rectThumbnailSelectArea.Width
                   || thumbY < Canvas.GetTop(m_rectThumbnailSelectArea) || thumbY > Canvas.GetTop(m_rectThumbnailSelectArea) + m_rectThumbnailSelectArea.Height)
                {//选择的测试点在单板区域外
                    MessageBox.Show("请在单板区域内选择测试点！",
                         Doc.MESSAGE_SOFTNAME,
                         MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }

                if (App.m_ShowState.CurrentProductType.XTestRoute.Element("SubRectangle") == null)
                {
                    double subRectPosX = (App.m_ShowState.CurrentProductType.ThumbnailWidth - Canvas.GetLeft(m_rectThumbnailSelectArea) - m_rectThumbnailSelectArea.Width / 2) * App.m_ShowState.CurrentProductType.MotionWidth / App.m_ShowState.CurrentProductType.ThumbnailWidth;
                    double subRectPosY = (App.m_ShowState.CurrentProductType.ThumbnailHeight - Canvas.GetTop(m_rectThumbnailSelectArea) - m_rectThumbnailSelectArea.Height / 2) * App.m_ShowState.CurrentProductType.MotionHeight / App.m_ShowState.CurrentProductType.ThumbnailHeight;
                    //testRoute.XElement.Add(new XElement("MultiBoard", true));
                    testRoute.XElement.Add(new XElement("SubRectangle",
                                              new XElement("Width", m_rectThumbnailSelectArea.Width * App.m_ShowState.CurrentProductType.MotionWidth / App.m_ShowState.CurrentProductType.ThumbnailWidth),
                                              new XElement("Height", m_rectThumbnailSelectArea.Height * App.m_ShowState.CurrentProductType.MotionHeight / App.m_ShowState.CurrentProductType.ThumbnailHeight),
                                              new XElement("Rotation", 0),
                                              new XElement("Position", new System.Windows.Point(subRectPosX, subRectPosY))));
                }
            }
            #endregion 拼版
            //测试点拍照图片左下角位置，单位mm
            double x1 = testPoint.X - canvasCameraImage.Width / Doc.m_SystemParam.HelicalPitchMap.X / 2 * Doc.m_SystemParam.HelicalPitch;
            double x2 = testPoint.X + canvasCameraImage.Width / Doc.m_SystemParam.HelicalPitchMap.X / 2 * Doc.m_SystemParam.HelicalPitch;
            //测试点拍照图片右上角位置，单位mm
            double y1 = testPoint.Y - canvasCameraImage.Height / Doc.m_SystemParam.HelicalPitchMap.Y / 2 * Doc.m_SystemParam.HelicalPitch;
            double y2 = testPoint.Y + canvasCameraImage.Height / Doc.m_SystemParam.HelicalPitchMap.Y / 2 * Doc.m_SystemParam.HelicalPitch;
            //var layer = AdornerLayer.GetAdornerLayer(canvasCameraImage);
            TestCell testCell = new TestCell(new XElement("TestCell"));
            testCell.Position = testPoint;
            foreach (XElement meta in App.m_ShowState.CurrentProductType.XMetaData.Elements("Meta"))
            {//检索导入的坐标文件中的元件数据
                double x = System.Windows.Point.Parse((string)meta.Element("Position")).X;
                double y = System.Windows.Point.Parse((string)meta.Element("Position")).Y;
                if (x > x1 && x < x2 && y > y1 && y < y2)
                {//元件在拍照图像范围内
                    double X = (x - x1) * Doc.m_SystemParam.HelicalPitchMap.X / Doc.m_SystemParam.HelicalPitch;
                    double Y = (y - y1) * Doc.m_SystemParam.HelicalPitchMap.Y / Doc.m_SystemParam.HelicalPitch;
                    TestMeta testMeta = new TestMeta(new XElement("TestMeta"));
                    //if (testMeta.MetaXElement == null)
                    //    testMeta.MetaXElement = new XElement("TestMeta");
                    testMeta.Name = (string)meta.Element("Name");
                    testMeta.IsManual = false;
                    testMeta.Position = new System.Windows.Point(X, Y);//元件在拍照图像上相对左下角的位置 单位：像素
                    testMeta.Rotation = double.Parse((string)meta.Element("Rotation")); ;
                    testMeta.MetaXElement.Add(meta.Elements("Code"));
                    testMeta.Threshold = (int)Doc.m_SystemParam.MetaThreshold;
                    testMeta.PadThreshold = (int)Doc.m_SystemParam.PadThreshold;
                    testMeta.Test = false;//默认不测试
                    
                   

                    System.Windows.Shapes.Rectangle rect = new System.Windows.Shapes.Rectangle()
                    {
                        Name = (string)meta.Element("Name"),
                        Fill = new SolidColorBrush() { Color = Colors.Red, Opacity = 0.0 },
                        Stroke = new SolidColorBrush(Colors.White),
                        StrokeThickness = 3,
                        Width = 50,
                        Height = 50,
                    };
                    rect.DataContext = meta;
                    if (checkboxMetaMatch.IsChecked == true)
                    {//所有元件均从元件库中自动匹配
                        //bool bFlag = false;
                        List<MetaItem> metaItemList = new List<MetaItem>();
                        IEnumerable<XElement> CodeList = meta.Elements("Code");
                        double rotation = double.Parse((string)meta.Element("Rotation"));
                        foreach (XElement code in CodeList)
                        {
                            string name = code.Value;
                            foreach (MetaItem metaItem in Doc.m_MetaLib.Items)
                            {
                                if (metaItem.Name == name)
                                {
                                    testMeta.Polarity = metaItem.Polarity;//默认为无极性元件
                                    testMeta.TypeName = metaItem.TypeName;
                                    metaItemList.Add(metaItem);
                                }
                            }
                        }
                       
                        if (metaItemList.Count != 0)//库中无匹配的元件，继续下一个元件
                        {
                            rect.DataContext = metaItemList;
                            foreach (MetaItem metaItem in metaItemList)
                            {//将匹配到的元件更改元件框的大小
                                if (rotation % 360 == 90 || rotation % 360 == 270)
                                {
                                    rect.Width = metaItem.MetaHeight;
                                    rect.Height = metaItem.MetaWidth;
                                }
                                else
                                {
                                    rect.Width = metaItem.MetaWidth;
                                    rect.Height = metaItem.MetaHeight;
                                }
                                break;
                            }
                        }

                    }

                    testCell.XElement.Add(testMeta.MetaXElement);
                    rect.SetValue(Canvas.LeftProperty, canvasCameraImage.Width - X - rect.Width / 2);
                    rect.SetValue(Canvas.TopProperty, canvasCameraImage.Height - Y - rect.Height / 2);
                    canvasCameraImage.Children.Add(rect);
                    System.Windows.Controls.ContextMenu contextMenu = new System.Windows.Controls.ContextMenu();
                    System.Windows.Controls.MenuItem menuItem = new System.Windows.Controls.MenuItem() { Header = "元件匹配" };
                    menuItem.Click += MetaMatch_Click;
                    contextMenu.Items.Add(menuItem);
                    menuItem = new System.Windows.Controls.MenuItem() { Header = "添加测试" };
                    menuItem.Command = CustomCommand.CommandAddMeta;
                    contextMenu.Items.Add(menuItem);
                    menuItem = new System.Windows.Controls.MenuItem() { Header = "取消测试" };
                    menuItem.Click += TestCancle_Click;
                    contextMenu.Items.Add(menuItem);
                    menuItem = new System.Windows.Controls.MenuItem() { Header = "极性检测", IsCheckable = true, IsChecked = false };
                    menuItem.Click += IsPolarity_Click;
                    contextMenu.Items.Add(menuItem);
                    menuItem = new System.Windows.Controls.MenuItem() { Header = "旋转检测框" };
                    menuItem.Click += RotateCheckItem_Click;
                    contextMenu.Items.Add(menuItem);
                    menuItem = new System.Windows.Controls.MenuItem() { Header = "元件旋转" };
                    menuItem.Click += RotateMeta_Click;
                    contextMenu.Items.Add(menuItem);
                    menuItem = new System.Windows.Controls.MenuItem() { Header = "添加到元件库" };
                    menuItem.Click += AddToLib_Click;
                    contextMenu.Items.Add(menuItem);
                    menuItem = new System.Windows.Controls.MenuItem() { Header = "设置阈值" };
                    menuItem.Click += MetaSetThreshold_Click;
                    contextMenu.Items.Add(menuItem);
                    rect.ContextMenu = contextMenu;
                }
            }

            if (checkboxMulti.IsChecked == true)
            {
                testRoute.XElement.Element("SubRectangle").Add(testCell.XElement);
            }
            else
            {
                if (testRoute.AddTestCell(testCell.XElement) == false)
                    return;
            }


            #region 焊盘显示

            //foreach (XElement pad in m_TestProductType.XPadData.Elements("Pad"))
            //{
            //    if (String.IsNullOrEmpty((string)pad.Element("Type")))
            //        continue;

            //    switch ((PadType)Enum.Parse(typeof(PadType), (string)pad.Element("Type")))
            //    {
            //        case PadType.Ellipse:
            //            double x = System.Windows.Point.Parse((string)pad.Element("Position")).X;
            //            double y = System.Windows.Point.Parse((string)pad.Element("Position")).Y;
            //            if (x > x1 && x < x2 && y > y1 && y < y2)
            //            {//焊盘在拍照图像范围内

            //                //元件在拍照图像上的位置，单位：像素
            //                double X = (x - x1) * Doc.m_SystemParam.HelicalPitchMap.X / Doc.m_SystemParam.HelicalPitch;
            //                double Y = (y - y1) * Doc.m_SystemParam.HelicalPitchMap.Y / Doc.m_SystemParam.HelicalPitch;
            //                DrawEllipsePad((double)pad.Element("Width") * Doc.m_SystemParam.HelicalPitchMap.X / Doc.m_SystemParam.HelicalPitch, (double)pad.Element("Height") * Doc.m_SystemParam.HelicalPitchMap.Y / Doc.m_SystemParam.HelicalPitch, X, Y);

            //            }
            //            break;
            //        case PadType.Line:
            //            double xs = System.Windows.Point.Parse((string)pad.Element("PosStart")).X;
            //            double ys = System.Windows.Point.Parse((string)pad.Element("PosStart")).Y;
            //            double xe = System.Windows.Point.Parse((string)pad.Element("PosEnd")).X;
            //            double ye = System.Windows.Point.Parse((string)pad.Element("PosEnd")).Y;
            //            if ((xs > x1 && xs < x2 && ys > y1 && ys < y2) || (xe > x1 && xe < x2 && ye > y1 && ye < y2))
            //            {
            //                double XS = (xs - x1) * Doc.m_SystemParam.HelicalPitchMap.X / Doc.m_SystemParam.HelicalPitch;
            //                double YS = (ys - y1) * Doc.m_SystemParam.HelicalPitchMap.Y / Doc.m_SystemParam.HelicalPitch;
            //                double XE = (xe - x1) * Doc.m_SystemParam.HelicalPitchMap.X / Doc.m_SystemParam.HelicalPitch;
            //                double YE = (ye - y1) * Doc.m_SystemParam.HelicalPitchMap.Y / Doc.m_SystemParam.HelicalPitch;
            //                DrawLinePad(XS, YS, XE, YE, (double)pad.Element("Width") * Doc.m_SystemParam.HelicalPitchMap.X / Doc.m_SystemParam.HelicalPitch);

            //            }
            //            break;
            //        case PadType.Rectangle:
            //            double xr = System.Windows.Point.Parse((string)pad.Element("Position")).X;
            //            double yr = System.Windows.Point.Parse((string)pad.Element("Position")).Y;
            //            if (xr > x1 && xr < x2 && yr > y1 && yr < y2)
            //            {//焊盘在拍照图像范围内
            //                //元件在拍照图像上的位置，单位：像素
            //                double X = (xr - x1) * Doc.m_SystemParam.HelicalPitchMap.X / Doc.m_SystemParam.HelicalPitch;
            //                double Y = (yr - y1) * Doc.m_SystemParam.HelicalPitchMap.Y / Doc.m_SystemParam.HelicalPitch;
            //                DrawRectanglePad((double)pad.Element("Width") * Doc.m_SystemParam.HelicalPitchMap.X / Doc.m_SystemParam.HelicalPitch, (double)pad.Element("Height") * Doc.m_SystemParam.HelicalPitchMap.Y / Doc.m_SystemParam.HelicalPitch, X, Y, (double)pad.Element("Rotation"));
            //            }
            //            break;
            //    }
            //}
            #endregion 焊盘显示
            if (checkboxPathShow.IsChecked == true)
            {//显示编程路径

                canvasProgramThumbnail.Children.Clear();
                PathShow();
            }
            //保存编程数据文件
            if (App.m_ShowState.CurrentProductType.XElement != null)
                App.m_ShowState.CurrentProductType.XElement.Save(string.Format(Doc.m_SystemParam.SetupPath + "\\" + Doc.m_SystemParam.ProductFilename));
            MessageBox.Show("测试点添加成功！",
                       Doc.MESSAGE_SOFTNAME,
                       MessageBoxButton.OK, MessageBoxImage.Exclamation);

            m_xCurrentCell = testCell.XElement;
        }

        #region 焊盘图形

        /// <summary>
        /// 椭圆焊盘
        /// </summary>
        /// <param name="width">直径</param>
        /// <param name="X">位置X</param>
        /// <param name="Y">位置Y</param>
        //public void DrawEllipsePad(double width, double height, double X, double Y)
        //{

        //    System.Windows.Shapes.Path ellipsePad = new System.Windows.Shapes.Path()
        //    {
        //        Fill = new SolidColorBrush() { Color = Colors.Yellow, Opacity = 0.3 },
        //        Stroke = new SolidColorBrush() { Color = Colors.Yellow },
        //        StrokeThickness = 0,
        //    };
        //    EllipseGeometry elli = new EllipseGeometry() { Center = new System.Windows.Point(X, canvasCameraImage.Height - Y), RadiusX = width / 2, RadiusY = height / 2 };
        //    ellipsePad.Data = elli;
        //    canvasCameraImage.Children.Add(ellipsePad);

        //}
        /// <summary>
        /// 线状焊盘
        /// </summary>
        /// <param name="x1">起点x</param>
        /// <param name="y1">起点y</param>
        /// <param name="x2">终点x</param>
        /// <param name="y2">终点y</param>
        /// <param name="width">线宽</param>
        //public void DrawLinePad(double X1, double Y1, double X2, double Y2, double width)
        //{

        //    System.Windows.Shapes.Path linePad = new System.Windows.Shapes.Path()
        //    {
        //        Fill = new SolidColorBrush() { Color = Colors.Yellow, Opacity = 0.3 },
        //        Stroke = new SolidColorBrush() { Color = Colors.Yellow },
        //        StrokeThickness = width
        //    };
        //    LineGeometry line = new LineGeometry() { StartPoint = new System.Windows.Point(X1, canvasCameraImage.Height - Y1), EndPoint = new System.Windows.Point(X2, canvasCameraImage.Height - Y2), };
        //    linePad.Data = line;
        //    canvasCameraImage.Children.Add(linePad);
        //}
        /// <summary>
        /// 矩形焊盘
        /// </summary>
        /// <param name="width">宽</param>
        /// <param name="height">高</param>
        /// <param name="X">位置X</param>
        /// <param name="Y">位置Y</param>
        public void DrawRectPad(double width, double height, double X, double Y, double angle)
        {

            System.Windows.Shapes.Path rectanglePad = new System.Windows.Shapes.Path()
            {
                Fill = new SolidColorBrush() { Color = Colors.Yellow, Opacity = 1 },
                Stroke = new SolidColorBrush() { Color = Colors.Yellow },
                StrokeThickness = 3,
            };
            rectanglePad.SetValue(Canvas.LeftProperty, canvasCameraImage.Width - X);
            rectanglePad.SetValue(Canvas.TopProperty, canvasCameraImage.Height - Y);
            RectangleGeometry r = new RectangleGeometry() { Rect = new Rect(-width / 2, -height / 2, width, height), Transform = new RotateTransform() { Angle = angle } };
            //RectangleGeometry rect = new RectangleGeometry() { Rect = new Rect(X - width / 2, canvasCameraImage.Height - Y - height / 2, width, height), Transform = new RotateTransform() { Angle = angle, CenterX = X, CenterY = canvasCameraImage.Height - Y } };
            rectanglePad.Data = r;
            canvasCameraImage.Children.Add(rectanglePad);
        }


        //public void DrawRectanglePad(double width, double height, double X, double Y, double angle)
        //{

        //    System.Windows.Shapes.Path rectanglePad = new System.Windows.Shapes.Path()
        //    {
        //        Fill = new SolidColorBrush() { Color = Colors.Yellow, Opacity = 0.3 },
        //        Stroke = new SolidColorBrush() { Color = Colors.Yellow },
        //        StrokeThickness = 0,
        //    };
        //    //rectanglePad.SetValue(Canvas.LeftProperty, X);
        //    //rectanglePad.SetValue(Canvas.TopProperty, canvasCameraImage.Height - Y);
        //    //RectangleGeometry r = new RectangleGeometry() { Rect = new Rect(-width / 2, -height / 2, width, height), Transform = new RotateTransform() { Angle = angle } };
        //    RectangleGeometry rect = new RectangleGeometry() { Rect = new Rect(X - width / 2, canvasCameraImage.Height - Y - height / 2, width, height), Transform = new RotateTransform() { Angle = angle, CenterX = X, CenterY = canvasCameraImage.Height - Y } };
        //    rectanglePad.Data = rect;
        //    canvasCameraImage.Children.Add(rectanglePad);
        //}
        #endregion 焊盘图形

        private System.Windows.Shapes.Rectangle GetContextMenuRectangle(object sender)
        {
            if ((sender is System.Windows.Controls.MenuItem) == false)
                return null;
            System.Windows.Controls.MenuItem mi = sender as System.Windows.Controls.MenuItem;
            if (mi.Parent == null || (mi.Parent is System.Windows.Controls.ContextMenu) == false)
                return null;
            System.Windows.Controls.ContextMenu cm = mi.Parent as System.Windows.Controls.ContextMenu;
            if (cm.PlacementTarget == null || (cm.PlacementTarget is System.Windows.Shapes.Rectangle) == false)
                return null;
            return cm.PlacementTarget as System.Windows.Shapes.Rectangle;
        }

        private System.Windows.Shapes.Path GetContextMenuPath(object sender)
        {
            if ((sender is System.Windows.Controls.MenuItem) == false)
                return null;
            System.Windows.Controls.MenuItem mi = sender as System.Windows.Controls.MenuItem;
            if (mi.Parent == null || (mi.Parent is System.Windows.Controls.ContextMenu) == false)
                return null;
            System.Windows.Controls.ContextMenu cm = mi.Parent as System.Windows.Controls.ContextMenu;
            if (cm.PlacementTarget == null || (cm.PlacementTarget is System.Windows.Shapes.Path) == false)
                return null;
            return cm.PlacementTarget as System.Windows.Shapes.Path;
        }
        /// <summary>
        /// 增加拼版
        /// </summary>             
        private void OnClick_AddTestArea(object sender, RoutedEventArgs e)
        {

            if (rb_ByGerber.IsChecked == true)
            {
                #region 通过gerber文件定位子板
                if (App.m_ShowState.CurrentProductType.XTestRoute.Element("SubRectangle") == null)
                    return;
                if (App.m_ShowState.CurrentProductType.XPadData == null)
                    return;

                System.Windows.Point subRectPos = new System.Windows.Point(0, 0);
                double subRectWidth = 0;
                double subRectHeight = 0;
                XElement xSubRectData = null;//第一子板矩形区测试CELL数据
                foreach (XElement xTmpSubRectangle in App.m_ShowState.CurrentProductType.XTestRoute.Elements("SubRectangle"))
                {//复制：取第一子板矩形区测试CELL数据
                    xSubRectData = new XElement(xTmpSubRectangle);

                    subRectWidth = double.Parse((string)xTmpSubRectangle.Element("Width"));
                    subRectHeight = double.Parse((string)xTmpSubRectangle.Element("Height"));
                    subRectPos = System.Windows.Point.Parse((string)xTmpSubRectangle.Element("Position"));
                    break;
                }
                foreach (XElement xCell in xSubRectData.Elements("TestCell"))
                {//测试CELL中心点相对第一子板矩形区域中心的位置,单位mm
                    TestCell testCell = new TestCell(xCell);

                    double x = testCell.Position.X - System.Windows.Point.Parse((string)xSubRectData.Element("Position")).X;
                    double y = testCell.Position.Y - System.Windows.Point.Parse((string)xSubRectData.Element("Position")).Y;
                    testCell.Position = new System.Windows.Point(x, y);
                }

                double x1 = subRectPos.X - subRectWidth / 2;
                double y1 = subRectPos.Y - subRectHeight / 2;
                double x2 = subRectPos.X + subRectWidth / 2;
                double y2 = subRectPos.Y + subRectHeight / 2;
                double dOld = 0, dNew = 0;
                XElement xCurPad = null;//距离第一子板矩形中心距离最短的焊盘；
                foreach (XElement xPad in App.m_ShowState.CurrentProductType.XPadData.Elements("Pad"))
                {// 寻找相对于第一子板中心距离最短的焊盘
                    if (xPad.Element("Position") != null)
                    {
                        double x = System.Windows.Point.Parse((string)xPad.Element("Position")).X;
                        double y = System.Windows.Point.Parse((string)xPad.Element("Position")).Y;
                        if (x > x1 && x < x2 && y > y1 && y < y2)
                        {//焊盘在单板区域内
                            System.Windows.Point pPadToSubRect = new System.Windows.Point(x - subRectPos.X, y - subRectPos.Y);//焊盘相对于子板中心的位置
                            if (xCurPad == null)
                            {
                                dOld = Math.Pow(pPadToSubRect.X, 2) + Math.Pow(pPadToSubRect.Y, 2);//焊盘相对于子板中心距离的平方值
                                xCurPad = xPad;
                            }
                            else dNew = Math.Pow(pPadToSubRect.X, 2) + Math.Pow(pPadToSubRect.Y, 2);

                            if (dNew != 0)
                            {// 比较，找距离最短的
                                if (dOld > dNew)
                                {
                                    dOld = dNew;
                                    xCurPad = xPad;
                                }
                            }
                        }
                    }
                }
                if (xCurPad == null)
                    return;

                //第一子板区域焊盘网格拓扑图数据
                XElement xSubRectPadMesh = new XElement("PadMesh");

                double dMinPadX = System.Windows.Point.Parse((string)xCurPad.Element("Position")).X;
                double dMinPadY = System.Windows.Point.Parse((string)xCurPad.Element("Position")).Y;
                System.Windows.Point pMinPad = new System.Windows.Point(dMinPadX - subRectPos.X, dMinPadY - subRectPos.Y);//相对子板中心的第一焊盘
                                                                                                                          //加入第一焊盘
                xSubRectPadMesh.Add(new XElement("Pad",
                                    new XElement(xCurPad.Element("Type")),
                                    new XElement("Position", new System.Windows.Point(0, 0)),//距离第一点的位置
                                    new XElement("DCenter", pMinPad)));//距离子板中心的位置
                XElement xCopySubRectPadMesh = new XElement("PadMash");
                foreach (XElement xPad in App.m_ShowState.CurrentProductType.XPadData.Elements("Pad"))
                {
                    if (xPad.Element("Position") != null)
                    {
                        double x = System.Windows.Point.Parse((string)xPad.Element("Position")).X;
                        double y = System.Windows.Point.Parse((string)xPad.Element("Position")).Y;
                        if (x > x1 && x < x2 && y > y1 && y < y2)
                        {//焊盘在单板区域内
                            if (xPad == xCurPad)
                                continue;

                            System.Windows.Point pPadToMinPad = new System.Windows.Point(x - dMinPadX, y - dMinPadY);//相对第一焊盘的距离
                            xCopySubRectPadMesh.Add(new XElement("Pad",                         //焊盘
                                                new XElement(xPad.Element("Type")),      //焊盘类型
                                                new XElement("Position", pPadToMinPad)));  //焊盘相对第一焊盘的位置
                        }
                    }
                }
                XElement xCurrentPad = null;
                int num = PADNUMBER;
                while (true)
                {//按距离新增子板区域中心由近到远排序后的焊盘
                    IEnumerable<XElement> xPads = xCopySubRectPadMesh.Elements("Pad");

                    if (xPads.Count() <= 0)
                        break;
                    if (num <= 0)
                        break;

                    dOld = 0;
                    dNew = 0;
                    xCurrentPad = null;
                    foreach (XElement xPad in xPads)
                    {
                        double x = System.Windows.Point.Parse((string)xPad.Element("Position")).X;
                        double y = System.Windows.Point.Parse((string)xPad.Element("Position")).Y;
                        if (xCurrentPad == null)
                        {
                            dOld = Math.Pow(x, 2) + Math.Pow(y, 2);//焊盘相对于子板中心距离的平方值
                            xCurrentPad = xPad;
                        }
                        else dNew = Math.Pow(x, 2) + Math.Pow(y, 2);

                        if (dNew != 0)
                        {// 比较，找距离最短的
                            if (dOld > dNew)
                            {
                                dOld = dNew;
                                xCurrentPad = xPad;
                            }
                        }
                    }
                    if (xCurrentPad != null)
                    {
                        xCurrentPad.Remove();
                        xSubRectPadMesh.Add(xCurrentPad);
                    }
                    num--;
                }

                double dCentX = System.Windows.Point.Parse((string)xSubRectData.Element("Position")).X + 20;//拼板第一矩形区中心X位置（相对PCB起点，单位毫米）+ 20
                double dCentY = System.Windows.Point.Parse((string)xSubRectData.Element("Position")).Y + 20;//拼板第一矩形区中心Y位置（相对PCB起点，单位毫米）+ 20
                double RectCenterX = App.m_ShowState.CurrentProductType.ThumbnailWidth - dCentX * App.m_ShowState.CurrentProductType.ThumbnailWidth / App.m_ShowState.CurrentProductType.MotionWidth;
                double RectCenterY = App.m_ShowState.CurrentProductType.ThumbnailHeight - dCentY * App.m_ShowState.CurrentProductType.ThumbnailWidth / App.m_ShowState.CurrentProductType.MotionWidth;
                double left = RectCenterX - double.Parse((string)xSubRectData.Element("Width")) * App.m_ShowState.CurrentProductType.ThumbnailWidth / App.m_ShowState.CurrentProductType.MotionWidth / 2;
                double top = RectCenterY - double.Parse((string)xSubRectData.Element("Height")) * App.m_ShowState.CurrentProductType.ThumbnailHeight / App.m_ShowState.CurrentProductType.MotionHeight / 2;
                m_rectThumbnailAddArea = new System.Windows.Shapes.Rectangle() { Stroke = new SolidColorBrush(Colors.Red), StrokeThickness = 5, Width = double.Parse((string)xSubRectData.Element("Width")) * App.m_ShowState.CurrentProductType.ThumbnailWidth / App.m_ShowState.CurrentProductType.MotionWidth, Height = double.Parse((string)xSubRectData.Element("Height")) * App.m_ShowState.CurrentProductType.ThumbnailHeight / App.m_ShowState.CurrentProductType.MotionHeight };
                Canvas.SetTop(m_rectThumbnailAddArea, top);
                Canvas.SetLeft(m_rectThumbnailAddArea, left);
                canvasProgramThumbnail.Children.Add(m_rectThumbnailAddArea);
                RegionDataLocationDlg regionDataLocationDlg = new RegionDataLocationDlg();
                regionDataLocationDlg.Owner = this;
                regionDataLocationDlg.PosX = dCentX;
                regionDataLocationDlg.PosY = dCentY;
                regionDataLocationDlg.SubBoardWidth = double.Parse((string)xSubRectData.Element("Width"));
                regionDataLocationDlg.SubBoardHeight = double.Parse((string)xSubRectData.Element("Height")); ;
                regionDataLocationDlg.XSubRectPadMesh = xSubRectPadMesh;
                regionDataLocationDlg.ShowDialog();
                // 取消退出
                if (regionDataLocationDlg.DialogResult == false)
                {
                    canvasProgramThumbnail.Children.Remove(m_rectThumbnailAddArea);
                    return;
                }
                if (regionDataLocationDlg.MultiBoardRegionMatch == false)
                {
                    MessageBox.Show("没有识别到拼板位置，不允许增加拼板！",
                                    Doc.MESSAGE_SOFTNAME,
                                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    canvasProgramThumbnail.Children.Remove(m_rectThumbnailAddArea);
                    return;
                }
                // 获取识别到拼板位置和设置参数
                double rotation = regionDataLocationDlg.Angle;
                XElement xNewSubRectangle = new XElement("SubRectangle");
                xNewSubRectangle.Add(new XElement("Width", double.Parse((string)xSubRectData.Element("Width"))));
                xNewSubRectangle.Add(new XElement("Height", double.Parse((string)xSubRectData.Element("Height"))));
                xNewSubRectangle.Add(new XElement("Rotation", rotation));
                xNewSubRectangle.Add(new XElement("Position", new System.Windows.Point(regionDataLocationDlg.PosX, regionDataLocationDlg.PosY)));
                xNewSubRectangle.Add(xSubRectData.Elements("TestCell"));
                foreach (XElement cell in xNewSubRectangle.Elements("TestCell"))
                {
                    TestCell testCell = new TestCell(cell);
                    double cellPosX = testCell.Position.X;
                    double cellPosY = testCell.Position.Y;
                    //计算TestCell旋转后的相对位置 mm
                    double newCellPosX = cellPosX * Math.Cos(rotation * Math.PI / 180) - cellPosY * Math.Sin(rotation * Math.PI / 180);
                    double newCellPosY = cellPosX * Math.Sin(rotation * Math.PI / 180) + cellPosY * Math.Cos(rotation * Math.PI / 180);
                    // 将TestCell的位置转换为运动系统中的绝对位置 mm
                    double PosX = newCellPosX + regionDataLocationDlg.PosX;
                    double PosY = newCellPosY + regionDataLocationDlg.PosY;
                    testCell.Position = new System.Windows.Point(PosX, PosY);
                    foreach (XElement meta in testCell.XElement.Elements("TestMeta"))
                    {
                        //元件相对于图像中心的位置
                        double metaPosX = System.Windows.Point.Parse((string)meta.Element("Position")).X - canvasCameraImage.Width / 2;
                        double metaPosY = System.Windows.Point.Parse((string)meta.Element("Position")).Y - canvasCameraImage.Height / 2;
                        //旋转后元件相对于图像左下角的位置
                        double newMetaPosX = metaPosX * Math.Cos(rotation * Math.PI / 180) - metaPosY * Math.Sin(rotation * Math.PI / 180) + canvasCameraImage.Width / 2;
                        double newMetaPosY = metaPosX * Math.Sin(rotation * Math.PI / 180) + metaPosY * Math.Cos(rotation * Math.PI / 180) + canvasCameraImage.Height / 2;
                        double newRotation = double.Parse((string)meta.Element("Rotation")) + rotation;
                        meta.SetElementValue("Position", new System.Windows.Point(newMetaPosX, newMetaPosY));
                        meta.SetElementValue("Rotation", newRotation);
                    }
                }
                App.m_ShowState.CurrentProductType.XTestRoute.Add(xNewSubRectangle);
                canvasProgramThumbnail.Children.Clear();
                PathShow();
                #endregion 通过gerber文件定位子板
            }
            else
            {
                #region 通过坐标文件定位子板
                if (App.m_ShowState.CurrentProductType.XTestRoute.Element("SubRectangle") == null)
                    return;
                if (App.m_ShowState.CurrentProductType.XMetaData == null)
                    return;

                System.Windows.Point subRectPos = new System.Windows.Point(0, 0);
                double subRectWidth = 0;
                double subRectHeight = 0;
                XElement xSubRectData = null;//第一子板矩形区测试CELL数据
                foreach (XElement xTmpSubRectangle in App.m_ShowState.CurrentProductType.XTestRoute.Elements("SubRectangle"))
                {//复制：取第一子板矩形区测试CELL数据
                    xSubRectData = new XElement(xTmpSubRectangle);

                    subRectWidth = double.Parse((string)xTmpSubRectangle.Element("Width"));
                    subRectHeight = double.Parse((string)xTmpSubRectangle.Element("Height"));
                    subRectPos = System.Windows.Point.Parse((string)xTmpSubRectangle.Element("Position"));
                    break;
                }
                foreach (XElement xCell in xSubRectData.Elements("TestCell"))
                {//测试CELL中心点相对第一子板矩形区域中心的位置,单位mm
                    TestCell testCell = new TestCell(xCell);

                    double x = testCell.Position.X - System.Windows.Point.Parse((string)xSubRectData.Element("Position")).X;
                    double y = testCell.Position.Y - System.Windows.Point.Parse((string)xSubRectData.Element("Position")).Y;
                    testCell.Position = new System.Windows.Point(x, y);
                }
                //第一子板区域焊盘网格拓扑图数据
                XElement xSubRectMetaMesh = new XElement("MetaMesh");
                double x1 = subRectPos.X - subRectWidth / 2;
                double y1 = subRectPos.Y - subRectHeight / 2;
                double x2 = subRectPos.X + subRectWidth / 2;
                double y2 = subRectPos.Y + subRectHeight / 2;
                foreach (XElement xMeta in App.m_ShowState.CurrentProductType.XMetaData.Elements("Meta"))
                {
                    string S = (string)xMeta.Element("Name");
                    if (string.Equals(S, "mark", StringComparison.CurrentCultureIgnoreCase) || string.Equals(S, "mark1", StringComparison.CurrentCultureIgnoreCase) || string.Equals(S, "mark2", StringComparison.CurrentCultureIgnoreCase))
                        continue;
                    double x = System.Windows.Point.Parse((string)xMeta.Element("Position")).X;
                    double y = System.Windows.Point.Parse((string)xMeta.Element("Position")).Y;
                    if (x > x1 && x < x2 && y > y1 && y < y2)
                    {//元件在单板区域内
                        System.Windows.Point pMetaToSubRect = new System.Windows.Point(x - subRectPos.X, y - subRectPos.Y);//焊盘相对于子板中心的位置
                        string sName = (string)xMeta.Element("Name");
                        xSubRectMetaMesh.Add(new XElement("Name", sName));
                        xSubRectMetaMesh.Add(new XElement("Position", pMetaToSubRect));
                        break;
                    }

                }
                double dCentX = System.Windows.Point.Parse((string)xSubRectData.Element("Position")).X + 20;//拼板第一矩形区中心X位置（相对PCB起点，单位毫米）+ 20
                double dCentY = System.Windows.Point.Parse((string)xSubRectData.Element("Position")).Y + 20;//拼板第一矩形区中心Y位置（相对PCB起点，单位毫米）+ 20
                double RectCenterX = App.m_ShowState.CurrentProductType.ThumbnailWidth - dCentX * App.m_ShowState.CurrentProductType.ThumbnailWidth / App.m_ShowState.CurrentProductType.MotionWidth;
                double RectCenterY = App.m_ShowState.CurrentProductType.ThumbnailHeight - dCentY * App.m_ShowState.CurrentProductType.ThumbnailWidth / App.m_ShowState.CurrentProductType.MotionWidth;
                double left = RectCenterX - double.Parse((string)xSubRectData.Element("Width")) * App.m_ShowState.CurrentProductType.ThumbnailWidth / App.m_ShowState.CurrentProductType.MotionWidth / 2;
                double top = RectCenterY - double.Parse((string)xSubRectData.Element("Height")) * App.m_ShowState.CurrentProductType.ThumbnailHeight / App.m_ShowState.CurrentProductType.MotionHeight / 2;
                m_rectThumbnailAddArea = new System.Windows.Shapes.Rectangle() { Stroke = new SolidColorBrush(Colors.Red), StrokeThickness = 5, Width = double.Parse((string)xSubRectData.Element("Width")) * App.m_ShowState.CurrentProductType.ThumbnailWidth / App.m_ShowState.CurrentProductType.MotionWidth, Height = double.Parse((string)xSubRectData.Element("Height")) * App.m_ShowState.CurrentProductType.ThumbnailHeight / App.m_ShowState.CurrentProductType.MotionHeight };
                Canvas.SetTop(m_rectThumbnailAddArea, top);
                Canvas.SetLeft(m_rectThumbnailAddArea, left);
                canvasProgramThumbnail.Children.Add(m_rectThumbnailAddArea);
                RegionDataLocationPosDlg regionDataLocationDlg = new RegionDataLocationPosDlg();
                regionDataLocationDlg.Owner = this;
                regionDataLocationDlg.PosX = dCentX;
                regionDataLocationDlg.PosY = dCentY;
                regionDataLocationDlg.SubBoardWidth = double.Parse((string)xSubRectData.Element("Width"));
                regionDataLocationDlg.SubBoardHeight = double.Parse((string)xSubRectData.Element("Height"));
                regionDataLocationDlg.XSubRectMetaMesh = xSubRectMetaMesh;
                regionDataLocationDlg.ShowDialog();
                // 取消退出
                if (regionDataLocationDlg.DialogResult == false)
                {
                    canvasProgramThumbnail.Children.Remove(m_rectThumbnailAddArea);
                    return;
                }
                if (regionDataLocationDlg.MultiBoardRegionMatch == false)
                {
                    MessageBox.Show("没有识别到拼板位置，不允许增加拼板！",
                                    Doc.MESSAGE_SOFTNAME,
                                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    canvasProgramThumbnail.Children.Remove(m_rectThumbnailAddArea);
                    return;
                }
                // 获取识别到拼板位置和设置参数
                double rotation = regionDataLocationDlg.Angle;
                XElement xNewSubRectangle = new XElement("SubRectangle");
                xNewSubRectangle.Add(new XElement("Width", double.Parse((string)xSubRectData.Element("Width"))));
                xNewSubRectangle.Add(new XElement("Height", double.Parse((string)xSubRectData.Element("Height"))));
                xNewSubRectangle.Add(new XElement("Rotation", rotation));
                xNewSubRectangle.Add(new XElement("Position", new System.Windows.Point(regionDataLocationDlg.PosX, regionDataLocationDlg.PosY)));
                xNewSubRectangle.Add(xSubRectData.Elements("TestCell"));

                foreach (XElement cell in xNewSubRectangle.Elements("TestCell"))
                {

                    TestCell testCell = new TestCell(cell);
                    double cellPosX = testCell.Position.X;
                    double cellPosY = testCell.Position.Y;
                    //计算TestCell旋转后的相对位置 mm
                    double newCellPosX = cellPosX * Math.Cos(rotation * Math.PI / 180) - cellPosY * Math.Sin(rotation * Math.PI / 180);
                    double newCellPosY = cellPosX * Math.Sin(rotation * Math.PI / 180) + cellPosY * Math.Cos(rotation * Math.PI / 180);

                    // 将TestCell的位置转换为运动系统中的绝对位置 mm
                    double PosX = newCellPosX + regionDataLocationDlg.PosX;
                    double PosY = newCellPosY + regionDataLocationDlg.PosY;

                    testCell.Position = new System.Windows.Point(PosX, PosY);

                    foreach (XElement meta in testCell.XElement.Elements("TestMeta"))
                    {
                        //元件相对于图像中心的位置
                        double metaPosX = System.Windows.Point.Parse((string)meta.Element("Position")).X - canvasCameraImage.Width / 2;
                        double metaPosY = System.Windows.Point.Parse((string)meta.Element("Position")).Y - canvasCameraImage.Height / 2;

                        //旋转后元件相对于图像左下角的位置
                        double newMetaPosX = metaPosX * Math.Cos(rotation * Math.PI / 180) - metaPosY * Math.Sin(rotation * Math.PI / 180) + canvasCameraImage.Width / 2;
                        double newMetaPosY = metaPosX * Math.Sin(rotation * Math.PI / 180) + metaPosY * Math.Cos(rotation * Math.PI / 180) + canvasCameraImage.Height / 2;

                        double newRotation = double.Parse((string)meta.Element("Rotation")) + rotation;
                        meta.SetElementValue("Position", new System.Windows.Point(newMetaPosX, newMetaPosY));
                        meta.SetElementValue("Rotation", newRotation);
                    }

                }
                m_TestProductType.XTestRoute.Add(xNewSubRectangle);
                canvasProgramThumbnail.Children.Clear();
                PathShow();

                #endregion 通过坐标文件定位子板

            }

        }


        /// <summary>
        /// 拼板的子板区识别
        /// </summary>
        /// <param name="dPosX">子板区的中心相对缩略起点的X位置</param>
        /// <param name="dPosY">子板区的中心相对缩略起点的Y位置</param>
        /// <param name="dAngle">子板区的旋转角度</param>
        public bool SubBoardRegionTest(ref double dPosX, ref double dPosY, double width, double height, double dAngle, XElement xSubRectPadMesh)
        {
            if (App.m_Motion.MotionEnable() == false || xSubRectPadMesh == null)
                return false;

            if (App.m_ShowState.CurrentProductType == null || App.m_ShowState.CurrentProductType.XTestRoute == null)
            {
                MessageBox.Show("产品型号或测试路径设置为空，不允许测试！",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }
            TestRoute tr = new TestRoute();
            tr.XElement = App.m_ShowState.CurrentProductType.XTestRoute;
            if (bool.Parse((string)tr.XElement.Element("MultiBoard")) == false)
            {// 单板
                MessageBox.Show("只允许拼板的子板区识别！",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }

            IEnumerable<XElement> subBoardList = tr.XElement.Elements("SubRectangle");
            if (subBoardList == null || subBoardList.Count() <= 0)
            {
                MessageBox.Show("第一子板区设置为空，不允许识别子板区！",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }
            foreach (XElement route in subBoardList)
            {
                if (route.Elements("TestCell").Count() <= 0)
                {
                    MessageBox.Show("第一子板测试路径个数设置为空，不允许识别子板区！",
                                    Doc.MESSAGE_SOFTNAME,
                                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return false;
                }
                break;
            }

            //找出第一子板的第一焊盘
            XElement xSourcePadHead = null;
            foreach (XElement xPad in xSubRectPadMesh.Elements("Pad"))
            {
                xSourcePadHead = xPad;
                break;
            }
            if (xSourcePadHead == null)
            {
                MessageBox.Show("第一子板焊盘网格数据不能为空，不允许识别子板区！",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }
            double dOld = 0, dNew = 0;


            //新增单板中心四分之一区左下角位置
            double x1 = 0;
            double y1 = 0;
            //新增单板中心四分之一区右上角角位置
            double x2 = 0;
            double y2 = 0;
            if (dAngle == 0 || dAngle == 180)
            {
                x1 = dPosX - width / 4;
                y1 = dPosY - height / 4;
                x2 = dPosX + width / 4;
                y2 = dPosY + height / 4;
            }
            if (dAngle == 90 || dAngle == 270)
            {
                x1 = dPosX - height / 4;
                y1 = dPosY - width / 4;
                x2 = dPosX + height / 4;
                y2 = dPosY + width / 4;
            }
            XElement xPadList = new XElement("PadList");
            foreach (XElement xPad in App.m_ShowState.CurrentProductType.XPadData.Elements("Pad"))
            {// 取新增子板中心四分之一区域内的焊盘
                if (xPad.Element("Position") != null)
                {
                    double x = System.Windows.Point.Parse((string)xPad.Element("Position")).X;
                    double y = System.Windows.Point.Parse((string)xPad.Element("Position")).Y;
                    if (x > x1 && x < x2 && y > y1 && y < y2)
                    {//焊盘在单板中心四分之一区域内
                        xPadList.Add(xPad);
                    }
                }
            }

            XElement xCurPad = null;
            XElement xRstPadList = new XElement("PadList");//保存按距离新增子板区域中心有近到远排序后的焊盘
            while (true)
            {//按距离新增子板区域中心由近到远排序后的焊盘
                IEnumerable<XElement> xPads = xPadList.Elements("Pad");
                if (xPads.Count() <= 0)
                    break;

                dOld = 0;
                dNew = 0;
                xCurPad = null;
                foreach (XElement xPad in xPads)
                {
                    //焊盘相对于新增子板中心的位置
                    double x = System.Windows.Point.Parse((string)xPad.Element("Position")).X - dPosX;
                    double y = System.Windows.Point.Parse((string)xPad.Element("Position")).Y - dPosY;
                    if (xCurPad == null)
                    {
                        dOld = Math.Pow(x, 2) + Math.Pow(y, 2);//焊盘相对于子板中心距离的平方值
                        xCurPad = xPad;
                    }
                    else dNew = Math.Pow(x, 2) + Math.Pow(y, 2);

                    if (dNew != 0)
                    {// 比较，找距离最短的
                        if (dOld > dNew)
                        {
                            dOld = dNew;
                            xCurPad = xPad;
                        }
                    }
                }
                if (xCurPad != null)
                {
                    xCurPad.Remove();
                    xRstPadList.Add(xCurPad);
                }
            }
            int iMaxCount = 0;
            XElement xSeletPad = null;
            //int iMaxPad = xRstPadList.Elements("Pad").Count();
            //int iPadCount = 0;
            foreach (XElement xNewPad in xRstPadList.Elements("Pad"))
            {
                //iPadCount++;
                //只对同类型的焊盘进行MESH识别               
                if ((string)xNewPad.Element("Type") != (string)xSourcePadHead.Element("Type"))
                    continue;
                //按距离排序
                System.Windows.Point xNewPadPoint = System.Windows.Point.Parse((string)xNewPad.Element("Position"));
                xPadList = new XElement(xRstPadList);
                int iCount = 0;
                while (true)
                {//按距离xNewPad由近到远排序后的焊盘
                    IEnumerable<XElement> xPads = xPadList.Elements("Pad");
                    if (xPads.Count() <= 0)
                        break;
                    dOld = 0;
                    dNew = 0;
                    xCurPad = null;
                    bool bSame = false;
                    foreach (XElement xPad in xPads)
                    {
                        //焊盘相对于xNewPad的位置
                        double x = System.Windows.Point.Parse((string)xPad.Element("Position")).X - xNewPadPoint.X;
                        double y = System.Windows.Point.Parse((string)xPad.Element("Position")).Y - xNewPadPoint.Y;
                        if (x == 0 && y == 0)
                        {//xCurPad == xNewPad
                            xCurPad = xPad;
                            bSame = true;
                            break;
                        }

                        if (xCurPad == null)
                        {
                            dOld = Math.Pow(x, 2) + Math.Pow(y, 2);//焊盘相对于子板中心距离的平方值
                            xCurPad = xPad;
                            xCurPad.SetElementValue("Length", dOld);
                        }
                        else dNew = Math.Pow(x, 2) + Math.Pow(y, 2);
                        if (dNew != 0)
                        {// 比较，找距离最短的
                            if (dOld > dNew)
                            {
                                dOld = dNew;
                                xCurPad = xPad;
                                xCurPad.SetElementValue("Length", dNew);
                            }
                        }
                    }
                    // Mesh识别
                    if (xCurPad != null)
                    {
                        xCurPad.Remove();
                        if (bSame == false)
                        {
                            double xOld = System.Windows.Point.Parse((string)xCurPad.Element("Position")).X;
                            double yOld = System.Windows.Point.Parse((string)xCurPad.Element("Position")).Y;

                            double posX = (xOld - xNewPadPoint.X) * Math.Cos(dAngle * Math.PI / 180) - (yOld - xNewPadPoint.Y) * Math.Sin(dAngle * Math.PI / 180);
                            double posY = (xOld - xNewPadPoint.X) * Math.Sin(dAngle * Math.PI / 180) + (yOld - xNewPadPoint.Y) * Math.Cos(dAngle * Math.PI / 180);
                            //System.Windows.Point pPadToSubRect = new System.Windows.Point(posX, posY);
                            double sX = double.Parse(string.Format("{0:N2}", posX));
                            double sY = double.Parse(string.Format("{0:N2}", posY));
                            foreach (XElement xPad in xSubRectPadMesh.Elements("Pad"))
                            {
                                System.Windows.Point sourcePoint = System.Windows.Point.Parse((string)xPad.Element("Position"));
                                double sPx = double.Parse(string.Format("{0:N2}", sourcePoint.X));
                                double sPy = double.Parse(string.Format("{0:N2}", sourcePoint.Y));
                                if (sX == sPx && sY == sPy)
                                {
                                    iCount++;
                                    break;
                                }
                                //距离超出：退出循环
                                if ((Math.Pow(sourcePoint.X, 2) + Math.Pow(sourcePoint.Y, 2)) > Double.Parse((string)xCurPad.Element("Length")))
                                    break;
                            }
                            //排序后至少离最近的焊盘是相位匹配的，否则寻找下一个焊盘点
                            if (iCount == 0)
                                break;
                        }
                    }
                }
                if (iMaxCount < iCount)
                {
                    iMaxCount = iCount;
                    xSeletPad = xNewPad;
                    if (iMaxCount >= PADNUMBER)
                        break;
                }
            }
            if (xSeletPad == null)
                return false;
            //焊盘相对子板中心的位置
            double X = System.Windows.Point.Parse((string)xSourcePadHead.Element("DCenter")).X;
            double Y = System.Windows.Point.Parse((string)xSourcePadHead.Element("DCenter")).Y;
            //旋转后的位置
            double newX = X * Math.Cos(dAngle * Math.PI / 180) - Y * Math.Sin(dAngle * Math.PI / 180);
            double newY = X * Math.Sin(dAngle * Math.PI / 180) + Y * Math.Cos(dAngle * Math.PI / 180);
            //新增子板精确位置
            dPosX = System.Windows.Point.Parse((string)xSeletPad.Element("Position")).X - newX;
            dPosY = System.Windows.Point.Parse((string)xSeletPad.Element("Position")).Y - newY;
            return true;
        }


        public bool SubBoardRegionPosTest(ref double dPosX, ref double dPosY, double width, double height, double dAngle, XElement xSubRectMetaMesh)
        {
            if (App.m_Motion.MotionEnable() == false || xSubRectMetaMesh == null)
                return false;

            if (App.m_ShowState.CurrentProductType == null || App.m_ShowState.CurrentProductType.XTestRoute == null)
            {
                MessageBox.Show("产品型号或测试路径设置为空，不允许测试！",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }
            TestRoute tr = new TestRoute();
            tr.XElement = App.m_ShowState.CurrentProductType.XTestRoute;
            if (bool.Parse((string)tr.XElement.Element("MultiBoard")) == false)
            {// 单板
                MessageBox.Show("只允许拼板的子板区识别！",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }

            IEnumerable<XElement> subBoardList = tr.XElement.Elements("SubRectangle");
            if (subBoardList == null || subBoardList.Count() <= 0)
            {
                MessageBox.Show("第一子板区设置为空，不允许识别子板区！",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }
            foreach (XElement route in subBoardList)
            {
                if (route.Elements("TestCell").Count() <= 0)
                {
                    MessageBox.Show("第一子板测试路径个数设置为空，不允许识别子板区！",
                                    Doc.MESSAGE_SOFTNAME,
                                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return false;
                }
                break;
            }
            //新增单板区左下角位置
            double x1 = 0;
            double y1 = 0;
            //新增单板区右上角角位置
            double x2 = 0;
            double y2 = 0;
            if (dAngle == 0 || dAngle == 180)
            {
                x1 = dPosX - width / 2;
                y1 = dPosY - height / 2;
                x2 = dPosX + width / 2;
                y2 = dPosY + height / 2;
            }
            if (dAngle == 90 || dAngle == 270)
            {
                x1 = dPosX - height / 2;
                y1 = dPosY - width / 2;
                x2 = dPosX + height / 2;
                y2 = dPosY + width / 2;
            }
            XElement xPadList = new XElement("PadList");
            foreach (XElement xMeta in App.m_ShowState.CurrentProductType.XMetaData.Elements("Meta"))
            {// 取新增子板中心四分之一区域内的焊盘

                double x = System.Windows.Point.Parse((string)xMeta.Element("Position")).X;
                double y = System.Windows.Point.Parse((string)xMeta.Element("Position")).Y;
                if (x > x1 && x < x2 && y > y1 && y < y2)
                {//焊盘在单板中心四分之一区域内
                    string sMetaName = (string)xMeta.Element("Name");
                    System.Windows.Point p = System.Windows.Point.Parse((string)xSubRectMetaMesh.Element("Position"));
                    if (sMetaName == (string)xSubRectMetaMesh.Element("Name"))
                    {
                        double X = p.X * Math.Cos(dAngle * Math.PI / 180) - p.Y * Math.Sin(dAngle * Math.PI / 180);
                        double Y = p.X * Math.Sin(dAngle * Math.PI / 180) + p.Y * Math.Cos(dAngle * Math.PI / 180);
                        dPosX = x - X;
                        dPosY = y - Y;
                        break;
                    }
                }
            }
            return true;
        }


        /// <summary>
        /// 更新当前显示的拼板增加的子板矩形区的显示
        /// </summary>
        /// <param name="dPosX">子板区的中心相对缩略起点的X位置</param>
        /// <param name="dPosY">子板区的中心相对缩略起点的Y位置</param>
        /// <param name="dAngle">子板区的旋转角度</param>
        public void Update_SubBoard_Rectangle(double dPosX, double dPosY, double dAngle)
        {
            if (m_rectThumbnailAddArea == null)
                return;
            //子板中心位置换算成缩略图上的位置
            double posX = App.m_ShowState.CurrentProductType.ThumbnailWidth - dPosX * App.m_ShowState.CurrentProductType.ThumbnailWidth / App.m_ShowState.CurrentProductType.MotionWidth;
            double posY = App.m_ShowState.CurrentProductType.ThumbnailHeight - dPosY * App.m_ShowState.CurrentProductType.ThumbnailHeight / App.m_ShowState.CurrentProductType.MotionHeight;
            //double posY = dPosY * m_TestProductType.ThumbnailHeight / m_TestProductType.MotionHeight;
            double x = canvasProgramThumbnail.Width;
            double y = canvasProgramThumbnail.Height;
            double width = m_rectThumbnailAddArea.Width;
            double height = m_rectThumbnailAddArea.Height;


            double left = posX - m_rectThumbnailAddArea.Width / 2;
            double top = posY - m_rectThumbnailAddArea.Height / 2;



            Canvas.SetTop(m_rectThumbnailAddArea, top);
            Canvas.SetLeft(m_rectThumbnailAddArea, left);

            //添加旋转
            m_rectThumbnailAddArea.RenderTransform = new RotateTransform() { Angle = dAngle, CenterX = m_rectThumbnailAddArea.Width / 2, CenterY = m_rectThumbnailAddArea.Height / 2 };
        }
        #endregion 测试路径

        #region 相机图像窗口中的右键弹出菜单

        /// <summary>
        /// 删除手动增加的检测框
        /// </summary>
        private void DelTestMeta_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Shapes.Rectangle rcTmp = GetContextMenuRectangle(sender);
            if (rcTmp == null)
                return;
            //double x1 = Canvas.GetLeft(rcTmp);
            //double y1 = Canvas.GetTop(rcTmp);
            //double X = canvasCameraImage.Width - x1 - rcTmp.Width / 2;
            //double Y = canvasCameraImage.Height - y1 - rcTmp.Height / 2;
            //System.Windows.Point pMeta = new System.Windows.Point(X, Y);
            IEnumerable<XElement> Cells;
            if (App.m_ShowState.CurrentProductType.XTestRoute.Element("MultiBoard") != null && bool.Parse((string)App.m_ShowState.CurrentProductType.XTestRoute.Element("MultiBoard")) == true)
            {//判断是否为拼版
                Cells = from CellInfo in App.m_ShowState.CurrentProductType.XTestRoute.Elements("SubRectangle").Elements("TestCell")
                        where (string)CellInfo.Element("Position") == (string)m_xCurrentCell.Element("Position")
                        select CellInfo;
            }
            else
            {
                Cells = from CellInfo in App.m_ShowState.CurrentProductType.XTestRoute.Elements("TestCell")
                        where (string)CellInfo.Element("Position") == (string)m_xCurrentCell.Element("Position")
                        select CellInfo;
            }
            foreach (XElement xMeta in Cells.Elements("TestMeta"))
            {
                if ((string)xMeta.Element("Name") == rcTmp.Name)
                {
                    xMeta.Remove();
                }
            }
            canvasCameraImage.Children.Remove(rcTmp);
        }

        /// <summary>
        /// 选择元件取消检测
        /// </summary>
        private void TestCancle_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Shapes.Rectangle rcTmp = GetContextMenuRectangle(sender);
            if (rcTmp == null)
                return;
            //double x1 = Canvas.GetLeft(rcTmp);
            //double y1 = Canvas.GetTop(rcTmp);
            //double X = canvasCameraImage.Width - x1 + rcTmp.Width / 2;
            //double Y = canvasCameraImage.Height - y1 - rcTmp.Height / 2;
            //System.Windows.Point pMeta = new System.Windows.Point(X, Y);
            IEnumerable<XElement> Cells;
            if (App.m_ShowState.CurrentProductType.XTestRoute.Element("MultiBoard") != null && bool.Parse((string)App.m_ShowState.CurrentProductType.XTestRoute.Element("MultiBoard")) == true)
            {//判断是否为拼版
                Cells = from CellInfo in App.m_ShowState.CurrentProductType.XTestRoute.Elements("SubRectangle").Elements("TestCell")
                        where (string)CellInfo.Element("Position") == (string)m_xCurrentCell.Element("Position")
                        select CellInfo;
            }
            else
            {
                Cells = from CellInfo in App.m_ShowState.CurrentProductType.XTestRoute.Elements("TestCell")
                        where (string)CellInfo.Element("Position") == (string)m_xCurrentCell.Element("Position")
                        select CellInfo;
            }

            //增加元件是否在当前测试框内的判断
            foreach (XElement Meta in Cells.Elements("TestMeta"))
            {
                if ((string)Meta.Element("Name") == rcTmp.Name)
                {
                    Meta.SetElementValue("Test", false);
                    rcTmp.Stroke = new SolidColorBrush(Colors.White);
                }
            }

        }
        /// <summary>
        /// 选择元件是否有极性
        /// </summary>       
        private void IsPolarity_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Shapes.Rectangle rcTmp = GetContextMenuRectangle(sender);
            if (rcTmp == null)
                return;

            //double x1 = Canvas.GetLeft(rcTmp);
            //double y1 = Canvas.GetTop(rcTmp);
            //double X = canvasCameraImage.Width - x1 - rcTmp.Width / 2;
            //double Y = canvasCameraImage.Height - y1 - rcTmp.Height / 2;
            //System.Windows.Point pMeta = new System.Windows.Point(X, Y);
            System.Windows.Controls.MenuItem mi = sender as System.Windows.Controls.MenuItem;

            IEnumerable<XElement> Cells;
            if (App.m_ShowState.CurrentProductType.XTestRoute.Element("MultiBoard") != null && bool.Parse((string)App.m_ShowState.CurrentProductType.XTestRoute.Element("MultiBoard")) == true)
            {//判断是否为拼版
                Cells = from CellInfo in App.m_ShowState.CurrentProductType.XTestRoute.Elements("SubRectangle").Elements("TestCell")
                        where (string)CellInfo.Element("Position") == (string)m_xCurrentCell.Element("Position")
                        select CellInfo;
            }
            else
            {
                Cells = from CellInfo in App.m_ShowState.CurrentProductType.XTestRoute.Elements("TestCell")
                        where (string)CellInfo.Element("Position") == (string)m_xCurrentCell.Element("Position")
                        select CellInfo;
            }


            //增加元件是否在当前测试框内的判断
            foreach (XElement Meta in Cells.Elements("TestMeta"))
            {
                if ((string)Meta.Element("Name") == rcTmp.Name)
                {
                    if (mi.IsChecked == true)
                        Meta.SetElementValue("Polarity", true);
                    else Meta.SetElementValue("Polarity", false);
                }
            }


        }

        /// <summary>
        ///旋转元件中的检测框 
        /// </summary>
        private void RotateCheckItem_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Shapes.Rectangle rcTmp = GetContextMenuRectangle(sender);
            if (rcTmp == null)
                return;
            double rotation = 0;
            RotationSet rotationSet = new RotationSet();
            rotationSet.ShowDialog();

            rotation = rotationSet.Angle;


            double x1 = Canvas.GetLeft(rcTmp);
            double y1 = Canvas.GetTop(rcTmp);
            double x2 = x1 + rcTmp.Width;
            double y2 = y1 + rcTmp.Height;

            double metaCenterX = x1 + rcTmp.Width / 2;
            double metaCenterY = y1 + rcTmp.Height / 2;

            double width = 0;
            double height = 0;

            foreach (System.Windows.Shapes.Rectangle testRect in canvasCameraImage.Children)
            {

                if (testRect.DataContext is XElement)
                    continue;
                double xCenter = Canvas.GetLeft(testRect) + testRect.Width / 2;
                double yCenter = Canvas.GetTop(testRect) + testRect.Height / 2;
                if (xCenter > x1 && xCenter < x2 && yCenter > y1 && yCenter < y2)
                {

                    if (rotation % 360 == 90 || rotation % 360 == 270)
                    {
                        width = testRect.Height;
                        height = testRect.Width;
                    }
                    else
                    {
                        width = testRect.Width;
                        height = testRect.Height;
                    }
                    double posX = (xCenter - metaCenterX) * Math.Cos(rotation * Math.PI / 180) - (yCenter - metaCenterY) * Math.Sin(rotation * Math.PI / 180) + metaCenterX;
                    double posY = (xCenter - metaCenterX) * Math.Sin(rotation * Math.PI / 180) + (yCenter - metaCenterY) * Math.Cos(rotation * Math.PI / 180) + metaCenterY;
                    testRect.SetValue(Canvas.LeftProperty, posX - width / 2);
                    testRect.SetValue(Canvas.TopProperty, posY - height / 2);
                    testRect.Width = width;
                    testRect.Height = height;
                }
            }
        }


        /// <summary>
        /// 修改元件旋转角度
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RotateMeta_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Shapes.Rectangle rcTmp = GetContextMenuRectangle(sender);
            if (rcTmp == null)
                return;
            double angle = 0;
            RotationSet rotationSet = new RotationSet();
            rotationSet.ShowDialog();

            angle = rotationSet.Angle;
            IEnumerable<XElement> Cells;
            if (App.m_ShowState.CurrentProductType.XTestRoute.Element("MultiBoard") != null && bool.Parse((string)App.m_ShowState.CurrentProductType.XTestRoute.Element("MultiBoard")) == true)
            {//判断是否为拼版
                Cells = from CellInfo in App.m_ShowState.CurrentProductType.XTestRoute.Elements("SubRectangle").Elements("TestCell")
                        where (string)CellInfo.Element("Position") == (string)m_xCurrentCell.Element("Position")
                        select CellInfo;
            }
            else
            {
                Cells = from CellInfo in App.m_ShowState.CurrentProductType.XTestRoute.Elements("TestCell")
                        where (string)CellInfo.Element("Position") == (string)m_xCurrentCell.Element("Position")
                        select CellInfo;
            }

            //增加元件是否在当前测试框内的判断
            foreach (XElement Meta in Cells.Elements("TestMeta"))
            {
                if ((string)Meta.Element("Name") == rcTmp.Name)
                {
                    double rotation = Double.Parse((string)Meta.Element("Rotation")) + angle;
                    Meta.SetElementValue("Rotation", rotation);
                }
            }

        }
        /// <summary>
        /// 将元件添加到测试路径的CELL
        /// </summary>
        private void AddTestMeta_Click(object sender, ExecutedRoutedEventArgs e)
        {
            //System.Windows.Shapes.Rectangle rcTmp = GetContextMenuRectangle(sender);
            //if (rcTmp == null)
            //    return;


            if (m_rCurrentRect == null)
                return;
            //XElement metaData = m_rCurrentRect.DataContext as XElement;
            ////List<MetaItem> metaItemList = m_rCurrentRect.DataContext as List<MetaItem>;

            //if (metaData == null)
            //    return;
            //元件框在图像上的位置：左下角、右上角、中心，单位像素
            double x1 = Canvas.GetLeft(m_rCurrentRect);
            double x2 = x1 + m_rCurrentRect.Width;
            double y2 = m_rCurrentRect.Height + Canvas.GetTop(m_rCurrentRect);
            double y1 = Canvas.GetTop(m_rCurrentRect);
            double metaCeterX = x1 + m_rCurrentRect.Width / 2;
            double metaCeterY = y1 + m_rCurrentRect.Height / 2;

            IEnumerable<XElement> Cells;
            if (App.m_ShowState.CurrentProductType.XTestRoute.Element("MultiBoard") != null && bool.Parse((string)App.m_ShowState.CurrentProductType.XTestRoute.Element("MultiBoard")) == true)
            {//判断是否为拼版
                Cells = from CellInfo in App.m_ShowState.CurrentProductType.XTestRoute.Elements("SubRectangle").Elements("TestCell")
                        where (string)CellInfo.Element("Position") == (string)m_xCurrentCell.Element("Position")
                        select CellInfo;
            }
            else
            {
                Cells = from CellInfo in App.m_ShowState.CurrentProductType.XTestRoute.Elements("TestCell")
                        where (string)CellInfo.Element("Position") == (string)m_xCurrentCell.Element("Position")
                        select CellInfo;
            }


            TestMeta findTestMeta = null;
            foreach (XElement Meta in Cells.Elements("TestMeta"))
            {
                if ((string)Meta.Element("Name") == m_rCurrentRect.Name)
                {
                    findTestMeta = new TestMeta(Meta);
                    foreach (XElement metaImage in Meta.Elements("MetaImage"))
                    {//添加前先清除之前的数据
                        metaImage.Remove();
                    }
                    double rotation = double.Parse((string)Meta.Element("Rotation"));
                    Meta.SetElementValue("Test", true);
                    //System.Windows.Controls.MenuItem mi = sender as System.Windows.Controls.MenuItem;
                    //System.Windows.Controls.ContextMenu cm = mi.Parent as System.Windows.Controls.ContextMenu;
                    System.Windows.Controls.ContextMenu cm = m_rCurrentRect.ContextMenu;
                    foreach (System.Windows.Controls.MenuItem menu in cm.Items)
                    {
                        if ((string)menu.Header == "极性检测")
                        {
                            Meta.SetElementValue("Polarity", menu.IsChecked);
                        }
                    }
                    if (rotation % 360 == 90 || rotation % 360 == 270)
                    {
                        Meta.SetElementValue("MetaWidth", (int)m_rCurrentRect.Height);
                        Meta.SetElementValue("MetaHeight", (int)m_rCurrentRect.Width);
                    }
                    else
                    {
                        Meta.SetElementValue("MetaWidth", (int)m_rCurrentRect.Width);
                        Meta.SetElementValue("MetaHeight", (int)m_rCurrentRect.Height);
                    }


                    //XElement rectData = m_rCurrentRect.DataContext as XElement;

                    if (m_rCurrentRect.DataContext is XElement)
                    {//没有元件匹配的元件
                        XElement xMetaData = m_rCurrentRect.DataContext as XElement;
                        Meta.SetElementValue("TypeName", (string)xMetaData.Element("TypeName"));
                        XElement metaImage = new XElement("MetaImage");
                        System.Drawing.Bitmap pBmpImg = GetBitmapFromBitmapSource(((BitmapSource)cameraImage.ImageSource));
                        if (pBmpImg == null)
                        {
                            MessageBox.Show("相机图像转换失败！",
                                            Doc.MESSAGE_SOFTNAME,
                                            MessageBoxButton.OK, MessageBoxImage.Exclamation);
                            return;
                        }
                        System.Drawing.Rectangle pRec = new System.Drawing.Rectangle((int)Canvas.GetLeft(m_rCurrentRect), (int)Canvas.GetTop(m_rCurrentRect), (int)m_rCurrentRect.Width, (int)m_rCurrentRect.Height);
                        System.Drawing.Bitmap pSubImage = OpenCVUtility.GetSubBitmap_Matching(pBmpImg, pRec);
                        System.Drawing.Bitmap libMetaBitmap = OpenCVUtility.RotateImage(pSubImage, rotation);
                        if (pSubImage != null)
                        {
                            metaImage.SetElementValue("Bitmap", Doc.Convert_Bitmap2String(libMetaBitmap));

                        }
                        else
                        {
                            MessageBox.Show("元件图像增加失败！",
                                            Doc.MESSAGE_SOFTNAME,
                                            MessageBoxButton.OK, MessageBoxImage.Exclamation);
                            return;
                        }
                        //double metaCeterX = System.Windows.Point.Parse((string)Meta.Element("Position")).X;
                        //double metaCeterY = System.Windows.Point.Parse((string)Meta.Element("Position")).Y;

                        XElement checkItemList = new XElement("CheckItemList");

                        foreach (System.Windows.Shapes.Rectangle r in canvasCameraImage.Children)
                        {


                            double x = Canvas.GetLeft(r) + r.Width / 2;
                            double y = Canvas.GetTop(r) + r.Height / 2;
                            if (x > x1 && x < x2 && y > y1 && y < y2)
                            {
                                if (r.DataContext is XElement)//剔除元件框
                                    continue;
                                //计算旋转前相对元件中心的位置
                                double posX = (x - metaCeterX) * Math.Cos(rotation * Math.PI / 180) - (y - metaCeterY) * Math.Sin(rotation * Math.PI / 180);
                                double posY = (x - metaCeterX) * Math.Sin(rotation * Math.PI / 180) + (y - metaCeterY) * Math.Cos(rotation * Math.PI / 180);
                                System.Drawing.Rectangle checkItemRect = new System.Drawing.Rectangle((int)Canvas.GetLeft(r), (int)Canvas.GetTop(r), (int)r.Width, (int)r.Height);
                                System.Drawing.Bitmap checkItemImage = OpenCVUtility.GetSubBitmap_Matching(pBmpImg, pRec);
                                System.Drawing.Bitmap libcheckItemBitmap = OpenCVUtility.RotateImage(checkItemImage, rotation);
                                if (checkItemImage != null)
                                {
                                    CheckItem checkItem = new CheckItem(new XElement("CheckItem"));
                                    checkItem.Position = new System.Windows.Point(posX, posY);
                                    checkItem.Bitmap = Convert_Bitmap2String(libcheckItemBitmap);
                                    checkItem.Name = r.Name;
                                    checkItemList.Add(checkItem.XElement);
                                }
                                else
                                {
                                    MessageBox.Show("检测框图像增加失败！",
                                                    Doc.MESSAGE_SOFTNAME,
                                                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                                    return;
                                }
                            }

                        }
                        metaImage.Add(checkItemList);
                        Meta.Add(metaImage);
                    }
                    else if (m_rCurrentRect.DataContext is List<MetaItem>)
                    {//已匹配的元件
                        List<MetaItem> metaItemList = m_rCurrentRect.DataContext as List<MetaItem>;

                        foreach (MetaItem metaItem in metaItemList)
                        {
                            XElement metaImage = new XElement("MetaImage");
                            metaImage.Add(metaItem.XElement.Element("Bitmap"));
                            metaImage.Add(metaItem.XElement.Element("CheckItemList"));

                            Meta.Add(metaImage);
                        }
                    }
                }
            }
            if (findTestMeta == null)
            {//测试路径文件中未找到此元件框对应的元件数据，说明此元件框是手动画的
                #region 手动画的检测框
                findTestMeta = new TestMeta(new XElement("TestMeta"));
                findTestMeta.Name = m_rCurrentRect.Name;
                findTestMeta.Position = new System.Windows.Point(canvasCameraImage.Width - metaCeterX, canvasCameraImage.Height - metaCeterY);//元件在拍照图像上相对左下角的位置 单位：像素
                findTestMeta.Rotation = 0;
                findTestMeta.IsManual = true;
                findTestMeta.Threshold = (int)Doc.m_SystemParam.ManualThreshold;
                findTestMeta.PadThreshold = (int)Doc.m_SystemParam.PadThreshold;
                findTestMeta.MetaWidth = (int)m_rCurrentRect.Width;
                findTestMeta.MetaHeight = (int)m_rCurrentRect.Height;
                findTestMeta.Test = true;//默认不测试
                System.Windows.Controls.ContextMenu cm = m_rCurrentRect.ContextMenu;
                foreach (System.Windows.Controls.MenuItem menu in cm.Items)
                {
                    if ((string)menu.Header == "极性检测")
                    {
                        findTestMeta.Polarity = menu.IsChecked;//默认为无极性元件
                    }
                }

                
                XElement xfindTestMeta = m_rCurrentRect.DataContext as XElement;
               findTestMeta.TypeName =(string)xfindTestMeta.Element("TypeName");

                XElement metaImage = new XElement("MetaImage");
                System.Drawing.Bitmap pBmpImg = GetBitmapFromBitmapSource(((BitmapSource)cameraImage.ImageSource));
                if (pBmpImg == null)
                {
                    MessageBox.Show("相机图像转换失败！",
                                    Doc.MESSAGE_SOFTNAME,
                                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }
                System.Drawing.Rectangle pRec = new System.Drawing.Rectangle((int)Canvas.GetLeft(m_rCurrentRect), (int)Canvas.GetTop(m_rCurrentRect), (int)m_rCurrentRect.Width, (int)m_rCurrentRect.Height);
                System.Drawing.Bitmap pSubImage = OpenCVUtility.GetSubBitmap_Matching(pBmpImg, pRec);
                System.Drawing.Bitmap libMetaBitmap = OpenCVUtility.RotateImage(pSubImage, findTestMeta.Rotation);
                if (pSubImage != null)
                {
                    metaImage.SetElementValue("Bitmap", Doc.Convert_Bitmap2String(libMetaBitmap));
                }
                else
                {
                    MessageBox.Show("元件图像增加失败！",
                                    Doc.MESSAGE_SOFTNAME,
                                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }
                //double metaCeterX = System.Windows.Point.Parse((string)Meta.Element("Position")).X;
                //double metaCeterY = System.Windows.Point.Parse((string)Meta.Element("Position")).Y;

                XElement checkItemList = new XElement("CheckItemList");

                foreach (System.Windows.Shapes.Rectangle r in canvasCameraImage.Children)
                {


                    double x = Canvas.GetLeft(r) + r.Width / 2;
                    double y = Canvas.GetTop(r) + r.Height / 2;
                    if (x > x1 && x < x2 && y > y1 && y < y2)
                    {
                        if (r.DataContext is XElement)//剔除元件框
                            continue;
                        //计算旋转前相对元件中心的位置
                        double posX = (x - metaCeterX) * Math.Cos(findTestMeta.Rotation * Math.PI / 180) - (y - metaCeterY) * Math.Sin(findTestMeta.Rotation * Math.PI / 180);
                        double posY = (x - metaCeterX) * Math.Sin(findTestMeta.Rotation * Math.PI / 180) + (y - metaCeterY) * Math.Cos(findTestMeta.Rotation * Math.PI / 180);
                        System.Drawing.Rectangle checkItemRect = new System.Drawing.Rectangle((int)Canvas.GetLeft(r), (int)Canvas.GetTop(r), (int)r.Width, (int)r.Height);
                        System.Drawing.Bitmap checkItemImage = OpenCVUtility.GetSubBitmap_Matching(pBmpImg, pRec);
                        System.Drawing.Bitmap libcheckItemBitmap = OpenCVUtility.RotateImage(checkItemImage, findTestMeta.Rotation);
                        if (checkItemImage != null)
                        {
                            CheckItem checkItem = new CheckItem(new XElement("CheckItem"));
                            checkItem.Position = new System.Windows.Point(posX, posY);
                            checkItem.Bitmap = Convert_Bitmap2String(libcheckItemBitmap);
                            checkItem.Name = r.Name;
                            checkItemList.Add(checkItem.XElement);
                        }
                        else
                        {
                            MessageBox.Show("检测框图像增加失败！",
                                            Doc.MESSAGE_SOFTNAME,
                                            MessageBoxButton.OK, MessageBoxImage.Exclamation);
                            return;
                        }
                    }

                }
                metaImage.Add(checkItemList);
                findTestMeta.MetaXElement.Add(metaImage);
                foreach (XElement cell in Cells)
                {
                    cell.Add(findTestMeta.MetaXElement);
                    break;
                }
                #endregion 手动画的检测框
            }

            m_rCurrentRect.Stroke = new SolidColorBrush(Colors.Red);
            if (App.m_ShowState.CurrentProductType.XElement != null)
                App.m_ShowState.CurrentProductType.XElement.Save(string.Format(Doc.m_SystemParam.SetupPath + "\\" + Doc.m_SystemParam.ProductFilename));
        }
        /// <summary>
        /// 将元件添加到元件库
        /// </summary>
        private void AddToLib_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Shapes.Rectangle rcTmp = GetContextMenuRectangle(sender);
            if (rcTmp == null)
                return;
            double x1 = Canvas.GetLeft(rcTmp);
            double x2 = x1 + rcTmp.Width;
            double y1 = Canvas.GetTop(rcTmp);
            double y2 = y1 + rcTmp.Height;
            System.Windows.Point testPoint = new System.Windows.Point();
            testPoint.X = LTDMC.dmc_get_encoder(0, MotionDMC5000.AXIS_X) / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) - App.m_ShowState.CurrentProductType.PcbStart.X;
            testPoint.Y = LTDMC.dmc_get_encoder(0, MotionDMC5000.AXIS_Y) / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) - App.m_ShowState.CurrentProductType.PcbStart.Y;

            //测试点拍照图片左下角位置，单位mm
            double X1 = testPoint.X - canvasCameraImage.Width / 2 / Doc.m_SystemParam.HelicalPitchMap.X * Doc.m_SystemParam.HelicalPitch;
            double Y1 = testPoint.Y - canvasCameraImage.Height / 2 / Doc.m_SystemParam.HelicalPitchMap.Y * Doc.m_SystemParam.HelicalPitch;
            //测试点拍照图片右上角位置，单位mm
            double X2 = testPoint.X + canvasCameraImage.Width / Doc.m_SystemParam.HelicalPitchMap.X / 2 * Doc.m_SystemParam.HelicalPitch;
            double Y2 = testPoint.Y + canvasCameraImage.Height / Doc.m_SystemParam.HelicalPitchMap.Y / 2 * Doc.m_SystemParam.HelicalPitch;
            IEnumerable<XElement> Metas = from MetaInfo in App.m_ShowState.CurrentProductType.XMetaData.Elements("Meta")
                                          where (string)MetaInfo.Element("Name") == rcTmp.Name
                                          select MetaInfo;
            XElement Meta = null;
            foreach (XElement meta in Metas)
            {
                //元件位置
                double metaX = System.Windows.Point.Parse((string)meta.Element("Position")).X;
                double metaY = System.Windows.Point.Parse((string)meta.Element("Position")).Y;
                //判断元件是否在相机图像范围内
                if (metaX > X1 && metaX < X2 && metaY > Y1 && metaY < Y2)
                {
                    Meta = meta;
                    break;
                }
            }
            double X = System.Windows.Point.Parse((string)Meta.Element("Position")).X;
            double Y = System.Windows.Point.Parse((string)Meta.Element("Position")).Y;
            double rotation = Double.Parse((string)Meta.Element("Rotation"));
            //元件在相机拍照图像上的位置
            //double metaCeterX = (X - X1) * Doc.m_SystemParam.HelicalPitchMap.X / Doc.m_SystemParam.HelicalPitch;
            //double metaCeterY = (Y - Y1) * Doc.m_SystemParam.HelicalPitchMap.Y / Doc.m_SystemParam.HelicalPitch;

            double metaCeterX = x1 + rcTmp.Width / 2;
            double metaCeterY = y1 + rcTmp.Height / 2;
            MetaItem metaItem = new MetaItem();
            if (metaItem.XElement == null)
                metaItem.XElement = new XElement("MetaItem");
            XElement checkItemList = new XElement("CheckItemList");
            System.Windows.Controls.MenuItem mi = sender as System.Windows.Controls.MenuItem;
            System.Windows.Controls.ContextMenu cm = mi.Parent as System.Windows.Controls.ContextMenu;
            foreach (System.Windows.Controls.MenuItem menu in cm.Items)
            {
                if ((string)menu.Header == "极性元件")
                {
                    metaItem.Polarity = menu.IsChecked;
                }
            }

            System.Drawing.Bitmap pBmpImg = GetBitmapFromBitmapSource(((BitmapSource)cameraImage.ImageSource));
            if (pBmpImg == null)
            {
                MessageBox.Show("相机图像转换失败！",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            System.Drawing.Rectangle pRec = new System.Drawing.Rectangle((int)Canvas.GetLeft(rcTmp), (int)Canvas.GetTop(rcTmp), (int)rcTmp.Width, (int)rcTmp.Height);
            System.Drawing.Bitmap pSubImage = OpenCVUtility.GetSubBitmap_Matching(pBmpImg, pRec);
            System.Drawing.Bitmap libMetaBitmap = OpenCVUtility.RotateImage(pSubImage, rotation);
            if (pSubImage != null)
            {
                metaItem.Bitmap = Doc.Convert_Bitmap2String(libMetaBitmap);
                //pSubImage.Save(String.Format("{0}\\Pad.bmp", Doc.m_SystemParam.DataPath));
            }
            else
            {
                MessageBox.Show("元件图像增加失败！",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            if (rotation % 360 == 90 || rotation % 360 == 270)
            {
                metaItem.MetaWidth = (int)rcTmp.Height;
                metaItem.MetaHeight = (int)rcTmp.Width;
            }
            else
            {
                metaItem.MetaWidth = (int)rcTmp.Width;
                metaItem.MetaHeight = (int)rcTmp.Height;
            }

            foreach (System.Windows.Shapes.Rectangle r in canvasCameraImage.Children)
            {
                //剔除元件框

                double x = Canvas.GetLeft(r) + r.Width / 2;
                double y = Canvas.GetTop(r) + r.Height / 2;
                if (x > x1 && x < x2 && y > y1 && y < y2)
                {
                    if (r.DataContext is XElement)
                        continue;
                    //计算旋转前相对元件中心的位置
                    double posX = (x - metaCeterX) * Math.Cos(rotation * Math.PI / 180) - (y - metaCeterY) * Math.Sin(rotation * Math.PI / 180);
                    double posY = (x - metaCeterX) * Math.Sin(rotation * Math.PI / 180) + (y - metaCeterY) * Math.Cos(rotation * Math.PI / 180);
                    System.Drawing.Rectangle checkItemRect = new System.Drawing.Rectangle((int)Canvas.GetLeft(r), (int)Canvas.GetTop(r), (int)r.Width, (int)r.Height);
                    System.Drawing.Bitmap checkItemImage = OpenCVUtility.GetSubBitmap_Matching(pBmpImg, checkItemRect);
                    System.Drawing.Bitmap libcheckItemBitmap = OpenCVUtility.RotateImage(checkItemImage, rotation);


                    if (checkItemImage != null)
                    {
                        CheckItem checkItem = new CheckItem(new XElement("CheckItem"));
                        checkItem.Position = new System.Windows.Point(posX, posY);
                        checkItem.Bitmap = Convert_Bitmap2String(libcheckItemBitmap);
                        checkItem.Name = r.Name;
                        checkItemList.Add(checkItem.XElement);
                    }
                    else
                    {
                        MessageBox.Show("检测图像增加失败！",
                                        Doc.MESSAGE_SOFTNAME,
                                        MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        return;
                    }
                }
            }
            metaItem.XElement.Add(checkItemList);
            MetaInfoDlg metaInfoDlg = new MetaInfoDlg();
            metaInfoDlg.Meta = metaItem;
            metaInfoDlg.ShowDialog();
            Doc.m_MetaLib.AddMetaItem(metaItem.XElement);
            //保存元件库文件
            Doc.SaveLibFile(App.m_ShowState.LibMode);
        }

        /// <summary>
        /// 匹配元件库中的元件
        /// </summary>
        private void MetaMatch_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Shapes.Rectangle rcTmp = GetContextMenuRectangle(sender);
            if (rcTmp == null)
                return;

            //System.Windows.Point testPoint = new System.Windows.Point();
            //testPoint.X = LTDMC.dmc_get_encoder(0, MotionDMC5000.AXIS_X) / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) - App.m_ShowState.CurrentProductType.PcbStart.X;
            //testPoint.Y = LTDMC.dmc_get_encoder(0, MotionDMC5000.AXIS_Y) / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) - App.m_ShowState.CurrentProductType.PcbStart.Y;

            ////测试点拍照图片左下角位置，单位mm
            //double x1 = testPoint.X - canvasCameraImage.Width / Doc.m_SystemParam.HelicalPitchMap.X / 2 * Doc.m_SystemParam.HelicalPitch;
            //double y1 = testPoint.Y - canvasCameraImage.Height / Doc.m_SystemParam.HelicalPitchMap.Y / 2 * Doc.m_SystemParam.HelicalPitch;
            IEnumerable<XElement> Cells;
            if (App.m_ShowState.CurrentProductType.XTestRoute.Element("MultiBoard") != null && bool.Parse((string)App.m_ShowState.CurrentProductType.XTestRoute.Element("MultiBoard")) == true)
            {//判断是否为拼版
                Cells = from CellInfo in App.m_ShowState.CurrentProductType.XTestRoute.Elements("SubRectangle").Elements("TestCell")
                        where (string)CellInfo.Element("Position") == (string)m_xCurrentCell.Element("Position")
                        select CellInfo;
            }
            else
            {
                Cells = from CellInfo in App.m_ShowState.CurrentProductType.XTestRoute.Elements("TestCell")
                        where (string)CellInfo.Element("Position") == (string)m_xCurrentCell.Element("Position")
                        select CellInfo;
            }

            XElement Meta = null;
            //增加元件是否在当前测试框内的判断
            foreach (XElement meta in Cells.Elements("TestMeta"))
            {
                if ((string)meta.Element("Name") == rcTmp.Name)
                {
                    Meta = meta;
                    break;
                }
            }

            if (Meta == null)
                return;


            IEnumerable<XElement> CodeList = Meta.Elements("Code");
            double rotation = double.Parse((string)Meta.Element("Rotation"));

            double x = System.Windows.Point.Parse((string)Meta.Element("Position")).X;
            double y = System.Windows.Point.Parse((string)Meta.Element("Position")).Y;

            List<MetaItem> metaItemList = new List<MetaItem>();
            foreach (XElement code in CodeList)
            {
                string name = code.Value;
                foreach (MetaItem metaItem in Doc.m_MetaLib.Items)
                {
                    if (metaItem.Name == name)
                    {
                        Meta.SetElementValue("TypeName", metaItem.TypeName);
                        Meta.SetElementValue("Polarity", metaItem.Polarity);
                        metaItemList.Add(metaItem);
                    }
                }
            }
            if (metaItemList.Count == 0)//库中无匹配的元件，返回
                return;
            rcTmp.DataContext = metaItemList;
            foreach (MetaItem metaItem in metaItemList)
            {//将匹配到的元件更改元件框的大小
                if (rotation % 360 == 90 || rotation % 360 == 270)
                {
                    rcTmp.Width = metaItem.MetaHeight;
                    rcTmp.Height = metaItem.MetaWidth;
                }
                else
                {
                    rcTmp.Width = metaItem.MetaWidth;
                    rcTmp.Height = metaItem.MetaHeight;
                }
                rcTmp.SetValue(Canvas.LeftProperty, canvasCameraImage.Width - x - rcTmp.Width / 2);
                rcTmp.SetValue(Canvas.TopProperty, canvasCameraImage.Height - y - rcTmp.Height / 2);
                break;
            }
        }

        /// <summary>
        /// 修改测试元件的阈值
        /// </summary>
        private void MetaSetThreshold_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Shapes.Rectangle rcTmp = GetContextMenuRectangle(sender);
            if (rcTmp == null)
                return;

            SetThresholdDlg SetThresholdDlg = new SetThresholdDlg();
            SetThresholdDlg.ShowDialog();
            if (SetThresholdDlg.Result == false)
                return;

            IEnumerable<XElement> Cells;
            if (App.m_ShowState.CurrentProductType.XTestRoute.Element("MultiBoard") != null && bool.Parse((string)App.m_ShowState.CurrentProductType.XTestRoute.Element("MultiBoard")) == true)
            {//判断是否为拼版
                Cells = from CellInfo in App.m_ShowState.CurrentProductType.XTestRoute.Elements("SubRectangle").Elements("TestCell")
                        where (string)CellInfo.Element("Position") == (string)m_xCurrentCell.Element("Position")
                        select CellInfo;
            }
            else
            {
                Cells = from CellInfo in App.m_ShowState.CurrentProductType.XTestRoute.Elements("TestCell")
                        where (string)CellInfo.Element("Position") == (string)m_xCurrentCell.Element("Position")
                        select CellInfo;
            }

            //增加元件是否在当前测试框内的判断
            foreach (XElement Meta in Cells.Elements("TestMeta"))
            {
                if ((string)Meta.Element("Name") == rcTmp.Name)
                {
                    Meta.SetElementValue("Threshold", SetThresholdDlg.MetaThreshold);
                    Meta.SetElementValue("PadThreshold", SetThresholdDlg.PadThreshold);
                }
            }
            if (App.m_ShowState.CurrentProductType.XElement != null)
                App.m_ShowState.CurrentProductType.XElement.Save(string.Format(Doc.m_SystemParam.SetupPath + "\\" + Doc.m_SystemParam.ProductFilename));
        }

        #endregion 相机图像窗口中的弹出菜单

        #region 相机图像窗口画元件检测框

        private int m_OffsetMetaRectangl = 10;
        private int m_ThinknessMetaRectangl = 3;

        public void DrawTestRect(double w, double h, double xCenter, double yCenter, System.Windows.Point pMetaCenter, double rotation, string name)
        {

            double width = 0;
            double height = 0;
            if (rotation % 360 == 90 || rotation % 360 == 270)
            {
                width = h;
                height = w;
            }
            else
            {
                width = w;
                height = h;
            }

            System.Windows.Shapes.Rectangle testRect = new System.Windows.Shapes.Rectangle()
            {
                Width = width,
                Height = height,
                Fill = new SolidColorBrush() { Color = Colors.Red, Opacity = 0.0 },
                StrokeThickness = m_ThinknessMetaRectangl,
                Stroke = new SolidColorBrush(Colors.Red),
                Name = name
            };

            double posX = (xCenter - pMetaCenter.X) * Math.Cos(rotation * Math.PI / 180) - (yCenter - pMetaCenter.Y) * Math.Sin(rotation * Math.PI / 180) + pMetaCenter.X;
            double posY = (xCenter - pMetaCenter.X) * Math.Sin(rotation * Math.PI / 180) + (yCenter - pMetaCenter.Y) * Math.Cos(rotation * Math.PI / 180) + pMetaCenter.Y;
            testRect.SetValue(Canvas.LeftProperty, posX - width / 2);
            testRect.SetValue(Canvas.TopProperty, posY - height / 2);
            canvasCameraImage.Children.Add(testRect);
        }
        /// <summary>
        /// 二极管
        /// </summary>
        private void DrawDiode(System.Windows.Point pCenter, double MetaWidth, double MetaHeight, double rotation)
        {
            double width = 0;
            double height = 0;
            if (rotation % 360 == 90 || rotation % 360 == 270)
            {
                width = MetaHeight - m_OffsetMetaRectangl;
                height = MetaWidth - m_OffsetMetaRectangl;
            }
            else
            {
                width = MetaWidth - m_OffsetMetaRectangl;
                height = MetaHeight - m_OffsetMetaRectangl;
            }

            DrawTestRect(width * 0.6, height, pCenter.X, pCenter.Y, pCenter, rotation, "CheckItem0");
            DrawTestRect(width * 0.2, height, pCenter.X - width * 0.4, pCenter.Y, pCenter, rotation, "CheckItem1");
            DrawTestRect(width * 0.2, height, pCenter.X + width * 0.4, pCenter.Y, pCenter, rotation, "CheckItem2");

        }

        /// <summary>
        ///12引脚元件 
        /// </summary>
        private void DrawDynatron(System.Windows.Point pCenter, double MetaWidth, double MetaHeight, double rotation)
        {
            double width = 0;
            double height = 0;
            if (rotation % 360 == 90 || rotation % 360 == 270)
            {
                width = MetaHeight - m_OffsetMetaRectangl;
                height = MetaWidth - m_OffsetMetaRectangl;
            }
            else
            {
                width = MetaWidth - m_OffsetMetaRectangl;
                height = MetaHeight - m_OffsetMetaRectangl;
            }
            DrawTestRect(width, height * 0.4, pCenter.X, pCenter.Y, pCenter, rotation, "CheckItem0");
            DrawTestRect(width * 0.2, height * 0.3, pCenter.X, pCenter.Y - height * 0.35, pCenter, rotation, "CheckItem1");
            DrawTestRect(width * 0.2, height * 0.3, pCenter.X - width * 0.4, pCenter.Y + height * 0.35, pCenter, rotation, "CheckItem2");
            DrawTestRect(width * 0.2, height * 0.3, pCenter.X + width * 0.4, pCenter.Y + height * 0.35, pCenter, rotation, "CheckItem3");
        }

        /// <summary>
        /// 四引脚IC
        /// </summary>
        private void DrawIC22(System.Windows.Point pCenter, double MetaWidth, double MetaHeight, double rotation)
        {
            double width = 0;
            double height = 0;
            if (rotation % 360 == 90 || rotation % 360 == 270)
            {
                width = MetaHeight - m_OffsetMetaRectangl;
                height = MetaWidth - m_OffsetMetaRectangl;
            }
            else
            {
                width = MetaWidth - m_OffsetMetaRectangl;
                height = MetaHeight - m_OffsetMetaRectangl;
            }


            DrawTestRect(width * 0.6, height, pCenter.X, pCenter.Y, pCenter, rotation, "CheckItem0");
            DrawTestRect(width * 0.2, height * 0.2, pCenter.X - width * 0.4, pCenter.Y + height * 0.35, pCenter, rotation, "CheckItem1");
            DrawTestRect(width * 0.2, height * 0.2, pCenter.X - width * 0.4, pCenter.Y - height * 0.35, pCenter, rotation, "CheckItem2");
            DrawTestRect(width * 0.2, height * 0.2, pCenter.X + width * 0.4, pCenter.Y - height * 0.35, pCenter, rotation, "CheckItem3");
            DrawTestRect(width * 0.2, height * 0.2, pCenter.X + width * 0.4, pCenter.Y + height * 0.35, pCenter, rotation, "CheckItem4");
        }

        private void DrawIC13(System.Windows.Point pCenter, double MetaWidth, double MetaHeight, double rotation)
        {
            double width = 0;
            double height = 0;
            if (rotation % 360 == 90 || rotation % 360 == 270)
            {
                width = MetaHeight - m_OffsetMetaRectangl;
                height = MetaWidth - m_OffsetMetaRectangl;
            }
            else
            {
                width = MetaWidth - m_OffsetMetaRectangl;
                height = MetaHeight - m_OffsetMetaRectangl;
            }


            DrawTestRect(width * 0.6, height, pCenter.X, pCenter.Y, pCenter, rotation, "CheckItem0");
            DrawTestRect(width * 0.2, height * 0.2, pCenter.X - width * 0.4, pCenter.Y + height * 0.35, pCenter, rotation, "CheckItem1");
            DrawTestRect(width * 0.2, height * 0.2, pCenter.X - width * 0.4, pCenter.Y - height * 0.35, pCenter, rotation, "CheckItem2");
            DrawTestRect(width * 0.2, height * 0.2, pCenter.X - width * 0.4, pCenter.Y, pCenter, rotation, "CheckItem3");
            DrawTestRect(width * 0.2, height * 0.2, pCenter.X + width * 0.4, pCenter.Y, pCenter, rotation, "CheckItem4");
        }

        private void DrawIC31(System.Windows.Point pCenter, double MetaWidth, double MetaHeight, double rotation)
        {
            double width = 0;
            double height = 0;
            if (rotation % 360 == 90 || rotation % 360 == 270)
            {
                width = MetaHeight - m_OffsetMetaRectangl;
                height = MetaWidth - m_OffsetMetaRectangl;
            }
            else
            {
                width = MetaWidth - m_OffsetMetaRectangl;
                height = MetaHeight - m_OffsetMetaRectangl;
            }
            DrawTestRect(width * 0.6, height, pCenter.X, pCenter.Y, pCenter, rotation, "CheckItem0");
            DrawTestRect(width * 0.2, height * 0.2, pCenter.X - width * 0.4, pCenter.Y + height * 0.35, pCenter, rotation, "CheckItem1");
            DrawTestRect(width * 0.2, height * 0.2, pCenter.X - width * 0.4, pCenter.Y - height * 0.35, pCenter, rotation, "CheckItem2");
            DrawTestRect(width * 0.2, height * 0.2, pCenter.X + width * 0.4, pCenter.Y - height * 0.35, pCenter, rotation, "CheckItem3");
            DrawTestRect(width * 0.2, height * 0.2, pCenter.X + width * 0.4, pCenter.Y + height * 0.35, pCenter, rotation, "CheckItem4");
        }
        /// <summary>
        /// 五引脚IC
        /// </summary>
        private void DrawIC23(System.Windows.Point pCenter, double MetaWidth, double MetaHeight, double rotation)
        {

            double width = 0;
            double height = 0;
            if (rotation % 360 == 90 || rotation % 360 == 270)
            {
                width = MetaHeight - m_OffsetMetaRectangl;
                height = MetaWidth - m_OffsetMetaRectangl;
            }
            else
            {
                width = MetaWidth - m_OffsetMetaRectangl;
                height = MetaHeight - m_OffsetMetaRectangl;
            }

            DrawTestRect(width * 0.6, height, pCenter.X, pCenter.Y, pCenter, rotation, "CheckItem0");
            DrawTestRect(width * 0.2, height * 0.2, pCenter.X - width * 0.4, pCenter.Y + height * 0.35, pCenter, rotation, "CheckItem1");
            DrawTestRect(width * 0.2, height * 0.2, pCenter.X - width * 0.4, pCenter.Y - height * 0.35, pCenter, rotation, "CheckItem2");
            DrawTestRect(width * 0.2, height * 0.2, pCenter.X + width * 0.4, pCenter.Y - height * 0.35, pCenter, rotation, "CheckItem3");
            DrawTestRect(width * 0.2, height * 0.2, pCenter.X + width * 0.4, pCenter.Y + height * 0.35, pCenter, rotation, "CheckItem4");
            DrawTestRect(width * 0.2, height * 0.2, pCenter.X + width * 0.4, pCenter.Y, pCenter, rotation, "CheckItem5");
        }

        /// <summary>
        /// 六引脚IC
        /// </summary>
        private void DrawIC33(System.Windows.Point pCenter, double MetaWidth, double MetaHeight, double rotation)
        {
            double width = 0;
            double height = 0;
            if (rotation % 360 == 90 || rotation % 360 == 270)
            {
                width = MetaHeight - m_OffsetMetaRectangl;
                height = MetaWidth - m_OffsetMetaRectangl;
            }
            else
            {
                width = MetaWidth - m_OffsetMetaRectangl;
                height = MetaHeight - m_OffsetMetaRectangl;
            }

            DrawTestRect(width * 0.6, height, pCenter.X, pCenter.Y, pCenter, rotation, "CheckItem0");
            DrawTestRect(width * 0.2, height * 0.2, pCenter.X - width * 0.4, pCenter.Y + height * 0.35, pCenter, rotation, "CheckItem1");
            DrawTestRect(width * 0.2, height * 0.2, pCenter.X - width * 0.4, pCenter.Y - height * 0.35, pCenter, rotation, "CheckItem2");
            DrawTestRect(width * 0.2, height * 0.2, pCenter.X + width * 0.4, pCenter.Y - height * 0.35, pCenter, rotation, "CheckItem3");
            DrawTestRect(width * 0.2, height * 0.2, pCenter.X + width * 0.4, pCenter.Y + height * 0.35, pCenter, rotation, "CheckItem4");
            DrawTestRect(width * 0.2, height * 0.2, pCenter.X + width * 0.4, pCenter.Y, pCenter, rotation, "CheckItem5");
            DrawTestRect(width * 0.2, height * 0.2, pCenter.X - width * 0.4, pCenter.Y, pCenter, rotation, "CheckItem6");
        }

        /// <summary>
        /// 四排引脚IC
        /// </summary>
        private void DrawIC4(System.Windows.Point pCenter, double MetaWidth, double MetaHeight, double rotation)
        {
            double width = 0;
            double height = 0;
            if (rotation % 360 == 90 || rotation % 360 == 270)
            {
                width = MetaHeight - m_OffsetMetaRectangl;
                height = MetaWidth - m_OffsetMetaRectangl;
            }
            else
            {
                width = MetaWidth - m_OffsetMetaRectangl;
                height = MetaHeight - m_OffsetMetaRectangl;
            }

            DrawTestRect(width * 0.6, height * 0.6, pCenter.X, pCenter.Y, pCenter, rotation, "CheckItem0");
            DrawTestRect(width * 0.2, height * 0.6, pCenter.X - width * 0.4, pCenter.Y, pCenter, rotation, "CheckItem1");
            DrawTestRect(width * 0.2, height * 0.6, pCenter.X + width * 0.4, pCenter.Y, pCenter, rotation, "CheckItem2");
            DrawTestRect(width * 0.6, height * 0.2, pCenter.X, pCenter.Y - height * 0.4, pCenter, rotation, "CheckItem3");
            DrawTestRect(width * 0.6, height * 0.2, pCenter.X, pCenter.Y + height * 0.4, pCenter, rotation, "CheckItem4");
        }

        #endregion 相机图像窗口画元件检测框





        #region 数据导入

        /// <summary>
        /// 数据导入
        /// </summary>
        private void OnClick_leadIn(object sender, RoutedEventArgs e)
        {
            DataProgramDlg dataProgramDlg = new DataProgramDlg();
            dataProgramDlg.TestProductType = App.m_ShowState.CurrentProductType;
            dataProgramDlg.ShowDialog();
        }

        private List<ThumbDragMoveAdorner> m_listAdorner = new List<ThumbDragMoveAdorner>();//存储元件校准时相机窗口界面上增加的adorner

        /// <summary>
        /// 所有元件整体位置校准
        /// </summary>
        private void OnClick_MetaPosCalibrate(object sender, RoutedEventArgs e)
        {
            canvasCameraImage.Children.Clear();
            m_listAdorner.Clear();
            AdornerLayer layer = AdornerLayer.GetAdornerLayer(canvasCameraImage);

            if (cameraImage.ImageSource == null)
                return;

            double posX = LTDMC.dmc_get_encoder(0, MotionDMC5000.AXIS_X) / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) - App.m_ShowState.CurrentProductType.PcbStart.X;
            double posY = LTDMC.dmc_get_encoder(0, MotionDMC5000.AXIS_Y) / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) - App.m_ShowState.CurrentProductType.PcbStart.Y;
            double x1 = posX - canvasCameraImage.Width / Doc.m_SystemParam.HelicalPitchMap.X / 2 * Doc.m_SystemParam.HelicalPitch;
            double x2 = posX + canvasCameraImage.Width / Doc.m_SystemParam.HelicalPitchMap.X / 2 * Doc.m_SystemParam.HelicalPitch;
            double y1 = posY - canvasCameraImage.Height / Doc.m_SystemParam.HelicalPitchMap.Y / 2 * Doc.m_SystemParam.HelicalPitch;
            double y2 = posY + canvasCameraImage.Height / Doc.m_SystemParam.HelicalPitchMap.Y / 2 * Doc.m_SystemParam.HelicalPitch;

            if (App.m_ShowState.CurrentProductType.XMetaData.Elements("Meta") == null)
            {
                MessageBox.Show("请先导入文件！",
                           Doc.MESSAGE_SOFTNAME,
                           MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            foreach (XElement meta in App.m_ShowState.CurrentProductType.XMetaData.Elements("Meta"))
            {
                double x = System.Windows.Point.Parse((string)meta.Element("Position")).X;
                double y = System.Windows.Point.Parse((string)meta.Element("Position")).Y;
                if (x > x1 && x < x2 && y > y1 && y < y2)
                {//元件在拍照图像范围内
                    //元件在拍照图像上的位置，单位：像素
                    double X = (x - x1) * Doc.m_SystemParam.HelicalPitchMap.X / Doc.m_SystemParam.HelicalPitch;
                    double Y = (y - y1) * Doc.m_SystemParam.HelicalPitchMap.Y / Doc.m_SystemParam.HelicalPitch;
                    DrawRectangle((string)meta.Element("Name"), 100, 50, X, Y, (double)meta.Element("Rotation"));
                }
            }
            foreach (UIElement rect in canvasCameraImage.Children)
            {
                ThumbDragMoveAdorner thumbDragMoveAdorner = new ThumbDragMoveAdorner(rect, canvasCameraImage, App.m_ShowState.CurrentProductType.XMetaData, true);
                layer.Add(thumbDragMoveAdorner);
                m_listAdorner.Add(thumbDragMoveAdorner);
            }
        }

        //元件个体位置调整
        private void MetaPosCalibrateClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Shapes.Path rcTmp = GetContextMenuPath(sender);
            if (rcTmp == null)
                return;
            rcTmp.Stroke = new SolidColorBrush(Colors.Yellow);
            var layer = AdornerLayer.GetAdornerLayer(canvasCameraImage);
            foreach (var adorner in m_listAdorner)
            {
                layer.Remove(adorner);
            }
            layer.Add(new ThumbDragMoveAdorner(rcTmp, canvasCameraImage, App.m_ShowState.CurrentProductType.XMetaData, false));
        }

        private void Mark1Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Shapes.Path rcTmp = GetContextMenuPath(sender);
            if (rcTmp == null)
                return;
            //Mark点                                                                                                
            double dMark1X = App.m_Motion.MotionPositon[0] / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) - canvasCameraImage.Width / 2 * Doc.m_SystemParam.HelicalPitch / Doc.m_SystemParam.HelicalPitchMap.X + (canvasCameraImage.Width - Canvas.GetLeft(rcTmp)) * Doc.m_SystemParam.HelicalPitch / Doc.m_SystemParam.HelicalPitchMap.X - App.m_ShowState.CurrentProductType.PcbStart.X;
            double dMark1Y = App.m_Motion.MotionPositon[1] / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) - canvasCameraImage.Height / 2 * Doc.m_SystemParam.HelicalPitch / Doc.m_SystemParam.HelicalPitchMap.Y + (canvasCameraImage.Height - Canvas.GetTop(rcTmp)) * Doc.m_SystemParam.HelicalPitch / Doc.m_SystemParam.HelicalPitchMap.Y - App.m_ShowState.CurrentProductType.PcbStart.Y;
            m_pMarkOnePos = new System.Windows.Point(dMark1X, dMark1Y);
        }
        private void Mark2Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Shapes.Path rcTmp = GetContextMenuPath(sender);
            if (rcTmp == null)
                return;

            double dMark2X = App.m_Motion.MotionPositon[0] / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) - canvasCameraImage.Width / 2 * Doc.m_SystemParam.HelicalPitch / Doc.m_SystemParam.HelicalPitchMap.X + (canvasCameraImage.Width - Canvas.GetLeft(rcTmp)) * Doc.m_SystemParam.HelicalPitch / Doc.m_SystemParam.HelicalPitchMap.X - App.m_ShowState.CurrentProductType.PcbStart.X;
            double dMark2Y = App.m_Motion.MotionPositon[1] / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) - canvasCameraImage.Height / 2 * Doc.m_SystemParam.HelicalPitch / Doc.m_SystemParam.HelicalPitchMap.Y + (canvasCameraImage.Height - Canvas.GetTop(rcTmp)) * Doc.m_SystemParam.HelicalPitch / Doc.m_SystemParam.HelicalPitchMap.Y - App.m_ShowState.CurrentProductType.PcbStart.Y;
            m_pMarkTwoPos = new System.Windows.Point(dMark2X, dMark2Y);
        }
        /// <summary>
        /// 焊盘位置校准
        /// </summary>
        private void OnClick_PadPosCalibrate(object sender, RoutedEventArgs e)
        {
            canvasCameraImage.Children.Clear();
            var layer = AdornerLayer.GetAdornerLayer(canvasCameraImage);

            if (cameraImage.ImageSource == null)
                return;

            double posX = LTDMC.dmc_get_encoder(0, MotionDMC5000.AXIS_X) / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) - App.m_ShowState.CurrentProductType.PcbStart.X;
            double posY = LTDMC.dmc_get_encoder(0, MotionDMC5000.AXIS_Y) / (Doc.m_SystemParam.PulsePerCircle / Doc.m_SystemParam.HelicalPitch) - App.m_ShowState.CurrentProductType.PcbStart.Y;
            double x1 = posX - canvasCameraImage.Width / Doc.m_SystemParam.HelicalPitchMap.X / 2 * Doc.m_SystemParam.HelicalPitch;
            double x2 = posX + canvasCameraImage.Width / Doc.m_SystemParam.HelicalPitchMap.X / 2 * Doc.m_SystemParam.HelicalPitch;
            double y1 = posY - canvasCameraImage.Height / Doc.m_SystemParam.HelicalPitchMap.Y / 2 * Doc.m_SystemParam.HelicalPitch;
            double y2 = posY + canvasCameraImage.Height / Doc.m_SystemParam.HelicalPitchMap.Y / 2 * Doc.m_SystemParam.HelicalPitch;

            if (App.m_ShowState.CurrentProductType.XMetaData.Elements("Meta") == null)
            {
                MessageBox.Show("请先导入文件！",
                           Doc.MESSAGE_SOFTNAME,
                           MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            foreach (XElement pad in App.m_ShowState.CurrentProductType.XPadData.Elements("Pad"))
            {
                if (String.IsNullOrEmpty((string)pad.Element("Type")))
                    continue;

                switch ((PadType)Enum.Parse(typeof(PadType), (string)pad.Element("Type")))
                {
                    case PadType.Rectangle:
                        double xr = System.Windows.Point.Parse((string)pad.Element("Position")).X;
                        double yr = System.Windows.Point.Parse((string)pad.Element("Position")).Y;
                        if (xr > x1 && xr < x2 && yr > y1 && yr < y2)
                        {//焊盘在拍照图像范围内
                            //元件在拍照图像上的位置，单位：像素
                            double X = (xr - x1) * Doc.m_SystemParam.HelicalPitchMap.X / Doc.m_SystemParam.HelicalPitch;
                            double Y = (yr - y1) * Doc.m_SystemParam.HelicalPitchMap.Y / Doc.m_SystemParam.HelicalPitch;
                            DrawRectPad((double)pad.Element("Width") * Doc.m_SystemParam.HelicalPitchMap.X / Doc.m_SystemParam.HelicalPitch, (double)pad.Element("Height") * Doc.m_SystemParam.HelicalPitchMap.Y / Doc.m_SystemParam.HelicalPitch, X, Y, (double)pad.Element("Rotation"));
                        }
                        break;
                }
            }
            foreach (UIElement rect in canvasCameraImage.Children)
                layer.Add(new ThumbPadDragMoveAdorner(rect, canvasCameraImage, App.m_ShowState.CurrentProductType.XPadData));
        }

        /// <summary>
        /// 画元件框用于元件位置校准
        /// </summary>
        /// <param name="name">元件位置名</param>
        /// <param name="width">元件框宽</param>
        /// <param name="height">元件框高</param>
        /// <param name="X">元件框位置x坐标</param>
        /// <param name="Y">元件框位置y坐标</param>
        /// <param name="angle">元件旋转角度</param>
        public void DrawRectangle(string name, double width, double height, double X, double Y, double angle)
        {
            System.Windows.Shapes.Path rect = new System.Windows.Shapes.Path()
            {
                Stroke = new SolidColorBrush(Colors.Red),
                Fill = new SolidColorBrush() { Color = Colors.Red, Opacity = 0.0 },
                StrokeThickness = 3,
            };
            rect.SetValue(Canvas.LeftProperty, canvasCameraImage.Width - X);
            rect.SetValue(Canvas.TopProperty, canvasCameraImage.Height - Y);
            RectangleGeometry r = new RectangleGeometry() { Rect = new Rect(-width / 2, -height / 2, width, height), Transform = new RotateTransform() { Angle = angle } };
            rect.Data = r;
            rect.Name = name;
            canvasCameraImage.Children.Add(rect);
            System.Windows.Controls.ContextMenu contextMenu = new System.Windows.Controls.ContextMenu();
            System.Windows.Controls.MenuItem menuItem = new System.Windows.Controls.MenuItem() { Header = "校准位置" };
            menuItem.Click += MetaPosCalibrateClick;
            contextMenu.Items.Add(menuItem);
            menuItem = new System.Windows.Controls.MenuItem() { Header = "Mark1" };
            menuItem.Click += Mark1Click;
            contextMenu.Items.Add(menuItem);
            menuItem = new System.Windows.Controls.MenuItem() { Header = "Mark2" };
            menuItem.Click += Mark2Click;
            contextMenu.Items.Add(menuItem);
            rect.ContextMenu = contextMenu;

        }


        #endregion 数据导入

        #region 元件库编辑
        //private MetaItem m_MetaItem;
        /// <summary>
        /// 删除元件
        /// </summary>
        private void MetaLibEdit_Click(object sender, RoutedEventArgs e)
        {
            MetaLibDlg metaLibDlg = new MetaLibDlg();
            metaLibDlg.ShowDialog();

            //if (MetaLibEditComboBox.SelectedItem == null)
            //{
            //    MessageBox.Show("没有选择元件，不允许删除！",
            //                    Doc.MESSAGE_SOFTNAME,
            //                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
            //    return;
            //}

            //if (MessageBox.Show(String.Format("真的要删除元件：\"{0}\"及其包含的所以数据？", MetaLibEditComboBox.SelectedItem.ToString()),
            //                Doc.MESSAGE_SOFTNAME,
            //                MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.Yes)
            //{
            //    MetaItem item = (MetaItem)MetaLibEditComboBox.SelectedItem;
            //    Doc.m_MetaLib.DeleteMetaItem(item);
            //    App.m_ShowState.MetaItems = Doc.m_MetaLib.Items;
            //    if (MetaLibEditComboBox.Items.Count > 0)
            //        MetaLibEditComboBox.SelectedIndex = 0;
            //}

        }

        /// <summary>
        /// 修改元件名称
        /// </summary>
        //private void MetaItemModify_Click(object sender, RoutedEventArgs e)
        //{
        //    if (MetaLibEditComboBox.SelectedItem == null)
        //    {
        //        MessageBox.Show("没有选择元件，不允许修改！",
        //                        Doc.MESSAGE_SOFTNAME,
        //                        MessageBoxButton.OK, MessageBoxImage.Exclamation);
        //        return;
        //    }
        //    MetaInfoDlg metaInfoDlg = new MetaInfoDlg();
        //    metaInfoDlg.Meta = MetaLibEditComboBox.SelectedItem as MetaItem;
        //    metaInfoDlg.ShowDialog();
        //}

        //private void MetaLibEditComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    if (e.RemovedItems.Count > 0)
        //        m_MetaItem = (MetaItem)e.RemovedItems[0];
        //}


        #endregion 元件库编辑

        #endregion 编程

        #region 测试
        public static string m_TestResultFileName;//测试结果保存文件名

        #region 运动系统

        /// <summary>
        /// 根据测试结果显示三色灯
        /// </summary>       
        private void LightShow_TestResult(XElement xResult)
        {
            if (!this.Dispatcher.CheckAccess()) // CheckAccess returns true if you're on the dispatcher thread
            {
                this.Dispatcher.BeginInvoke(new DelegateXElementParameter(LightShow_TestResult), xResult);
                return;
            }
            if (xResult == null)
                return;
            LTDMC.dmc_write_outbit(App.m_Motion.CardID, MotionDMC5000.OUTPUT_YELLOWLIGHT, MotionDMC5000.LEVEL_HIGH);
            bool bError = false;
            foreach (XElement xCell in xResult.Elements("TestCell"))
            {
                TestCell testCell = new TestCell(xCell);
                foreach (XElement xMeta in xCell.Elements("TestMeta"))
                {
                    if (xMeta.Element("Test") == null || bool.Parse((string)xMeta.Element("Test")) == false)
                        continue;

                    if (xMeta.Element("MetaResult") == null)
                        continue;
                    else if (bool.Parse((string)xMeta.Element("MetaResult")) == false)
                    {
                        bError = true;
                        //continue;
                    }

                    //if (xMeta.Element("CheckItemList") != null)
                    //{
                    //    foreach (XElement xPad in xMeta.Element("CheckItemList").Elements("CheckItem"))
                    //    {
                    //        if (xPad.Element("CheckResult") == null)
                    //            continue;
                    //        else if (bool.Parse((string)xPad.Element("CheckResult")) == false)
                    //        {
                    //            bError = true;
                    //            break;
                    //        }
                    //    }

                    //    if (bError)
                    //        break;
                    //}
                }
            }


            if (bError)
            {
                LTDMC.dmc_write_outbit(App.m_Motion.CardID, MotionDMC5000.OUTPUT_REDLIGHT, MotionDMC5000.LEVEL_LOW);
                //LTDMC.dmc_write_outbit(Doc.m_Motion.CardID, MotionDMC5000.OUTPUT_BUZZER, MotionDMC5000.LEVEL_LOW);
                App.m_ShowState.ResultError = true;
            }
            else
            {
                LTDMC.dmc_write_outbit(App.m_Motion.CardID, MotionDMC5000.OUTPUT_GREENLIGHT, MotionDMC5000.LEVEL_LOW);
                App.m_ShowState.ResultError = false;
            }
            lbl_ResultNext.Content = string.Format("1/{0}", App.m_ListTestResult.Count);
            lbl_MTResultNext.Content = string.Format("1/{0}", App.m_ListTestResult.Count);
        }

        /// <summary>
        /// 清除三色灯异常状态
        /// </summary>       
        private void BtnClearException_Click(object sender, RoutedEventArgs e)
        {
            LTDMC.dmc_write_outbit(App.m_Motion.CardID, MotionDMC5000.OUTPUT_REDLIGHT, MotionDMC5000.LEVEL_HIGH);
            LTDMC.dmc_write_outbit(App.m_Motion.CardID, MotionDMC5000.OUTPUT_BUZZER, MotionDMC5000.LEVEL_HIGH);
            App.m_ShowState.ResultError = false;
        }


        /// <summary>
        /// 运动状态复位
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnReStart_Click(object sender, RoutedEventArgs e)
        {
            App.m_Motion.MotionType = MotionType.None;
        }

        /// <summary>
        /// 测试结束时，在测试缩略图上显示测试结果
        /// </summary>
        private void Show_TestResult(XElement xResult)
        {
            if (!this.Dispatcher.CheckAccess()) // CheckAccess returns true if you're on the dispatcher thread
            {
                this.Dispatcher.BeginInvoke(new DelegateXElementParameter(Show_TestResult), xResult);
                return;
            }

            if (xResult == null)
            {
                if (Doc.m_SystemParam.ShowMode != ShowMode.Repair)
                    canvasProgramThumbnail.Children.Clear();
                if (Doc.m_SystemParam.ShowMode != ShowMode.System)
                    canvasMTProgramThumbnail.Children.Clear();
            }
            else
            {
                if (Doc.m_SystemParam.ShowMode != ShowMode.Repair)
                {// 在测试缩略图显示区画测试点区域框

                    DrawCell_TestResult(xResult);
                }
                if (Doc.m_SystemParam.ShowMode != ShowMode.System)
                {// 在维修站缩略图显示区画测试点区域框
                    if (xResult.Element("TestTime") != null)
                        App.m_ShowState.TestTime = (string)xResult.Element("TestTime");

                    if (App.m_ShowState.CurrentProductType.XElement != null)
                    {
                        if (App.m_ShowState.CurrentProductType.Thumbnail != null)
                        {
                            string sSample = Doc.m_SystemParam.SetupPath + "\\Thumbnail\\" + App.m_ShowState.CurrentProductType.Thumbnail;
                            if (File.Exists(sSample))
                            {
                                BitmapImage bmpImage = new BitmapImage(new Uri(sSample));
                                MTThumbnailImage.ImageSource = bmpImage;
                            }
                        }
                    }
                    DrawCell_MaintenanceTestResult(xResult);

                    Doc.m_DBTestResult.ProductType = m_TestProductType.Name;
                    Doc.m_DBTestResult.ErrorCount = m_XErrorMeta.Count - m_iMisAdjCount;
                    Doc.m_DBTestResult.MisAdjCount = m_iMisAdjCount;
                    Doc.m_DBTestResult.IsReview = false;
                    Doc.m_DBTestResult.IsOK = m_XErrorMeta.Count == m_iMisAdjCount ? true : false;

                    //DataBase.AddTestResult(Doc.m_DBTestResult);
                    m_XResultShow = xResult;
                    m_iIndexCell = 0;
                }
            }
        }

        /// <summary>
        /// 在缩略图显示区画测试点区域框
        /// </summary>
        private void DrawCell_TestResult(XElement xResult)
        {
            if (xResult == null)
                return;

            int iErrorMetaNum = 0;//异常元件数
            foreach (XElement xCell in xResult.Elements("TestCell"))
            {
                TestCell testCell = new TestCell(xCell);
                double X = App.m_ShowState.CurrentProductType.ThumbnailWidth - testCell.Position.X / App.m_ShowState.CurrentProductType.MotionWidth * App.m_ShowState.CurrentProductType.ThumbnailWidth;
                double Y = App.m_ShowState.CurrentProductType.ThumbnailHeight - testCell.Position.Y / App.m_ShowState.CurrentProductType.MotionHeight * App.m_ShowState.CurrentProductType.ThumbnailHeight;

                bool bError = false;
                foreach (XElement xMeta in xCell.Elements("TestMeta"))
                {
                    if (xMeta.Element("Test") == null || bool.Parse((string)xMeta.Element("Test")) == false)
                        continue;

                    if (xMeta.Element("MetaResult") == null)
                        continue;
                    else if (bool.Parse((string)xMeta.Element("MetaResult")) == false)
                    {
                        bError = true;
                        iErrorMetaNum++;
                        TestMeta testMeta = new TestMeta(xMeta);
                        Point p = Point.Parse((string)xMeta.Element("Position"));
                        double dMetaThumbX = X + (canvasCameraImage.Width / 2 - p.X) / m_iScale;
                        //double dMetaThumbX = X + (p.X-canvasMTCameraImage.Width/2 ) / m_iScale;
                        double dMetaThumbY = Y + (canvasCameraImage.Height / 2 - p.Y) / m_iScale;
                        System.Windows.Shapes.Rectangle recMeta = new System.Windows.Shapes.Rectangle
                        {
                            Fill = new SolidColorBrush() { Color = bError ? Colors.Red : Colors.Green, Opacity = 0.5 },
                            Stroke = new SolidColorBrush(bError ? Colors.Red : Colors.Green),
                            StrokeThickness = 2,
                        };
                        double rotation = testMeta.Rotation;
                        if (rotation % 360 == 0 || rotation % 360 == 180)
                        {
                            recMeta.Width = testMeta.MetaWidth / m_iScale;
                            recMeta.Height = testMeta.MetaHeight / m_iScale;
                        }
                        else if (rotation % 360 == 90 || rotation % 360 == 270)
                        {
                            recMeta.Width = testMeta.MetaHeight / m_iScale;
                            recMeta.Height = testMeta.MetaWidth / m_iScale;
                        }
                        recMeta.SetValue(Canvas.LeftProperty, dMetaThumbX - recMeta.Width / 2);
                        recMeta.SetValue(Canvas.TopProperty, dMetaThumbY - recMeta.Height / 2);
                        recMeta.MouseLeftButtonDown += MouseLeftButtonDown_Result;
                        recMeta.DataContext = xMeta;
                        canvasProgramThumbnail.Children.Add(recMeta);
                        //continue;
                    }

                    //if (xMeta.Element("CheckItemList") != null)
                    //{
                    //    foreach (XElement xPad in xMeta.Element("CheckItemList").Elements("CheckItem"))
                    //    {
                    //        if (xPad.Element("CheckResult") == null)
                    //            continue;
                    //        else if (bool.Parse((string)xPad.Element("CheckResult")) == false)
                    //        {
                    //            bError = true;
                    //            iErrorMetaNum++;
                    //            break;
                    //        }
                    //    }

                    //}
                }

                //System.Windows.Shapes.Rectangle rec = new System.Windows.Shapes.Rectangle
                //{
                //    Fill = new SolidColorBrush() { Color = bError ? Colors.Red : Colors.Green, Opacity = 0.5 },
                //    Stroke = new SolidColorBrush(bError ? Colors.Red : Colors.Green),
                //    StrokeThickness = 2,
                //    Width = App.m_ShowState.CurrentProductType.ThumbnailSubWidth,
                //    Height = App.m_ShowState.CurrentProductType.ThumbnailSubHeight
                //};
                //rec.SetValue(Canvas.LeftProperty, X - App.m_ShowState.CurrentProductType.ThumbnailSubWidth / 2);
                //rec.SetValue(Canvas.TopProperty, Y - App.m_ShowState.CurrentProductType.ThumbnailSubHeight / 2);
                //canvasProgramThumbnail.Children.Add(rec);

                ////rec.MouseLeftButtonDown += MouseLeftButtonDown_CellResult;
                //rec.DataContext = xCell;
            }


            lbl_ErrorMetaShow.Content = string.Format("异常元件总数：{0}", iErrorMetaNum);
            lbl_TestResult.Content = string.Format("测试结果：{0}", iErrorMetaNum > 0 ? "NG" : "OK");

            //lbl_ErrorMetaShow.Content = string.Format("当前显示/异常总数：{0}/{1}", m_iIndexMeta, m_XErrorMeta.Count);
        }

        private void MouseLeftButtonDown_CellResult(object sender, MouseButtonEventArgs e)
        {// 判断鼠标双击
            if (e.ClickCount != 2)
                return;

            e.Handled = true;

            if ((sender is System.Windows.Shapes.Rectangle) == false)
                return;
            System.Windows.Shapes.Rectangle recCell = sender as System.Windows.Shapes.Rectangle;

            if (recCell.DataContext == null || (recCell.DataContext is XElement) == false)
                return;

            if (recCell.Parent == canvasProgramThumbnail)
                DrawCell_Camera(recCell.DataContext as XElement, false);
            else if (recCell.Parent == canvasMTProgramThumbnail)
                DrawCell_Camera(recCell.DataContext as XElement, true);
        }

        /// <summary>
        /// 在相机图像窗口显示CELL的测试结果
        /// </summary>
        /// <param name="xCell">路径测试点测试结果数据</param>
        private void DrawCell_Camera(XElement xCell, bool bMaintenance)
        {
            if (xCell == null)
                return;

            if (xCell.Element("Picture") == null)
            {
                MessageBox.Show("CELL图像不存在！",
                    Doc.MESSAGE_SOFTNAME,
                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            try
            {
                // http://www.aichengxu.com/view/64902
                byte[] reqData = Convert.FromBase64String((string)xCell.Element("Picture"));
                System.IO.MemoryStream ms = new System.IO.MemoryStream();
                ms = new System.IO.MemoryStream(reqData);
                BitmapImage pBImage = new BitmapImage();
                pBImage.BeginInit();
                pBImage.CacheOption = BitmapCacheOption.OnLoad;
                pBImage.StreamSource = ms;
                pBImage.EndInit();
                // 如下两选一
                //ms.Dispose();
                pBImage.Freeze();//非常重要！！！

                if (bMaintenance)
                    App.m_ShowState.CameraMTImage = pBImage;
                else App.m_ShowState.CameraImage = pBImage;

                //显示元件区域，有问题是红色，无问题是绿色，点击相机元件区域会在相机图像上方显示：模板元件、识别结果元件
                foreach (XElement xMeta in xCell.Elements("TestMeta"))
                {
                    if (xMeta.Element("MetaWidth") == null || xMeta.Element("MetaHeight") == null || xMeta.Element("Position") == null)
                        continue;
                    bool bError = false;
                    if (xMeta.Element("MetaResult") == null)
                        continue;
                    else if (bool.Parse((string)xMeta.Element("MetaResult")) == false)
                        bError = true;

                    //if (bError == false && xMeta.Element("MetaImage") != null && xMeta.Element("MetaImage").Element("CheckItemList") != null)
                    //{
                    //    foreach (XElement xPad in xMeta.Element("MetaImage").Element("CheckItemList").Elements("CheckItem"))
                    //    {
                    //        if (xPad.Element("CheckResult") != null && bool.Parse((string)xPad.Element("CheckResult")) == false)
                    //        {
                    //            bError = true;
                    //            break;
                    //        }
                    //    }
                    //}

                    TestMeta testMeta = new TestMeta(xMeta);
                    //testMeta.MetaXElement = xMeta;
                    System.Windows.Shapes.Rectangle rec = new System.Windows.Shapes.Rectangle
                    {
                        Fill = new SolidColorBrush() { Color = bError ? Colors.Red : Colors.Green, Opacity = 0.5 },
                        Stroke = new SolidColorBrush(bError ? Colors.Red : Colors.Green),
                        StrokeThickness = 2,
                        //Width = testMeta.MetaWidth,
                        //Height = testMeta.MetaHeight,
                    };
                    double rotation = testMeta.Rotation;

                    if (rotation % 360 == 90 || rotation % 360 == 270)
                    {
                        rec.Width = testMeta.MetaHeight;
                        rec.Height = testMeta.MetaWidth;
                    }
                    else
                    {
                        rec.Width = testMeta.MetaWidth;
                        rec.Height = testMeta.MetaHeight;
                    }

                    rec.SetValue(Canvas.LeftProperty, canvasCameraImage.Width - testMeta.Position.X - rec.Width / 2);
                    rec.SetValue(Canvas.TopProperty, canvasCameraImage.Height - testMeta.Position.Y - rec.Height / 2);

                    if (bMaintenance)
                    {
                        //canvasMTCameraImage.Children.Add(rec);

                        rec.MouseLeftButtonDown += MouseLeftButtonDown_CameraResult;
                        rec.DataContext = xMeta;
                    }
                    else canvasCameraImage.Children.Add(rec);

                }
            }
            catch { }
        }

        /// <summary>
        /// 主窗口缩略图图像上测试结果元件双击
        /// </summary>
        private void MouseLeftButtonDown_Result(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2)
                return;

            e.Handled = true;

            if ((sender is System.Windows.Shapes.Rectangle) == false)
                return;
            System.Windows.Shapes.Rectangle recMeta = sender as System.Windows.Shapes.Rectangle;

            if (recMeta.DataContext == null || (recMeta.DataContext is XElement) == false)
                return;

            DrawMeta_Camera(recMeta.DataContext as XElement, false);
        }

        /// <summary>
        /// 相机图像上测试结果元件双击
        /// </summary>
        private void MouseLeftButtonDown_CameraResult(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2)
                return;

            e.Handled = true;

            if ((sender is System.Windows.Shapes.Rectangle) == false)
                return;
            System.Windows.Shapes.Rectangle recMeta = sender as System.Windows.Shapes.Rectangle;

            if (recMeta.DataContext == null || (recMeta.DataContext is XElement) == false)
                return;

            DrawMeta_Camera(recMeta.DataContext as XElement, true);
        }

        /// <summary>
        /// 相机图像上测试结果元件中：模板图像、识别结果图像的显示
        /// </summary>
        /// <param name="xMeta"></param>
        private void DrawMeta_Camera(XElement xMeta, bool bMaintenance)
        {
            if (xMeta == null)
                return;

            if (xMeta.Element("MetaImage") == null || xMeta.Element("MetaImage").Element("Bitmap") == null)
            {
                MessageBox.Show("元件图像不存在！",
                    Doc.MESSAGE_SOFTNAME,
                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            try
            {
                // http://www.aichengxu.com/view/64902
                //模板图像、识别结果图像的显示
                byte[] reqData = Convert.FromBase64String((string)xMeta.Element("MetaImage").Element("Bitmap"));
                System.IO.MemoryStream ms = new System.IO.MemoryStream(reqData);
                BitmapImage pBImage = new BitmapImage();
                pBImage.BeginInit();
                pBImage.CacheOption = BitmapCacheOption.OnLoad;
                pBImage.StreamSource = ms;
                pBImage.EndInit();
                // 如下两选一
                //ms.Dispose();
                pBImage.Freeze();//非常重要！！！
                if (bMaintenance)
                    m_MetaTemplateImage.Source = pBImage;
                else m_TemplateImage.Source = pBImage;

                //App.m_ShowState.CameraImage = pBImage;
                //cameraImage.ImageSource = pBImage;
                //bitmap.Save(String.Format("{0}\\Cell.bmp", Doc.m_SystemParam.DataPath));
                if (xMeta.Element("BitmapResult") != null)
                {
                    reqData = Convert.FromBase64String((string)xMeta.Element("BitmapResult"));
                    ms = new System.IO.MemoryStream(reqData);
                    pBImage = new BitmapImage();
                    pBImage.BeginInit();
                    pBImage.CacheOption = BitmapCacheOption.OnLoad;
                    pBImage.StreamSource = ms;
                    pBImage.EndInit();
                    // 如下两选一
                    //ms.Dispose();
                    pBImage.Freeze();//非常重要！！！
                    if (bMaintenance)
                        m_MetaResultImage.Source = pBImage;
                    else m_ResultImage.Source = pBImage;
                }
            }
            catch { }
        }
        #endregion 运动系统

        /// <summary>
        /// 显示缓存中的下一块板
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShowNextTestResult(object sender, ExecutedRoutedEventArgs e)
        {
            if (!this.Dispatcher.CheckAccess()) // CheckAccess returns true if you're on the dispatcher thread
            {
                this.Dispatcher.BeginInvoke(new DelegateBoardShow(ShowNextTestResult), sender, e);
                return;
            }


            if (App.m_ListTestResult.Count > 1)
            {
                App.m_ListTestResult.RemoveAt(0);
                //XElement result = null;
                //if (File.Exists(App.m_ListTestResult[0]))
                //{
                //    result = XElement.Load(App.m_ListTestResult[0]);
                //}
                Show_TestResult(null);
                Show_TestResult(App.m_ListTestResult[0]);
                lbl_MTResultNext.Content = string.Format("1/{0}", App.m_ListTestResult.Count);
                lbl_ResultNext.Content = string.Format("1/{0}", App.m_ListTestResult.Count);
            }
        }

        #region 测试阈值设置
        private void ThresholdSet_Click(object sender, RoutedEventArgs e)
        {
            SetThresholdDlg SetThresholdDlg = new SetThresholdDlg();
            SetThresholdDlg.ShowDialog();
            SetThresholdDlg.Owner = this;
            if (SetThresholdDlg.Result == false)
                return;

            if (App.m_ShowState.CurrentProductType != null && App.m_ShowState.CurrentProductType.XTestRoute != null && App.m_ShowState.CurrentProductType.XTestRoute.Element("MultiBoard") != null)
            {
                if (bool.Parse((string)App.m_ShowState.CurrentProductType.XTestRoute.Element("MultiBoard")) == true)
                {
                    foreach (XElement xMeta in App.m_ShowState.CurrentProductType.XTestRoute.Elements("SubRectangle").Elements("TestCell").Elements("TestMeta"))
                    {
                        xMeta.SetElementValue("Threshold", SetThresholdDlg.MetaThreshold);
                        xMeta.SetElementValue("PadThreshold", SetThresholdDlg.PadThreshold);
                    }
                }
                else
                {
                    foreach (XElement xMeta in App.m_ShowState.CurrentProductType.XTestRoute.Elements("TestCell").Elements("TestMeta"))
                    {
                        xMeta.SetElementValue("Threshold", SetThresholdDlg.MetaThreshold);
                        xMeta.SetElementValue("PadThreshold", SetThresholdDlg.PadThreshold);
                    }
                }
                App.m_ShowState.CurrentProductType.XElement.Save(string.Format(Doc.m_SystemParam.SetupPath + "\\" + Doc.m_SystemParam.ProductFilename));
            }
        }
        #endregion 测试阈值设置

        #endregion 测试

        #region 维修站

        #region 测试结果查看和判定

        //private bool m_bResultShow = false;//表示测试缩略图窗口有测试结果显示
        private bool m_bMTResultShow = false;//表示维修站缩略图窗口是否有测试结果显示
        private int m_iMisAdjCount = 0;//误判元件数量
        private XElement m_XResultShow = null;//缩略图窗口显示的测试结果数据
        private List<XElement> m_XErrorMeta = new List<XElement>();//异常原件列表
        private List<XElement> m_XErrorCell = new List<XElement>();//异常TestCell列表
        private List<int> m_iErrorMetaNumInCell = new List<int>();//每个异常TestCell里异常元件数量列表
        int m_iIndexCell = 0;//异常TestCell索引
        int m_iIndexMeta = 0;//异常元件索引

        /// <summary>
        /// 缩略图上用于指示异常元件的十字线
        /// </summary>
        private System.Windows.Shapes.Rectangle rectThumbVertical = null;
        private System.Windows.Shapes.Rectangle rectThumbHorizontal = null;

        /// <summary>
        /// 显示异常指示元件的十字线
        /// </summary>
        private void ShowMetaCorss(Point p)
        {
            if (rectThumbVertical == null)
            {
                rectThumbVertical = new Rectangle()
                {
                    Fill = new SolidColorBrush() { Color = Colors.Red, Opacity = 1 },
                    Stroke = new SolidColorBrush(Colors.Red),
                    StrokeThickness = 1,
                    Width = 10,
                    Height = canvasMTProgramThumbnail.Height
                };

            }
            if (rectThumbHorizontal == null)
            {
                rectThumbHorizontal = new Rectangle()
                {
                    Fill = new SolidColorBrush() { Color = Colors.Red, Opacity = 1 },
                    Stroke = new SolidColorBrush(Colors.Red),
                    StrokeThickness = 1,
                    Width = canvasMTProgramThumbnail.Width,
                    Height = 10
                };
            }
            Canvas.SetLeft(rectThumbVertical, p.X);
            Canvas.SetTop(rectThumbVertical, 0);
            Canvas.SetLeft(rectThumbHorizontal, 0);
            Canvas.SetTop(rectThumbHorizontal, p.Y);
            if (!canvasMTProgramThumbnail.Children.Contains(rectThumbVertical))
                canvasMTProgramThumbnail.Children.Add(rectThumbVertical);
            if (!canvasMTProgramThumbnail.Children.Contains(rectThumbHorizontal))
                canvasMTProgramThumbnail.Children.Add(rectThumbHorizontal);

        }


        /// <summary>
        /// 向下键按下查看下一个异常元件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClickHandlerKeyDown(object sender, RoutedEventArgs e)
        {
            if (!this.Dispatcher.CheckAccess()) // CheckAccess returns true if you're on the dispatcher thread
            {
                this.Dispatcher.BeginInvoke(new DelegateMetaShow(ClickHandlerKeyDown), sender, e);
                return;
            }

            if (m_bMTResultShow == false || m_XResultShow == null)
                return;

            if (m_iIndexMeta > m_XErrorMeta.Count - 1)
            {
                //indexMeta = 0;
                return;
            }
            m_iIndexMeta = m_iIndexMeta + 1;
            m_iIndexCell = FindNum(m_iIndexMeta - 1, m_iErrorMetaNumInCell);
            //DrawCell_Camera(m_XErrorCell[m_iIndexCell], true);
            DrawMeta_Camera(m_XErrorMeta[m_iIndexMeta - 1], true);
            ButtonShowResult();

            double xCell = Point.Parse((string)m_XErrorCell[m_iIndexCell].Element("Position")).X;
            double yCell = Point.Parse((string)m_XErrorCell[m_iIndexCell].Element("Position")).Y;
            double xMeta = Point.Parse((string)m_XErrorMeta[m_iIndexMeta - 1].Element("Position")).X;
            double yMeta = Point.Parse((string)m_XErrorMeta[m_iIndexMeta - 1].Element("Position")).Y;

            double X = canvasMTProgramThumbnail.Width - xCell * Doc.m_SystemParam.HelicalPitchMap.X / Doc.m_SystemParam.HelicalPitch / m_iScale;
            double Y = canvasMTProgramThumbnail.Height - yCell * Doc.m_SystemParam.HelicalPitchMap.Y / Doc.m_SystemParam.HelicalPitch / m_iScale;
            double dMetaThumbX = X + (canvasCameraImage.Width / 2 - xMeta) / m_iScale;
            double dMetaThumbY = Y + (canvasCameraImage.Height / 2 - yMeta) / m_iScale;
            ShowMetaCorss(new Point(dMetaThumbX, dMetaThumbY));

            lbl_MTErrorMetaShow.Content = string.Format("当前显示/异常总数：{0}/{1}", m_iIndexMeta, m_XErrorMeta.Count);

        }

        /// <summary>
        /// 向上键按下查看上一个异常元件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClickHandlerKeyUp(object sender, RoutedEventArgs e)
        {
            if (!this.Dispatcher.CheckAccess()) // CheckAccess returns true if you're on the dispatcher thread
            {
                this.Dispatcher.BeginInvoke(new DelegateMetaShow(ClickHandlerKeyUp), sender, e);
                return;
            }
            if (m_bMTResultShow == false || m_XResultShow == null)
                return;

            if (m_iIndexMeta <= 1)
                return;
            m_iIndexMeta = m_iIndexMeta - 1;
            m_iIndexCell = FindNum(m_iIndexMeta - 1, m_iErrorMetaNumInCell);
            //DrawCell_Camera(m_XErrorCell[m_iIndexCell], true);
            DrawMeta_Camera(m_XErrorMeta[m_iIndexMeta - 1], true);
            double xCell = Point.Parse((string)m_XErrorCell[m_iIndexCell].Element("Position")).X;
            double yCell = Point.Parse((string)m_XErrorCell[m_iIndexCell].Element("Position")).Y;
            double xMeta = Point.Parse((string)m_XErrorMeta[m_iIndexMeta - 1].Element("Position")).X;
            double yMeta = Point.Parse((string)m_XErrorMeta[m_iIndexMeta - 1].Element("Position")).Y;

            double X = canvasMTProgramThumbnail.Width - xCell * Doc.m_SystemParam.HelicalPitchMap.X / Doc.m_SystemParam.HelicalPitch / m_iScale;
            double Y = canvasMTProgramThumbnail.Height - yCell * Doc.m_SystemParam.HelicalPitchMap.Y / Doc.m_SystemParam.HelicalPitch / m_iScale;
            double dMetaThumbX = X + (canvasCameraImage.Width / 2 - xMeta) / m_iScale;
            double dMetaThumbY = Y + (canvasCameraImage.Height / 2 - yMeta) / m_iScale;
            ShowMetaCorss(new Point(dMetaThumbX, dMetaThumbY));

            lbl_MTErrorMetaShow.Content = string.Format("当前显示/异常总数：{0}/{1}", m_iIndexMeta, m_XErrorMeta.Count);
            lbl_MTTestResult.Content = string.Format("测试结果：{0}", m_XErrorMeta.Count > 0 ? "NG" : "OK");
            ButtonShowResult();
        }


        /// <summary>
        /// 左键键按下判定当前异常元件的测试结果
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClickHandlerKeyLeft(object sender, RoutedEventArgs e)
        {
            if (m_iIndexMeta == 0)
                return;
            if (m_bMTResultShow == false || m_XResultShow == null || Doc.m_DBTestResult == null)
                return;
            if ((string)Btn_TestResult.Content == "正常")
            {
                m_XErrorMeta[m_iIndexMeta - 1].SetElementValue("MetaResult", false);
                ButtonShowResult();
                m_iMisAdjCount--;
            }
            else if ((string)Btn_TestResult.Content == "异常")
            {
                m_XErrorMeta[m_iIndexMeta - 1].SetElementValue("MetaResult", true);
                ButtonShowResult();
                m_iMisAdjCount++;
            }


        }

        /// <summary>
        /// 右键键保存测试数据到数据库
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClickHandlerKeyRight(object sender, RoutedEventArgs e)
        {
            if (m_bMTResultShow == false || m_XResultShow == null || Doc.m_DBTestResult == null)
                return;
            string sIdentifyPath = String.Format("{0}\\{1}_", Doc.m_SystemParam.DataPath, m_TestProductType.Name);
            string sIdentifyFilename = sIdentifyPath + Doc.m_DBTestResult.TestFilename;
            App.m_Motion.TestRecord.SetElementValue("Review", true);
            App.m_Motion.TestRecord.Save(sIdentifyFilename);


            Doc.m_DBTestResult.ProductType = m_TestProductType.Name;
            Doc.m_DBTestResult.ErrorCount = m_XErrorMeta.Count - m_iMisAdjCount;
            Doc.m_DBTestResult.MisAdjCount = m_iMisAdjCount;
            Doc.m_DBTestResult.IsReview = true;
            Doc.m_DBTestResult.IsOK = m_XErrorMeta.Count == m_iMisAdjCount ? true : false;

            //DataBase.AddTestResult(Doc.m_DBTestResult);
            //MessageBox.Show("保存成功");
        }

        /// <summary>
        /// 根据异常元件索引判断异常元件属于哪个TestCell
        /// </summary>
        private int FindNum(int n, List<int> list)//
        {
            int result = 0;
            int m = 0;
            for (int i = 0; i < list.Count; i++)
            {
                m += list[i];
                if (m > n)
                {
                    result = i;
                    break;
                }
            }
            return result;
        }

        private void ButtonShowResult()
        {
            if ((bool)m_XErrorMeta[m_iIndexMeta - 1].Element("MetaResult") == false)
            {
                Btn_TestResult.Content = "异常";
            }
            else if ((bool)m_XErrorMeta[m_iIndexMeta - 1].Element("MetaResult") == true)
            {
                Btn_TestResult.Content = "正常";
            }
            //DrawCell_Camera(m_XErrorCell[m_iIndexCell], true);
            DrawMeta_Camera(m_XErrorMeta[m_iIndexMeta - 1], true);
        }

        #endregion 测试结果查看和判定

        /// <summary>
        /// 维修站相机图像更改
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        //private void cameraMTImage_Changed(object sender, EventArgs e)
        //{
        //    // 背景的消隐与显示根据cameraImage.ImageSource是否为空
        //    if (recCameraMTImageBkg == null)
        //        return;

        //    if (cameraMTImage.ImageSource != null)
        //    {
        //        if (recCameraMTImageBkg.Visibility == Visibility.Visible)
        //            recCameraMTImageBkg.Visibility = Visibility.Collapsed;
        //    }
        //    else
        //    {
        //        if (recCameraMTImageBkg.Visibility == Visibility.Collapsed)
        //            recCameraMTImageBkg.Visibility = Visibility.Visible;
        //    }
        //    canvasMTCameraImage.Children.Clear();
        //    m_MetaTemplateImage.Source = null;
        //    m_MetaResultImage.Source = null;
        //}

        /// <summary>
        /// 维修站缩略图图像更改：缩略图图像改变时自动调用，缩略图图像为空时显示背景图像，缩略图图像不为空时显示缩略图图像
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MTThumbnailImage_Changed(object sender, EventArgs e)
        {
            if (recMTAxisMaxRunBkg == null)
                return;

            if (MTThumbnailImage.ImageSource != null)
            {
                canvasMTProgramThumbnail.Width = MTThumbnailImage.ImageSource.Width;
                canvasMTProgramThumbnail.Height = MTThumbnailImage.ImageSource.Height;
                if (recMTAxisMaxRunBkg.Visibility != Visibility.Collapsed)
                    recMTAxisMaxRunBkg.Visibility = Visibility.Collapsed;
            }
            else
            {
                canvasMTProgramThumbnail.Width = Doc.m_SystemParam.AxisMaxRun_X * MotionDMC5000.MAP_ThumbnailPixel;
                canvasMTProgramThumbnail.Height = Doc.m_SystemParam.AxisMaxRun_Y * MotionDMC5000.MAP_ThumbnailPixel;
                if (recMTAxisMaxRunBkg.Visibility != Visibility.Visible)
                    recMTAxisMaxRunBkg.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// 当前显示的文件名
        /// 为空：当天文件不存在
        /// </summary>
        private string m_sHistoryFilename = "";

        /// <summary>
        /// 历史记录的日期更改
        /// </summary>
        private void HistoryDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (App.m_ShowState.CurrentProductType.XElement == null)
                return;
            if (HistoryDatePicker.SelectedDate > DateTime.Now)
                return;

            e.Handled = true;

            m_sHistoryFilename = "";
        }

        /// <summary>
        /// 当天测试的第一块板的数据记录
        /// </summary>
        private void BtnHistoryHead_Click(object sender, RoutedEventArgs e)
        {
            if (FindHistoryFile(0))
                HistoryShow();
            else
            {
                if (String.IsNullOrEmpty(m_sHistoryFilename))
                    MessageBox.Show("没有可显示的测试记录！",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                else
                {
                    MessageBox.Show("当前显示的已是第一块板的测试记录",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }
        }

        /// <summary>
        /// 当天测试的当前位置向第一块板方向移动的数据记录
        /// </summary>
        private void BtnHistoryBack_Click(object sender, RoutedEventArgs e)
        {
            if (FindHistoryFile(1))
                HistoryShow();
            else
            {
                if (String.IsNullOrEmpty(m_sHistoryFilename))
                    MessageBox.Show("没有可显示的测试记录！",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                else
                {
                    MessageBox.Show("当前显示的已是第一块板的测试记录",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }
        }

        /// <summary>
        /// 当天测试的当前位置向最后一块板方向移动的数据记录
        /// </summary>
        private void BtnHistoryBefor_Click(object sender, RoutedEventArgs e)
        {
            if (FindHistoryFile(2))
                HistoryShow();
            else
            {
                if (String.IsNullOrEmpty(m_sHistoryFilename))
                    MessageBox.Show("没有可显示的测试记录！",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                else
                {
                    MessageBox.Show("当前显示的已是最后一块板的测试记录",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }
        }

        /// <summary>
        /// 当天测试的最后一块板的数据记录
        /// </summary>
        private void BtnHistoryEnd_Click(object sender, RoutedEventArgs e)
        {
            if (FindHistoryFile(3))
                HistoryShow();
            else
            {
                if (String.IsNullOrEmpty(m_sHistoryFilename))
                    MessageBox.Show("没有可显示的测试记录！",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                else
                {
                    MessageBox.Show("当前显示的已是最后一块板的测试记录",
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }
        }

        /// <summary>
        /// 寻找当前型号、当前日期中的测试文件名
        /// </summary>
        /// <param name="iFlag">0：到开始；1：前一块板；2：后一块板；3：结束</param>
        /// <returns>true：找到有效文件名</returns>
        private bool FindHistoryFile(int iFlag)
        {
            try
            {
                // 路径不存在：返回
                if (Directory.Exists(Doc.m_SystemParam.DataPath) == false)
                    return false;

                //循环获得
                String sFilter = String.Format("{0}_{1}_??????.xml", App.m_ShowState.CurrentProductType.Name, ((DateTime)HistoryDatePicker.SelectedDate).ToString("yyyyMMdd"));
                string[] files = Directory.GetFiles(Doc.m_SystemParam.DataPath, sFilter);//得到文件

                //查找
                IComparer comparer = null;
                switch (iFlag)
                {
                    //从小到大顺序查找
                    case 0://到开始
                    case 2://后一块板
                        comparer = new AscendComparer();
                        break;
                    //从大到小顺序查找
                    case 1://前一块板
                    case 3://结束
                    default:
                        comparer = new ReduceComparer();
                        break;
                }
                Array.Sort(files, comparer);
                bool bFlag = false;
                foreach (string file in files)//循环文件
                {
                    if (iFlag == 0 || iFlag == 3 || String.IsNullOrEmpty(m_sHistoryFilename))
                    {
                        if (m_sHistoryFilename == file)
                            return false;
                        else
                        {
                            m_sHistoryFilename = file;
                            return true;
                        }
                    }
                    else
                    {
                        if (bFlag == false)
                        {
                            if (m_sHistoryFilename == file)
                                bFlag = true;
                        }
                        else
                        {
                            m_sHistoryFilename = file;
                            return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        /// <summary>
        /// 清理维修站显示
        /// </summary>
        private void HistoryShow_Clear()
        {
            if (String.IsNullOrEmpty(m_sHistoryFilename))
                return;

            canvasMTProgramThumbnail.Children.Clear();
        }

        /// <summary>
        /// 显示当前定位的文件
        /// </summary>
        private void HistoryShow()
        {
            if (String.IsNullOrEmpty(m_sHistoryFilename))
                return;

            HistoryShow_Clear();
            try
            {
                XElement rstXElement = null;
                if (File.Exists(m_sHistoryFilename) == true)
                    rstXElement = XElement.Load(m_sHistoryFilename);
                if (rstXElement == null)
                    return;
                Show_MTTestResult(rstXElement);
            }
            catch
            {
                MessageBox.Show(String.Format("文档{0}解析错误", m_sHistoryFilename),
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }

        }

        /// <summary>
        /// 在维修站缩略图上显示测试结果
        /// </summary>
        private void Show_MTTestResult(XElement xResult)
        {
            if (xResult == null)
            {
                canvasMTProgramThumbnail.Children.Clear();
            }
            else
            {// 在缩略图显示区画测试点区域框
                MTThumbnailImage.ImageSource = null;

                if (App.m_ShowState.CurrentProductType.XElement != null)
                {
                    if (App.m_ShowState.CurrentProductType.Thumbnail != null)
                    {
                        string sSample = Doc.m_SystemParam.SetupPath + "\\Thumbnail\\" + App.m_ShowState.CurrentProductType.Thumbnail;
                        if (File.Exists(sSample))
                        {
                            BitmapImage bmpImage = new BitmapImage(new Uri(sSample));
                            MTThumbnailImage.ImageSource = bmpImage;
                        }
                    }
                }

                if (xResult.Element("TestTime") != null)
                    App.m_ShowState.TestTime = (string)xResult.Element("TestTime");
                DrawCell_MaintenanceTestResult(xResult);
                m_XResultShow = xResult;
                m_bMTResultShow = true;
            }
        }

        /// <summary>
        /// 在维修站缩略图显示区画测试点区域框
        /// </summary>
        private void DrawCell_MaintenanceTestResult(XElement xResult)
        {
            if (xResult == null)
                return;

            m_XErrorMeta.Clear();
            m_XErrorCell.Clear();
            m_iErrorMetaNumInCell.Clear();
            foreach (XElement xCell in xResult.Elements("TestCell"))
            {
                int iErrorMetaNum = 0;//每个cell里异常元件数
                TestCell testCell = new TestCell(xCell);
                double X = App.m_ShowState.CurrentProductType.ThumbnailWidth - testCell.Position.X / App.m_ShowState.CurrentProductType.MotionWidth * App.m_ShowState.CurrentProductType.ThumbnailWidth;
                double Y = App.m_ShowState.CurrentProductType.ThumbnailHeight - testCell.Position.Y / App.m_ShowState.CurrentProductType.MotionHeight * App.m_ShowState.CurrentProductType.ThumbnailHeight;

                bool bError = false;
                foreach (XElement xMeta in xCell.Elements("TestMeta"))
                {
                    if (xMeta.Element("Test") == null || bool.Parse((string)xMeta.Element("Test")) == false)
                        continue;

                    if (xMeta.Element("MetaResult") == null)
                        continue;
                    else if (bool.Parse((string)xMeta.Element("MetaResult")) == false)
                    {
                        bError = true;
                        iErrorMetaNum++;
                        TestMeta testMeta = new TestMeta(xMeta);
                        Point p = Point.Parse((string)xMeta.Element("Position"));
                        double dMetaThumbX = X + (canvasCameraImage.Width / 2 - p.X) / m_iScale;
                        //double dMetaThumbX = X + (p.X-canvasMTCameraImage.Width/2 ) / m_iScale;
                        double dMetaThumbY = Y + (canvasCameraImage.Height / 2 - p.Y) / m_iScale;
                        System.Windows.Shapes.Rectangle recMeta = new System.Windows.Shapes.Rectangle
                        {
                            Fill = new SolidColorBrush() { Color = bError ? Colors.Red : Colors.Green, Opacity = 0.5 },
                            Stroke = new SolidColorBrush(bError ? Colors.Red : Colors.Green),
                            StrokeThickness = 2,
                        };
                        double rotation = testMeta.Rotation;
                        if (rotation % 360 == 0 || rotation % 360 == 180)
                        {
                            recMeta.Width = testMeta.MetaWidth / m_iScale;
                            recMeta.Height = testMeta.MetaHeight / m_iScale;
                        }
                        else if (rotation % 360 == 90 || rotation % 360 == 270)
                        {
                            recMeta.Width = testMeta.MetaHeight / m_iScale;
                            recMeta.Height = testMeta.MetaWidth / m_iScale;
                        }
                        recMeta.SetValue(Canvas.LeftProperty, dMetaThumbX - recMeta.Width / 2);
                        recMeta.SetValue(Canvas.TopProperty, dMetaThumbY - recMeta.Height / 2);
                        recMeta.MouseLeftButtonDown += MouseLeftButtonDown_CameraResult;
                        recMeta.DataContext = xMeta;
                        canvasMTProgramThumbnail.Children.Add(recMeta);
                        m_XErrorMeta.Add(xMeta);
                    }
                    //if (xMeta.Element("MetaImage") != null && xMeta.Element("MetaImage").Element("CheckItemList") != null)
                    //{
                    //    foreach (XElement xPad in xMeta.Element("MetaImage").Element("CheckItemList").Elements("CheckItem"))
                    //    {
                    //        if (xPad.Element("CheckResult") == null)
                    //            continue;
                    //        else if (bool.Parse((string)xPad.Element("CheckResult")) == false)
                    //        {
                    //            bError = true;
                    //            iErrorMetaNum++;
                    //            m_XErrorMeta.Add(xMeta);
                    //            break;
                    //        }
                    //    }

                    //}
                    //if (!bError)
                    //{//元件正常 删除相关数据
                    //    if (xMeta.Element("MetaImage") != null)
                    //        xMeta.Element("MetaImage").Remove();
                    //    if (xMeta.Element("BitmapResult") != null)
                    //        xMeta.Element("BitmapResult").Remove();
                    //}
                }

                if (bError)
                {
                    m_XErrorCell.Add(xCell);
                    m_iErrorMetaNumInCell.Add(iErrorMetaNum);
                }


                //System.Windows.Shapes.Rectangle recCell = new System.Windows.Shapes.Rectangle
                //{
                //    Fill = new SolidColorBrush() { Color = bError ? Colors.Red : Colors.Green, Opacity = 0.0 },
                //    Stroke = new SolidColorBrush(bError ? Colors.Red : Colors.Green),
                //    StrokeThickness = 2,
                //    Width = App.m_ShowState.CurrentProductType.ThumbnailSubWidth,
                //    Height = App.m_ShowState.CurrentProductType.ThumbnailSubHeight
                //};
                //recCell.SetValue(Canvas.LeftProperty, X - App.m_ShowState.CurrentProductType.ThumbnailSubWidth / 2);
                //recCell.SetValue(Canvas.TopProperty, Y - App.m_ShowState.CurrentProductType.ThumbnailSubHeight / 2);
                //canvasMTProgramThumbnail.Children.Add(recCell);

                //recCell.MouseLeftButtonDown += MouseLeftButtonDown_CellResult;
                //recCell.DataContext = xCell;
            }

            m_iIndexMeta = 0;

            lbl_MTErrorMetaShow.Content = string.Format("当前显示/异常总数：{0}/{1}", m_iIndexMeta, m_XErrorMeta.Count);

            lbl_MTTestResult.Content = string.Format("测试结果：{0}", m_XErrorMeta.Count > 0 ? "NG" : "OK");

            m_bMTResultShow = true;

        }

        #region IComparer
        /// <summary>
        /// 升序比较器
        /// </summary>
        public class AscendComparer : IComparer
        {
            // Calls CaseInsensitiveComparer.Compare with the parameters reversed.
            int IComparer.Compare(Object x, Object y)
            {
                return ((new CaseInsensitiveComparer()).Compare(x, y));
            }
        }

        /// <summary>
        /// 降序比较器
        /// </summary>
        public class ReduceComparer : IComparer
        {
            // Calls CaseInsensitiveComparer.Compare with the parameters reversed.
            int IComparer.Compare(Object x, Object y)
            {
                return ((new CaseInsensitiveComparer()).Compare(y, x));
            }
        }

        #endregion IComparer

        /// <summary>
        /// 在线扫描枪扫描结果处理
        /// </summary>
        private void OnlineScanner_Handler(string sData)
        {
            if (!this.Dispatcher.CheckAccess()) // CheckAccess returns true if you're on the dispatcher thread
            {
                this.Dispatcher.BeginInvoke(new DelegateStringParameter(OnlineScanner_Handler), sData);
                return;
            }

            if (String.IsNullOrEmpty(sData))
                return;

            if (Doc.m_SystemParam.ShowMode != ShowMode.Repair)
                App.m_ShowState.BarcodeTest = sData;

            if (Doc.m_SystemParam.ShowMode != ShowMode.System && String.Compare(Doc.m_SystemParam.OfflineScanner, "None") == 0)
            {
                App.m_ShowState.BarcodeTrack = sData;

                string sIdentifyFilename = String.Format("{0}\\{1}_{2}.xml", Doc.m_SystemParam.DataPath, App.m_ShowState.CurrentProductType.Name, App.m_ShowState.BarcodeTrack);
                if (File.Exists(sIdentifyFilename) == true)
                {
                    m_sHistoryFilename = sIdentifyFilename;
                    HistoryShow();
                }
                else
                    MessageBox.Show(String.Format("二维码：{0}记录不存在", sData),
                                    Doc.MESSAGE_SOFTNAME,
                                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        /// <summary>
        /// 离线扫描枪扫描结果处理
        /// </summary>
        private void OfflineScanner_Handler(string sData)
        {
            if (!this.Dispatcher.CheckAccess()) // CheckAccess returns true if you're on the dispatcher thread
            {
                this.Dispatcher.BeginInvoke(new DelegateStringParameter(OfflineScanner_Handler), sData);
                return;
            }

            if (String.IsNullOrEmpty(sData))
                return;

            App.m_ShowState.BarcodeTrack = sData;

            string sIdentifyFilename = String.Format("{0}\\{1}_{2}.xml", Doc.m_SystemParam.DataPath, App.m_ShowState.CurrentProductType.Name, App.m_ShowState.BarcodeTrack);
            if (File.Exists(sIdentifyFilename) == true)
            {
                m_sHistoryFilename = sIdentifyFilename;
                HistoryShow();
            }
            else
                MessageBox.Show(String.Format("二维码：{0}记录不存在", sData),
                                Doc.MESSAGE_SOFTNAME,
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }






        #endregion 维修站

       
    }
}
