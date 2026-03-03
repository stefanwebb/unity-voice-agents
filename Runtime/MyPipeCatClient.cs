using System;
using System.Collections;
using System.IO;
using System.Net;
using UnityEngine;
using Unity.WebRTC;
using System.Text;
using TMPro;

namespace GenerativeGamedev.VoiceAgents
{
public class MyPipeCatClient : MonoBehaviour
{
    [SerializeField]
    private AudioStreamSender audioSender;

    [SerializeField]
    private GameObject agentTextBackground;

    [SerializeField]
    private GameObject playerTextBackground;

    [SerializeField]
    private TextMeshProUGUI agentTextbox;

    [SerializeField]
    private TextMeshProUGUI playerTextbox;

    public class OfferData
    {
        public string sdp;
        public string type;
    }

    public class AnswerData
    {
        public string pc_id;
        public string sdp;
        public string type;
    }

    public class MessageData
    {
        // public string id;
        public string label;
        public string type;
        public string data;
    }

    // For both user and bot llm-text messages
    public class LLMTextData
    {
        public string text;
    }

    private RTCPeerConnection _peer;

    private RTCDataChannel _dataChannel;

    // TODO: StringBuilder for user transcription
    private StringBuilder _response = new StringBuilder("", 1024);

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(CreateTrack());
    }

    private IEnumerator CreateTrack()
    {
        // Wait for microphone track to be active
        var op = audioSender.CreateTrack();
        if (op.Track == null)
            yield return op;
        audioSender.SetTrack(op.Track);
        Debug.Log(audioSender.Track);
        Debug.Log($"Device: {Microphone.devices[0]}");

        _peer = new RTCPeerConnection();
        _dataChannel = _peer.CreateDataChannel("chat", new RTCDataChannelInit { ordered = true });

        _dataChannel.OnOpen += OnOpen;
        _dataChannel.OnClose += OnClose;
        _dataChannel.OnError += OnError;
        _dataChannel.OnMessage += OnMessage;

        _peer.OnTrack += OnTrack;
        _peer.OnConnectionStateChange += OnConnectionStateChange;
        _peer.OnDataChannel += OnDataChannel;
        _peer.OnIceCandidate += OnIceCandidate;
        _peer.OnIceConnectionChange += OnIceConnectionChange;
        _peer.OnIceGatheringStateChange += OnIceGatheringStateChange;
        _peer.OnNegotiationNeeded += OnNegotiationNeeded;
        
        _peer.AddTransceiver(audioSender.Track, new RTCRtpTransceiverInit { direction=RTCRtpTransceiverDirection.SendRecv });

        RTCSessionDescriptionAsyncOperation asyncOperation = _peer.CreateOffer();
        yield return asyncOperation;

        if (!asyncOperation.IsError)
        {
            RTCSessionDescription description = asyncOperation.Desc;
            RTCSetSessionDescriptionAsyncOperation asyncOperationB = _peer.SetLocalDescription(ref description);
            yield return asyncOperationB;
        }

        var offer = _peer.LocalDescription;

        // Prepare payload
        var data = new OfferData {
                sdp=offer.sdp,
                type=offer.type.ToString().ToLower()
            };
        string str = JsonUtility.ToJson(data);

        // TODO: Migrate to HttpClient
        byte[] bytes = new System.Text.UTF8Encoding().GetBytes(str);

        Debug.Log("Signaling: Posting HTTP data: " + str);

        HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:7860/api/offer");
        request.Method = "POST";
        request.ContentType = "application/json";
        request.KeepAlive = false;

        using (Stream dataStream = request.GetRequestStream())
        {
            dataStream.Write(bytes, 0, bytes.Length);
            dataStream.Close();
        }

        HttpWebResponse response = (HttpWebResponse)request.GetResponse();
        
        // Stream response into Json object
        // Gets the stream associated with the response.
        Stream receiveStream = response.GetResponseStream();
        Encoding encode = System.Text.Encoding.GetEncoding("utf-8");
        
        // Pipes the stream to a higher level stream reader with the required encoding format.
        StreamReader readStream = new StreamReader( receiveStream, encode );
        Console.WriteLine("\r\nResponse stream received.");
        Char[] read = new Char[256];
        
        // Reads 256 characters at a time.
        StringBuilder sb = new StringBuilder("", 256);
        int count = readStream.Read( read, 0, 256 );
        Console.WriteLine("HTML...\r\n");
        while (count > 0)
            {
                // Dumps the 256 characters on a string and displays the string to the console.
                String strB = new String(read, 0, count);
                sb.Append(strB);
                Console.Write(strB);
                count = readStream.Read(read, 0, 256);
            }
        Console.WriteLine("");

        var answer = JsonUtility.FromJson<AnswerData>(sb.ToString());

        // TODO: Implement connection id
        // _peer. pc.pc_id = answer['pc_id']
        Debug.Log($"Connection id: {answer.pc_id}");

        var desc = new RTCSessionDescription { sdp=answer.sdp, type=RTCSdpType.Answer};
        yield return _peer.SetRemoteDescription( ref desc );

        // await pc.setRemoteDescription(RTCSessionDescription(sdp=answer['sdp'], type=answer['type']))

        // Releases the resources of the response.
        response.Close();

        // Releases the resources of the Stream.
        readStream.Close();
    }

    private void OnNegotiationNeeded()
    {
        Debug.Log("Peer event: OnNegotiationNeeded");
    }

    private void OnIceGatheringStateChange(RTCIceGatheringState state)
    {
        Debug.Log($"Peer event: OnIceGatheringStateChange -> {state}");
    }

    private void OnIceConnectionChange(RTCIceConnectionState state)
    {
        Debug.Log($"Peer event: OnIceConnectionStateChange -> {state}");
    }

    private void OnIceCandidate(RTCIceCandidate candidate)
    {
        Debug.Log($"Peer event: OnIceCandidate");
    }

    private void OnDataChannel(RTCDataChannel channel)
    {
        Debug.Log($"Peer event: OnDataChannel -> {channel.Label}");
    }

    private void OnConnectionStateChange(RTCPeerConnectionState state)
    {
        Debug.Log($"Peer event: OnConnectionStateChange -> {state}");
    }

    private void OnTrack(RTCTrackEvent e)
    {
        Debug.Log($"Peer event: OnTrack -> {e}");
    }
        
    private void OnError(RTCError error)
    {
        Debug.Log($"Data channel errror: ");
    }

        private void OnOpen()
    {
        Debug.Log("Data channel event: OnOpen()");
    }

    private void OnClose()
    {
        Debug.Log("Data channel event: OnClose()");
    }

    // Code to handle data channel message goes here!
    // TODO: Triggering events!
    private void OnMessage(byte[] bytes)
    {
        string message = Encoding.UTF8.GetString(bytes);
        var messageData = JsonUtility.FromJson<MessageData>(message);

        // We only handle RTVI messages from PipeCat
        if (messageData.label != "rtvi-ai")
            return;

        switch(messageData.type)
        {
            case "user-started-speaking":
                // Pause the game 
                Time.timeScale = 0f;
                
                // Open dialog box
                // TODO: Animation when opening box
                playerTextbox.text = "";
                playerTextBackground.SetActive(true);

                // TODO: Set game state variable to user-speaking
                _response.Clear();

                break;

            case "user-stopped-speaking":
                // TODO: Set game state variable to user-has-spoken

                break;

            case "user-llm-text":
                // TODO: Confirm that user has started and stopped speaking via other messages
                // TODO: Stream output?
                var dataString = message.Substring(message.IndexOf(", \"data\": ") + 10, message.Length - message.IndexOf(", \"data\": ") - 11);
                var userText = JsonUtility.FromJson<LLMTextData>(dataString).text.Trim();

                playerTextbox.SetText(userText);
                // Debug.Log($"User: {userText}");

                break;

            // case "user-transcription":

            case "bot-llm-text":
                // TODO: Is there a bot-llm-started?
                if (!agentTextBackground.activeSelf)
                    agentTextBackground.SetActive(true);

                // TODO: Update canvas
                dataString = message.Substring(message.IndexOf(", \"data\": ") + 10, message.Length - message.IndexOf(", \"data\": ") - 11);
                var botText = JsonUtility.FromJson<LLMTextData>(dataString).text;
                _response.Append(botText);
                agentTextbox.SetText(_response);

                // Debug.Log($"bot-llm-text: {botResponse}");
                break;

            case "bot-llm-stopped":
                // TODO: Pause PipeCat until message is dismissed with key or button press

                break;

            default:
                // Debug.Log($"Unhandled RTVI message: {messageData.type}");
                break;
        }

        // Debug.Log("Message received: " + message);
    }

    void OnDestroy()
    {
        _peer.Close();
    }
    }
}
