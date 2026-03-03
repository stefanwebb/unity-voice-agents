using System;
using System.Collections.Generic;
using System.Linq;
using Unity.WebRTC;
using UnityEngine;

namespace Stefanwebb.Voiceagents
{
    /// <summary>
    /// AudioStreamReceiver is a component that receives audio streams and plays them through a specified AudioSource.
    /// </summary>
    /// <seealso cref="AudioCodecInfo"/>
    [AddComponentMenu("Render Streaming/Audio Stream Receiver")]
    public class AudioStreamReceiver : StreamReceiverBase
    {
        internal const string CodecPropertyName = nameof(m_Codec);
        internal const string TargetAudioSourcePropertyName = nameof(m_TargetAudioSource);

        /// <summary>
        /// Delegate for handling updates to the received audio source.
        /// </summary>
        /// <param name="source">The updated AudioSource.</param>
        public delegate void OnUpdateReceiveAudioSourceHandler(AudioSource source);

        /// <summary>
        /// Event triggered when the received audio source is updated.
        /// </summary>
        public OnUpdateReceiveAudioSourceHandler OnUpdateReceiveAudioSource;

        [SerializeField]
        private AudioSource m_TargetAudioSource;

        [SerializeField]
        private AudioCodecInfo m_Codec;

        /// <summary>
        /// Gets the codec information for the audio stream.
        /// </summary>
        public AudioCodecInfo codec
        {
            get { return m_Codec; }
        }

        /// <summary>
        /// Gets or sets the target AudioSource where the received audio will be played.
        /// </summary>
        public AudioSource targetAudioSource
        {
            get { return m_TargetAudioSource; }
            set { m_TargetAudioSource = value; }
        }

        /// <summary>
        /// Gets the available audio codecs.
        /// </summary>
        /// <code>
        /// var codecs = AudioStreamReceiver.GetAvailableCodecs();
        /// foreach (var codec in codecs)
        ///     Debug.Log(codec.name);
        /// </code>
        /// </example>
        /// <returns>A list of available codecs.</returns>
        static public IEnumerable<AudioCodecInfo> GetAvailableCodecs()
        {
            var excludeCodecMimeType = new[] { "audio/CN", "audio/telephone-event" };
            var capabilities = RTCRtpReceiver.GetCapabilities(TrackKind.Audio);
            return capabilities.codecs.Where(codec => !excludeCodecMimeType.Contains(codec.mimeType)).Select(codec => AudioCodecInfo.Create(codec));
        }

        internal IEnumerable<RTCRtpCodecCapability> SelectCodecCapabilities(IEnumerable<AudioCodecInfo> codecs)
        {
            return RTCRtpReceiver.GetCapabilities(TrackKind.Audio).SelectCodecCapabilities(codecs);
        }

        private protected virtual void Start()
        {
            OnStartedStream += StartedStream;
            OnStoppedStream += StoppedStream;
        }

        private void StartedStream(string connectionId)
        {
            if (Track is AudioStreamTrack audioTrack)
            {
                m_TargetAudioSource?.SetTrack(audioTrack);
                OnUpdateReceiveAudioSource?.Invoke(m_TargetAudioSource);
            }
        }

        private void StoppedStream(string connectionId)
        {
        }
    }
}