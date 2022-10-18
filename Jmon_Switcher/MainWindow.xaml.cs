using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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

using BMDSwitcherAPI;
using HandyControl.Tools.Extension;

namespace Jmon_Switcher
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        private IBMDSwitcherDiscovery m_switcherDiscovery;          //ATEM 연결을 위해 장비 찾는 것.

        private IBMDSwitcher m_switcher;                            //ATEM 스위쳐 장비 그 자체.
        private IBMDSwitcherMixEffectBlock m_mixEffectBlock1;       //ATEM 화면 입,출력 + 화면전환.
        private IBMDSwitcherKey m_switcher_key;                     //ATEM 크로마키 담당.
        private IBMDSwitcherKeyChromaParameters m_chromaParameters; //ATEM 크로마키에서 Hue,Gain 등 파라미터 담당.

        private IBMDSwitcherKeyFlyParameters m_flyParameters;       //? 필요없는듯.
        private IBMDSwitcherKeyDVEParameters m_dVEParameters;       //? 필요없는듯.

        private IBMDSwitcherAudioMixer m_audioMixer;                                    //오디오 믹서 - out
        private IBMDSwitcherAudioInput m_audioInput;                                    //오디오 gain, balance - cam
        private IBMDSwitcherAudioMonitorOutput m_audioMonitorOutput;                    //? 필요없는듯.
        private IBMDSwitcherAudioInputIterator m_audioInputiterator = null;
        private IBMDSwitcherAudioMonitorOutputIterator m_audioOutputIterator = null;

        private SwitcherMonitor m_switcherMonitor;
        private MixEffectBlockMonitor m_mixEffectBlockMonitor;
        private ChromaMonitor m_chromaMonitor;                                  
        private List<InputMonitor> m_inputMonitors = new List<InputMonitor>();  //Callback을 관리함.
        private string Switcher_IP = "192.168.21.199";


        Chroma_Window cw ;

        public enum _ATEM_TRAN_TYPE_ : int
        {
            eATT_Mix = 0,
            eATT_LeftRight,
            eATT_UpDown,
            eATT_InOut,
            eATT_FourBox,
            eATT_HoriBox,
            eATT_VertiBox,
            eATT_DiaIris,
            eATT_TopLeft,
            eATT_TopRight,
            eATT_Max
        }        //transition enum
        private bool m_moveSliderDownwards = false;
        private bool m_currentTransitionReachedHalfway = false;



        public MainWindow()
        {
            InitializeComponent();

            //Callback 함수 구현부
            m_switcherMonitor = new SwitcherMonitor();
            m_switcherMonitor.SwitcherDisconnected += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => SwitcherDisconnected())));

            m_mixEffectBlockMonitor = new MixEffectBlockMonitor();
            m_mixEffectBlockMonitor.ProgramInputChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => UpdateProgramButtonSelection())));
            m_mixEffectBlockMonitor.PreviewInputChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => UpdatePreviewButtonSelection())));
            m_mixEffectBlockMonitor.TransitionFramesRemainingChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => UpdateTransitionFramesRemaining())));
            m_mixEffectBlockMonitor.TransitionPositionChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => UpdateSliderPosition())));
            m_mixEffectBlockMonitor.InTransitionChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => OnInTransitionChanged())));

            m_chromaMonitor = new ChromaMonitor();
            m_chromaMonitor.ChromaHueChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => Chroma_Hue_Changed_Callback())));

            //ATEM 스위치 연결
            m_switcherDiscovery = new CBMDSwitcherDiscovery();
            if (m_switcherDiscovery == null)
            {
                MessageBox.Show("Could not create Switcher Discovery Instance.\nATEM Switcher Software may not be installed.", "Error");
                Environment.Exit(1);
            }

            SwitcherDisconnected();		// start with switcher disconnected
            Connect_Switcher();         // auto connect to switcher
            InitKeyersData();
        }

        private void SwitcherDisconnected()
        {
            textBoxSwitcherName.Content = "";

            MixEffectBlockSetEnable(false);

            // Remove all input monitors, remove callbacks
            foreach (InputMonitor inputMon in m_inputMonitors)
            {
                inputMon.Input.RemoveCallback(inputMon);
                inputMon.LongNameChanged -= new SwitcherEventHandler(OnInputLongNameChanged);
            }
            m_inputMonitors.Clear();

            if (m_mixEffectBlock1 != null)
            {
                // Remove callback
                m_mixEffectBlock1.RemoveCallback(m_mixEffectBlockMonitor);

                // Release reference
                m_mixEffectBlock1 = null;
            }

            if (m_switcher != null)
            {
                // Remove callback:
                m_switcher.RemoveCallback(m_switcherMonitor);

                // release reference:
                m_switcher = null;
            }
        }
        private void Connect_Switcher()
        {
            _BMDSwitcherConnectToFailure failReason = 0;
            string address = Switcher_IP;

            try
            {
                m_switcherDiscovery.ConnectTo(address, out m_switcher, out failReason); //연결을 시도.
            }
            catch (COMException)
            {
                // An exception will be thrown if ConnectTo fails. For more information, see failReason.
                switch (failReason)
                {
                    case _BMDSwitcherConnectToFailure.bmdSwitcherConnectToFailureNoResponse:
                        MessageBox.Show("No response from Switcher", "Error");
                        break;
                    case _BMDSwitcherConnectToFailure.bmdSwitcherConnectToFailureIncompatibleFirmware:
                        MessageBox.Show("Switcher has incompatible firmware", "Error");
                        break;
                    default:
                        MessageBox.Show("Connection failed for unknown reason", "Error");
                        break;
                }
                return;
            }

            SwitcherConnected();
        } //ok
        private void SwitcherConnected()
        {

            // Get the switcher name:
            //string switcherName;
            //m_switcher.GetProductName(out switcherName);
            //textBoxSwitcherName.Content = switcherName;

            // Install SwitcherMonitor callbacks:
            m_switcher.AddCallback(m_switcherMonitor);

            // We create input monitors for each input. To do this we iterate over all inputs:
            // This will allow us to update the combo boxes when input names change:


            IBMDSwitcherInputIterator inputIterator = null;
            IntPtr inputIteratorPtr;
            Guid inputIteratorIID = typeof(IBMDSwitcherInputIterator).GUID;
            m_switcher.CreateIterator(ref inputIteratorIID, out inputIteratorPtr);
            if (inputIteratorPtr != null)
            {
                inputIterator = (IBMDSwitcherInputIterator)Marshal.GetObjectForIUnknown(inputIteratorPtr);
            }

            if (inputIterator != null)
            {
                IBMDSwitcherInput input;
                inputIterator.Next(out input);
                while (input != null)
                {
                    InputMonitor newInputMonitor = new InputMonitor(input);
                    input.AddCallback(newInputMonitor);
                    newInputMonitor.LongNameChanged += new SwitcherEventHandler(OnInputLongNameChanged);

                    m_inputMonitors.Add(newInputMonitor);

                    inputIterator.Next(out input);
                }
            }

            // We want to get the first Mix Effect block (ME 1). We create a ME iterator,
            // and then get the first one:
            m_mixEffectBlock1 = null;

            IBMDSwitcherMixEffectBlockIterator meIterator = null;
            IntPtr meIteratorPtr;
            Guid meIteratorIID = typeof(IBMDSwitcherMixEffectBlockIterator).GUID;
            m_switcher.CreateIterator(ref meIteratorIID, out meIteratorPtr);
            if (meIteratorPtr != null)
            {
                meIterator = (IBMDSwitcherMixEffectBlockIterator)Marshal.GetObjectForIUnknown(meIteratorPtr);
            }

            if (meIterator == null)
                return;

            if (meIterator != null)
            {
                meIterator.Next(out m_mixEffectBlock1);
            }

            if (m_mixEffectBlock1 == null)
            {
                MessageBox.Show("Unexpected: Could not get first mix effect block", "Error");
                return;
            }

            // Install MixEffectBlockMonitor callbacks:
            m_mixEffectBlock1.AddCallback(m_mixEffectBlockMonitor);

            m_audioMixer = (IBMDSwitcherAudioMixer)m_switcher;


            //Audio Input iterator +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=

            IntPtr AinIteratorPtr = IntPtr.Zero;
            Guid AinIteratorIID = typeof(IBMDSwitcherAudioInputIterator).GUID;
            m_audioMixer.CreateIterator(ref AinIteratorIID, out AinIteratorPtr);
            if (AinIteratorPtr != null)
            {
                m_audioInputiterator = (IBMDSwitcherAudioInputIterator)Marshal.GetObjectForIUnknown(AinIteratorPtr);
            }

            if (m_audioInputiterator != null)
            {
                m_audioInputiterator.Next(out m_audioInput);
                m_audioInputiterator.Next(out m_audioInput);
            }
            //m_audioInput.AddCallback(m_audioInputMonitor);

            //Audio Output iterator +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
            IntPtr AoutIteratorPtr = IntPtr.Zero;
            Guid AoutIteratorIID = typeof(IBMDSwitcherAudioMonitorOutputIterator).GUID;
            m_audioMixer.CreateIterator(ref AoutIteratorIID, out AoutIteratorPtr);
            if (AoutIteratorPtr != null)
            {
                m_audioOutputIterator = (IBMDSwitcherAudioMonitorOutputIterator)Marshal.GetObjectForIUnknown(AoutIteratorPtr);
            }

            if (m_audioOutputIterator != null)
            {
                m_audioOutputIterator.Next(out m_audioMonitorOutput);
            }
            //m_audioMonitorOutput.AddCallback(m_audioOutputMonitor);




            MixEffectBlockSetEnable(true);      //스위치에 연결되면, UI를 사용할 수 있도록 enable 해주는 함수.
            UpdatePopupItems();
            UpdateTransitionFramesRemaining();
            UpdateSliderPosition();
        }

        private void OnInputLongNameChanged(object sender, object args)
        {
            this.Dispatcher.Invoke((Action)(() => UpdatePopupItems()));
        }



        private void MixEffectBlockSetEnable(bool enable)
        {
            //comboBoxProgramSel.IsEnabled = enable;
            //comboBoxPreviewSel.IsEnabled = enable;
            //대신 아래 코드
            prog_Btn_1.IsEnabled = enable;
            prog_Btn_2.IsEnabled = enable;
            prog_Btn_3.IsEnabled = enable;
            prog_Btn_4.IsEnabled = enable;
            prog_Btn_5.IsEnabled = enable;
            prog_Btn_6.IsEnabled = enable;
            prog_Btn_7.IsEnabled = enable;
            prog_Btn_8.IsEnabled = enable;

            prev_Btn_1.IsEnabled = enable;
            prev_Btn_2.IsEnabled = enable;
            prev_Btn_3.IsEnabled = enable;
            prev_Btn_4.IsEnabled = enable;
            prev_Btn_5.IsEnabled = enable;
            prev_Btn_6.IsEnabled = enable;
            prev_Btn_7.IsEnabled = enable;
            prev_Btn_8.IsEnabled = enable;

            buttonAuto.IsEnabled = enable;
            buttonCut.IsEnabled = enable;
            Slider_transition_bar.IsEnabled = enable;

            //다른 버튼들도 추가 해야함.
        }
        private void UpdatePopupItems()
        {
            // Clear the combo boxes:
            //comboBoxProgramSel.Items.Clear();
            //comboBoxPreviewSel.Items.Clear();
            chroma_key_combo.Items.Clear();

            // Get an input iterator.
            IBMDSwitcherInputIterator inputIterator = null;
            IntPtr inputIteratorPtr;
            Guid inputIteratorIID = typeof(IBMDSwitcherInputIterator).GUID;
            m_switcher.CreateIterator(ref inputIteratorIID, out inputIteratorPtr);
            if (inputIteratorPtr != null)
            {
                inputIterator = (IBMDSwitcherInputIterator)Marshal.GetObjectForIUnknown(inputIteratorPtr);
            }

            if (inputIterator == null)
                return;

            IBMDSwitcherInput input;
            inputIterator.Next(out input);
            while (input != null)
            {
                string inputName;
                long inputId;

                input.GetInputId(out inputId);
                input.GetLongName(out inputName);

                // Add items to list:
                //comboBoxProgramSel.Items.Add(new StringObjectPair<long>(inputName, inputId));
                //comboBoxPreviewSel.Items.Add(new StringObjectPair<long>(inputName, inputId));
                chroma_key_combo.Items.Add(new StringObjectPair<long>(inputName, inputId));

                inputIterator.Next(out input);
            }

            UpdateProgramButtonSelection();
            UpdatePreviewButtonSelection();
        }
        private void UpdateProgramButtonSelection()
        {
            long programId;

            m_mixEffectBlock1.GetProgramInput(out programId);

            // Select the program popup entry that matches the input id:

            //선택된 item의 버튼 색 변경
            Black_program.Background = prog_Btn_1.Background = prog_Btn_2.Background = prog_Btn_3.Background = prog_Btn_4.Background = prog_Btn_5.Background = prog_Btn_6.Background = prog_Btn_7.Background = prog_Btn_8.Background = Brushes.LightGray;
            switch (programId)
            {
                case 0: Black_program.Background = Brushes.Red; break;
                case 1: prog_Btn_1.Background = Brushes.Red; break;
                case 2: prog_Btn_2.Background = Brushes.Red; break;
                case 3: prog_Btn_3.Background = Brushes.Red; break;
                case 4: prog_Btn_4.Background = Brushes.Red; break;
                case 5: prog_Btn_5.Background = Brushes.Red; break;
                case 6: prog_Btn_6.Background = Brushes.Red; break;
                case 7: prog_Btn_7.Background = Brushes.Red; break;
                case 8: prog_Btn_8.Background = Brushes.Red; break;

            }
        }

        private void UpdatePreviewButtonSelection()
        {
            long previewId;

            m_mixEffectBlock1.GetPreviewInput(out previewId);

            // Select the program popup entry that matches the input id:

            //선택된 item의 버튼 색 변경

            //선택된 item의 버튼 색 변경
            Black_preview.Background = prev_Btn_1.Background = prev_Btn_2.Background  = prev_Btn_3.Background = prev_Btn_4.Background = prev_Btn_5.Background = prev_Btn_6.Background = prev_Btn_7.Background = prev_Btn_8.Background = Brushes.LightGray;
            switch (previewId)
            {
                case 0: Black_preview.Background = Brushes.Red; break;
                case 1: prev_Btn_1.Background = Brushes.Red; break;
                case 2: prev_Btn_2.Background = Brushes.Red; break;
                case 3: prev_Btn_3.Background = Brushes.Red; break;
                case 4: prev_Btn_4.Background = Brushes.Red; break;
                case 5: prev_Btn_5.Background = Brushes.Red; break;
                case 6: prev_Btn_6.Background = Brushes.Red; break;
                case 7: prev_Btn_7.Background = Brushes.Red; break;
                case 8: prev_Btn_8.Background = Brushes.Red; break;

            }
        }

        private void UpdateTransitionFramesRemaining()
        {
            uint framesRemaining;

            m_mixEffectBlock1.GetTransitionFramesRemaining(out framesRemaining);
        }

        private void UpdateSliderPosition()
        {
            double transitionPos;

            m_mixEffectBlock1.GetTransitionPosition(out transitionPos);

            m_currentTransitionReachedHalfway = (transitionPos >= 0.50);

            if (m_moveSliderDownwards)
                Slider_transition_bar.Value = 100 - (int)(transitionPos * 100);
            else
                Slider_transition_bar.Value = (int)(transitionPos * 100);
        } //ok

        private void OnInTransitionChanged()
        {
            int inTransition;

            m_mixEffectBlock1.GetInTransition(out inTransition);

            if (inTransition == 0)
            {
                // Toggle the starting orientation of slider handle if a transition has passed through halfway
                if (m_currentTransitionReachedHalfway)
                {
                    m_moveSliderDownwards = !m_moveSliderDownwards;
                    UpdateSliderPosition();
                }
                m_currentTransitionReachedHalfway = false;
            }
        } //ok


        private void SetProgramCurrentInput(int number)
        {
            long inputId = number;

            if (m_mixEffectBlock1 != null)
            {
                m_mixEffectBlock1.SetProgramInput(inputId);
            }
        }

        private void SetPreviewCurrentInput(int number)
        {
            long inputId = number;

            if (m_mixEffectBlock1 != null)
            {
                m_mixEffectBlock1.SetPreviewInput(inputId);
            }
        }

        private void buttonAuto_Click(object sender, EventArgs e)
        {
            if (m_mixEffectBlock1 != null)
            {
                m_mixEffectBlock1.PerformAutoTransition();
            }
        } //ok

        private void buttonCut_Click(object sender, EventArgs e)
        {
            if (m_mixEffectBlock1 != null)
            {
                m_mixEffectBlock1.PerformCut();
            }
        } //ok

        private void Slider_transition_bar_Scroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
        {
            if (m_mixEffectBlock1 != null)
            {
                double position = Slider_transition_bar.Value / 100.0;
                if (m_moveSliderDownwards)
                    position = (100 - Slider_transition_bar.Value) / 100.0;

                m_mixEffectBlock1.SetTransitionPosition(position);
            }
        } //ok

        /// <summary>
        /// Used for putting other object types into combo boxes.
        /// </summary>
        struct StringObjectPair<T>
        {
            public string name;
            public T value;

            public StringObjectPair(string name, T value)
            {
                this.name = name;
                this.value = value;
            }

            public override string ToString()
            {
                return name;
            }
        }

        private void Button_Click_Program(object sender, RoutedEventArgs e)
        {
            Button program_Btn = sender as Button;
            string button_kind = program_Btn.Tag.ToString();

            long id;
            (FindInputByName(button_kind)).GetInputId(out id);
            SetProgramCurrentInput((int)id);
        }

        private void Button_Click_Preview(object sender, RoutedEventArgs e)
        {
            Button preview_Btn = sender as Button;
            string button_kind = preview_Btn.Tag.ToString();

            long id;
            (FindInputByName(button_kind)).GetInputId(out id);
            SetPreviewCurrentInput((int)id);
        }

        private IBMDSwitcherInput FindInputByName(string inputName)
        {
            IBMDSwitcherInputIterator inputIterator = null;
            IntPtr inputIteratorPtr;
            Guid inputIteratorIID = typeof(IBMDSwitcherInputIterator).GUID;
            m_switcher.CreateIterator(ref inputIteratorIID, out inputIteratorPtr);
            if (inputIteratorPtr != null)
            {
                inputIterator = (IBMDSwitcherInputIterator)Marshal.GetObjectForIUnknown(inputIteratorPtr);
            }

            if (inputIterator != null)
            {
                IBMDSwitcherInput input;
                inputIterator.Next(out input);
                while (input != null)
                {
                    string s = null;
                    input.GetLongName(out s);
                    if (s.ToLower() == inputName.ToLower()) return input;
                
                    inputIterator.Next(out input);
                }
            }
            return null;

        }


        #region transition function

        public int GetTransitionPattern() //현재 transitionpattern을 반환
        {
            int retVal = (int)_ATEM_TRAN_TYPE_.eATT_Mix;
            IBMDSwitcherTransitionParameters param = m_mixEffectBlock1 as IBMDSwitcherTransitionParameters;
            IBMDSwitcherTransitionWipeParameters wipeparam = (IBMDSwitcherTransitionWipeParameters)m_mixEffectBlock1;
            if (param != null)
            {
                _BMDSwitcherTransitionStyle mSTS;
                param.GetNextTransitionStyle(out mSTS);
                switch(mSTS)
                {
                    case _BMDSwitcherTransitionStyle.bmdSwitcherTransitionStyleMix:
                        {
                            retVal = (int)_ATEM_TRAN_TYPE_.eATT_Mix; break;
                        }
                    case _BMDSwitcherTransitionStyle.bmdSwitcherTransitionStyleDip:
                        {
                            retVal = 10; break;
                        }
                    case _BMDSwitcherTransitionStyle.bmdSwitcherTransitionStyleWipe:
                        {
                            if(wipeparam != null)
                            {
                                _BMDSwitcherPatternStyle mSPS;
                                wipeparam.GetPattern(out mSPS);

                                switch (mSPS)
                                {
                                    case _BMDSwitcherPatternStyle.bmdSwitcherPatternStyleLeftToRightBar: { retVal = (int)_ATEM_TRAN_TYPE_.eATT_LeftRight; break; }
                                    case _BMDSwitcherPatternStyle.bmdSwitcherPatternStyleTopToBottomBar: { retVal = (int)_ATEM_TRAN_TYPE_.eATT_UpDown; break; }
                                    case _BMDSwitcherPatternStyle.bmdSwitcherPatternStyleRectangleIris: { retVal = (int)_ATEM_TRAN_TYPE_.eATT_InOut; break; }
                                    case _BMDSwitcherPatternStyle.bmdSwitcherPatternStyleCornersInFourBox: { retVal = (int)_ATEM_TRAN_TYPE_.eATT_FourBox; break; }
                                    case _BMDSwitcherPatternStyle.bmdSwitcherPatternStyleHorizontalBarnDoor: { retVal = (int)_ATEM_TRAN_TYPE_.eATT_HoriBox; break; }
                                    case _BMDSwitcherPatternStyle.bmdSwitcherPatternStyleVerticalBarnDoor: { retVal = (int)_ATEM_TRAN_TYPE_.eATT_VertiBox; break; }
                                    case _BMDSwitcherPatternStyle.bmdSwitcherPatternStyleDiamondIris: { retVal = (int)_ATEM_TRAN_TYPE_.eATT_DiaIris; break; }
                                    case _BMDSwitcherPatternStyle.bmdSwitcherPatternStyleTopLeftDiagonal: { retVal = (int)_ATEM_TRAN_TYPE_.eATT_TopLeft; break; }
                                    case _BMDSwitcherPatternStyle.bmdSwitcherPatternStyleTopRightDiagonal: { retVal = (int)_ATEM_TRAN_TYPE_.eATT_TopRight; break; }
                                    
                                }
                            }
                            break;
                        }
                }
            }

            return retVal;
        }

        public void SetTransitionPattern(int PatternVal)
        {

            IBMDSwitcherTransitionParameters param = m_mixEffectBlock1 as IBMDSwitcherTransitionParameters;
            IBMDSwitcherTransitionWipeParameters wipeparam = (IBMDSwitcherTransitionWipeParameters)m_mixEffectBlock1;
            
            if ((PatternVal < 11)&&((int)_ATEM_TRAN_TYPE_.eATT_Mix <= PatternVal))
            {
                if (GetTransitionPattern() != PatternVal) //현재 패턴과 바꾸려는 패턴이 다를시
                {
                    if (PatternVal == (int)_ATEM_TRAN_TYPE_.eATT_Mix)
                    {
                        if (param != null)
                        {
                            param.SetNextTransitionStyle(_BMDSwitcherTransitionStyle.bmdSwitcherTransitionStyleMix);
                            
                        }
                    }
                    else
                    {
                        if (param != null)
                        {
                            param.SetNextTransitionStyle(_BMDSwitcherTransitionStyle.bmdSwitcherTransitionStyleWipe);
                            
                            switch (PatternVal)
                            {
                                case (int)_ATEM_TRAN_TYPE_.eATT_LeftRight: { wipeparam.SetPattern(_BMDSwitcherPatternStyle.bmdSwitcherPatternStyleLeftToRightBar); break; }
                                case (int)_ATEM_TRAN_TYPE_.eATT_UpDown: { wipeparam.SetPattern(_BMDSwitcherPatternStyle.bmdSwitcherPatternStyleTopToBottomBar); break; }
                                case (int)_ATEM_TRAN_TYPE_.eATT_InOut: { wipeparam.SetPattern(_BMDSwitcherPatternStyle.bmdSwitcherPatternStyleRectangleIris); break; }
                                case (int)_ATEM_TRAN_TYPE_.eATT_FourBox: { wipeparam.SetPattern(_BMDSwitcherPatternStyle.bmdSwitcherPatternStyleCornersInFourBox); break; }
                                case (int)_ATEM_TRAN_TYPE_.eATT_HoriBox: { wipeparam.SetPattern(_BMDSwitcherPatternStyle.bmdSwitcherPatternStyleHorizontalBarnDoor); break; }
                                case (int)_ATEM_TRAN_TYPE_.eATT_VertiBox: { wipeparam.SetPattern(_BMDSwitcherPatternStyle.bmdSwitcherPatternStyleVerticalBarnDoor); break; }
                                case (int)_ATEM_TRAN_TYPE_.eATT_DiaIris: { wipeparam.SetPattern(_BMDSwitcherPatternStyle.bmdSwitcherPatternStyleDiamondIris); break; }
                                case (int)_ATEM_TRAN_TYPE_.eATT_TopLeft: { wipeparam.SetPattern(_BMDSwitcherPatternStyle.bmdSwitcherPatternStyleTopLeftDiagonal); break; }
                                case (int)_ATEM_TRAN_TYPE_.eATT_TopRight: { wipeparam.SetPattern(_BMDSwitcherPatternStyle.bmdSwitcherPatternStyleTopRightDiagonal); break; }
                            }
                        }
                    }
                }
            }
        }

        private void Transition_Btn_Click(object sender, RoutedEventArgs e)
        {
            Button b = sender as Button;
            int tran_type = int.Parse(b.Tag.ToString());

            SetTransitionPattern(tran_type);
        }


        #endregion

        #region audio function


        int SetAudioInputGainByIndex(int idx, double gainVal)
        {
            int retVal = -1;

            if (m_audioInputiterator != null && m_audioInput != null)
            {
                m_audioInputiterator.GetById(idx, out m_audioInput); //null 오류
                if (gainVal == -60)
                {
                    gainVal = -21474836.48;
                }
                m_audioInput.SetGain(gainVal);


            }
            return retVal;
        } //이거다 
        int SetAudioInputBalanceByIndex(int idx, double LRVal)
        {
            int retVal = -1;

            if (m_audioInputiterator != null && m_audioInput != null)
            {
                m_audioInputiterator.GetById(idx, out m_audioInput); //null 오류

                m_audioInput.SetBalance(LRVal);


            }
            return retVal;
        } //이거다 


        private void LR_Audio_balance_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Slider slider = sender as Slider; 
            if (slider != null)
            {
                int idx = int.Parse(slider.Tag.ToString());
                double LR_val = double.Parse(slider.Value.ToString());


                SetAudioInputBalanceByIndex(idx, LR_val);
            }
        }
        private void Volume_Audio_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Slider slider = sender as Slider;
            if(slider != null)
            {
                int idx = int.Parse(slider.Tag.ToString());
                double gainval = double.Parse(slider.Value.ToString());
                SetAudioInputGainByIndex(idx, gainval);
            }
        }


        private void EXT_Btn_Click(object sender, RoutedEventArgs e)
        {
            //외부 음향 조절
        }

        private void External_Gain_Slider_Value_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Slider sld = sender as Slider;
            double gain = double.Parse(sld.Value.ToString());
            if (gain == -60)
            {
                gain = -21474836.48;
            }
            

            





            m_audioInput.SetGain(gain);
        }

        private void OUT_Btn_Click(object sender, RoutedEventArgs e)
        {
            //마스터 음향 조절

        }
        private void Master_Gain_Slider_Value_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Slider sld = sender as Slider;
            double gain = double.Parse(sld.Value.ToString());
            if (gain == -60)
            {
                gain = -21474836.48;
            }
            m_audioMixer.SetProgramOutGain(gain);
        }

        #endregion

        #region chroma key function

        public int InitKeyersData()
        {
            int retVal = 0;
            ResetKeyersData();

            if (m_switcher != null)
            {
                IBMDSwitcherKeyIterator pSwitcherKeyIterator = null;

                IntPtr pSwitcherKeyIteratorPtr;
                Guid iid = typeof(IBMDSwitcherKeyIterator).GUID;
                m_mixEffectBlock1.CreateIterator(ref iid, out pSwitcherKeyIteratorPtr);
                
                if(pSwitcherKeyIteratorPtr != null)
                {
                    pSwitcherKeyIterator = (IBMDSwitcherKeyIterator)Marshal.GetObjectForIUnknown(pSwitcherKeyIteratorPtr);
                }
                if (pSwitcherKeyIterator != null)
                {
                    IBMDSwitcherKey key;
                    pSwitcherKeyIterator.Next(out key);
                    if(key != null)
                    {
                        m_switcher_key = key;
                        m_chromaParameters = key as IBMDSwitcherKeyChromaParameters;
                        m_chromaParameters.AddCallback(m_chromaMonitor);
                        m_flyParameters = key as IBMDSwitcherKeyFlyParameters;
                        m_dVEParameters = key as IBMDSwitcherKeyDVEParameters;
                        long selecteditem; 
                        m_switcher_key.GetInputFill(out selecteditem);
                        chroma_key_combo.SelectedIndex = int.Parse(selecteditem.ToString());
                        Update_Chroma_Slider_Value();
                        Update_Chroma_Text_Value();

                        cw = new Chroma_Window();
                        cw.Show();
                        retVal = 1;
                    }
                }
            }


            return retVal;
        }  //ok

        public void ResetKeyersData()
        {
            if (m_chromaParameters != null)
            {
                m_chromaParameters = null;
            }

            if (m_flyParameters != null)
            {
                m_flyParameters = null;
            }

            if (m_dVEParameters != null)
            {
                m_dVEParameters = null;
            }

            m_switcher_key = null;
        }//ok

        int SetKeyersCharGenStatus(int mSts)
        {
            // mSts == 
            int retVal = -1;
            if (mSts == 0)
            {
                if (m_switcher_key != null)
                {
                    m_switcher_key.SetOnAir(0);
                    retVal = mSts;
                }
            }
            else if (mSts == 1)
            {
                if ((m_switcher_key != null) && (m_chromaParameters != null))
                {
                    m_switcher_key.SetType(_BMDSwitcherKeyType.bmdSwitcherKeyTypeChroma);
                    m_switcher_key.SetInputFill(4);//hardcoded 4
                    m_chromaParameters.SetHue(319.5);
                    m_chromaParameters.SetGain(0.55);
                    m_chromaParameters.SetYSuppress(0.43);
                    m_chromaParameters.SetLift(0);

                    m_switcher_key.SetOnAir(1);
                    //CGlobalMain::GetGMain()->DVEPIP_ShowWindow(FALSE);
                    retVal = mSts;
                }
            }
            else if (mSts == 2)
            {
                if ((m_switcher_key != null) && (m_flyParameters != null))
                {
                    m_switcher_key.SetType(_BMDSwitcherKeyType.bmdSwitcherKeyTypeDVE);
                    m_switcher_key.SetInputFill(4);
                    double sizex, sizey = 0;
                    m_flyParameters.GetSizeX(out sizex);
                    m_flyParameters.GetSizeY(out sizey);
                    m_flyParameters.SetSizeX(sizex);
                    m_flyParameters.SetSizeY(sizey);
                    double posix, posiy = 0;
                    m_flyParameters.GetPositionX(out posix);
                    m_flyParameters.GetPositionY(out posiy);

                    m_flyParameters.SetPositionX(posix);
                    m_flyParameters.SetPositionY(posiy);
                    m_dVEParameters.SetBorderEnabled(0);
                    m_switcher_key.SetOnAir(1);

                    //CGlobalMain::GetGMain()->CharGen_ShowWindow(FALSE);
                    retVal = mSts;
                }
            }
            return retVal;
        }

        int SetKeyersOnAir(int mSts)
        {
            int retVal = 0;

            if (m_switcher_key != null)
            {
                bool isOnAir = !(mSts == 0);
                if(isOnAir)
                {
                    m_switcher_key.SetOnAir(1);
                }
                else
                {
                    m_switcher_key.SetOnAir(0);
                }
                retVal = 1;
                
            }

            return retVal;
        }

        int GetKeyersOnAir()
        {
            int retVal = 0;

            if (m_switcher_key != null)
            {
                int isOnAir = 0;
                m_switcher_key.GetOnAir(out isOnAir);
                if (isOnAir == 1)
                {
                    retVal = 1;
                    
                }
            }

            return retVal;
        }


        private void Chroma_Hue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //Hue
            Slider s = sender as Slider;
            SetChromaHue(s.Value);
            Update_Chroma_Text_Value();


        } //ok

        private void Chroma_Gain_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //Gain
            Slider s = sender as Slider;
            SetChromaGain(s.Value);
            Update_Chroma_Text_Value();

        }//ok

        private void Chroma_YSup_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //Y Suppress
            Slider s = sender as Slider;
            SetChromaYSup(s.Value);
            Update_Chroma_Text_Value();

        }//ok

        private void Chroma_Lift_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //Lift
            Slider s = sender as Slider;
            SetChromaLift(s.Value);
            Update_Chroma_Text_Value();

        }//ok
        int SetChromaHue(double val)
        {
            int retVal = -1;

            if ((m_switcher_key != null) && (m_chromaParameters != null))
            {
                m_chromaParameters.SetHue(val);
                {
                    retVal = 0;
                }
            }

            return retVal;
        } //ok  (0 <= val <= 360)

        int SetChromaGain(double val)
        {
            int retVal = -1;

            if ((m_switcher_key != null) && (m_chromaParameters != null))
            {
                m_chromaParameters.SetGain(val);
                {
                    retVal = 0;
                }
            }

            return retVal;
        }//ok  (0 <= val <= 1)

        int SetChromaYSup(double val)
        {
            int retVal = -1;

            if ((m_switcher_key != null) && (m_chromaParameters != null))
            {
                m_chromaParameters.SetYSuppress(val);
                {
                    retVal = 0;
                }
            }

            return retVal;
        }//ok  (0 <= val <= 1)

        int SetChromaLift(double val)
        {
            int retVal = -1;

            if ((m_switcher_key != null) && (m_chromaParameters != null))
            {
                m_chromaParameters.SetLift(val);
                {
                    retVal = 0;
                }
            }

            return retVal;
        }//ok  (0 <= val <= 1)



        private void chroma_key_combo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //chroma input 변경
            long inputId;
            ComboBox cb = sender as ComboBox;
            (FindInputByName(cb.SelectedValue.ToString())).GetInputId(out inputId);
            m_switcher_key.SetInputFill(inputId);

        } //ok
        private void chroma_value_Auto_change(object sender, RoutedEventArgs e)
        {
            //색상을 클릭만 하면 자동으로 Hue, gain, YSup, Lift 값 변경.
            Button button = sender as Button;
            switch(int.Parse(button.Tag.ToString()))
            {
                case 1: //magenta
                    SetChromaHue(322);
                    SetChromaGain(0.57); 
                    SetChromaYSup(0.48); 
                    SetChromaLift(0.114);
                    Canvas_Chroma_preview.Background = Brushes.Magenta;
                    break;

                case 2: //green
                    SetChromaHue(142.5);
                    SetChromaGain(0.66);
                    SetChromaYSup(0.61);
                    SetChromaLift(0.089);
                    Canvas_Chroma_preview.Background = Brushes.Green;
                    break;

                case 3: //cyan
                    SetChromaHue(199);
                    SetChromaGain(0.66);
                    SetChromaYSup(0.61);
                    SetChromaLift(0.001);
                    Canvas_Chroma_preview.Background = Brushes.Cyan;
                    break;

            }
            Thread.Sleep(30);
            //update
            Update_Chroma_Slider_Value();
            Update_Chroma_Text_Value();
            Console.WriteLine("chroma_value_changed");
        } //ok
        private void Update_Chroma_Text_Value()
        {
            //현재 설정되어 있는 hue,gain,y-sup,lift 값을 가지고 와서 업데이트 함.
            double Hue_;
            double Gain_;
            double YSup_;
            double Lift_;

            m_chromaParameters.GetHue(out Hue_);
            m_chromaParameters.GetGain(out Gain_);
            m_chromaParameters.GetYSuppress(out YSup_);
            m_chromaParameters.GetLift(out Lift_);

            Dispatcher.Invoke(() =>
            {
                hueval.Text = Hue_.ToString();
                gainval.Text = (Gain_ * 100 + "%").ToString();
                ysupval.Text = (YSup_ * 100 + "%").ToString();
                liftval.Text = (Lift_ * 100 + "%").ToString();
            });

        } //ok
        private void Update_Chroma_Slider_Value()
        {
            //현재 설정되어 있는 hue,gain,y-sup,lift 값을 가지고 와서 업데이트 함.
            double Hue_;
            double Gain_;
            double YSup_;
            double Lift_;

            m_chromaParameters.GetHue(out Hue_);
            m_chromaParameters.GetGain(out Gain_);
            m_chromaParameters.GetYSuppress(out YSup_);
            m_chromaParameters.GetLift(out Lift_);

            hueslider.Value = Hue_;
            gainslider.Value = Gain_;
            ysupslider.Value = YSup_;
            liftslider.Value = Lift_;
            
            Dispatcher.Invoke(() =>
            {
             
                
            });
            Console.WriteLine("slider_value_chane");
        } //ok


        private void On_Air_Btn_Click(object sender, RoutedEventArgs e)
        {
            if (m_switcher_key != null)
            {
                int is_set_on_air;
                m_switcher_key.GetOnAir(out is_set_on_air);
                if (is_set_on_air == 0)
                {
                    m_switcher_key.SetOnAir(1);
                    on_air_Btn.Background = Brushes.Red;
                }
                else
                {
                    m_switcher_key.SetOnAir(0);
                    on_air_Btn.Background = Brushes.LightGray;
                }
            }
        } //ok

        private void Play_Btn_Click(object sender, RoutedEventArgs e)
        {
            //적용 버튼 인듯. 변경만,
            cw.Set_Text(Caption_main.Text);
            cw.Set_Font_Family(Caption_main.FontFamily);
            cw.Set_Font_Size(Caption_main.FontSize);
            cw.Set_Font_Color(Caption_main.Foreground);
            cw.Set_Background(Canvas_Chroma_preview.Background);
            cw.Set_VerticalAlignment(Caption_main.VerticalContentAlignment);
        } //ok

        private void TextBlock_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            //자막 내용이 바뀔때마다.
        }

        private void text_location_Btn_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            switch (button.Tag)
            {
                case "1": Caption_main.VerticalContentAlignment = VerticalAlignment.Top; break;//상단
                case "2": Caption_main.VerticalContentAlignment = VerticalAlignment.Center; break;//가운데
                case "3": Caption_main.VerticalContentAlignment = VerticalAlignment.Bottom; break;//하단 
            }
        }

        private void Font_Family_Change_Btn_Click(object sender, RoutedEventArgs e)
        {
            Button b = sender as Button;
            Caption_main.FontFamily = b.FontFamily;
        }

        private void Size_Change_Btn_Click(object sender, RoutedEventArgs e)
        {
            Button b = sender as Button;
            fsize_1.Background = fsize_2.Background = fsize_3.Background = fsize_4.Background = fsize_5.Background = Brushes.LightGray;
            switch (b.Tag)
            {
                case "1": Caption_main.FontSize = 30; fsize_1.Background = Brushes.LightGreen; break;
                case "2": Caption_main.FontSize = 40; fsize_2.Background = Brushes.LightGreen; break;
                case "3": Caption_main.FontSize = 50; fsize_3.Background = Brushes.LightGreen; break;
                case "4": Caption_main.FontSize = 60; fsize_4.Background = Brushes.LightGreen; break;
                case "5": Caption_main.FontSize = 70; fsize_5.Background = Brushes.LightGreen; break;

            }
        }

        private void Color_Change_Btn_Click(object sender, RoutedEventArgs e)
        {
            //글씨 색 바꾸는 것.
            Button b = sender as Button;
            Brush br = b.Background;//현재 색을 가지고 옴.
            Caption_main.Foreground = br;

        }

        private void Text_Flow_toggle_Btn_Click(object sender, RoutedEventArgs e)
        {
            //미구현
        }

        private void combo_screen_index_selector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //표시할 디스플레이 선택 변화
            ComboBox cb = sender as ComboBox;
            if(cw != null)
            {
                
                cw.Set_Screen_Index(int.Parse(cb.SelectedValue.ToString()));
                cw.Show_window_at_Screen_Index();
            }
        }

        private void Chroma_Hue_Changed_Callback ()
        {
            //스위쳐에서 Hue값이 변경되면 실행됨.
            double hue_;
            m_chromaParameters.GetHue(out hue_);

        }
        #endregion

    }
}
