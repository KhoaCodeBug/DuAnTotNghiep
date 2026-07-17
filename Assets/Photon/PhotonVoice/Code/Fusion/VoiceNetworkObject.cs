#if FUSION_WEAVER
namespace Photon.Voice.Fusion
{
    using global::Fusion;
    using Unity;
    using UnityEngine;
    using LogLevel = Photon.Voice.LogLevel;

    [AddComponentMenu("Photon Voice/Fusion/Voice Network Object")]
    public class VoiceNetworkObject : NetworkBehaviour
    {
#region Private Fields

        // VoiceComponentImpl instance instead if VoiceComponent inheritance
        private VoiceComponentImpl voiceComponentImpl = new VoiceComponentImpl();

        private VoiceConnection voiceConnection;
        private bool isRecorderSetupDone = false;

#endregion
#region Properties

        protected Voice.ILogger Logger => voiceComponentImpl.Logger;

        // to set logging level from code
        public VoiceLogger VoiceLogger => voiceComponentImpl.VoiceLogger;

        /// <summary> The Recorder component currently used by this VoiceNetworkObject </summary>
        public Recorder RecorderInUse { get; private set; }

        /// <summary> The Speaker component currently used by this VoiceNetworkObject </summary>
        public Speaker SpeakerInUse { get; private set; }

        /// <summary> If true, this VoiceNetworkObject has a Speaker that is currently playing received audio frames from remote audio source </summary>
        public bool IsSpeaking => this.SpeakerInUse != null && this.SpeakerInUse.IsPlaying;

        /// <summary> If true, this VoiceNetworkObject has a Recorder that is currently transmitting audio stream from local audio source </summary>
        public bool IsRecording => this.RecorderInUse != null && this.RecorderInUse.IsCurrentlyTransmitting;


#if FUSION2
        public bool IsLocal => Runner.Topology == Topologies.Shared ? this.Object.HasStateAuthority : this.Object.HasInputAuthority;
#else
        public bool IsLocal => Runner.Topology == SimulationConfig.Topologies.Shared ? this.Object.HasStateAuthority : this.Object.HasInputAuthority;
#endif
#endregion

#region Private Methods

        private void SetupRecorder()
        {
            Recorder recorder = null;

            Recorder[] recorders = this.GetComponentsInChildren<Recorder>();
            if (recorders.Length > 0)
            {
                if (recorders.Length > 1)
                {
                    this.Logger.Log(LogLevel.Warning, "Multiple Recorder components found attached to the GameObject or its children.");
                }
                recorder = recorders[0];
            }

            if (null == recorder && null != this.voiceConnection.PrimaryRecorder)
            {
                recorder = this.voiceConnection.PrimaryRecorder;
            }

            if (null == recorder)
            {
                this.Logger.Log(LogLevel.Warning, "Cannot find Recorder. Assign a Recorder to VoiceNetworkObject object or set up FusionVoiceClient.PrimaryRecorder.");
            }
            else
            {
                // 🔥 BIỆN PHÁP MẠNH MẼ: Ép dùng driver Photon Mic native và bắt đầu thu âm
                recorder.MicrophoneType = Recorder.MicType.Photon;
                recorder.RecordingEnabled = true;
                recorder.UserData = this.GetUserData();
                recorder.RestartRecording(); // Áp dụng và khởi động lại micro ngay tức khắc

                this.voiceConnection.AddRecorder(recorder);
            }
            this.RecorderInUse = recorder;
        }

        private void SetupSpeaker()
        {
            Speaker speaker = null;

            Speaker[] speakers = this.GetComponentsInChildren<Speaker>(true);
            if (speakers.Length > 0)
            {
                speaker = speakers[0];
                if (speakers.Length > 1)
                {
                    this.Logger.Log(LogLevel.Warning, "Multiple Speaker components found attached to the GameObject or its children. Using the first one we found.");
                }
            }

            // Chỉ dùng voiceConnection làm fallback khi không có speaker có sẵn
            if (null == speaker && this.voiceConnection != null && null != this.voiceConnection.SpeakerPrefab)
            {
                speaker = this.voiceConnection.InstantiateSpeakerPrefab(this.gameObject, false);
            }

            if (null == speaker)
            {
                this.Logger.Log(LogLevel.Error, "No Speaker component or prefab found. Assign a Speaker to VoiceNetworkObject object or set up FusionVoiceClient.SpeakerPrefab.");
            }
            else
            {
                this.Logger.Log(LogLevel.Info, "Speaker setup completed.");
            }
            this.SpeakerInUse = speaker;
        }

        private object GetUserData()
        {
            return this.Object.Id;
        }

        private void Awake()
        {
            // Thiết lập Speaker sớm nhất có thể ngay khi đối tượng được load vào bộ nhớ
            this.SetupSpeaker();
        }

        private void Update()
        {
            // Tự động setup lại Recorder khi Client đã thực sự nhận được quyền kiểm soát nhân vật (Input Authority)
            if (!isRecorderSetupDone && this.voiceConnection != null)
            {
                if (this.IsLocal)
                {
                    this.Logger.Log(LogLevel.Info, "[VOICE] Late setup Recorder for Local Player after getting Input Authority.");
                    this.SetupRecorder();
                    isRecorderSetupDone = true;
                }
            }
        }

        public override void Spawned()
        {
            voiceComponentImpl.Awake(this);

            this.voiceConnection = this.Runner.GetComponent<VoiceConnection>();

            if (this.IsLocal)
            {
                this.SetupRecorder();
                isRecorderSetupDone = true;
                if (this.RecorderInUse == null)
                {
                    this.Logger.Log(LogLevel.Warning, "Recorder not setup for VoiceNetworkObject: playback may not work properly.");
                }
                else
                {
                    if (!this.RecorderInUse.TransmitEnabled)
                    {
                        this.Logger.Log(LogLevel.Warning, "VoiceNetworkObject.RecorderInUse.TransmitEnabled is false, don't forget to set it to true to enable transmission.");
                    }
                }
            }
            else
            {
                // Nếu chưa có quyền, cho phép hàm Update() quét và đăng ký muộn sau đó
                isRecorderSetupDone = false;
            }

            // Setup lại lần nữa phòng hờ fallback prefab
            if (this.SpeakerInUse == null)
            {
                this.SetupSpeaker();
            }

            if (this.SpeakerInUse == null)
            {
                this.Logger.Log(LogLevel.Warning, "Speaker not setup for VoiceNetworkObject: voice chat will not work.");
            }
            else
            {
                this.voiceConnection.AddSpeaker(this.SpeakerInUse, this.GetUserData());
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            this.voiceConnection.RemoveRecorder(this.RecorderInUse);
        }

#endregion
    }
}
#endif