#include "esp_camera.h"
#include <WiFi.h>
#include <WebSocketsClient.h>

#define CAMERA_MODEL_AI_THINKER
#include "camera_pins.h"

// WiFi credentials
const char* ssid = "AryanPant";
const char* password = "12345678";

// WebSocket server
WebSocketsClient webSocket;
const char* websocket_host = "esp32-cam-relay.onrender.com";
const uint16_t websocket_port = 443;

bool connected = false;

// Frame timing
unsigned long lastSendTime = 0;
const int interval = 30; // ms per frame (~33 FPS max)

// FPS monitoring
unsigned long lastPrintTime = 0;
int frameCount = 0;

void webSocketEvent(WStype_t type, uint8_t * payload, size_t length) {
  switch(type) {
    case WStype_DISCONNECTED:
      Serial.println("WebSocket Disconnected");
      connected = false;
      break;

    case WStype_CONNECTED:
      Serial.println("WebSocket Connected");
      connected = true;
      break;

    default:
      break;
  }
}

void setup() {
  Serial.begin(115200);

  // Camera config
  camera_config_t config;
  config.ledc_channel = LEDC_CHANNEL_0;
  config.ledc_timer = LEDC_TIMER_0;
  config.pin_d0 = Y2_GPIO_NUM;
  config.pin_d1 = Y3_GPIO_NUM;
  config.pin_d2 = Y4_GPIO_NUM;
  config.pin_d3 = Y5_GPIO_NUM;
  config.pin_d4 = Y6_GPIO_NUM;
  config.pin_d5 = Y7_GPIO_NUM;
  config.pin_d6 = Y8_GPIO_NUM;
  config.pin_d7 = Y9_GPIO_NUM;
  config.pin_xclk = XCLK_GPIO_NUM;
  config.pin_pclk = PCLK_GPIO_NUM;
  config.pin_vsync = VSYNC_GPIO_NUM;
  config.pin_href = HREF_GPIO_NUM;
  config.pin_sccb_sda = SIOD_GPIO_NUM;
  config.pin_sccb_scl = SIOC_GPIO_NUM;
  config.pin_pwdn = PWDN_GPIO_NUM;
  config.pin_reset = RESET_GPIO_NUM;
  config.xclk_freq_hz = 20000000;
  config.pixel_format = PIXFORMAT_JPEG;
  config.frame_size = FRAMESIZE_QQVGA; // 160x120 = Low delay
  config.jpeg_quality = 30; // Lower quality = smaller size = faster send
  config.fb_count = 2;
  config.grab_mode = CAMERA_GRAB_LATEST;
  config.fb_location = CAMERA_FB_IN_PSRAM;

  // Initialize camera
  esp_err_t err = esp_camera_init(&config);
  if (err != ESP_OK) {
    Serial.printf("Camera init failed: 0x%x\n", err);
    return;
  }

  // Connect to WiFi
  WiFi.begin(ssid, password);
  WiFi.setTxPower(WIFI_POWER_19_5dBm); // Max power
  WiFi.setSleep(false); // Disable power-saving to avoid delays
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  Serial.println("\nWiFi connected");

  // Initialize WebSocket
  webSocket.beginSSL(websocket_host, websocket_port, "/");
  webSocket.onEvent(webSocketEvent);
  webSocket.setReconnectInterval(5000);

  Serial.println("ESP32-CAM Ready");
}

void loop() {
  webSocket.loop();

  if (connected && millis() - lastSendTime >= interval) {
    lastSendTime = millis();

    camera_fb_t *fb = esp_camera_fb_get();
    if (fb) {
      webSocket.sendBIN(fb->buf, fb->len);
      esp_camera_fb_return(fb);
      frameCount++;
    }
  }

  // Print FPS every 1 second
  if (millis() - lastPrintTime >= 1000) {
    Serial.printf("FPS: %d\n", frameCount);
    frameCount = 0;
    lastPrintTime = millis();
  }
}
