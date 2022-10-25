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

//ppt 사용을 위해 추가
using Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace Jmon_Switcher
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        private IBMDSwitcherDiscovery m_switcherDiscovery;          //ATEM 연결을 위해 장비 찾는 것.

        private IBMDSwitcher m_switcher;                            //ATEM 스위쳐 장비 그 자체.
        private IBMDSwitcherMixEffectBlock m_mixEffectBlock;        //ATEM 화면 입,출력 + 화면전환.
        private IBMDSwitcherKey m_switcher_key;                     //ATEM 크로마키 담당.
        private IBMDSwitcherKeyChromaParameters m_chromaParameters; //ATEM 크로마키에서 Hue,Gain 등 파라미터 담당.
        private IBMDSwitcherAudioMixer m_audioMixer;                //오디오 믹서 
        private IBMDSwitcherAudioInput m_audioInput;                //오디오 Input gain, balance - cam
        private IBMDSwitcherAudioMonitorOutput m_audioMonitorOutput;//오디오 Output  

        private SwitcherMonitor m_switcherMonitor;
        private MixEffectBlockMonitor m_mixEffectBlockMonitor;
        private SwitcherKeyMonitor m_switcherKeyMonitor;
        private ChromaParametersMonitor m_chromaParametersMonitor;
        private AudioMixerMonitor m_audioMixerMonitor;
        private AudioInputMonitor m_audioinputMonitor;
        private AudioMixerMonitorOutputMonitor m_audioOutputMonitor;

        private List<InputMonitor> m_inputMonitors = new List<InputMonitor>();  //Callback을 관리함.

        private IBMDSwitcherInputIterator inputIterator = null;
        private IBMDSwitcherMixEffectBlockIterator meIterator = null;
        private IBMDSwitcherAudioInputIterator m_audioInputiterator = null;
        private IBMDSwitcherAudioMonitorOutputIterator m_audioOutputIterator = null;

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
        Chroma_Window cw = new Chroma_Window(); //크로마키 보여줄 창 
        Save_Settings ss = new Save_Settings(); //설정 저장
        PPT ppt = PPT.Instance;                 //ppt 클래스(singleton)
        private bool m_moveSliderDownwards = false;
        private bool m_currentTransitionReachedHalfway = false;


        public MainWindow()
        {
            InitializeComponent();
            ss.set_Path(System.IO.Directory.GetCurrentDirectory() + @"JHD3000_settings");
            //Callback 함수 구현부
            m_switcherMonitor = new SwitcherMonitor();
            m_switcherMonitor.SwitcherDisconnected += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => SwitcherDisconnected())));

            m_mixEffectBlockMonitor = new MixEffectBlockMonitor();
            m_mixEffectBlockMonitor.ProgramInputChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => UpdateProgramButtonSelection())));
            m_mixEffectBlockMonitor.PreviewInputChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => UpdatePreviewButtonSelection())));
            m_mixEffectBlockMonitor.TransitionPositionChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => UpdateSliderPosition())));
            m_mixEffectBlockMonitor.InTransitionChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => OnInTransitionChanged())));

            m_chromaParametersMonitor = new ChromaParametersMonitor();

            m_chromaParametersMonitor.ChromaHueChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => Chroma_Hue_Changed_Callback())));  //크로마키 Hue 가 변경되면,
            m_chromaParametersMonitor.ChromaGainChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => Chroma_Gain_Changed_Callback())));//크로마키 Gain 이 변경되면,
            m_chromaParametersMonitor.ChromaYsupChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => Chroma_Ysup_Changed_Callback())));//크로마키 Ysup 가 변경되면,
            m_chromaParametersMonitor.ChromaLiftChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => Chroma_Lift_Changed_Callback())));//크로마키 Lift 가 변경되면

            m_audioMixerMonitor = new AudioMixerMonitor();
            m_audioMixerMonitor.ProgramOutBalanceChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => Update_AudioProgramOutBalance_Callback()))); //오디오 출력 balance 변경시,
            m_audioMixerMonitor.ProgramOutGainChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => Update_AudioProgramOutGain_Callback())));      //오디오 출력 gain 변경시,

            m_audioinputMonitor = new AudioInputMonitor();
            m_audioinputMonitor.BalanceChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => Update_Audio_Input_Balance_Callback())));  //오디오 입력 balance 변경시,
            m_audioinputMonitor.GainChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => Update_Audio_Input_Gain_Callback())));     //오디오 입력 gain 변경시,

            m_audioOutputMonitor = new AudioMixerMonitorOutputMonitor();
            m_audioOutputMonitor.DimChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => AudioOutputDimChanged_Callback())));
            m_audioOutputMonitor.DimLevelChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => AudioOutputDimLevelChanged_Callback())));
            m_audioOutputMonitor.GainChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => AudioOutputGainChanged_Callback())));
            m_audioOutputMonitor.LevelNotificationChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => AudioOutputLevelNotificationChanged_Callback())));
            m_audioOutputMonitor.MonitorEnableChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => AudioOutputEnableChanged_Callback())));
            m_audioOutputMonitor.MuteChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => AudioOutputMuteChanged_Callback())));
            m_audioOutputMonitor.SoloChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => AudioOutputSoloChanged_Callback())));
            m_audioOutputMonitor.SoloInputChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => AudioOutputSoloInputChanged_Callback())));

            //ATEM 스위치 연결
            m_switcherDiscovery = new CBMDSwitcherDiscovery();
            if (m_switcherDiscovery == null)
            {
                MessageBox.Show("Could not create Switcher Discovery Instance.\nATEM Switcher Software may not be installed.", "Error");
                Environment.Exit(1);
            }
            Load_Save_File();
        }
        private void Load_Save_File()
        {
            //저장된 설정을 불러오기
            if (ss.is_SaveFile_Exist())
            {
                //저장된 파일이 있으면,
                IP_Textbox.Text = ss.LOAD(0);
                combo_screen_index_selector.SelectedIndex = ss.LOAD(1);
            }
            else
            {
                MessageBox.Show("저장된 설정 파일이 없습니다.\nSwitcher IP를 입력 후 연결 버튼을 누르시오.", "주의", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Shutdown_Click(object sender, RoutedEventArgs e)
        {
            ss.SAVE(IP_Textbox.Text, combo_screen_index_selector.SelectedIndex);
            this.Close();
            cw.Close();
        } //시스템 끄기(저장을 곁들인)

        private void IP_connect_Click(object sender, RoutedEventArgs e)
        {
            SwitcherDisconnected();		// start with switcher disconnected
            Connect_Switcher();         // auto connect to switcher
        }
        private void SwitcherDisconnected()
        {
            textBoxSwitcherName.Content = "";

            UI_SetEnable(false);

            // Remove all input monitors, remove callbacks
            foreach (InputMonitor inputMon in m_inputMonitors)
            {
                inputMon.Input.RemoveCallback(inputMon);
                inputMon.LongNameChanged -= new SwitcherEventHandler(OnInputLongNameChanged);
            }
            m_inputMonitors.Clear();

            if (m_switcher != null)
            {
                // Remove callback:
                m_switcher.RemoveCallback(m_switcherMonitor);

                // release reference:
                m_switcher = null;
            }

            if (m_mixEffectBlock != null)
            {
                // Remove callback
                m_mixEffectBlock.RemoveCallback(m_mixEffectBlockMonitor);

                // Release reference
                m_mixEffectBlock = null;
            }

            if (m_chromaParameters != null)
            {
                // Remove callback:
                m_chromaParameters.RemoveCallback(m_chromaParametersMonitor);

                // release reference:
                m_chromaParameters = null;
            }
        }
        private void Connect_Switcher()
        {
            _BMDSwitcherConnectToFailure failReason = 0;
            string address = IP_Textbox.Text;

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
            MessageBox.Show("성공적으로 연결되었습니다.");
            // Get the switcher name:
            //string switcherName;
            //m_switcher.GetProductName(out switcherName);
            //textBoxSwitcherName.Content = switcherName;
            
            // Install SwitcherMonitor callbacks:
            m_switcher.AddCallback(m_switcherMonitor);

            // We create input monitors for each input. To do this we iterate over all inputs:
            // This will allow us to update the combo boxes when input names change:


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

            IntPtr meIteratorPtr;
            Guid meIteratorIID = typeof(IBMDSwitcherMixEffectBlockIterator).GUID;
            m_switcher.CreateIterator(ref meIteratorIID, out meIteratorPtr);
            if (meIteratorPtr != null)
            {
                meIterator = (IBMDSwitcherMixEffectBlockIterator)Marshal.GetObjectForIUnknown(meIteratorPtr);
            }


            if (meIterator != null)
            {
                meIterator.Next(out m_mixEffectBlock);
                
            }

            if (m_mixEffectBlock == null)
            {
                MessageBox.Show("Unexpected: Could not get first mix effect block", "Error");
                return;
            }

            // Install MixEffectBlockMonitor callbacks:
            m_mixEffectBlock.AddCallback(m_mixEffectBlockMonitor);



            //Audio Input iterator +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=

            m_audioMixer = (IBMDSwitcherAudioMixer)m_switcher;
            m_audioMixer.AddCallback(m_audioMixerMonitor);

            IntPtr AinIteratorPtr;
            Guid AinIteratorIID = typeof(IBMDSwitcherAudioInputIterator).GUID;
            m_audioMixer.CreateIterator(ref AinIteratorIID, out AinIteratorPtr);
            if (AinIteratorPtr != null)
            {
                m_audioInputiterator = (IBMDSwitcherAudioInputIterator)Marshal.GetObjectForIUnknown(AinIteratorPtr);                
            }

            if (m_audioInputiterator != null)
            {
                m_audioInputiterator.Next(out m_audioInput);
            }

            if (m_audioInputiterator == null)
            {
                MessageBox.Show("Unexpected: Could not get first audio input", "Error");
                return;
            }

            m_audioInput.AddCallback(m_audioinputMonitor);

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



            InitKeyersData();
            UI_SetEnable(true);      //스위치에 연결되면, UI를 사용할 수 있도록 enable 해주는 함수.
            Update_UI_From_ATEM_Switcher();
        }

        private void OnInputLongNameChanged(object sender, object args)
        {
            //입력 비디오 source가 바뀌면 
            this.Dispatcher.Invoke((Action)(() =>
            {
                //update items.
                Update_UI_From_ATEM_Switcher();

            }));
        }



        private void UI_SetEnable(bool enable)
        {
            Video_Grid.IsEnabled = enable;
            Transition_Grid.IsEnabled = enable;
            Chroma_Grid.IsEnabled = enable;
            Chroma_Grid_.IsEnabled = enable;
            IP_Textbox.IsEnabled = false;
            IP_connect.IsEnabled = false;
        }

        private void Update_UI_From_ATEM_Switcher()
        {
            //UI의 모든것(keyers 빼고)을 업데이트 하는 것.

            UpdateSliderPosition();
            UpdateProgramButtonSelection();
            UpdatePreviewButtonSelection();
            //오디오 업데이트 추가해야함.
        }
        private void UpdateProgramButtonSelection()
        {
            //프로그램 버튼 
            long programId;

            m_mixEffectBlock.GetProgramInput(out programId);
            
            // Select the program popup entry that matches the input id:

            //선택된 item의 버튼 색 변경
            Black_program.Background = Brushes.LightGray;
            prog_Btn_1.Background = Brushes.LightGray;
            prog_Btn_2.Background = Brushes.LightGray;
            prog_Btn_3.Background = Brushes.LightGray;
            prog_Btn_4.Background = Brushes.LightGray;
            prog_Btn_5.Background = Brushes.LightGray;
            prog_Btn_6.Background = Brushes.LightGray;
            prog_Btn_7.Background = Brushes.LightGray;
            prog_Btn_8.Background = Brushes.LightGray;
            Bar_program.Background = Brushes.LightGray;
            Color_program_1.Background = Brushes.LightGray;
            Color_program_2.Background = Brushes.LightGray;
            Media_program_1.Background = Brushes.LightGray;
            Media_program_2.Background = Brushes.LightGray;

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
                case 1000: Bar_program.Background = Brushes.Red; break;
                case 2001: Color_program_1.Background = Brushes.Red; break;
                case 2002: Color_program_2.Background = Brushes.Red; break;
                case 3010: Media_program_1.Background = Brushes.Red; break;
                case 3020: Media_program_2.Background = Brushes.Red; break;

            }
        }
        private void UpdatePreviewButtonSelection()
        {
            long previewId;

            m_mixEffectBlock.GetPreviewInput(out previewId);

            // Select the program popup entry that matches the input id:

            //선택된 item의 버튼 색 변경

            //선택된 item의 버튼 색 변경
            Black_preview.Background = Brushes.LightGray;
            prev_Btn_1.Background = Brushes.LightGray;
            prev_Btn_2.Background = Brushes.LightGray;
            prev_Btn_3.Background = Brushes.LightGray;
            prev_Btn_4.Background = Brushes.LightGray;
            prev_Btn_5.Background = Brushes.LightGray;
            prev_Btn_6.Background = Brushes.LightGray;
            prev_Btn_7.Background = Brushes.LightGray;
            prev_Btn_8.Background = Brushes.LightGray;
            Bar_preview.Background = Brushes.LightGray;
            Color_preview_1.Background = Brushes.LightGray;
            Color_preview_2.Background = Brushes.LightGray;
            Media_preview_1.Background = Brushes.LightGray;
            Media_preview_2.Background = Brushes.LightGray;


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
                case 1000: Bar_preview.Background = Brushes.Red; break;
                case 2001: Color_preview_1.Background = Brushes.Red; break;
                case 2002: Color_preview_2.Background = Brushes.Red; break;
                case 3010: Media_preview_1.Background = Brushes.Red; break;
                case 3020: Media_preview_2.Background = Brushes.Red; break;

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
            IBMDSwitcherTransitionParameters param = m_mixEffectBlock as IBMDSwitcherTransitionParameters;
            IBMDSwitcherTransitionWipeParameters wipeparam = (IBMDSwitcherTransitionWipeParameters)m_mixEffectBlock;
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

            IBMDSwitcherTransitionParameters param = m_mixEffectBlock as IBMDSwitcherTransitionParameters;
            IBMDSwitcherTransitionWipeParameters wipeparam = (IBMDSwitcherTransitionWipeParameters)m_mixEffectBlock;
            
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
        private void UpdateSliderPosition()
        {
            double transitionPos;

            m_mixEffectBlock.GetTransitionPosition(out transitionPos);

            m_currentTransitionReachedHalfway = (transitionPos >= 0.50);

            if (m_moveSliderDownwards)
                Slider_transition_bar.Value = 100 - (int)(transitionPos * 100);
            else
                Slider_transition_bar.Value = (int)(transitionPos * 100);
        } //ok
        private void OnInTransitionChanged()
        {
            int inTransition;

            m_mixEffectBlock.GetInTransition(out inTransition);

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

            if (m_mixEffectBlock != null)
            {
                m_mixEffectBlock.SetProgramInput(inputId);
            }
        }
        private void SetPreviewCurrentInput(int number)
        {
            long inputId = number;

            if (m_mixEffectBlock != null)
            {
                m_mixEffectBlock.SetPreviewInput(inputId);
            }
        }
        private void buttonAuto_Click(object sender, EventArgs e)
        {
            if (m_mixEffectBlock != null)
            {
                m_mixEffectBlock.PerformAutoTransition();
            }
        } //ok
        private void buttonCut_Click(object sender, EventArgs e)
        {
            if (m_mixEffectBlock != null)
            {
                m_mixEffectBlock.PerformCut();
            }
        } //ok
        private void Slider_transition_bar_Scroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
        {
            if (m_mixEffectBlock != null)
            {
                double position = Slider_transition_bar.Value / 100.0;
                if (m_moveSliderDownwards)
                    position = (100 - Slider_transition_bar.Value) / 100.0;

                m_mixEffectBlock.SetTransitionPosition(position);
            }
        } //ok

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

        //callback
        private void Update_AudioProgramOutBalance_Callback()
        {
            //미구현
        } //ok
        private void Update_AudioProgramOutGain_Callback()
        {
            double out_gain;
            m_audioMixer.GetProgramOutGain(out out_gain);
            Volume_Audio_OUT.Value = out_gain;
        } //ok
        private void Update_Audio_Input_Gain_Callback()
        {
            long idx;
            IBMDSwitcherAudioInput input;
            double gain;

            m_audioInput.GetAudioInputId(out idx);
            m_audioInputiterator.GetById(idx, out input);
            m_audioInput.GetGain(out gain);
        }
        private void Update_Audio_Input_Balance_Callback()
        {
        }
        private void AudioOutputDimChanged_Callback()
        {
            Console.WriteLine("dd");
        }
        private void AudioOutputDimLevelChanged_Callback()
        {
            Console.WriteLine("dd");
        }
        private void AudioOutputGainChanged_Callback()
        {
            Console.WriteLine("dd");
        }
        private void AudioOutputLevelNotificationChanged_Callback()
        {
            Console.WriteLine("dd");
        }
        private void AudioOutputEnableChanged_Callback()
        {
            Console.WriteLine("dd");
        }
        private void AudioOutputMuteChanged_Callback()
        {
            Console.WriteLine("dd");
        }
        private void AudioOutputSoloChanged_Callback()
        {
            Console.WriteLine("dd");
        }
        private void AudioOutputSoloInputChanged_Callback()
        {
            Console.WriteLine("dd");
        }           

        #endregion

        #region chroma key function

        public void ResetKeyersData()
        {
            if (m_chromaParameters != null)
            {
                m_chromaParameters = null;
            }

            m_switcher_key = null;
        }//ok
        public int InitKeyersData()
        {
            int retVal = 0;
            ResetKeyersData();

            if (m_switcher != null)
            {
                IBMDSwitcherKeyIterator pSwitcherKeyIterator = null;

                IntPtr pSwitcherKeyIteratorPtr;
                Guid iid = typeof(IBMDSwitcherKeyIterator).GUID;
                m_mixEffectBlock.CreateIterator(ref iid, out pSwitcherKeyIteratorPtr);
                
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
                        m_switcherKeyMonitor = new SwitcherKeyMonitor();
                        m_switcher_key.AddCallback(m_switcherKeyMonitor);
                        m_switcherKeyMonitor.KeyOnAirChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => Key_OnAirChanged_Callback())));
                        m_switcherKeyMonitor.KeyInputFillChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => Key_InputFillChanged_Callback())));

                        m_switcher_key.SetType(_BMDSwitcherKeyType.bmdSwitcherKeyTypeChroma);
                        m_chromaParameters = key as IBMDSwitcherKeyChromaParameters;
                        m_chromaParameters.AddCallback(m_chromaParametersMonitor);

                        retVal = 1;
                    }
                }
            }
            if(retVal == 1)
            {
                //초기화 성공시
                Update_Chroma_source_combobox();
                Update_Chroma_Input_source();
                Update_Chroma_Text_Value();
                Update_Chroma_Slider_Value();
                Update_Chroma_OnAir_Value();
                Show_Chroma_output_source();
            }

            return retVal;
        }  //ok
        private void Update_Chroma_source_combobox()
        {
            // Clear the combo boxes:
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
                long inputId;
                string inputName;
                input.GetInputId(out inputId);
                input.GetLongName(out inputName);

                // Add items to list:
                chroma_key_combo.Items.Add(new StringObjectPair<long>(inputName, inputId));

                inputIterator.Next(out input);
            }

        } //ok
        private void Update_Chroma_Input_source()
        {
            long selecteditem;
            m_switcher_key.GetInputFill(out selecteditem);
            chroma_key_combo.SelectedIndex = int.Parse(selecteditem.ToString());
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

            hueval.Text = Hue_.ToString();
            gainval.Text = (Gain_ * 100 + "%").ToString();
            ysupval.Text = (YSup_ * 100 + "%").ToString();
            liftval.Text = (Lift_ * 100 + "%").ToString();

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

        } //ok
        private void Update_Chroma_OnAir_Value()
        {
            if (m_switcher_key != null)
            {
                int is_set_on_air;
                m_switcher_key.GetOnAir(out is_set_on_air);
                if (is_set_on_air == 0)
                {
                    on_air_Btn.Background = Brushes.LightGray;
                }
                else
                {
                    on_air_Btn.Background = Brushes.Red;
                }
            }
        } //ok
        private void Show_Chroma_output_source()
        {
            //기본 설정(적용 버튼을 누르면 이곳에 표시됩니다.)으로 크로마 윈도우 표시. 
            cw.Set_Screen_Index(combo_screen_index_selector.SelectedIndex);
            cw.Show();
        } //ok 

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
                    SetChromaHue(326);
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
                    m_switcher_key.SetType(_BMDSwitcherKeyType.bmdSwitcherKeyTypeChroma);
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
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox tb = sender as TextBox;
            Caption_main.Text = tb.Text;
        } //ok
        private void text_location_Btn_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            switch (button.Tag)
            {
                case "1": Caption_main.VerticalContentAlignment = VerticalAlignment.Top; break;//상단
                case "2": Caption_main.VerticalContentAlignment = VerticalAlignment.Center; break;//가운데
                case "3": Caption_main.VerticalContentAlignment = VerticalAlignment.Bottom; break;//하단 
            }
        } //ok
        private void Font_Family_Change_Btn_Click(object sender, RoutedEventArgs e)
        {
            Button b = sender as Button;

            f1.Background = Brushes.LightGray;
            f2.Background = Brushes.LightGray;
            f3.Background = Brushes.LightGray;
            f4.Background = Brushes.LightGray;
            f5.Background = Brushes.LightGray;
            f6.Background = Brushes.LightGray;

            
            switch (b.Tag)
            {
                case "1": Caption_main.FontFamily = b.FontFamily; f1.Background = Brushes.LightGreen; break;
                case "2": Caption_main.FontFamily = b.FontFamily; f2.Background = Brushes.LightGreen; break;
                case "3": Caption_main.FontFamily = b.FontFamily; f3.Background = Brushes.LightGreen; break;
                case "4": Caption_main.FontFamily = b.FontFamily; f4.Background = Brushes.LightGreen; break;
                case "5": Caption_main.FontFamily = b.FontFamily; f5.Background = Brushes.LightGreen; break;
                case "6": Caption_main.FontFamily = b.FontFamily; f6.Background = Brushes.LightGreen; break;


            }
        } //ok
        private void Size_Change_Btn_Click(object sender, RoutedEventArgs e)
        {
            Button b = sender as Button;
            fsize_1.Background = Brushes.LightGray;
            fsize_2.Background = Brushes.LightGray;
            fsize_3.Background = Brushes.LightGray;
            fsize_4.Background = Brushes.LightGray;
            fsize_5.Background = Brushes.LightGray;
            fsize_6.Background = Brushes.LightGray;

            switch (b.Tag)
            {
                case "1": Caption_main.FontSize = 30; fsize_1.Background = Brushes.LightGreen; break;
                case "2": Caption_main.FontSize = 40; fsize_2.Background = Brushes.LightGreen; break;
                case "3": Caption_main.FontSize = 50; fsize_3.Background = Brushes.LightGreen; break;
                case "4": Caption_main.FontSize = 60; fsize_4.Background = Brushes.LightGreen; break;
                case "5": Caption_main.FontSize = 70; fsize_5.Background = Brushes.LightGreen; break;
                case "6": Caption_main.FontSize = 80; fsize_6.Background = Brushes.LightGreen; break;


            }
        } //ok
        private void Color_Change_Btn_Click(object sender, RoutedEventArgs e)
        {
            //글씨 색 바꾸는 것.
            Button b = sender as Button;
            Brush br = b.Background;//현재 색을 가지고 옴.
            Caption_main.Foreground = br;

        } //ok
        private void combo_screen_index_selector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //표시할 디스플레이 선택 변화
            ComboBox cb = sender as ComboBox;
            if(cw != null)
            {
                cw.Set_Screen_Index(int.Parse(cb.SelectedValue.ToString()));
                cw.Show_window_at_Screen_Index();
            }
            if(ppt != null)
            {
                ppt.Set_PPT_Screen_Index(int.Parse(cb.SelectedValue.ToString())); //변수 넣어주고
                ppt.Show_ppt_at_Screen_Index(); //실행
            }
        } //ok

        //callback
        private void Chroma_Hue_Changed_Callback()
        {
            //스위쳐에서 Hue값이 변경되면 실행됨.
            double hue_;
            m_chromaParameters.GetHue(out hue_);
            hueslider.Value = hue_;

        } //ok
        private void Chroma_Gain_Changed_Callback()
        {
            //스위쳐에서 Gain값이 변경되면 실행됨.
            double gain_;
            m_chromaParameters.GetGain(out gain_);
            gainslider.Value = gain_;

        } //ok
        private void Chroma_Ysup_Changed_Callback()
        {
            //스위쳐에서 Ysup값이 변경되면 실행됨.
            double ysup_;
            m_chromaParameters.GetYSuppress(out ysup_);
            ysupslider.Value = ysup_;

        } //ok
        private void Chroma_Lift_Changed_Callback()
        {
            //스위쳐에서 Lift값이 변경되면 실행됨.
            double lift_;
            m_chromaParameters.GetLift(out lift_);
            liftslider.Value = lift_;

        } //ok
        private void Key_OnAirChanged_Callback()
        {
            //추가 해야함.
            if (m_switcher_key != null)
            {
                int is_set_on_air;
                m_switcher_key.GetOnAir(out is_set_on_air);
                if (is_set_on_air == 0)
                {
                    on_air_Btn.Background = Brushes.LightGray;
                }
                else
                {
                    on_air_Btn.Background = Brushes.Red;
                }
            }
        }  //ok
        private void Key_InputFillChanged_Callback()
        {
            Update_Chroma_Input_source();
        } //ok

        #endregion

        #region PowerPoint

        private void Activate_PPT_Mode(object sender, RoutedEventArgs e)
        {
            //ppt 모드 활성화
            if (ppt.Load_File()) //성공시
            {
                ppt.Show();
                cw.Visibility = Visibility.Hidden;
                PPT_Grid.Visibility = Visibility.Visible;
                ppt_mode_Btn.Background = Brushes.Bisque;
                ppt_mode_Btn.IsEnabled = false;
            }
        }

        private void PPT_next_Click(object sender, RoutedEventArgs e)
        {
            ppt.Next();
        }

        private void PPT_prev_Click(object sender, RoutedEventArgs e)
        {
            ppt.Prev();
        }

        private void PPT_add_slide_Click(object sender, RoutedEventArgs e)
        {
            ppt.Test_addNewPage();
        }
        private void PPT_exit_Click(object sender, RoutedEventArgs e)
        {
            ppt.Close();
            cw.Visibility = Visibility.Visible;
            PPT_Grid.Visibility = Visibility.Hidden;
            ppt_mode_Btn.Background = Brushes.DimGray;
            ppt_mode_Btn.IsEnabled = true;
        }

        #endregion
    }
}
