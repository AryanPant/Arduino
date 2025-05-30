using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using NativeWebSocket;

public class ESP32CameraReceiver : MonoBehaviour
{
    [Header("WebSocket Configuration")]
    public string serverUrl = "wss://esp32-cam1-relay.onrender.com/";
    
    [Header("UI Components")]
    public RawImage cameraDisplay;
    public Text statusText;
    public Button connectButton;
    public Button disconnectButton;
    
    [Header("Settings")]
    public bool autoConnect = true;
    public float reconnectDelay = 5f;
    
    private WebSocket websocket;
    private Texture2D cameraTexture;
    private bool isConnected = false;
    private bool isConnecting = false;
    private Coroutine reconnectCoroutine;
    
    void Start()
    {
        InitializeUI();
        
        if (autoConnect)
        {
            ConnectToServer();
        }
    }
    
    void InitializeUI()
    {
        if (statusText != null)
            statusText.text = "Disconnected";
            
        if (connectButton != null)
        {
            connectButton.onClick.AddListener(ConnectToServer);
            connectButton.interactable = true;
        }
        
        if (disconnectButton != null)
        {
            disconnectButton.onClick.AddListener(DisconnectFromServer);
            disconnectButton.interactable = false;
        }
        
        // Initialize camera texture
        if (cameraTexture == null)
        {
            cameraTexture = new Texture2D(2, 2);
        }
    }
    
    public async void ConnectToServer()
    {
        if (isConnecting || isConnected) return;
        
        isConnecting = true;
        UpdateStatus("Connecting...");
        UpdateButtonStates();
        
        try
        {
            websocket = new WebSocket(serverUrl);
            
            websocket.OnOpen += OnWebSocketOpen;
            websocket.OnError += OnWebSocketError;
            websocket.OnClose += OnWebSocketClose;
            websocket.OnMessage += OnWebSocketMessage;
            
            await websocket.Connect();
        }
        catch (Exception e)
        {
            Debug.LogError($"WebSocket connection failed: {e.Message}");
            UpdateStatus($"Connection failed: {e.Message}");
            isConnecting = false;
            UpdateButtonStates();
            
            // Attempt reconnection
            if (reconnectCoroutine == null)
            {
                reconnectCoroutine = StartCoroutine(ReconnectCoroutine());
            }
        }
    }
    
    public async void DisconnectFromServer()
    {
        if (reconnectCoroutine != null)
        {
            StopCoroutine(reconnectCoroutine);
            reconnectCoroutine = null;
        }
        
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            await websocket.Close();
        }
        
        isConnected = false;
        isConnecting = false;
        UpdateStatus("Disconnected");
        UpdateButtonStates();
    }
    
    void OnWebSocketOpen()
    {
        Debug.Log("WebSocket connected!");
        isConnected = true;
        isConnecting = false;
        UpdateStatus("Connected");
        UpdateButtonStates();
        
        // Identify as Unity client
        if (websocket.State == WebSocketState.Open)
        {
            websocket.SendText("unity-client");
        }
        
        // Stop reconnection attempts
        if (reconnectCoroutine != null)
        {
            StopCoroutine(reconnectCoroutine);
            reconnectCoroutine = null;
        }
    }
    
    void OnWebSocketError(string error)
    {
        Debug.LogError($"WebSocket error: {error}");
        UpdateStatus($"Error: {error}");
        isConnected = false;
        isConnecting = false;
        UpdateButtonStates();
        
        // Attempt reconnection
        if (reconnectCoroutine == null)
        {
            reconnectCoroutine = StartCoroutine(ReconnectCoroutine());
        }
    }
    
    void OnWebSocketClose(WebSocketCloseCode closeCode)
    {
        Debug.Log($"WebSocket closed: {closeCode}");
        isConnected = false;
        isConnecting = false;
        UpdateStatus($"Disconnected: {closeCode}");
        UpdateButtonStates();
        
        // Attempt reconnection if it wasn't a manual disconnect
        if (closeCode != WebSocketCloseCode.Normal && reconnectCoroutine == null)
        {
            reconnectCoroutine = StartCoroutine(ReconnectCoroutine());
        }
    }
    
    void OnWebSocketMessage(byte[] data)
    {
        try
        {
            // Load JPEG data into texture
            if (cameraTexture.LoadImage(data))
            {
                // Display the texture on the RawImage component
                if (cameraDisplay != null)
                {
                    cameraDisplay.texture = cameraTexture;
                }
                
                Debug.Log($"Frame received and displayed - Size: {data.Length} bytes, Resolution: {cameraTexture.width}x{cameraTexture.height}");
            }
            else
            {
                Debug.LogWarning("Failed to load image data");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error processing camera frame: {e.Message}");
        }
    }
    
    IEnumerator ReconnectCoroutine()
    {
        while (!isConnected)
        {
            yield return new WaitForSeconds(reconnectDelay);
            
            if (!isConnected && !isConnecting)
            {
                Debug.Log("Attempting to reconnect...");
                ConnectToServer();
            }
        }
        
        reconnectCoroutine = null;
    }
    
    void UpdateStatus(string status)
    {
        if (statusText != null)
        {
            statusText.text = status;
        }
        Debug.Log($"Status: {status}");
    }
    
    void UpdateButtonStates()
    {
        if (connectButton != null)
        {
            connectButton.interactable = !isConnected && !isConnecting;
        }
        
        if (disconnectButton != null)
        {
            disconnectButton.interactable = isConnected || isConnecting;
        }
    }
    
    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (websocket != null)
        {
            websocket.DispatchMessageQueue();
        }
#endif
    }
    
    async void OnApplicationQuit()
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            await websocket.Close();
        }
    }
    
    void OnDestroy()
    {
        if (cameraTexture != null)
        {
            Destroy(cameraTexture);
        }
    }
}
