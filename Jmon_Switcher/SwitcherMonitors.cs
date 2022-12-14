/* -LICENSE-START-
** Copyright (c) 2011 Blackmagic Design
**
** Permission is hereby granted, free of charge, to any person or organization
** obtaining a copy of the software and accompanying documentation covered by
** this license (the "Software") to use, reproduce, display, distribute,
** execute, and transmit the Software, and to prepare derivative works of the
** Software, and to permit third-parties to whom the Software is furnished to
** do so, all subject to the following:
** 
** The copyright notices in the Software and this entire statement, including
** the above license grant, this restriction and the following disclaimer,
** must be included in all copies of the Software, in whole or in part, and
** all derivative works of the Software, unless such copies or derivative
** works are solely in the form of machine-executable object code generated by
** a source language processor.
** 
** THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
** IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
** FITNESS FOR A PARTICULAR PURPOSE, TITLE AND NON-INFRINGEMENT. IN NO EVENT
** SHALL THE COPYRIGHT HOLDERS OR ANYONE DISTRIBUTING THE SOFTWARE BE LIABLE
** FOR ANY DAMAGES OR OTHER LIABILITY, WHETHER IN CONTRACT, TORT OR OTHERWISE,
** ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
** DEALINGS IN THE SOFTWARE.
** -LICENSE-END-
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BMDSwitcherAPI;

namespace Jmon_Switcher
{

    public delegate void SwitcherEventHandler(object sender, object args);

    class SwitcherMonitor : IBMDSwitcherCallback
    {
        // Events:
        public event SwitcherEventHandler SwitcherDisconnected;

        public SwitcherMonitor()
        {
        }

        void IBMDSwitcherCallback.Notify(_BMDSwitcherEventType eventType, _BMDSwitcherVideoMode coreVideoMode)
        {
            if (eventType == _BMDSwitcherEventType.bmdSwitcherEventTypeDisconnected)
            {
                if (SwitcherDisconnected != null)
                    SwitcherDisconnected(this, null);
            }
        }
    }

    class MixEffectBlockMonitor : IBMDSwitcherMixEffectBlockCallback
    {
        // Events:
        public event SwitcherEventHandler ProgramInputChanged;
        public event SwitcherEventHandler PreviewInputChanged;
        public event SwitcherEventHandler TransitionPositionChanged;
        public event SwitcherEventHandler InTransitionChanged;

        public MixEffectBlockMonitor()
        {

        }

        void IBMDSwitcherMixEffectBlockCallback.Notify(_BMDSwitcherMixEffectBlockEventType eventType)
        {
            switch (eventType)
            {
                case _BMDSwitcherMixEffectBlockEventType.bmdSwitcherMixEffectBlockEventTypeProgramInputChanged:
                    if (ProgramInputChanged != null)
                        ProgramInputChanged(this, null);
                    break;
                case _BMDSwitcherMixEffectBlockEventType.bmdSwitcherMixEffectBlockEventTypePreviewInputChanged:
                    if (PreviewInputChanged != null)
                        PreviewInputChanged(this, null);
                    break;
                case _BMDSwitcherMixEffectBlockEventType.bmdSwitcherMixEffectBlockEventTypeTransitionPositionChanged:
                    if (TransitionPositionChanged != null)
                        TransitionPositionChanged(this, null);
                    break;
                case _BMDSwitcherMixEffectBlockEventType.bmdSwitcherMixEffectBlockEventTypeInTransitionChanged:
                    if (InTransitionChanged != null)
                        InTransitionChanged(this, null);
                    break;
            }
        }

    }

    class InputMonitor : IBMDSwitcherInputCallback
    {
        // Events:
        public event SwitcherEventHandler LongNameChanged;

        private IBMDSwitcherInput m_input;
        public IBMDSwitcherInput Input { get { return m_input; } }

        public InputMonitor(IBMDSwitcherInput input)
        {
            m_input = input;
        }

        void IBMDSwitcherInputCallback.Notify(_BMDSwitcherInputEventType eventType)
        {
            switch (eventType)
            {
                case _BMDSwitcherInputEventType.bmdSwitcherInputEventTypeLongNameChanged:
                    if (LongNameChanged != null)
                        LongNameChanged(this, null);
                    break;
            }
        }
    }

    class ChromaParametersMonitor : IBMDSwitcherKeyChromaParametersCallback
    {
        public event SwitcherEventHandler ChromaHueChanged;
        public event SwitcherEventHandler ChromaGainChanged;
        public event SwitcherEventHandler ChromaYsupChanged;
        public event SwitcherEventHandler ChromaLiftChanged;

        public ChromaParametersMonitor()
        {

        }

        void IBMDSwitcherKeyChromaParametersCallback.Notify(_BMDSwitcherKeyChromaParametersEventType eventType)
        {
            switch(eventType)
            {
                case _BMDSwitcherKeyChromaParametersEventType.bmdSwitcherKeyChromaParametersEventTypeHueChanged:
                    if (ChromaHueChanged != null)
                        ChromaHueChanged(this, null);
                    break;
                case _BMDSwitcherKeyChromaParametersEventType.bmdSwitcherKeyChromaParametersEventTypeGainChanged:
                    if (ChromaGainChanged != null)
                        ChromaGainChanged(this, null);
                    break;
                case _BMDSwitcherKeyChromaParametersEventType.bmdSwitcherKeyChromaParametersEventTypeYSuppressChanged:
                    if (ChromaYsupChanged != null)
                        ChromaYsupChanged(this, null);
                    break;
                case _BMDSwitcherKeyChromaParametersEventType.bmdSwitcherKeyChromaParametersEventTypeLiftChanged:
                    if (ChromaLiftChanged != null)
                        ChromaLiftChanged(this, null);
                    break;

            }
        }
    }

    class SwitcherKeyMonitor : IBMDSwitcherKeyCallback
    {
        public event SwitcherEventHandler KeyInputFillChanged;
        public event SwitcherEventHandler KeyOnAirChanged;

        public SwitcherKeyMonitor()
        {

        }
        void IBMDSwitcherKeyCallback.Notify(_BMDSwitcherKeyEventType eventType)
        {
            switch (eventType)
            {
                case _BMDSwitcherKeyEventType.bmdSwitcherKeyEventTypeInputFillChanged:
                    if (KeyInputFillChanged != null)
                        KeyInputFillChanged(this, null);
                    break;
                case _BMDSwitcherKeyEventType.bmdSwitcherKeyEventTypeOnAirChanged:
                    if (KeyOnAirChanged != null)
                        KeyOnAirChanged(this, null);
                    break;

            }
        }
    }
    
    class AudioMixerMonitor : IBMDSwitcherAudioMixerCallback
    {
        //Events:
        public event SwitcherEventHandler ProgramOutLevelNotificationChanged;
        public event SwitcherEventHandler ProgramOutBalanceChanged;
        public event SwitcherEventHandler ProgramOutGainChanged;

        public AudioMixerMonitor()
        {
        }

        void IBMDSwitcherAudioMixerCallback.Notify(_BMDSwitcherAudioMixerEventType EventType)
        {
            switch (EventType)
            {
                case (_BMDSwitcherAudioMixerEventType.bmdSwitcherAudioMixerEventTypeProgramOutBalanceChanged):
                    if (ProgramOutBalanceChanged != null)
                        ProgramOutBalanceChanged(this, null);
                    break;

                case (_BMDSwitcherAudioMixerEventType.bmdSwitcherAudioMixerEventTypeProgramOutGainChanged):
                    if (ProgramOutGainChanged != null)
                        ProgramOutGainChanged(this, null);
                    break;
            }
        }

        void IBMDSwitcherAudioMixerCallback.ProgramOutLevelNotification(double Left, double Right, double PeakLeft, double PeakRight)
        {
            if (ProgramOutLevelNotificationChanged != null)
                ProgramOutLevelNotificationChanged(this, null);
        }
    }
    class AudioInputMonitor : IBMDSwitcherAudioInputCallback
    {
        //Events:
        public event SwitcherEventHandler LevelNotificationChanged;
        public event SwitcherEventHandler BalanceChanged;
        public event SwitcherEventHandler GainChanged;
        public event SwitcherEventHandler IsMixedInChanged;
        public event SwitcherEventHandler MixOptionChanged;

        public AudioInputMonitor()
        {
        }

        void IBMDSwitcherAudioInputCallback.LevelNotification(double Left, double Right, double PeakLeft, double PeakRight)
        {
            if (LevelNotificationChanged != null)
                LevelNotificationChanged(this, null);
        }

        void IBMDSwitcherAudioInputCallback.Notify(_BMDSwitcherAudioInputEventType audioType)
        {
            switch (audioType)
            {
                case (_BMDSwitcherAudioInputEventType.bmdSwitcherAudioInputEventTypeBalanceChanged):
                    if (BalanceChanged != null)
                        BalanceChanged(this, null);
                    break;

                case (_BMDSwitcherAudioInputEventType.bmdSwitcherAudioInputEventTypeGainChanged):
                    if (GainChanged != null)
                        GainChanged(this, null);
                    break;

                case (_BMDSwitcherAudioInputEventType.bmdSwitcherAudioInputEventTypeIsMixedInChanged):
                    if (IsMixedInChanged != null)
                        IsMixedInChanged(this, null);
                    break;

                case (_BMDSwitcherAudioInputEventType.bmdSwitcherAudioInputEventTypeMixOptionChanged):
                    if (MixOptionChanged != null)
                        MixOptionChanged(this, null);
                    break;
            }
        }
    }
    class AudioMixerMonitorOutputMonitor : IBMDSwitcherAudioMonitorOutputCallback
    {
        //Events:
        public event SwitcherEventHandler LevelNotificationChanged;
        public event SwitcherEventHandler DimChanged;
        public event SwitcherEventHandler DimLevelChanged;
        public event SwitcherEventHandler GainChanged;
        public event SwitcherEventHandler MonitorEnableChanged;
        public event SwitcherEventHandler MuteChanged;
        public event SwitcherEventHandler SoloChanged;
        public event SwitcherEventHandler SoloInputChanged;

        public AudioMixerMonitorOutputMonitor()
        {
        }

        void IBMDSwitcherAudioMonitorOutputCallback.LevelNotification(double Left, double Right, double PeakLeft, double PeakRight)
        {
            if (LevelNotificationChanged != null)
                LevelNotificationChanged(this, null);
        }

        void IBMDSwitcherAudioMonitorOutputCallback.Notify(_BMDSwitcherAudioMonitorOutputEventType EventType)
        {
            switch (EventType)
            {
                case (_BMDSwitcherAudioMonitorOutputEventType.bmdSwitcherAudioMonitorOutputEventTypeDimChanged):
                    if (DimChanged != null)
                        DimChanged(this, null);
                    break;

                case (_BMDSwitcherAudioMonitorOutputEventType.bmdSwitcherAudioMonitorOutputEventTypeDimLevelChanged):
                    if (DimLevelChanged != null)
                        DimLevelChanged(this, null);
                    break;

                case (_BMDSwitcherAudioMonitorOutputEventType.bmdSwitcherAudioMonitorOutputEventTypeGainChanged):
                    if (GainChanged != null)
                        GainChanged(this, null);
                    break;

                case (_BMDSwitcherAudioMonitorOutputEventType.bmdSwitcherAudioMonitorOutputEventTypeMonitorEnableChanged):
                    if (MonitorEnableChanged != null)
                        MonitorEnableChanged(this, null);
                    break;

                case (_BMDSwitcherAudioMonitorOutputEventType.bmdSwitcherAudioMonitorOutputEventTypeMuteChanged):
                    if (MuteChanged != null)
                        MuteChanged(this, null);
                    break;

                case (_BMDSwitcherAudioMonitorOutputEventType.bmdSwitcherAudioMonitorOutputEventTypeSoloChanged):
                    if (SoloChanged != null)
                        SoloChanged(this, null);
                    break;

                case (_BMDSwitcherAudioMonitorOutputEventType.bmdSwitcherAudioMonitorOutputEventTypeSoloInputChanged):
                    if (SoloInputChanged != null)
                        SoloInputChanged(this, null);
                    break;
            }
        }
    }
}


